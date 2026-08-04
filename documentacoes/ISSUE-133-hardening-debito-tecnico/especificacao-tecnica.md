# Especificação Técnica — Issue #133 (Hardening e débito técnico)

Triagem técnica direta (issue puramente técnica, sem PM Fase 1/2 nem Arquiteto — sem ambiguidade
de design). Ver `estado.md` para a justificativa completa do que entra/fica de fora desta rodada.

---

## Sub-A — Backend .NET (segurança + lacuna funcional + limpeza)

### A1. Rate-limit em `DELETE /api/public/push/unsubscribe`
`backend/src/AfiliadoBot.Api/Controllers/PushController.cs` — o endpoint `Unsubscribe` não tem
`[EnableRateLimiting]`, diferente de `Subscribe` (`PublicWritePolicy`) e `GetVapidPublicKey`
(`PublicReadPolicy`). A policy `RateLimiterConfigurator.PublicWritePolicy` (10 req/min/IP) já
existe e está registrada em `Program.cs` — só falta aplicar o atributo:
```csharp
[HttpDelete("unsubscribe")]
[EnableRateLimiting(RateLimiterConfigurator.PublicWritePolicy)]
public async Task<IActionResult> Unsubscribe(...)
```
Critério: requisição 11 em <1min do mesmo IP retorna 429. Teste unitário/integração seguindo o
padrão já existente para `Subscribe` (ver testes de `RateLimiterConfigurator`/`PushController`).

### A2. `HangfireAuthFilter` — comparação tempo-constante + lockout por IP
`backend/src/AfiliadoBot.Api/Hangfire/HangfireAuthFilter.cs`. Dois problemas:
1. `providedPassword == configuredPassword` (linha 31) usa comparação padrão de string, vulnerável
   a timing attack. Trocar por `CryptographicOperations.FixedTimeEquals` sobre os bytes UTF-8
   das duas strings (usar `Encoding.UTF8.GetBytes`; se os tamanhos diferirem, comparar contra um
   buffer de tamanho fixo para não vazar o tamanho via early-return — ou aceitar que o tamanho da
   senha configurada não é segredo crítico e apenas usar `FixedTimeEquals` diretamente após
   igualar os buffers via padding).
2. Sem rate-limit/lockout: o `/hangfire` é um `IDashboardAuthorizationFilter` (middleware de
   Hangfire, não um `Controller`), então `[EnableRateLimiting]`/`UseRateLimiter()` do ASP.NET não
   se aplica diretamente a essa rota. Implementar lockout simples **dentro do próprio filtro**:
   `ConcurrentDictionary<string, (int Attempts, DateTime WindowStart)>` estático, chave = IP do
   cliente (`httpContext.Connection.RemoteIpAddress`), janela fixa (ex. 5 tentativas/5min por IP;
   reusar constantes similares às de `RateLimiterConfigurator` para consistência). Ao exceder,
   `Authorize` retorna `false` mesmo com senha correta até a janela expirar. Documentar no
   XML-doc da classe a limitação (contador em memória, não sobrevive a restart/múltiplas
   instâncias — aceitável para o único container `api`).
Testes: unitário do filtro simulando IPs/tentativas repetidas.

### A3. SSRF allowlist básica em `LocalMediaStorage`
`backend/src/AfiliadoBot.Infrastructure/Storage/LocalMediaStorage.cs:33-46`. Hoje aceita qualquer
URI absoluta (`Uri.TryCreate(..., UriKind.Absolute, ...)`). Adicionar validação antes do
`_httpClient.GetAsync`:
- Bloquear scheme != `http`/`https`.
- Resolver o host (`Dns.GetHostAddresses` ou usar `uri.Host` se já for IP) e rejeitar IPs em
  ranges privados/loopback/link-local: `127.0.0.0/8`, `10.0.0.0/8`, `172.16.0.0/12`,
  `192.168.0.0/16`, `169.254.0.0/16` (inclui o metadata endpoint de cloud, `169.254.169.254`),
  `::1`, `fc00::/7`, `fe80::/10`.
- Em caso de bloqueio: mesmo padrão de retorno das outras branches (`_logger.LogWarning` +
  `return (null, mediaType)`), nunca lançar exceção não capturada.
- Risco aceito documentado na issue original: exige comprometer a fonte externa (MediaUrl vem só
  de collectors internos, não de input direto do usuário) — ainda assim, defesa em profundidade
  barata.
Testes: URLs privadas/localhost/metadata rejeitadas; URLs públicas seguem funcionando (mock de
`HttpMessageHandler` já usado nos testes existentes de `LocalMediaStorage`).

