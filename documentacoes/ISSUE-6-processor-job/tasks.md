# Task Breakdown — Issue #6: Processor Job

## Decisão de particionamento

Duas sub-issues, ambas stack `dotnet`, com **dependência sequencial** (T-02 depende de T-01
mergeado em `desenv`):

- **T-01 — Fundações**: `LocalMediaStorage`, migration `AddMediaLocalPathToProducts` (+ enum
  `Processing`/`Error`), `CategoryDetector`. São 3 unidades pequenas e testáveis de forma
  isolada (sem dependência do fluxo do job), com contrato de saída bem definido para o T-02
  consumir (`LocalMediaStorage.DownloadAsync(url) -> (localPath, mediaType)`,
  `CategoryDetector.Detect(title) -> category`).
- **T-02 — ProcessorJob**: orquestração completa (`ExecuteAsync`), única unidade lógica que
  amarra busca de produtos, mudança de estado, mídia, slug, categoria, AffiliateLink ML,
  geração de legenda via IA e criação da fila com round-robin. Não faz sentido fatiar mais —
  é uma máquina de estados coesa por produto, cujos testes (given/when/then dos critérios
  CA4-CA21) exercitam o fluxo fim-a-fim.

Justificativa de **não** fazer 1 única sub-issue: T-01 tem componentes independentes e
testáveis sem qualquer conhecimento do job (download HTTP e matching de string), enquanto T-02
é fluxo de negócio propriamente dito. Separar reduz o escopo de revisão de cada PR e permite
começar T-02 already tendo os componentes de T-01 prontos e testados (evita retrabalho se
o contrato de `LocalMediaStorage`/`CategoryDetector` precisar ajuste após review).

---

## T-01 — LocalMediaStorage + Migration + CategoryDetector

**Repo:** DQM-BETA/omuletachou · **Stack:** dotnet · **Base:** `desenv`

### Escopo
1. **Migration `AddMediaLocalPathToProducts`** (incremental, NÃO alterar a migration inicial):
   - Adicionar coluna `MediaLocalPath` (`string?`, nullable) em `Products`.
   - Adicionar propriedade `MediaLocalPath` em `Product` (getter público, setter privado).
   - Adicionar valores `Processing` e `Error` ao enum `ProductStatus` (aditivo — não remover/
     renomear `Pending, Queued, Published, Rejected`).
   - Métodos de domínio em `Product`: `MarkAsProcessing()` (Status=Processing, UpdatedAt=UtcNow)
     e `MarkAsError(string reason)` (Status=Error, persiste mensagem — reaproveitar `AiReason`
     ou criar campo próprio; documentar a escolha no PR) e setter para `MediaLocalPath`
     (ex.: `SetLocalMedia(string? localPath, string? mediaType)`).

2. **`LocalMediaStorage`** (`AfiliadoBot.Infrastructure`, seguir padrão HTTP de
   `MercadoLivreCollector.cs`: `HttpClient` injetado, sem exception não capturada, log
   estruturado via `ILogger`):
   - `Task<(string? LocalPath, string MediaType)> DownloadAsync(string mediaUrl, CancellationToken ct)`
   - Baixa o arquivo de `mediaUrl` para `/app/media/` (nome de arquivo único, ex. `Guid` + extensão
     original da URL).
   - Detecção de tipo por extensão: `.mp4`/`.webm` → `"video"`; qualquer outra extensão →
     `"image"` (aplica-se tanto no sucesso quanto no fallback de falha, inferindo pela URL original).
   - Falha (404, timeout, URL malformada, exceção de rede): **não propaga exception** — retorna
     `LocalPath = null` e `MediaType` inferido pela extensão da URL original; loga `Warning`.
   - Volume `/app/media` já configurado via docker-compose (persistente) — não mexer em infra.

