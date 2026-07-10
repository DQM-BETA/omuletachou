# Tasks — ISSUE-9: Publisher Instagram (Meta Graph API)

## Decisão de escopo: 1 sub-issue única
Seguindo o padrão já validado da Issue #8 (`YoutubePublisher + fix retroativo no ProcessorJob`,
sub-issue #65 — única), esta issue também é entregue em **uma única sub-issue**, não split.
Justificativa:
- Mesma natureza de mudança: um novo `ISocialPublisher` aditivo + um fix retroativo pequeno e
  isolado no `ProcessorJob` (generalização de uma condição já existente, sem lógica nova).
  As duas partes são fortemente acopladas (o fix do `ProcessorJob` só faz sentido junto com o
  novo publisher — sem ele, nada testa o "produto sem vídeo não é mais enfileirado para
  Instagram" de ponta a ponta) e pequenas o suficiente para não justificar overhead de
  coordenação entre duas sub-issues/PRs.
- Sem ambiguidade arquitetural, sem nova infraestrutura, sem necessidade de UI (backend puro).
- `UseStaticFiles` em `Program.cs` é uma linha de configuração adicional a esta mesma sub-issue
  (não split): é pré-requisito direto do `InstagramPublisher` (mídia local precisa virar URL
  pública), sem valor isolado sem o publisher.

## T-01 — InstagramPublisher + fix retroativo ProcessorJob + UseStaticFiles

### O que fazer
1. Criar `InstagramPublisher : ISocialPublisher` em
   `backend/src/AfiliadoBot.Infrastructure/Integrations/Social/InstagramPublisher.cs`, seguindo
   a estrutura do `YoutubePublisher.cs` (DI: `HttpClient`, `AfiliadoBotDbContext`, `ILogger`;
   sem necessidade de `IMediaStorage` — ver especificação técnica, item 4):
   - Carregar credenciais de `app_settings` (`instagram.access_token`, `instagram.app_id`,
     `instagram.app_secret`, `instagram.page_id`, `instagram.token_expires_at`,
     `instagram.token_invalid`); ausência de credenciais essenciais → `InvalidOperationException`
     (mesmo padrão do `YoutubePublisher`).
   - Verificar validade do token (margem 7 dias) e renovar via `fb_exchange_token` quando
     necessário (CA12/CA13); persistir novo token/expiração; falha de renovação →
     `FailPermanently` + `instagram.token_invalid=true` (CA14).
   - Fallback de segurança: produto sem `MediaType="video"` (nem `MediaLocalPath`/`MediaUrl`)
     → `FailPermanently` com mensagem descritiva (CA15).
   - Resolver `video_url` pública: `MediaLocalPath` → URL pública via base configurada;
     fallback `MediaUrl`; nenhum dos dois acessível → `false` sem criar container (CA6/CA7/CA8).
   - Montar caption: `product.AiCaption` + disclosure `#publi`/`#publicidade` anexado se
     ausente, sem duplicar se já presente (CA9/CA10/CA11) — ver especificação técnica item 6
     para a decisão de pós-processamento isolado (NÃO alterar `ClaudeAiService`).
   - Etapa 1: `POST /{ig-user-id}/media` (`media_type=REELS`, `video_url`, `caption`) → capturar
     `creation-id` (CA1).
   - Etapa 2: polling `GET /{creation-id}?fields=status_code` a cada 3s, timeout total 2min;
     `FINISHED` → prossegue; `FAILED` → falha imediata sem retry adicional além do padrão normal
     (CA4); timeout → `Failed` com retry padrão via `RegisterAttempt` (CA5, CA2).
   - Etapa 3: `POST /{ig-user-id}/media_publish` com `creation_id` → `true` em sucesso (CA3).
2. Registrar `InstagramPublisher` no DI em `Program.cs` (mesmo padrão de
   `AddHttpClient<YoutubePublisher>`/`TelegramPublisher`).
3. Adicionar `app.UseStaticFiles(...)` em `Program.cs` mapeando o diretório físico usado por
   `LocalMediaStorage` para `RequestPath="/media"` (confirmar path raiz lendo
   `LocalMediaStorage.cs`) — posicionar antes de `app.UseHangfireDashboard`.
4. Fix retroativo em `ProcessorJob.CreatePublicationQueueEntriesAsync`
   (`backend/src/AfiliadoBot.Application/Jobs/ProcessorJob.cs`, ~linha 247): generalizar a
   condição `network == SocialNetwork.Youtube && !HasVideoAvailable(product)` para também cobrir
   `SocialNetwork.Instagram`, reaproveitando o método `HasVideoAvailable` já existente (CA16/CA17).
5. Testes (mock de `HttpMessageHandler`, seguindo `YoutubePublisherTests.cs`): cobrir CA1-CA19
   (todos exceto CA20). Incluir teste de regressão explícito garantindo que Telegram, Youtube,
   TikTok e Facebook permanecem inalterados no `ProcessorJob` (CA18).
6. **CA20 (definição de pronto, fora do CI)**: solicitar ao Gerente as credenciais reais de
   teste (`instagram.access_token`, `instagram.app_id`, `instagram.app_secret`,
   `instagram.page_id` de uma conta Business/Creator já onboardada). Publicar um Reel de teste
   contra a API real, confirmar visualmente no perfil, validar a legenda com disclosure.
   Evidenciar (print/link do post) no PR ou na sub-issue antes de considerar a task pronta —
   **sem essa evidência, não solicitar o Gate 2**.

### Critérios de aceite
Ver `documentacoes/ISSUE-9-publisher-instagram/criterios-aceite.md` — CA1 a CA20 completos
(CA1-CA19 automatizados via mock; CA20 validação manual em conta real, obrigatória e
não-negociável antes do Gate 2).

### Contexto técnico
- PRD: `documentacoes/ISSUE-9-publisher-instagram/prd.md`
- Critérios de aceite: `documentacoes/ISSUE-9-publisher-instagram/criterios-aceite.md`
- Especificação técnica (contratos de API, schema, padrões obrigatórios): `documentacoes/ISSUE-9-publisher-instagram/especificacao-tecnica.md`
- Design resumido: `openspec/changes/issue-9-publisher-instagram/design.md`
- Stack: .NET 8, Meta Graph API (chamadas HTTP diretas via `HttpClient`, sem SDK), EF Core 8
- Repo: `DQM-BETA/omuletachou`, branch base: `desenv`
- Referências de padrão (ler antes de implementar):
  - `backend/src/AfiliadoBot.Infrastructure/Integrations/Social/YoutubePublisher.cs` (padrão de
    renovação de token, `FailPermanently`, resolução de mídia)
  - `backend/src/AfiliadoBot.Infrastructure/Integrations/Social/TelegramPublisher.cs`
  - `backend/src/AfiliadoBot.Domain/Interfaces/ISocialPublisher.cs`
  - `backend/src/AfiliadoBot.Application/Jobs/ProcessorJob.cs` (método `HasVideoAvailable`,
    ~linha 270, e `CreatePublicationQueueEntriesAsync`, ~linha 225)
  - `backend/src/AfiliadoBot.Infrastructure/Services/ClaudeAiService.cs` (tom Instagram já
    existente no prompt — NÃO alterar para o disclosure)
  - `backend/src/AfiliadoBot.Tests/Integrations/YoutubePublisherTests.cs` (padrão de mock de
    `HttpClient`/`HttpMessageHandler` para os testes)
