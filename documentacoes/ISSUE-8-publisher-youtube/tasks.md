# Task breakdown — ISSUE-8: Publisher YouTube Shorts

## Decisão de escopo: 1 sub-issue única (não separar o fix do ProcessorJob)

Avaliadas as duas opções:
- **Separar** o fix do `ProcessorJob` em sub-issue própria isolaria o teste de regressão em um PR menor e mais fácil de revisar isoladamente.
- **Manter unificado** evita overhead de coordenação (2 branches, 2 PRs, 2 merges sequenciais) para um dev só, numa issue cujo escopo total já é menor que a Issue #7 (um único publisher + um `if` aditivo no ProcessorJob).

**Decisão: sub-issue única (T-01).** Motivos:
1. O fix do ProcessorJob é uma pré-condição funcional do `YoutubePublisher` (CA16 é justamente o fallback de segurança para quando o fix falhar/não existir) — os dois pontos do código formam uma unidade de comportamento coesa: "produto sem vídeo nunca é publicado no YouTube", garantida em duas camadas (ProcessorJob não enfileira + YoutubePublisher recusa se enfileirado mesmo assim).
2. O risco do fix no ProcessorJob já foi avaliado como BAIXO pelo PM (filtro aditivo isolado, sem tocar nas demais redes) — não há necessidade de isolar merge/revisão por segurança adicional.
3. O teste de regressão das demais redes (CA19) é exigido explicitamente nos critérios de aceite e no `tasks.md` desta sub-issue, independente de estar na mesma sub-issue ou não — a garantia de qualidade não depende da divisão.
4. Escopo total (1 classe nova + 1 dicionário estático + ~5 linhas de alteração em método existente) é pequeno o suficiente para uma branch/PR único sem ficar difícil de revisar.

Mitigação do risco de regressão: a task abaixo exige explicitamente testes separados por rede (Telegram/Instagram/TikTok/Facebook inalteradas) como item de aceite não-negociável, ANTES de considerar a sub-issue pronta.

## Reavaliação da chave de credencial do YouTube em `NetworkSettings`

`ProcessorJob.NetworkSettings` hoje usa `youtube.access_token` como `CredentialKeys` para decidir se a rede Youtube está "habilitada com credenciais". Isso está desalinhado com o padrão do `YoutubePublisher`, que usa `youtube.client_id` / `youtube.client_secret` / `youtube.refresh_token` como credenciais estáveis de configuração (o `access_token` é renovado em runtime via refresh_token e nunca é uma credencial de configuração — pode nem existir em `app_settings` até a primeira renovação).

**Decisão do LT:** trocar `CredentialKeys` da entrada Youtube em `NetworkSettings` de `["youtube.access_token"]` para `["youtube.client_id", "youtube.client_secret", "youtube.refresh_token"]`. Isso é parte do mesmo fix retroativo (mesmo método, mesma tabela estática) — incluir na T-01, não é uma mudança adicional de escopo.

## T-01 — YoutubePublisher + fix retroativo no ProcessorJob

Sub-issue única. Ver corpo completo criado no GitHub (inclui critérios de aceite e contexto técnico).

**Arquivos a criar:**
- `backend/src/AfiliadoBot.Infrastructure/Integrations/Social/YoutubePublisher.cs` (implementa `ISocialPublisher`)
- Dicionário estático de mapeamento `Product.Category → categoryId` (pode viver como `private static readonly Dictionary<string,string>` dentro do próprio `YoutubePublisher`, seguindo o padrão de `NetworkSettings` estático do `ProcessorJob` — não precisa de novo arquivo/classe)

**Arquivos a alterar:**
- `backend/src/AfiliadoBot.Application/Jobs/ProcessorJob.cs`:
  - `CreatePublicationQueueEntriesAsync`: adicionar condição para `SocialNetwork.Youtube` — pular criação da entrada (e não chamar `GenerateCaptionAsync` para essa rede) se `product.MediaType != "video"` (tratando ausência de `MediaLocalPath`/`MediaUrl` como "sem vídeo", i.e., checar também que ao menos uma das duas fontes está preenchida).
  - `NetworkSettings`: trocar `CredentialKeys` da linha Youtube para `["youtube.client_id", "youtube.client_secret", "youtube.refresh_token"]`.