3. **`CategoryDetector`** (classe estática, `AfiliadoBot.Application`, sem dependência de IA/banco):
   - `static string Detect(string title)`
   - Dicionário estático `categoria → List<string> palavras-chave` (case-insensitive,
     `Contains`/`IndexOf` com `StringComparison.OrdinalIgnoreCase`). Popular com um conjunto
     inicial razoável de categorias/palavras-chave (ex.: "Eletrônicos" ← fone, celular, notebook,
     tv, câmera; "Casa" ← panela, cama, sofá, decoração; "Moda" ← camisa, tênis, bolsa, relógio;
     "Beleza" ← perfume, maquiagem, creme; demais a critério do dev, documentar no PR).
   - Primeira categoria com match (ordem do dicionário) vence; sem nenhum match → `"Geral"`.

### Critérios de aceite (Given/When/Then)
Cobrir integralmente: **CA1, CA2, CA3** (LocalMediaStorage), **CA10, CA11** (CategoryDetector),
**CA20** (migration aplicada, coluna nullable, migration inicial intacta). Ver detalhes em
`documentacoes/ISSUE-6-processor-job/criterios-aceite.md`.

### Contexto técnico
- Docs: `documentacoes/ISSUE-6-processor-job/prd.md`, `criterios-aceite.md`
- Design: sem design.md (PM/LT sem ambiguidade arquitetural — ver `estado.md`)
- Padrão de referência HTTP: `backend/src/AfiliadoBot.Infrastructure/Integrations/Platforms/MercadoLivreCollector.cs`
- Entidades: `backend/src/AfiliadoBot.Domain/Entities/Product.cs`,
  `backend/src/AfiliadoBot.Domain/Enums/ProductStatus.cs` (ajustar)
- Stack: .NET 8, EF Core 8 (migrations), PostgreSQL 16
- Repo: `repos/omuletachou` (branch `feature/ISSUE-<sub>-local-media-storage`, base `desenv`)
- Testes: cobertura dos CAs listados acima; seguir convenções de teste já usadas no repo
  (`backend/tests/`, se existente — checar padrão nos testes de `MercadoLivreCollector`)

---

## T-02 — ProcessorJob.ExecuteAsync (orquestração completa)

**Repo:** DQM-BETA/omuletachou · **Stack:** dotnet · **Base:** `desenv`
**Depende de:** T-01 mergeado em `desenv` (usa `LocalMediaStorage` e `CategoryDetector`)

### Escopo
Implementar `ProcessorJob.ExecuteAsync(CancellationToken ct)` (Hangfire job), orquestrando
por produto elegível:

1. **Busca**: `Status = Queued` (nunca `Rejected` — CA7). Ordenar o lote por `AiScore` desc
   (necessário para o round-robin do passo 6).
2. **Lock otimista**: para cada produto, `Status = Processing` (via `Product.MarkAsProcessing()`)
   **imediatamente**, antes de qualquer outra operação, e persistir (SaveChanges) antes de seguir
   para as próximas etapas desse produto — evita colisão entre execuções paralelas do Hangfire (CA4).
3. **Mídia**: se `MediaUrl != null`, chamar `LocalMediaStorage.DownloadAsync`. Sucesso preenche
   `MediaLocalPath`/`MediaType`; falha não bloqueia o processamento (produto segue, log Warning,
   `MediaLocalPath` nulo) — CA1, CA2, CA3.
4. **Slug**: se `Product.Slug` já preenchido → pular (CA8). Se nulo/vazio → gerar
   `Slugify(Title) + "-" + Id.ToString()[..6]` e persistir (CA9).
5. **Categoria**: `CategoryDetector.Detect(Title)`, substitui o `"Geral"` hardcoded quando há
   match mais específico (CA10, CA11).
6. **AffiliateLink MercadoLivre**: se `Platform == MercadoLivre` e `AffiliateLink` nulo, chamar
   `POST https://api.mercadolibre.com/affiliate-tools/links` com body `{"url": permalink}`
   (usar `Product` já existente para obter a URL do produto — checar se há campo de permalink
   ou usar `ImageUrl`/URL construída a partir de `ExternalId`; documentar a escolha no PR),
   seguindo o padrão de client HTTP de `MercadoLivreCollector` (sem exception não capturada,
   log estruturado). Sucesso → `Product.SetAffiliateLink(link)` (CA12). Amazon/Shopee: nenhuma
   chamada adicional, valor do collector preservado (CA13). Falha (HTTP não-2xx ou exceção) →
   `Product.MarkAsError(mensagem descritiva)`, **não cria nenhuma entrada de fila** para esse
   produto, segue para o próximo produto do lote (CA14, CA6).
