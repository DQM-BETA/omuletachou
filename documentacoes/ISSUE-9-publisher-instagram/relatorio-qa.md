# Relatório QA — ISSUE-9: Publisher Instagram (Meta Graph API)

**Status: APROVADO** (considerando CA20 como pendência conhecida, não bloqueante para este gate — ver nota no final)

## Ambiente e sincronização
- `git fetch origin` + `git checkout homolog` + `git pull origin homolog` executados.
- Confirmado `af9c26c` (merge commit do PR #75) no topo de `git log --oneline -5` local após o pull. Estado remoto mergeado corretamente sincronizado antes de qualquer validação.

## Testes automatizados
- `dotnet test` (suíte completa, fora do Docker): **156/156 aprovados**, 0 falhas, ~24s. Confirma o número reportado pelo Dev e pelo Code Review com execução própria nesta rodada.
- Build: `dotnet build` implícito no `dotnet test`, sem erros (1 warning pré-existente e não relacionado — `UsePostgreSqlStorage` obsoleto).

## Validação integrada (Docker Compose real — obrigatória)
Docker Desktop disponível nesta sessão (recuperado da infra reportada anteriormente como indisponível). Subida real via `docker compose up -d --build db api`:
- Build da imagem `omuletachou-api`: sucesso.
- `afiliado_db` (Postgres 16) e `afiliado_api` subiram e permaneceram `Up`.
- Log de boot: migrations aplicadas ("No migrations were applied. The database is already up to date." — banco já estava com o schema da migration `SeedInstagramCredentials`), Hangfire instalado e conectado ao Postgres real, `Application started`.
- `GET /health` → **HTTP 200**, `{"status":"healthy",...}`.
- `GET /media/arquivo-inexistente-xyz.mp4` (contra o container real, `UseStaticFiles`/`PhysicalFileProvider`) → **HTTP 404** (não 500). Confirma o comportamento exigido pela especificação técnica contra o ambiente real, não apenas via `WebApplicationFactory`/mock.
- `POST /api/jobs/processor/trigger` → HTTP 200.
- `POST /api/jobs/publisher/trigger` → HTTP 200.
- Consulta direta ao Postgres real (`app_settings`) confirmou os placeholders semeados pela migration `SeedInstagramCredentials`: `instagram.access_token`, `instagram.page_id`, `instagram.app_id`, `instagram.app_secret`, `instagram.token_expires_at` (vazios, como esperado — sem credenciais reais), `networks.instagram.enabled=true`, `instagram.token_invalid=false`.
- Containers derrubados ao final (`docker compose down`) — ambiente limpo, sem side effects deixados.

Esta é a primeira validação desta Issue com boot Docker real bem-sucedido (as tentativas anteriores do Dev, LT e Code Review falharam por indisponibilidade de infra, não por código).

## Gate visual (Playwright/screenshots)
Esta Issue é 100% backend (.NET) — nenhum arquivo de `dashboard/` ou `website/` foi alterado no diff do PR #75. Verificação obrigatória por `package.json` (não por julgamento de plataforma): `dashboard/package.json` e `website/package.json` não possuem script `test:visual` nem menção a Playwright.
**E2E/screenshots: N/A (projeto/mudança sem UI — sem script `test:visual` configurado nos front-ends do repo).**

## Inspeção qualitativa por critério de aceite

| CA | Descrição resumida | Evidência | Resultado |
|---|---|---|---|
| CA1 | Etapa 1 — criação do container (`POST /{ig-user-id}/media`, REELS, video_url, caption) | `InstagramPublisher.CreateMediaContainerAsync` + teste `PublishAsync_ExecutaAs3Etapas_QuandoFluxoFeliz` | Passa |
| CA2 | Polling a cada 3s até FINISHED | `PollContainerStatusAsync` (`_pollInterval` default 3s) + teste `PublishAsync_PollingContinuaAteFinished_QuandoInProgress` | Passa |
| CA3 | Etapa 3 — `media_publish` com creation_id | `PublishContainerAsync` + teste do fluxo feliz (mesmo CA1) | Passa |
| CA4 | Polling FAILED → falha imediata sem publish | Trata `FAILED`/`ERROR`/`EXPIRED` + teste `PublishAsync_FalhaImediatamente_QuandoStatusFalho` (Theory) | Passa |
| CA5 | Timeout de 2min → `Failed` com retry (`RegisterAttempt`, não `FailPermanently`) | `_pollTimeout` default 2min + `CancellationTokenSource.CancelAfter` + teste `PublishAsync_MarcaFailedComRetry_QuandoTimeoutDePolling` | Passa |
| CA6 | URL pública via `MediaLocalPath` + `api.public_base_url` | `ResolveVideoUrlAsync` + teste `PublishAsync_MontaUrlPublica_ViaMediaLocalPathEBaseUrl` | Passa |
| CA7 | Fallback para `MediaUrl` quando `MediaLocalPath` nulo | Mesmo método + teste `PublishAsync_UsaMediaUrl_QuandoMediaLocalPathNulo` | Passa |
| CA8 | URL de mídia inacessível → falso sem criar container | Retorno `null` de `ResolveVideoUrlAsync` interrompe antes de `CreateMediaContainerAsync` + teste `PublishAsync_FalhaSemCriarContainer_QuandoNenhumaMidiaResolvivel` | Passa |
| CA9 | Caption com tom Instagram, 3-5 hashtags, CTA, ≤300 chars | Fora do escopo desta issue (já implementado em `GenerateCaptionAsync`/`ClaudeAiService`, issue anterior) — não regressado; `InstagramPublisher` apenas consome `product.AiCaption` | Passa (não regredido) |
| CA10 | Disclosure anexado automaticamente e deterministicamente | `AppendDisclosureIfMissing` (regex, não depende do LLM) + teste `PublishAsync_AnexaDisclosure_QuandoLegendaNaoContem` | Passa |
| CA11 | Disclosure não duplicado | Mesmo método (regex `IsMatch` antes de anexar) + teste `PublishAsync_NaoDuplicaDisclosure_QuandoJaPresente` (Theory, variações de caixa) | Passa |
| CA12 | Renovação automática com margem de 7 dias | `NeedsRenewal` + `RenewTokenAsync` (fb_exchange_token) + teste `PublishAsync_RenovaToken_QuandoRestamMenosDe7Dias` | Passa |
| CA13 | Token com validade suficiente não renova | `NeedsRenewal` retorna false + teste `PublishAsync_NaoRenovaToken_QuandoValidadeSuficiente` | Passa |
| CA14 | Falha de renovação → `Failed` sem retry + `token_invalid=true` | `FailPermanently` + `UpsertAppSettingAsync("instagram.token_invalid","true")` + teste `PublishAsync_FailPermanently_QuandoRenovacaoDeTokenFalha` | Passa |
| CA15 | Item sem vídeo chega ao publisher (fallback de segurança) → Failed sem retry | `HasVideoMedia` + `FailPermanently` + teste `PublishAsync_FailPermanently_QuandoProdutoSemVideo` | Passa |
| CA16 | `ProcessorJob` não enfileira Instagram sem vídeo, sem chamar `GenerateCaptionAsync` | `HasVideoAvailable` generalizado (Youtube + Instagram) em `CreatePublicationQueueEntriesAsync` + teste `ExecuteAsync_NaoCriaEntradaInstagram_QuandoProdutoSemVideo` (valida `Times.Never` do mock de IA) | Passa |
| CA17 | `ProcessorJob` enfileira Instagram normalmente com vídeo, no slot round-robin | Mesmo trecho + teste `ExecuteAsync_CriaEntradaInstagram_QuandoProdutoComVideo` (valida `ScheduledAt.Hour == 9` e `Times.Once`) | Passa |
| CA18 | Regressão zero nas demais redes (Telegram/Youtube/TikTok/Facebook) | Filtro `HasVideoAvailable` aplicado só a `Youtube`/`Instagram`; demais redes seguem fluxo antigo + teste `ExecuteAsync_NaoAfetaDemaisRedes_QuandoYoutubeEInstagramFiltrados` | Passa |
| CA19 | Testes com mocks, sem chamadas reais | Suíte completa `InstagramPublisherTests.cs`/`ProcessorJobTests.cs`/`JobsTriggerTests.cs` usa `HttpMessageHandler` mockado e `Moq`/EF InMemory — nenhuma chamada de rede real. `dotnet test`: 156/156 aprovados | Passa |
| CA20 | Publicação real validada visualmente na conta Instagram | **Não avaliado nesta rodada** — pendente de credenciais reais do Gerente. Fora do escopo desta validação (conforme instrução explícita no spawn). Ver `estado.md` (`ca20_pendente: true`) | Não avaliado — pendência conhecida, não bloqueante para este gate |

## Validação integrada adicional
- Migration `SeedInstagramCredentials` aplicada corretamente contra Postgres real (sem erro, placeholders vazios conforme esperado — sem secrets hardcoded).
- `Program.cs`: `UseStaticFiles` com `PhysicalFileProvider` mapeado em `/media`, `Directory.CreateDirectory` prévio evita exceção em ambiente novo — comportamento confirmado em boot real.
- `NetworkSettings` em `ProcessorJob` inclui `instagram.access_token`/`instagram.page_id` como credenciais obrigatórias, consistente com `InstagramPublisher.LoadCredentialsAsync`.

## Divergências / issues encontradas
Nenhuma. Nenhum critério de aceite (CA1-CA19) apresentou falha, contradição de teste, ou comportamento divergente da especificação. Nenhuma regressão identificada nas demais redes.

## Nota sobre CA20 (não conta como reprovação)
CA20 exige publicação real em conta Instagram Business/Creator com confirmação visual — depende de credenciais reais que o Gerente ainda não forneceu (`estado.md`: `ca20_pendente: true`). Por instrução explícita do spawn desta tarefa, CA20 está **fora do escopo desta rodada de QA** e não bloqueia a aprovação do merge `homolog→main` neste momento — mas segue **bloqueante para o Gate 2** (aprovação final do Gerente), que deve exigir a evidência de CA20 antes de autorizar produção, conforme definição de pronto estabelecida no PRD.

## Conclusão
CA1-CA19: 100% aprovados, com evidência de execução real (testes automatizados + boot Docker Compose real contra Postgres real, endpoints exercitados via HTTP real, incluindo o caso crítico `/media/{inexistente}` → 404). CA20 não avaliado (pendência conhecida, não bloqueante para este gate). QA aprova o PR #75 para seguir ao Líder Técnico (PR homolog→main) e Gate 2 do Gerente — reforçando que o Gerente deve exigir evidência de CA20 antes de aprovar o merge final para `main`.
