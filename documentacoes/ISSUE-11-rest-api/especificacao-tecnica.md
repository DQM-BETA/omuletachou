# Especificação Técnica — ISSUE-11: REST API (Dashboard + Endpoints Públicos)

Este documento fecha os contratos técnicos que Sub-A a Sub-E devem seguir. Não redecide nada já definido em `proposal.md`/`design.md` — apenas concretiza em schema/config/contrato para implementação.

---

## 1. Schema da tabela `users` (migration nova)

Nova migration EF Core (`AddUsersTable` ou similar), no mesmo `DbContext` já usado pelas Issues #2/#6.

```csharp
public class User
{
    public int Id { get; set; }
    public string Email { get; set; } = null!;      // unique index
    public string PasswordHash { get; set; } = null!; // bcrypt hash (BCrypt.Net-Next)
    public DateTime CreatedAt { get; set; }
}
```

- `Email`: `varchar(255)`, **unique index** (`CREATE UNIQUE INDEX ix_users_email ON users (email)`).
- `PasswordHash`: `varchar(255)` — armazena o hash bcrypt completo (formato `$2a$...`, ~60 chars; 255 dá folga).
- `CreatedAt`: `timestamp with time zone`, default `now()` (padrão já usado nas demais tabelas do domínio, conforme Issues #2/#6).
- Sem soft delete, sem campos de role/permissão (usuário único, sem multi-tenant — CA-A conforme proposal.md).
- Pacote: `BCrypt.Net-Next` (NuGet) para hash/verify — `BCrypt.Verify(senhaPlana, hash)` em `CA-A1`/`CA-A2`, `BCrypt.HashPassword(senha, workFactor: 12)` no seed (`CA-A4`/`CA-A5`).

### Seed do usuário (CA-A4)
No startup (`Program.cs`, após `db.Database.Migrate()` ou equivalente), checar se `users` está vazia:
```
if (!db.Users.Any())
{
    var email = configuration["Seed:UserEmail"];      // env var Seed__UserEmail
    var password = configuration["Seed:UserPassword"]; // env var Seed__UserPassword
    if (!string.IsNullOrEmpty(email) && !string.IsNullOrEmpty(password))
    {
        db.Users.Add(new User { Email = email, PasswordHash = BCrypt.HashPassword(password, 12), CreatedAt = DateTime.UtcNow });
        db.SaveChanges();
    }
}
```
Se as env vars de seed não estiverem definidas e a tabela estiver vazia, a aplicação sobe normalmente mas sem usuário (login sempre 401 até seed manual) — não é fail-fast (diferente da chave JWT, que é fail-fast). Documentar isso no `README`/`CLAUDE.md` do repo como passo de deploy inicial.

---

## 2. Configuração de `Jwt__SigningKey`

`appsettings.json` (versionado, sem secret real):
```json
{
  "Jwt": {
    "SigningKey": "",
    "Issuer": "omuletachou-api",
    "Audience": "omuletachou-dashboard",
    "ExpirationHours": 24
  }
}
```

Variável de ambiente (Docker Compose / `.env` não versionado): `Jwt__SigningKey` (ASP.NET Core faz o bind automático `Jwt__SigningKey` → `Jwt:SigningKey`). Gerar com `openssl rand -base64 32` (≥256 bits, conforme design.md §1.1).

`Program.cs` — fail-fast se a chave estiver ausente/vazia em qualquer ambiente:
```csharp
var signingKey = builder.Configuration["Jwt:SigningKey"];
if (string.IsNullOrWhiteSpace(signingKey))
    throw new InvalidOperationException("Jwt:SigningKey não configurada (env var Jwt__SigningKey ausente).");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:Audience"],
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1) // tolerância mínima, não os 5min default
        };
    });

builder.Services.AddAuthorization();
```

Emissão do token (`AuthController.Login`, Sub-A):
```csharp
var claims = new[] { new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()), new Claim(JwtRegisteredClaimNames.Email, user.Email) };
var creds = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)), SecurityAlgorithms.HmacSha256);
var token = new JwtSecurityToken(
    issuer: issuer, audience: audience, claims: claims,
    expires: DateTime.UtcNow.AddHours(24),
    signingCredentials: creds);
return new JwtSecurityTokenHandler().WriteToken(token);
```

`SettingsController` (Sub-C) usa o mesmo `IConfiguration["Jwt:SigningKey"]` para o log estruturado (`{UserId}` via `User.FindFirst(JwtRegisteredClaimNames.Sub)`), nunca reexpõe a chave em si — a chave JWT não é uma linha de `app_settings`, não passa pelo mascaramento de Sub-C (é infraestrutura de auth, não configuração de domínio).

---

## 3. Ordem do pipeline de middlewares (`Program.cs`)

Confirmando a ordem do `design.md` §3.2, com CORS e Authentication explicitados (o design.md focou em ForwardedHeaders/RateLimiter; ordem completa abaixo é a extensão necessária para todas as sub-issues):

```
app.UseForwardedHeaders(...);   // 1. reescreve RemoteIpAddress ANTES de qualquer coisa que dependa dele
app.UseHttpsRedirection();      // 2. (se aplicável atrás do nginx — nginx já termina TLS; manter por padrão ASP.NET)
app.UseCors(corsPolicyName);    // 3. CORS antes de Authentication (preflight OPTIONS não deve exigir token)
app.UseAuthentication();        // 4. resolve o ClaimsPrincipal a partir do JWT
app.UseAuthorization();         // 5. avalia [Authorize] usando o ClaimsPrincipal resolvido acima
app.UseRateLimiter();           // 6. rate limit por IP (já correto, pós-ForwardedHeaders) — aplicado a endpoints específicos via .RequireRateLimiting()
app.MapControllers();
```

Justificativa da ordem CORS → Authentication → Authorization → RateLimiter:
- **ForwardedHeaders sempre primeiro**: tanto CORS quanto RateLimiter podem depender indiretamente do IP/scheme corrigido (RateLimiter depende diretamente — CA-D11/D12).
- **CORS antes de Authentication**: uma requisição preflight (`OPTIONS`) do navegador não carrega o header `Authorization`; se `UseAuthentication`/`UseAuthorization` rodassem antes, o preflight poderia falhar/ser rejeitado antes do middleware CORS responder os headers `Access-Control-Allow-*`, quebrando o fluxo de CORS para os endpoints protegidos consumidos futuramente pelo Dashboard Angular (Issue #13).
- **Authentication antes de Authorization**: padrão ASP.NET Core — `UseAuthorization`/`[Authorize]` precisa que `HttpContext.User` já esteja populado pelo middleware de Authentication (CA-A6/A7/A8 dependem dessa ordem).
- **RateLimiter após Authentication/Authorization**: neste sistema o rate limit é aplicado especificamente aos endpoints **públicos** (`/api/public/deals*`, `/api/public/push/subscribe` — CA-D11/E4), que não passam por `[Authorize]`; a posição relativa a Authentication/Authorization é irrelevante para esses endpoints (não exigem auth), mas mantê-lo por último evita rejeitar (429) requisições que seriam de qualquer forma barradas por 401/403 mais cedo em endpoints protegidos que eventualmente também tenham `.RequireRateLimiting()` — evita gastar "budget" de rate limit em requests já inválidas por token.

Rate limiting configurado com policies nomeadas (`AddRateLimiter` com `AddPolicy("public-read", ...)` 60 req/min e `AddPolicy("public-write", ...)` 10 req/min), aplicadas via `.RequireRateLimiting("public-read")` nos endpoints de `PublicController` (deals/slug/category) e `.RequireRateLimiting("public-write")` em `POST /api/public/push/subscribe` (Sub-D define a policy; Sub-E consome `"public-write"` no `PushController`).

---

## 4. Envelope de paginação padrão (reaproveitado por Products/Queue/Public)

Contrato único, implementado uma vez (sugestão: classe genérica em pasta compartilhada, ex. `Api/Common/PagedResult<T>.cs`, e um helper de extensão `IQueryable<T>.ToPagedResultAsync(page, pageSize)`), consumido por Sub-B (`ProductsController`, `QueueController`), Sub-D (`PublicController`) e qualquer outro endpoint de listagem futuro:

```csharp
public class PagedResult<T>
{
    public IReadOnlyList<T> Items { get; init; } = [];
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalItems { get; init; }
    public int TotalPages => (int)Math.Ceiling(TotalItems / (double)PageSize);
}
```

Resposta JSON (camelCase, padrão ASP.NET Core):
```json
{ "items": [...], "page": 1, "pageSize": 20, "totalItems": 137, "totalPages": 7 }
```

Normalização de `page`/`pageSize` (aplicar ANTES da query, no helper compartilhado — não duplicar em cada controller):
- `page < 1` → `1`
- `pageSize < 1` → `20` (padrão)
- `pageSize > 100` → `100` (truncamento, CA-D7)
- Sem parâmetros → `page=1`, `pageSize=20` (CA-D6)

Quem implementa primeiro (`PagedResult<T>` + helper) deve colocar em um namespace/pasta comum não vinculada a nenhum controller específico, já que Sub-B e Sub-D são paralelizáveis entre si (ver `tasks.md` — ordem de dependência) e ambas precisam do mesmo contrato. **Recomendação: a sub-issue que nascer primeiro entre Sub-B/Sub-D implementa o helper compartilhado; a outra reaproveita via merge de `desenv`** (ver `tasks.md` §Ordem para o desdobramento operacional).

---

## 5. Mascaramento de secrets (Sub-C) — formato exato

Chave termina em `_key`, `_secret`, `_token` ou `_password` (case-insensitive, sufixo do nome da chave, ex. `telegram.bot_token`):
```csharp
static string Mask(string value)
{
    if (string.IsNullOrEmpty(value)) return null; // CA-C3: "não configurado", nunca mascara string vazia
    var last4 = value.Length <= 4 ? value : value[^4..];
    var maskLength = Math.Max(value.Length - last4.Length, 16); // formato do exemplo do Gerente usa 16 asteriscos fixos
    return new string('*', 16) + last4;
}
```
Nota: o exemplo do Gate 1/CA-C1 (`****************a1b2`) usa 16 asteriscos fixos independentemente do tamanho real do valor (não codifica o tamanho real do secret na resposta — evita vazar por inferência o comprimento da chave). Dev deve seguir esse formato fixo, não proporcional ao tamanho do valor.

Log estruturado recomendado pelo Arquiteto (não bloqueante, Sub-C): `ILogger<SettingsController>.LogInformation("Settings GET by user {UserId} at {Timestamp}", userId, DateTimeOffset.UtcNow)` em `GET`, e equivalente com `{Key}` (nunca o valor) em `PUT`.

---

## 6. CA-E3 — decisão de idempotência do unsubscribe

Adotado: **204 idempotente** (não 404) para `DELETE /api/public/push/unsubscribe` com `endpoint` não cadastrado. Justificativa: é uma operação pública sem autenticação — retornar 404 permite a um chamador malicioso inferir (por tentativa e erro) se um determinado `endpoint` de push está cadastrado; 204 idempotente evita esse vazamento de informação e segue a semântica usual de `DELETE` idempotente (delete de recurso já ausente = sucesso, sem efeito). Sub-E implementa dessa forma.