7. **Legendas + Fila de publicação**: ler `networks.*.enabled` de `app_settings` (padrão de
   leitura: ver `LoadSettingsAsync` em `MercadoLivreCollector.cs`). Para cada rede habilitada:
   - Sem credenciais configuradas (`{rede}.access_token` ou equivalente ausente/vazio em
     `app_settings`) → pular a rede, **não cria entrada de fila**, log Warning (CA18).
   - Com credenciais → chamar `IAiService.GenerateCaptionAsync` para gerar a legenda, criar
     `PublicationQueue(productId, socialNetwork, scheduledAt)`:
     - Facebook → forçar `Status = ManualPending` após construção (CA15). Checar se
       `PublicationQueue`/`PublicationStatus` precisam de ajuste para permitir `ManualPending`
       sem `ScheduledAt` automático — se o construtor atual sempre seta `Scheduled`, avaliar
       adicionar um construtor/factory ou método `MarkAsManualPending()` (mudança pontual,
       documentar no PR).
     - Demais redes → `Status = Scheduled`, `ScheduledAt` calculado pelo round-robin (passo 8) (CA16).
   - Uma entrada por rede habilitada+com credenciais (CA19).
8. **Round-robin de `ScheduledAt`**: horários `9h/12h/15h/18h/20h` (UTC-3), offset aleatório
   0-10min. Produtos do lote ordenados por `AiScore` desc definem a posição round-robin
   (índice 0→9h, 1→12h, ..., 5→9h do dia seguinte, etc. — CA17). Calcular os slots para o
   lote inteiro numa única execução do job (sem depender de estado persistido entre ciclos,
   salvo se o dev identificar necessidade de continuidade — decisão de implementação, documentar
   no PR se divergir).
9. **Finalização**: todas as entradas de fila criadas com sucesso → `Product.MarkAsPublished()`
   (reaproveita `MarkAsPublished()` já existente) (CA5).
10. **Encadeamento**: garantir/confirmar que `CollectorJob` já enfileira `ProcessorJob` via
    `BackgroundJob.Enqueue` ao final do ciclo de coleta (CA21) — se não estiver feito, ajustar
    `CollectorJob` (mudança pequena e localizada, não é escopo de repensar o Collector).

### Critérios de aceite (Given/When/Then)
Cobrir integralmente: **CA4, CA5, CA6, CA7** (máquina de estados), **CA8, CA9** (slug),
**CA12, CA13, CA14** (AffiliateLink ML), **CA15, CA16, CA17, CA18, CA19** (fila/round-robin),
**CA21** (encadeamento). Ver detalhes em `documentacoes/ISSUE-6-processor-job/criterios-aceite.md`.

### Contexto técnico
- Docs: `documentacoes/ISSUE-6-processor-job/prd.md`, `criterios-aceite.md`
- Design: sem design.md (ver `estado.md`)
- Depende de: `LocalMediaStorage` e `CategoryDetector` (T-01, deve estar mergeado em `desenv`)
- Entidades: `Product.cs`, `PublicationQueue.cs`, `Domain/Enums/ProductStatus.cs`,
  `Domain/Enums/PublicationStatus.cs`, `Domain/Enums/SocialNetwork.cs`
- Padrão HTTP/app_settings: `MercadoLivreCollector.cs` (client HTTP, tratamento de erro,
  leitura de `AppSettings` por chave)
- Interface existente: `IAiService.GenerateCaptionAsync` (mesma usada no scoring dos collectors)
- Stack: .NET 8, Hangfire, EF Core 8, PostgreSQL 16
- Repo: `repos/omuletachou` (branch `feature/ISSUE-<sub>-processor-job`, base `desenv`)
- Testes: cobertura dos CAs listados acima; mocks de `IAiService`, `HttpClient` (handler),
  `DbContext` (in-memory ou fixture já usada no repo)