- DI (`Program.cs` ou `ServiceCollectionExtensions` — seguir onde `TelegramPublisher` está registrado): registrar `YoutubePublisher` como `ISocialPublisher` (aditivo, sem tocar no `PublisherJob`).
- `README`/`app_settings` seeds, se existir arquivo de seed dedicado: confirmar que `youtube.client_id`, `youtube.client_secret`, `youtube.refresh_token` já estão semeados (Issue #3, segundo o PRD) — se não estiverem, adicionar seeds vazios/placeholder.

**Padrão estrutural a seguir (`TelegramPublisher` como referência):**
- Constructor injection: `HttpClient`, `AfiliadoBotDbContext`, `ILogger<YoutubePublisher>` (mais o client do Google, ver abaixo).
- `Network => SocialNetwork.Youtube`.
- `PublishAsync(PublicationQueue item, CancellationToken ct)`: carregar credenciais de `app_settings`, resolver produto (via `item.Product` ou fallback de query), resolver fonte de mídia, montar metadados, chamar upload.
- Tratamento de exceção compatível com `PublisherJob.RegisterAttempt` (ver `PublisherJob.cs` para o contrato exato de retry — não fazer o publisher decidir retry sozinho, exceto nos dois casos explícitos de "Failed sem retry": refresh_token inválido e produto sem vídeo).

**Cliente Google (`Google.Apis.YouTube.v3`):**
- Adicionar pacote NuGet `Google.Apis.YouTube.v3` ao `.csproj` do projeto Infrastructure.
- Renovação de token: POST `https://oauth2.googleapis.com/token` com `client_id`, `client_secret`, `refresh_token`, `grant_type=refresh_token` (usar `HttpClient` injetado, não precisa do SDK do Google para isso — mesmo padrão HTTP já usado em `EnsureAffiliateLinkAsync` do ProcessorJob como referência de chamada HTTP simples com parse de JSON).
- Persistir `access_token` renovado em `app_settings` (upsert por `Key`).
- Falha na renovação (HTTP não-2xx ou refresh_token ausente): `Failed` sem retry (lançar exceção de tipo que o `PublisherJob` NÃO re-tenta, ou retornar sinalização equivalente ao padrão usado — checar como `PublisherJob.RegisterAttempt` diferencia "falha com retry" de "falha definitiva"; se não houver esse contrato hoje, usar o mesmo mecanismo do CA16, uma exceção/retorno que o PublisherJob trata como falha final) + `youtube.token_invalid=true` em `app_settings` (upsert).

**Resolução de mídia (fallback `MediaUrl` → stream temporário):**
- Reaproveitar `IMediaStorage.DownloadAsync` (já injetável, já usado pelo `ProcessorJob`) para baixar `MediaUrl` para um arquivo local temporário quando `MediaLocalPath` for nulo/vazio. Motivo: já existe, já testado, já resolve "baixar bytes para disco com tratamento de erro de rede" — não reinventar um segundo caminho de download dentro do publisher. Diferença de uso: `ProcessorJob` grava em `/app/media` (permanente); `YoutubePublisher` pode usar o mesmo `IMediaStorage` (grava no mesmo diretório) e, ao final do upload (sucesso ou falha), pode deletar o arquivo baixado sob demanda para não acumular lixo — critério de limpeza fica a critério do dev, não é requisito de aceite formal, mas documentar a decisão no PR.
- Se `MediaLocalPath` E `MediaUrl` ausentes (ou `MediaType != "video"`): não tentar download — cair direto no fallback de segurança do CA16 (`Failed` sem retry, `ErrorMessage = "Produto sem mídia de vídeo, não aplicável ao YouTube"`).

**Upload resumable (chunks de 8MB):**
- Fluxo padrão do YouTube Data API v3 resumable upload: 1) POST inicial para `https://www.googleapis.com/upload/youtube/v3/videos?uploadType=resumable` com metadados no corpo (JSON) e `Content-Length`/`X-Upload-Content-Type`/`X-Upload-Content-Length` nos headers → recebe `Location` (upload URL) na resposta; 2) PUT dos bytes em chunks de 8MB (múltiplo de 256KB) para a `Location`, cada chunk com header `Content-Range: bytes {start}-{end}/{total}`.
- Timeout por chunk: 5 min (usar `CancellationTokenSource` com timeout linked ao `ct` externo, por chunk).
- Timeout total: 15 min (`CancellationTokenSource` com timeout global, criado no início do `PublishAsync`, usado como token pai/linked de todos os chunks).
- Exceder qualquer um dos dois timeouts: abortar o upload em andamento e retornar/lançar de forma que o item seja marcado `Failed` (mas SEGUE o retry padrão do `PublisherJob`, diferente do caso de token — reler CA14/CA15: não dizem "sem retry", diferente de CA12/CA16 que dizem explicitamente "sem retry"; portanto timeout de chunk/total usa o mesmo caminho de exceção genérica que erro de rede/quota, caindo no retry padrão de até 3 tentativas do `PublisherJob`).

**Metadados:**
- `title`: `Product.Title[..Math.Min(100, Product.Title.Length)]`.
- `description`: `Product.AiCaption ?? string.Empty`.
- `tags`: `new[] { "oferta", "desconto", "promocao", "youtube" }` (fixo, platform sempre "youtube" pois é publisher dedicado).
- `categoryId`: dicionário estático `Product.Category → categoryId`, fallback `"22"` se categoria não mapeada (usar `TryGetValue`).
- `privacyStatus`: `"public"` fixo.