### A4. `ProcessorJob.MarkAsPublished()` incondicional
`backend/src/AfiliadoBot.Application/Jobs/ProcessorJob.cs:90-93`. Hoje `CreatePublicationQueueEntriesAsync`
pode não criar nenhuma entrada (nenhuma rede habilitada e com credenciais completas — ver
`NetworkSettings`/`HasCredentials`), e mesmo assim o loop chama `product.MarkAsPublished()`
incondicionalmente logo em seguida. Decisão de domínio: **não introduzir novo `ProductStatus`** —
reaproveitar `ProductStatus.Error` (já existe e já é usado para falhas não recuperáveis, ex.
`EnsureAffiliateLinkAsync`). Fix:
```csharp
var queuedCount = await CreatePublicationQueueEntriesAsync(product, settingsMap, slots[i], ct);
if (queuedCount == 0)
{
    product.MarkAsError("Nenhuma rede qualificada para publicacao (sem credenciais/configuracao habilitada)");
}
else
{
    product.MarkAsPublished();
}
await _dbContext.SaveChangesAsync(ct);
```
`CreatePublicationQueueEntriesAsync` passa a retornar `Task<int>` (contagem de entradas
efetivamente adicionadas a `_dbContext.PublicationQueues`) em vez de `Task`. Atualizar os 3 `if
(!...) continue;` internos para não incrementar o contador quando pulam a rede. Testes: produto
com zero redes habilitadas/credenciadas vai para `Error` (não `Published`); produto com ao menos
1 rede qualificada segue indo para `Published` (comportamento atual preservado).

### A5. Seed das credenciais do Facebook em `app_settings`
`networks.facebook.enabled` já existe (seed id 30, `InitialSchema`), mas `facebook.access_token`/
`facebook.page_id` nunca foram inseridos — são as chaves que `ProcessorJob.NetworkSettings` exige
via `HasCredentials` para a rede `Facebook`, e o `SettingsController` (`PUT`) só atualiza chave já
existente, não cria. Nova migration seguindo o padrão exato de `SeedInstagramCredentials`/
`SeedYoutubeCredentials` (`backend/src/AfiliadoBot.Infrastructure/Migrations/`), próximos ids
livres **49 e 50** (maior id usado atualmente: 48):
```csharp
migrationBuilder.InsertData(
    table: "app_settings",
    columns: new[] { "id", "key", "updated_at", "value" },
    values: new object[,]
    {
        { 49, "facebook.access_token", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "" },
        { 50, "facebook.page_id", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "" }
    });
```
Com `Down` fazendo `DeleteData` para os mesmos ids. Gerar via `dotnet ef migrations add
SeedFacebookCredentials --project backend/src/AfiliadoBot.Infrastructure --startup-project
backend/src/AfiliadoBot.Api` (não escrever a `Designer.cs`/snapshot a mão — deixar o `ef` gerar,
conferir o diff). Após a migration, as chaves ficam editáveis via tela de Settings do dashboard
como as demais redes.

### A6. `Newtonsoft.Json` transitivo — fixar versão
Confirmado via `dotnet list package --include-transitive` em `AfiliadoBot.Api`: `Newtonsoft.Json
11.0.1` (High, deserialização insegura) vem transitivamente de `Hangfire.Core 1.8.14` — não há
referência direta em nenhum `.csproj`. Fix mecânico de baixo risco (NuGet resolve a versão mais
alta entre direta/transitiva do mesmo pacote): adicionar referência direta pinada em
`backend/src/AfiliadoBot.Api/AfiliadoBot.Api.csproj`:
```xml
<PackageReference Include="Newtonsoft.Json" Version="13.0.3" />
```
(13.0.3 é a última estável sem as vulnerabilidades reportadas). Validar com `dotnet list package
--include-transitive` pós-fix (deve mostrar só 13.0.3) e rodar a suíte completa (Hangfire usa
Newtonsoft internamente para serialização de jobs — regressão improvável dado que é só um bump
minor/patch dentro da mesma major, mas testes de integração do Hangfire, se houver, cobrem isso).

### A7. Remover `Class1.cs` mortos
Excluir os 3 arquivos de scaffolding nunca usados:
- `backend/src/AfiliadoBot.Application/Class1.cs`
- `backend/src/AfiliadoBot.Domain/Class1.cs`
- `backend/src/AfiliadoBot.Infrastructure/Class1.cs`
Confirmar antes (`grep -rn "class Class1"` / referências) que não há uso — são classes vazias de
template do `dotnet new classlib`. Build deve continuar limpo após a remoção.

---

## Sub-B — Infraestrutura

### B1. `.dockerignore` por serviço
`.gitignore` (raiz) tem a linha `.dockerignore` sob o comentário `# Docker` — bloqueia versionar
qualquer `.dockerignore` dos 3 serviços buildáveis (`backend`, `dashboard`, `website`). Remover
essa linha (linhas 15-16 do `.gitignore` atual) e criar:
- `backend/.dockerignore`: `bin/`, `obj/`, `**/bin/`, `**/obj/`, `.vs/`, `*.user`, `**/*.Tests/`
  (contexto de build é `./backend`, dockerfile em `src/AfiliadoBot.Api/Dockerfile` — não precisa
  do projeto de testes na imagem final).
