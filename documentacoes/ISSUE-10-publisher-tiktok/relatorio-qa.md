# Relatório QA — ISSUE-10: Publisher TikTok (Content Posting API)

**Status: APROVADO** (considerando CA20 como pendência conhecida, não bloqueante para este gate — decisão explícita do Gerente no Gate 1)

## Ambiente e sincronização
- `git fetch origin` + `git checkout homolog` + `git pull origin homolog` executados.
- Confirmado `b6149d8` (merge commit do PR #79) no topo de `git log --oneline -8` local após o pull. Estado remoto mergeado corretamente sincronizado antes de qualquer validação.

## Testes automatizados
- `dotnet test` (suíte completa, fora do Docker): **187/187 aprovados**, 0 falhas, ~25s. Confirma o número reportado pelo Dev e pelo Code Review com execução própria nesta rodada.
- Build: sem erros (1 warning pré-existente e não relacionado — `UsePostgreSqlStorage` obsoleto).

## Validação integrada (Docker Compose real — obrigatória)
Subida real via `docker compose up -d --build db api`:
- Build da imagem `omuletachou-api`: sucesso.
- `afiliado_db` (Postgres 16) e `afiliado_api` subiram e permaneceram `Up`.
- Log de boot: "No migrations were applied. The database is already up to date." (schema já incluía `SeedTikTokCredentials`), Hangfire instalado e conectado ao Postgres real, `Application started`.
- `GET /health` → **HTTP 200**, `{"status":"healthy",...}`.
- `GET /media/arquivo-inexistente-xyz.mp4` → **HTTP 404** (não 500) — repetição do teste de regressão da Issue #9 contra o container real; confirma que o fluxo de mídia estática do Instagram não foi afetado pela refatoração do disclosure helper.
- `POST /api/jobs/processor/trigger` → HTTP 200.
- `POST /api/jobs/publisher/trigger` → HTTP 200.
- Consulta direta ao Postgres real (`app_settings`) confirmou os placeholders semeados pela migration `SeedTikTokCredentials`: `tiktok.client_key`, `tiktok.client_secret`, `tiktok.refresh_token`, `tiktok.access_token`, `tiktok.open_id` (vazios, como esperado — sem credenciais reais), `tiktok.privacy_level = SELF_ONLY` (seed correto — CA14), `tiktok.min_duration_seconds = 3`, `tiktok.max_duration_seconds = 600` (seed correto — CA6/CA9), `networks.tiktok.enabled = true` (e `networks.instagram/youtube/facebook/telegram.enabled = true`, sem regressão nas demais redes).
- Containers derrubados ao final (`docker compose down`) — ambiente limpo, sem side effects deixados.

## Gate visual (Playwright/screenshots)
Verificação obrigatória por `package.json` (não por julgamento de plataforma): `dashboard/package.json` e `website/package.json` não possuem script `test:visual`. Adicionalmente, `git diff af9c26c..b6149d8 --stat -- dashboard website` não retornou nenhum arquivo alterado — a Issue é 100% backend (.NET).
**E2E/screenshots: N/A (projeto/mudança sem UI — sem script `test:visual` configurado nos front-ends do repo e nenhum arquivo de UI tocado no diff).**

## Inspeção qualitativa por critério de aceite

| CA | Descrição resumida | Evidência | Resultado |
|---|---|---|---|
| CA1 | Etapa 1 — init do upload (`POST /video/init/`, title/privacy_level/brand_content_toggle/disable_duet/disable_comment, captura upload_url/publish_id) | `TikTokPublisher.BuildInitRequest` + `ParseInitResponse` + teste `PublishAsync_ExecutaAs3Etapas_QuandoFluxoFeliz` | Passa |
| CA2 | Etapa 2 — upload chunked via PUT, `Content-Range`/`Content-Length` corretos | `UploadChunksAsync` (chunk 8MB, `ContentRangeHeaderValue`) + teste `PublishAsync_EnviaMultiplosChunks_QuandoArquivoMaiorQueUmChunk` | Passa |
| CA3 | Etapa 3 — polling a cada 15s até `PUBLISH_COMPLETE` | `PollPublishStatusAsync` (`_pollInterval` default 15s) + teste `PublishAsync_PollingContinuaAtePublishComplete_QuandoProcessingUpload` | Passa |
| CA4 | Polling `FAILED` → falha imediata com `ErrorMessage` | Trata `FAILED` explicitamente + teste `PublishAsync_FalhaImediatamente_QuandoStatusFailed` | Passa |
| CA5 | Timeout de 10min → `Failed` com retry (`RegisterAttempt`, não `FailPermanently`) | `_pollTimeout` default 10min + `CancelAfter` + teste `PublishAsync_MarcaFailedComRetry_QuandoTimeoutDePolling` | Passa |
| CA6 | Vídeo dentro do intervalo aceito (seed 3s-600s) segue fluxo normal | `Mp4DurationReader.TryGetDurationSeconds` + teste `PublishAsync_PassaNaValidacao_QuandoDuracaoDentroDoIntervalo` + seed confirmado no Postgres real (`min=3`, `max=600`) | Passa |
| CA7 | Vídeo abaixo do mínimo → `Failed` sem retry, sem chamada de upload | `FailPermanently` antes de qualquer request HTTP + teste `PublishAsync_FailPermanently_QuandoDuracaoAbaixoDoMinimo` | Passa |
| CA8 | Vídeo acima do máximo → `Failed` sem retry, sem chamada de upload | Mesmo bloco de validação + teste `PublishAsync_FailPermanently_QuandoDuracaoAcimaDoMaximo` | Passa |
| CA9 | Limites parametrizáveis via `app_settings`, sem deploy | `LoadCredentialsAsync` lê `tiktok.min_duration_seconds`/`max_duration_seconds` dinamicamente + teste `PublishAsync_RespeitaLimitesParametrizados_QuandoAppSettingsDiferentesDoSeed` | Passa |
| CA10 | Proporção/resolução não validadas client-side | Nenhuma checagem de aspect ratio no código + teste `PublishAsync_NaoValidaProporcaoOuResolucao_SeguindoParaUpload` | Passa |
| CA11 | `brand_content_toggle = true` sempre incluído no init | Hardcoded em `BuildInitRequest` (não condicional) — confirmado por leitura de código, coberto implicitamente por todos os testes de fluxo feliz | Passa |
| CA12 | Hashtag `#publi` anexada automaticamente | `SocialDisclosureHelper.AppendIfMissing` + teste `PublishAsync_AnexaHashtagPubli_QuandoLegendaNaoContem` | Passa |
| CA13 | Disclosure não duplicado quando já presente | Regex `#publi\|#publicidade` (case-insensitive) evita duplicação + teste (Theory) `PublishAsync_NaoDuplicaDisclosure_QuandoJaPresente` | Passa |
| CA14 | `privacy_level` padrão `SELF_ONLY` (seed) | `DefaultPrivacyLevel = "SELF_ONLY"` + teste `PublishAsync_EnviaSelfOnly_QuandoPrivacyLevelSeedPadrao` + seed confirmado no Postgres real (`tiktok.privacy_level = SELF_ONLY`) | Passa |
| CA15 | `privacy_level` alterável manualmente para `PUBLIC_TO_EVERYONE`, sem deploy | `LoadCredentialsAsync` lê `tiktok.privacy_level` de `app_settings` + teste `PublishAsync_EnviaPublicToEveryone_QuandoConfiguradoManualmente` | Passa |
| CA16 | Refresh automático em 401, token persistido, chamada original repetida | `SendAuthorizedWithRetryAsync` + `RefreshAccessTokenAsync` + `UpsertAppSettingAsync` + teste `PublishAsync_RenovaTokenERepeteChamada_Quando401NoInit` (e falha: `PublishAsync_FailPermanently_QuandoRenovacaoDeTokenFalha`) | Passa |
| CA17 | Backoff exponencial em 429 (3 tentativas, 2s/4s/8s) | `SendWithRetryAsync` com `_retryDelays` default `[2s,4s,8s]` + teste `PublishAsync_TentaNovamenteAposBackoff_Quando429NoInit` (e esgotamento: `PublishAsync_FalhaComRetryPadrao_QuandoBackoff429Esgotado`) | Passa |
| CA18 | `RetryCount` incrementado até 3, `Failed` definitivo ao esgotar | `RegisterAttempt`/`FailPermanently` (mesmo padrão dos demais publishers) + teste `PublishAsync_RetryCountAtingeMaximo_AposTresFalhasRecuperaveisConsecutivas` | Passa |
| CA19 | Testes com mocks, sem chamadas reais | Suíte `TikTokPublisherTests.cs` (29 testes) usa `HttpMessageHandler` mockado; `Mp4DurationReaderTests.cs`/`Mp4TestFileBuilder` geram MP4 sintético local, sem rede. `dotnet test`: 187/187 aprovados, sem nenhuma chamada real à API do TikTok | Passa |
| CA20 | Publicação real validada visualmente na conta TikTok | **Não avaliado nesta rodada** — pendente de aprovação do app TikTok for Developers (fora do controle do time, prazo 3-7 dias sem garantia). Conforme instrução explícita no spawn e decisão do Gerente no Gate 1 (Issue #10), este critério **NÃO bloqueia** o Gate 2 desta issue | Não avaliado — pendência conhecida, não bloqueante para este gate |

## Regressão do InstagramPublisher (refatoração do SocialDisclosureHelper)
- Leitura de código: `InstagramPublisher.PublishAsync` (linha 118) chama `SocialDisclosureHelper.AppendIfMissing(product.AiCaption ?? string.Empty)` — comportamento idêntico ao método privado antigo `AppendDisclosureIfMissing` (mesma regex `#publi\b|#publicidade\b`, mesma lógica de não duplicação), agora extraído para a classe compartilhada e reutilizado também pelo `TikTokPublisher`.
- Suíte de testes do Instagram (`InstagramPublisherTests.cs`) permanece 100% passando dentro dos 187/187 — nenhum teste de disclosure do Instagram quebrou com a extração.
- Validação end-to-end real contra o container Docker: `GET /media/arquivo-inexistente-xyz.mp4` → 404 (mesmo teste de regressão usado na Issue #9), `networks.instagram.enabled = true` confirmado no Postgres real, endpoints de trigger de jobs (`processor`/`publisher`) responderam HTTP 200 normalmente com o `InstagramPublisher` registrado no DI (nenhuma exceção de boot/DI causada pela extração do helper compartilhado).
- Nenhuma regressão identificada.

## Divergências / issues encontradas
Nenhuma. CA1-CA19 aprovados sem contradição de teste, comportamento divergente da especificação, ou regressão no Instagram.

## Nota sobre CA20 (não conta como reprovação)
CA20 exige publicação real em conta TikTok com confirmação visual — depende da aprovação do app TikTok for Developers para acesso pleno à Content Posting API, fora do controle do time. Diferente da Issue #9 (onde essa dispensa só foi esclarecida no Gate 2, causando confusão), aqui a decisão já foi tomada antecipadamente pelo Gerente no Gate 1 da Issue #10: CA20 é débito de validação registrado, não bloqueante para este Gate 2, a ser concluído com evidência assim que o app for aprovado.

## Conclusão
CA1-CA19: 100% aprovados, com evidência de execução real (testes automatizados 187/187 + boot Docker Compose real contra Postgres real, endpoints exercitados via HTTP real, seeds de `app_settings` confirmados no banco real, incluindo o caso de regressão do Instagram `/media/{inexistente}` → 404). CA20 não avaliado nesta rodada (pendência conhecida, explicitamente não bloqueante — decisão do Gerente no Gate 1). QA aprova o PR #79 para seguir ao Líder Técnico (PR homolog→main) e Gate 2 do Gerente.