**Testes obrigatórios (unitários, mock do cliente Google e `HttpClient` — sem chamada real):**
- CA1: publicação com `MediaLocalPath` válido — sucesso, metadados corretos no payload.
- CA2: publicação com `MediaLocalPath` nulo e `MediaUrl` válido — aciona download antes do upload.
- CA5: title truncado em 100 chars.
- CA6: description = `AiCaption`.
- CA7: tags fixas com "youtube".
- CA8: categoryId mapeado para cada uma das 6 categorias da tabela.
- CA9: categoryId fallback "22" para categoria não mapeada.
- CA10: privacyStatus sempre "public".
- CA11: renovação de token expirado — novo `access_token` persistido em `app_settings`.
- CA12: falha de renovação de refresh_token — `Failed` sem retry + `youtube.token_invalid=true`.
- CA13: upload em chunks de 8MB (mock verificando número/tamanho de chunks enviados).
- CA14: timeout de chunk (5min) — abortar e marcar Failed (retry padrão, não CA12-style).
- CA15: timeout total (15min) — idem.
- CA16: fallback de segurança item sem vídeo — Failed sem retry, `ErrorMessage` exato.
- **CA17** (ProcessorJob): produto sem vídeo → nenhuma entrada `PublicationQueue` Youtube criada, `GenerateCaptionAsync` não chamado para Youtube.
- **CA18** (ProcessorJob): produto com vídeo → entrada Youtube criada normalmente com `ScheduledAt` do slot round-robin.
- **CA19** (ProcessorJob — REGRESSÃO, não-negociável): produto elegível para Telegram/Instagram/TikTok/Facebook (com ou sem vídeo) → comportamento de criação de entradas para essas redes idêntico ao anterior à mudança. Cobrir explicitamente com teste antes de considerar a task pronta.
- CA20: toda a suíte usa mocks (Google client + HttpClient), `dotnet test` verde, zero chamada real.

**Definição de pronto da sub-issue:**
- Todos os CA1-CA20 cobertos por teste e passando.
- `dotnet test` verde na branch antes de abrir o PR.
- PR único `feature/ISSUE-8-publisher-youtube` → `desenv`.

## T-02 — Fix: ErrorMessage sobrescrita no PublisherJob (achado E2E do QA, CA16)

Sub-issue #69. Bug funcional real (não lacuna de teste) encontrado pelo QA na validação
end-to-end contra a aplicação real (Docker + Postgres real): `PublisherJob.ExecuteAsync`
sobrescreve incondicionalmente a `ErrorMessage` que o `YoutubePublisher.FailPermanently`
ja havia registrado especificamente (CA16: "Produto sem midia de video, nao aplicavel ao
YouTube"), substituindo por uma mensagem generica.

**Decisao do LT — fix consistente com o padrao existente (ver `estado.md` para
justificativa completa da escolha de design vs alternativas descartadas):**

Em `PublisherJob.ExecuteAsync`, capturar `item.RetryCount` antes de `publisher.PublishAsync`.
Apos a chamada, so chamar `item.RegisterAttempt(false, mensagemGenerica)` quando o
`RetryCount` NAO mudou (ou seja, o publisher nao se auto-registrou). Quando o publisher ja
auto-registrou (RetryCount mudou, caso `YoutubePublisher.FailPermanently`), nao chamar
`RegisterAttempt` de novo — preserva a mensagem especifica. Sucesso continua chamando
`RegisterAttempt(true)` incondicionalmente.

Nao altera `ISocialPublisher` (contrato `Task<bool> PublishAsync`), nem
`PublicationQueue.RegisterAttempt`, nem `YoutubePublisher` — unico arquivo de producao
alterado: `PublisherJob.cs`. Compativel com `TelegramPublisher` (nunca se auto-registra,
comportamento inalterado).

**Escopo formal (sub-issue, nao branch de fix direto) — motivo:** ao contrario do gap de
cobertura do PR #68 (so testes, sem tocar codigo de producao), este fix altera codigo de
producao ja em producao (`PublisherJob.cs`, compartilhado por TODAS as redes, inclusive
Telegram/Issue #7 que ja esta em main). Risco de regressao em publisher que nao
auto-registra (Telegram) exige cobertura de teste explicita e rastreavel via CA formal
(CA21), nao apenas um teste avulso numa branch de fix. Justifica sub-issue.

**Criterios de aceite:** CA16 (revalidacao, ver criterios-aceite.md), CA21 (novo — regressao
Telegram/publishers que nao auto-registram), CA22 (novo — sucesso inalterado).

**Testes obrigatorios em `PublisherJobTests.cs`:**
- CA16: mock de `ISocialPublisher` que simula `FailPermanently` (chama `RegisterAttempt`
  internamente 3x com mensagem especifica antes de retornar `false`) — `PublisherJob` NAO
  deve sobrescrever a `ErrorMessage`.
- CA21: mock de `ISocialPublisher` que retorna `false` sem tocar o item (como
  `TelegramPublisher` hoje) — `PublisherJob` DEVE registrar a mensagem generica (regressao).
- CA22: mock retornando `true` — `Status=Published`, `ErrorMessage=null` (regressao).

**Definicao de pronto:** CA16/CA21/CA22 cobertos por teste e passando, suite completa
(128 + novos) sem regressao, `dotnet test` verde, boot Docker validado, PR unico
`feature/8-69-fix-publisherjob-errormessage` -> `desenv`.