- `dashboard/.dockerignore`: `node_modules/`, `dist/`, `.angular/`.
- `website/.dockerignore`: `node_modules/`, `.next/`, `dist/`.
Reduz contexto de build enviado ao daemon Docker (mais rápido) e evita vazar `bin`/`obj`/
`node_modules` locais divergentes para dentro da imagem.

### B2. `deploy.sh` — aguardar healthcheck pós-deploy
`deploy.sh` hoje só roda `docker compose up -d --build` e imprime `docker compose ps`, sem
confirmar que os serviços com `healthcheck` (`db`, `api` — ambos já definidos no
`docker-compose.yml`) chegaram a `healthy` antes de reportar sucesso. Adicionar após o `up`:
```bash
echo "==> aguardando healthcheck dos serviços"
for i in $(seq 1 30); do
  unhealthy=$(docker compose ps --format json | grep -c '"Health":"unhealthy"' || true)
  starting=$(docker compose ps --format json | grep -c '"Health":"starting"' || true)
  if [ "$unhealthy" -gt 0 ]; then
    echo "ERRO: serviço unhealthy após deploy." >&2
    docker compose ps
    exit 1
  fi
  if [ "$starting" -eq 0 ]; then
    echo "==> todos os serviços com healthcheck estão healthy"
    break
  fi
  sleep 2
done
```
(Ajustar ao formato real de saída de `docker compose ps --format json` na versão do Docker
Compose usada — validar localmente; o objetivo é falhar o script com exit != 0 se algum serviço
monitorado não ficar `healthy`, não replicar exatamente o snippet acima linha a linha.) Manter a
mensagem final `deploy concluído` só depois da confirmação.

### B3. Pin de versão exata nas imagens
`docker-compose.yml`:
- `db.image: postgres:16-alpine` → pinar no patch atual mais recente estável, ex.
  `postgres:16.4-alpine` (checar tag válida no Docker Hub no momento do fix — objetivo é sair de
  `16-alpine` flutuante, não necessariamente `16.4` se houver patch mais novo).
- `nginx-proxy-manager.image: jc21/nginx-proxy-manager:latest` → pinar em uma tag semver
  existente no Docker Hub (ex. `jc21/nginx-proxy-manager:2.12.3` — validar a última estável no
  momento do fix).
Documentar no `CLAUDE.md` do repo ou comentário inline que upgrades de imagem passam a ser
mudança explícita (bump de tag + teste), não silenciosos no próximo `docker compose pull`.

---

## Sub-C — Frontend / dashboard

### C1. `dashboard/nginx.conf` — `X-Forwarded-*` explícito
`dashboard/nginx.conf`, location `/api/`, já seta `Host`/`X-Real-IP` mas não
`X-Forwarded-For`/`X-Forwarded-Proto` explicitamente — hoje funciona "por acidente" porque o NPM
(nginx-proxy-manager) na frente já repassa esses headers e o nginx do dashboard não os sobrescreve,
mas isso é frágil (qualquer builder de imagem nginx padrão pode não repassar por default, e o
`ForwardedHeadersMiddleware` do backend, usado pelo rate-limiting por IP real —
`RateLimiterConfigurator.PartitionKey` —, depende desses headers estarem corretos e não
duplicados). Tornar explícito:
```nginx
location /api/ {
    proxy_pass http://api:8080/api/;
    proxy_set_header Host $host;
    proxy_set_header X-Real-IP $remote_addr;
    proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
    proxy_set_header X-Forwarded-Proto $scheme;
}
```
`$proxy_add_x_forwarded_for` acrescenta ao valor já recebido do NPM (encadeia corretamente em vez
de sobrescrever) — mantém a cadeia de proxies íntegra para o `ForwardedHeadersMiddleware` do
backend resolver o IP original do cliente.

### C2. Remover teste boilerplate do Angular CLI
`dashboard/src/app/app.component.spec.ts` — teste padrão gerado pelo `ng new`
(`should create the app` / `should render title`), nunca customizado e sem valor real de
regressão (o app real não tem esse "título" testado). Duas opções, decidir na implementação:
(a) remover o arquivo (mais simples, se `app.component` já é coberto indiretamente por outros
testes/e2e), ou (b) substituir por 1-2 asserts que reflitam o componente real (ex. roteador
carrega, layout base renderiza). Preferir (a) se não houver lógica própria em `AppComponent` além
de `<router-outlet>`; preferir (b) se `AppComponent` tiver alguma responsabilidade própria
(verificar antes de decidir).

---

## Fora de escopo desta rodada (ver `estado.md` para a justificativa completa)
- Upgrade major do Angular (`^17.3.0` → 18/19).
- Upgrade/substituição de `next-pwa`.
- Estratégia de backup automatizado do volume `postgres_data`.
