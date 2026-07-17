# Design — ISSUE-11: REST API (Dashboard + Endpoints Públicos)

## Escopo deste documento
Revisão **focada** em 3 pontos técnicos fora do julgamento de negócio do PM (regras já fechadas com o Gerente no Gate 1 — paginação, campos públicos, CORS, fatiamento em sub-issues — **não são revisitadas aqui**). Este design cobre exclusivamente:
1. Estratégia de assinatura JWT / ausência de refresh token.
2. Suficiência do mascaramento de secrets em `GET /api/settings`.
3. Rate limiting nativo do .NET 8 atrás de proxy reverso.

---

## 1. Assinatura JWT e ausência de refresh token

### 1.1 Algoritmo: HS256 (simétrico)
**Decisão: HS256.**

Justificativa: RS256 (assimétrico) se justifica quando o emissor do token e o(s) validador(es) são partes diferentes — por exemplo, múltiplos microsserviços validando tokens emitidos por um Identity Provider central, sem que cada serviço precise conhecer o segredo de assinatura. Não é o caso aqui: a mesma API (`omuletachou` backend) emite (`POST /api/auth/login`) e valida (`[Authorize]`) o token, com um único consumidor autenticado (o Dashboard Angular, Issue #13). Não há necessidade de distribuir uma chave pública para terceiros verificarem o token. RS256 adicionaria complexidade operacional (par de chaves, rotação, armazenamento de chave privada) sem ganho de segurança neste cenário single-tenant/single-issuer. HS256 com uma chave de no mínimo 256 bits (32 bytes) é adequado e é a prática padrão do ecossistema ASP.NET Core (`Microsoft.IdentityModel.Tokens.SymmetricSecurityKey` + `HmacSha256Signature`) para este perfil de uso.

Requisito de implementação: a chave deve ter **no mínimo 256 bits** gerada aleatoriamente (ex.: `openssl rand -base64 32`), nunca uma string curta/memorável — HS256 é tão forte quanto a entropia da chave.

### 1.2 Armazenamento da chave de assinatura
**Decisão: variável de ambiente, lida via `IConfiguration` em `appsettings.json`/`appsettings.{Environment}.json` — NUNCA em `app_settings` (tabela de banco).**

Justificativa contra usar `app_settings` (tabela do domínio, exposta via `SettingsController`):
- **Superfície de exposição**: `app_settings` é uma tabela **lida e escrita via API** (`GET/PUT /api/settings`, Sub-C). Colocar a chave JWT ali significa que ela passa a fazer parte da superfície HTTP do sistema — mesmo mascarada, um bug de mascaramento ou uma falha de autorização no `SettingsController` exporia a chave que assina/valida a autenticação de todo o sistema. Isso cria uma dependência circular de segurança: a chave que protege o acesso autenticado estaria acessível através do próprio sistema autenticado.
- **Dependência de boot**: a validação do JWT (middleware `[Authorize]`, registrado no pipeline HTTP) precisa da chave disponível no **startup** da aplicação, antes de qualquer request. Buscar a chave no banco (`app_settings`) introduz uma dependência de banco disponível/migrado antes mesmo do pipeline de autenticação estar configurado, e um ponto de falha adicional (banco indisponível no boot = API não sobe nem para health-check).
- **Consistência com CA-A4/CA-A10**: o próprio critério de aceite já define que o seed do usuário operador vem de variável de ambiente (CA-A4) e que a chave de assinatura deve vir de "variável de ambiente ou `appsettings` (não versionado com secret real)" (CA-A10) — variável de ambiente é o padrão já adotado no projeto para segredos de bootstrap (ver também `docker-compose` do projeto, que já injeta secrets via env vars para outras integrações).

Implementação prática: `Jwt__SigningKey` como variável de ambiente (Docker Compose / `.env` não versionado), lida em `appsettings.json` via `builder.Configuration["Jwt:SigningKey"]` (ASP.NET Core mapeia `Jwt__SigningKey` → `Jwt:SigningKey` automaticamente). `appsettings.json` versionado contém apenas a estrutura (`"Jwt": { "SigningKey": "" }`), nunca o valor real. Falha ao subir se a variável estiver ausente em produção (fail-fast no `Program.cs`, evita rodar com chave vazia/default).

### 1.3 Ausência de refresh token: aceitável
**Decisão: manter sem refresh token nesta fase, conforme já definido no Gate 1 — avaliação de risco confirma que é aceitável.**

Análise de risco:
- **Perfil de uso**: usuário único (operador), sessão de dashboard administrativo interno, sem app mobile, sem múltiplos dispositivos simultâneos exigindo renovação silenciosa. O caso de uso que normalmente justifica refresh token — manter uma sessão mobile "sempre logada" sem forçar re-login frequente, com access token de vida curta (minutos) e refresh token de vida longa revogável — não se aplica aqui.
- **Trade-off da alternativa (token de vida curta + refresh)**: exigiria endpoint adicional (`POST /api/auth/refresh`), armazenamento de refresh tokens (tabela + rotação + revogação), e lógica de renovação no frontend Angular (Issue #13, ainda não implementada) — complexidade desproporcional para 1 usuário interno.
- **Risco residual aceito**: um JWT comprometido (ex.: XSS no dashboard, log acidental, dispositivo do operador comprometido) permanece válido por até 24h sem mecanismo de revogação (não há blacklist/deny-list de tokens). Mitigação já prevista no proposal.md: HTTPS obrigatório via proxy reverso (TLS termina no nginx), reduzindo vetor de interceptação em trânsito. Como o dashboard expõe apenas dados do próprio sistema (produtos, fila de publicação, settings mascarados) e não dados de terceiros/clientes, o blast radius de um token vazado é limitado ao próprio operador.
- **Recomendação para o LT/Dev, não redesenho**: não é necessário implementar blacklist ou refresh token nesta issue. Se o Gerente identificar no futuro necessidade de revogação imediata (ex.: dispositivo perdido), a solução mais simples nesse momento é invalidar globalmente trocando a `Jwt:SigningKey` (derruba todas as sessões ativas) — suficiente para usuário único, sem precisar de infraestrutura de revogação por token individual.

---

## 2. Suficiência do mascaramento em `GET /api/settings`

### 2.1 Mascaramento (últimos 4 caracteres) é suficiente como controle primário
O mascaramento definido pelo Gerente (mostrar apenas os últimos 4 caracteres, formato `****************a1b2`) é uma medida de **exposição em tela/resposta**, adequada ao objetivo de evitar que o valor completo de um secret transite em uma resposta HTTP desnecessariamente (ex.: print de tela, log de rede, ferramenta de debug do navegador). Combinado com `[Authorize]` (só o operador autenticado acessa `GET /api/settings`) e `PUT` que nunca ecoa o valor completo (CA-C6), a superfície de vazamento por essa rota está adequadamente reduzida para o perfil de risco do sistema (single-tenant, sem dados de terceiros nos secrets — os `app_settings` protegidos são tokens de integrações como Telegram/redes sociais, não dados de cliente final).

### 2.2 Avaliação: log de auditoria de acesso — não necessário como estrutura dedicada
Analisado o pedido do Gerente de avaliar uma camada complementar (ex.: auditoria de quem/quando acessou `GET /api/settings`): **decisão é não construir uma tabela/estrutura de auditoria dedicada nesta issue**, pelos seguintes motivos:
- Com usuário único, "quem acessou" é sempre a mesma resposta (o operador) — uma tabela de auditoria por usuário não agrega valor de accountability (não há necessidade de diferenciar entre usuários).
- O valor real de um log de acesso a `GET /api/settings` seria **forense**: em caso de suspeita de comprometimento da conta/token, poder responder "quando os secrets foram visualizados" para cruzar com o momento de um incidente. Esse valor existe, mas construir uma tabela dedicada + endpoint de consulta é desproporcional ao risco (overkill), como o próprio Gerente antecipou.

**Recomendação de baixo custo (não é redesenho, é a única adição sugerida)**: logar em nível `Information` via `ILogger<SettingsController>` (infraestrutura de logging padrão do .NET, já presente no projeto — nenhuma tabela nova, nenhum endpoint novo) uma linha estruturada a cada `GET /api/settings` e `PUT /api/settings/{key}`, contendo timestamp, o `Sub`/claim do usuário do JWT e a chave afetada (nunca o valor, nem mascarado, no log — apenas metadados: `"Settings GET by user {UserId} at {Timestamp}"` / `"Settings PUT key={Key} by user {UserId} at {Timestamp}"`). Isso aproveita a stack de log já existente para os jobs (Hangfire) e dá rastreabilidade forense mínima sem estrutura adicional. Não é um requisito bloqueante para a Definição de Pronto desta issue — é uma melhoria de baixo custo que o LT pode incluir no refinamento da Sub-C se o esforço for trivial (1-2 linhas de log), mas não deve virar sub-issue nem CA novo.

---

## 3. Rate limiting nativo do .NET 8 atrás de proxy reverso

### 3.1 Problema
O deploy roda em Oracle Cloud VM, com um proxy reverso (nginx, conforme padrão do projeto para TLS/domínio — ver `docker-compose`/infra do repo) na frente da API. Sem tratamento explícito, `HttpContext.Connection.RemoteIpAddress` dentro do container ASP.NET Core é sempre o IP do proxy reverso (o hop direto de rede), não o IP real do cliente. Se o particionamento do `RateLimiter` (`PartitionedRateLimiter` por `RemoteIpAddress`) usar esse valor sem correção, **todos os clientes públicos compartilham o mesmo "IP" (o do nginx)** aos olhos do rate limiter — o limite de 60 req/min (CA-D11/D12) seria efetivamente global para todo o tráfego público, não por cliente, e um único cliente abusivo bloquearia todos os demais (viola CA-D12: "rate limit não afeta outros IPs").

### 3.2 Decisão: `ForwardedHeadersMiddleware` + `KnownProxies`/`KnownNetworks`, registrado antes do `UseRateLimiter`
Configuração no `Program.cs`:

1. **`ForwardedHeadersOptions`**: habilitar `ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto`, para que `RemoteIpAddress` seja reescrito com o IP real do cliente (via `X-Forwarded-For`) e o esquema (`https`) seja corretamente refletido em `HttpContext.Request.Scheme` (relevante para geração de URLs absolutas, ex. `MediaLocalPath` como URL pública no `PublicController`, Sub-D).
2. **`KnownProxies` / `KnownNetworks`**: por padrão, `ForwardedHeadersMiddleware` só confia em cabeçalhos vindos de `Loopback` (127.0.0.1). Como nginx roda como **container separado na mesma rede Docker** (padrão de infra do projeto — API e proxy em containers distintos), é necessário adicionar explicitamente a rede interna do Docker Compose a `KnownNetworks` (ex.: `172.18.0.0/16`, o CIDR da rede definida no `docker-compose.yml` do projeto) ou o IP fixo do container nginx a `KnownProxies`. Sem isso, o middleware ignora silenciosamente o header `X-Forwarded-For` (por não reconhecer a origem como proxy confiável) e o problema do item 3.1 persiste mesmo com o middleware registrado.
3. **`ForwardLimit = 1`**: limitar a profundidade de hops confiáveis a 1 (só o nginx imediatamente à frente da API). Isso evita que um cliente malicioso injete seu próprio header `X-Forwarded-For: 1.2.3.4` para spoofar um IP arbitrário e escapar do rate limit — o middleware só aceita reescrever o IP a partir do hop confiável (nginx), e nginx deve ser configurado para **sobrescrever** (não anexar/confiar em) qualquer `X-Forwarded-For` recebido do cliente, setando o header com o IP real da conexão TCP (`$remote_addr`), padrão já usual em configuração de nginx como reverse proxy.
4. **Ordem no pipeline**: `app.UseForwardedHeaders(...)` deve vir **antes** de `app.UseRateLimiter()` (e antes de qualquer middleware que dependa de `RemoteIpAddress`, incluindo CORS/logging), para que o particionamento do `RateLimiter` (`RateLimitPartition.GetFixedWindowLimiter(partitionKey: httpContext.Connection.RemoteIpAddress?.ToString(), ...)`) já enxergue o IP real reescrito.

Resumo da configuração (referência para o LT/Dev, não código de implementação — detalhamento fica para o refinamento/task breakdown):
```
Program.cs:
  builder.Services.Configure<ForwardedHeadersOptions>(opts => {
      opts.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
      opts.KnownNetworks.Add(new IPNetwork(<subnet docker do nginx>, <prefixo>));
      opts.ForwardLimit = 1;
  });
  ...
  app.UseForwardedHeaders();   // ANTES do rate limiter
  app.UseRateLimiter();
```
Responsabilidade de infra (fora desta API, documentar no `docker-compose`/config de nginx do projeto, não nesta issue): nginx deve enviar `proxy_set_header X-Forwarded-For $remote_addr;` (sobrescrevendo, não `$proxy_add_x_forwarded_for`, para não permitir que o cliente encadeie um IP falso antes do real) e `proxy_set_header X-Forwarded-Proto $scheme;`.

### 3.3 Risco residual e mitigação
Como o IP real do cliente ainda pode ser trivialmente rotacionado por um atacante determinado (múltiplos IPs, IPv6 /64, VPN), o rate limit por IP é uma mitigação de abuso casual/scraping simples, não uma defesa contra ataque distribuído — consistente com o objetivo já definido pelo Gerente (CA-D11/D12) de proteger endpoints públicos de uso excessivo básico. Não há necessidade de camada adicional (ex.: rate limit por token/API-key) nesta issue, pois os endpoints públicos são, por definição, sem autenticação.

---

## Decisões consolidadas (resumo)

| Ponto | Decisão |
|---|---|
| Algoritmo JWT | HS256, chave simétrica ≥256 bits |
| Armazenamento da chave JWT | Variável de ambiente (`Jwt__SigningKey`), nunca em `app_settings` (tabela de domínio) nem hardcoded |
| Refresh token | Não implementar nesta fase — risco residual aceitável dado usuário único/24h/sem multi-dispositivo; revogação futura (se necessária) via rotação da signing key |
| Mascaramento de settings | Últimos 4 caracteres é suficiente como controle primário; sem tabela de auditoria dedicada |
| Complemento de baixo custo | Log estruturado (`ILogger`, sem tabela nova) em GET/PUT de `/api/settings` — recomendado, não bloqueante |
| Rate limit atrás de proxy | `ForwardedHeadersMiddleware` com `KnownNetworks`/`KnownProxies` = rede Docker do nginx, `ForwardLimit=1`, registrado antes de `UseRateLimiter()`; nginx deve sobrescrever `X-Forwarded-For` com `$remote_addr` |

## Itens explicitamente fora de escopo deste design (não revisitados)
Paginação, campos expostos em `/api/public/deals*`, lista de origins CORS, fatiamento em 5 sub-issues, versionamento de API — todos já decididos pelo Gerente no Gate 1 e documentados em `proposal.md`.
