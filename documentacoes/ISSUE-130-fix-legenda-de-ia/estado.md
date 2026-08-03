issue: 130
titulo: "fix: Legenda de IA nunca é persistida — todo post sai sem legenda"
etapa_atual: Em Desenvolvimento
ultimo_agente: lider-tecnico
openspec_change: openspec/changes/issue-130-fix-legenda-de-ia
tech_stacks:
  - dotnet
  - angular
repos:
  omuletachou: "repos/omuletachou"
repo_path: repos/omuletachou
docs_path: repos/omuletachou/documentacoes/ISSUE-130-fix-legenda-de-ia
openspec_path: repos/omuletachou/openspec/changes/issue-130-fix-legenda-de-ia
sub_issues:
  - "#139 (stack:dotnet, task_id: Sub-A) — backend: migration, PublicationQueue.Caption, ProcessorJob, 4 publishers, ProductDetailDto, ProcessorJobTests.cs — BLOQUEANTE"
  - "#140 (stack:angular, task_id: Sub-B) — frontend: ProductDetail/ProductsService + facebook-manual.component consomem ai_caption — depende do contrato do DTO da Sub-A"
desenv_tasks_merged: []
sub_issues_frontend:
  "#140": angular
pr_homologacao: ~
pr_release: ~
code_review_homolog_pr: ~
qa_status: ~
figma_url: ~
blockers: nenhum
status_comment_id: 5167525186

## PM Fase 1
Levantamento de requisitos postado na Issue #130 (comentário https://github.com/DQM-BETA/omuletachou/issues/130#issuecomment-5167522736), com 5 perguntas objetivas para o Gerente:
1. Onde armazenar a legenda por rede social (novo campo `Caption` em `PublicationQueue` vs. gerar no momento da publicação vs. outra abordagem).
2. Quando gerar a legenda (no enfileiramento vs. no momento da publicação).
3. Se a correção deve expor a legenda de IA no Facebook Manual (dashboard) ou fica fora do escopo.
4. Se é necessário registrar/comunicar a retrocompatibilidade (produtos já publicados sem legenda) ou apenas seguir em frente.
5. Confirmação de que a cobertura de teste (`ProcessorJobTests.cs`) será corrigida para validar persistência, não só a chamada.

Aguardando respostas do Gerente (Gate 1) para prosseguir com PM Fase 2 (proposal.md + critérios de aceite).

## PM Fase 2
Gerente respondeu ao Gate 1 (comentário https://github.com/DQM-BETA/omuletachou/issues/130#issuecomment-5169630106):
1. Novo campo `Caption` em `PublicationQueue` — fonte de verdade para publicação. `Product.AiCaption` pode ser removido ou mantido só para propósitos não-autoritativos. Migration: `ALTER TABLE publication_queue ADD COLUMN caption TEXT NOT NULL DEFAULT ''`.
2. Geração mantida no `ProcessorJob` (não move para `PublisherJob`) — evita multiplicar chamadas pagas à API Claude em retries e preserva separação de responsabilidades.
3. Facebook Manual no escopo: `ProductDetailDto` (backend) + `ProductDetail` (frontend) passam a expor/consumir a caption real da rede Facebook.
4. Sem backfill/retrocompatibilidade — só uma linha de changelog no PR.
5. Confirmado: `ProcessorJobTests.cs` corrigido para validar persistência (`PublicationQueue.Caption`), não apenas chamada de mock.

PRD consolidado e postado (comentário https://github.com/DQM-BETA/omuletachou/issues/130#issuecomment-5169632533):
- `openspec/changes/issue-130-fix-legenda-de-ia/proposal.md`
- `documentacoes/ISSUE-130-fix-legenda-de-ia/criterios-aceite.md` (CA1–CA18)

**Ambiguidade arquitetural:** nenhuma. Todas as decisões de design vieram definidas pelo Gerente no Gate 1 (campo, ponto de geração, escopo do Facebook Manual, migration aditiva simples). Sem múltiplas stacks em conflito, integração externa nova, ou trade-off de arquitetura não-óbvio. Segue direto para o Líder Técnico (refinamento técnico / task breakdown), sem passar pelo Arquiteto.

## Líder Técnico (refinamento)
Código real inspecionado antes do design (`ProcessorJob.cs`, os 4 publishers, `PublicationQueue.cs`,
`ProductDtos.cs`/`ProductsController.cs`, `facebook-manual.component.ts/html`, `ProcessorJobTests.cs`):
confirmado que `GenerateCaptionAsync` é chamado em `CreatePublicationQueueEntriesAsync` mas o retorno é
descartado (linha 256 do `ProcessorJob.cs`); os 4 publishers leem `product.AiCaption` (nunca escrito por
código ativo); o Facebook Manual usa `post.product?.description` como legenda (mesmo bug na ponta do
dashboard).

Documentação escrita:
- `openspec/changes/issue-130-fix-legenda-de-ia/design.md` (resumido — sem ambiguidade arquitetural).
- `documentacoes/ISSUE-130-fix-legenda-de-ia/especificacao-tecnica.md` (contratos exatos: migration,
  construtor de `PublicationQueue`, trechos exatos dos 4 publishers, `ProductDetailDto.ai_caption`,
  `ProductsController.GetProduct`, `ProductDetail.ai_caption` no Angular, correção de
  `ProcessorJobTests.cs`).
- `openspec/changes/issue-130-fix-legenda-de-ia/tasks.md` (por sub-issue, critérios + contexto técnico).

Sub-issues criadas:
- #139 — Sub-A (backend, `stack:dotnet`, BLOQUEANTE): migration + `PublicationQueue.Caption` +
  `ProcessorJob` + 4 publishers + `ProductDetailDto`/`ProductsController` + `ProcessorJobTests.cs`.
- #140 — Sub-B (frontend, `stack:angular`, depende do contrato do DTO da Sub-A): `ProductDetail`/
  `ProductsService` + `facebook-manual.component` consomem `ai_caption`.

**Decisão UX/UI:** não acionado. Nenhuma tela/componente novo — apenas troca da fonte de dados de um texto
já existente na tela (campo de legenda), documentado em design.md.

Sumário técnico postado na Issue #130 (comentário
https://github.com/DQM-BETA/omuletachou/issues/130#issuecomment-5169710249). Comentário 📍 Status editado
para "Em Desenvolvimento".

## Dev Sub-A #139
Worktree `.worktrees/139-fix-caption-backend` (branch `fix/139-caption-backend`, a partir de `desenv`).

Implementado (TDD RED→GREEN):
- Migration `AddCaptionToPublicationQueue` (`ALTER TABLE publication_queue ADD COLUMN caption TEXT NOT NULL
  DEFAULT ''`), gerada via `dotnet ef migrations add` — SQL efetivo conferido, igual ao especificado.
- `PublicationQueue.cs`: nova propriedade `Caption`; construtor com 4º parâmetro `caption` (breaking change
  interno — único caller de produção era `ProcessorJob`; todos os ~80 call sites de teste em
  `PublicationQueueTests.cs`, `ReportsControllerTests.cs`, `QueueControllerTests.cs`,
  `InstagramPublisherTests.cs`, `PublisherJobTests.cs`, `TelegramPublisherTests.cs`,
  `TikTokPublisherTests.cs`, `YoutubePublisherTests.cs` atualizados para o novo construtor).
- `PublicationQueueConfiguration.cs`: mapeamento da coluna `caption`.
- `ProcessorJob.cs` (`CreatePublicationQueueEntriesAsync`): retorno de `GenerateCaptionAsync` agora
  persistido no item de `PublicationQueue` criado por rede (antes descartado).
- 4 publishers (`TelegramPublisher`, `YoutubePublisher` — `BuildMetadataJson` ganhou parâmetro `caption`,
  `InstagramPublisher`, `TikTokPublisher`): leitura trocada de `product.AiCaption` para `item.Caption`.
  Confirmado via grep: nenhuma referência a `AiCaption` restante nos 4 arquivos (CA5).
- `ProductDetailDto`/`ProductsController.GetProduct`: novo campo `ai_caption`, resolvido do item de
  `PublicationQueue` mais recente (`OrderByDescending(CreatedAt)`) da rede Facebook associado ao produto —
  `null` quando não existe item para essa rede (distinto de `""`, valor válido para itens legados).
- `ProcessorJobTests.cs`: mock de `GenerateCaptionAsync` retorna valor determinístico por rede (`$"Legenda
  {network}"`); asserts de persistência real em `PublicationQueue.Caption` (CA15); novo teste de múltiplas
  redes habilitadas comprovando captions distintas sem sobrescrita (CA16); testes `Times.Never` existentes
  (CA17) mantidos, já continham `entries.Should().BeEmpty()/NotContain(...)`.
- 2 novos testes em `ProductsControllerTests.cs` (CA12): `ai_caption` retorna a caption do item Facebook
  mais recente (múltiplos itens, incl. Telegram, para comprovar filtro por rede) / `null` quando não há
  item de fila para Facebook.
- Corrigidos 3 testes de publisher que quebraram silenciosamente com a extração mecânica dos call sites
  (`InstagramPublisherTests.PublishAsync_AnexaDisclosure_QuandoLegendaNaoContem`,
  `PublishAsync_NaoDuplicaDisclosure_QuandoJaPresente`, `TikTokPublisherTests` equivalentes): a legenda de
  teste precisa vir de `item.Caption`, não mais de `product.AiCaption`.

Gate de testes: `dotnet test` → **321/321 passando (100%)**.

Boot real (evidência, não suposição): `.env` local criado (gitignored) com `DB_USER`/`DB_PASSWORD`/
`JWT_SIGNING_KEY`/seed de usuário; `docker compose up -d --build db api` — API sobe sem exceção (migration
aplicada automaticamente no startup, seed de usuário ok, Hangfire registrado). Verificação funcional via
`psql` + chamadas HTTP internas ao container (`docker exec afiliado_api curl ...`):
- `\d publication_queue` confirma coluna `caption text NOT NULL DEFAULT ''::text` (CA1).
- Produto de teste inserido (`Status=Queued`, Telegram habilitado) + `POST /api/jobs/processor/trigger`
  (autenticado) → `publication_queue.caption` preenchida com texto real e não vazio (fallback determinístico
  do `ClaudeAiService.GenerateCaptionAsync` quando a API Claude não está configurada no ambiente local, que
  nunca lança exceção — comportamento preexistente, CA6 preservado).
- Item de fila Facebook inserido manualmente com caption própria → `GET /api/products/{id}` retornou
  `"ai_caption":"Legenda Facebook de teste real via banco"`, confirmando CA12 fim-a-fim.
- Dados de teste removidos, `docker compose down`, `.env` local removido ao final (não versionado).

PR aberto: https://github.com/DQM-BETA/omuletachou/pull/141 (`fix/139-caption-backend` → `desenv`, contém
nota de changelog CA18 no corpo do PR). Worktree removido após push.

## Custo (ledger)
| # | Etapa | Agente | Modelo | Tokens | Tools | Tempo (s) |
|---|-------|--------|--------|--------|-------|-----------|
| 1 | Preparacao (compartilhada com #130/#131/#132/#133) | coordenador | haiku-4.5 | 34090 | 19 | 271s |
| 2 | PM Fase 1 | pm-analista-negocios | sonnet | 29010 | 14 | 95s |
| 3 | PM Fase 2 | pm-analista-negocios | sonnet | 44474 | 21 | 188s |
| 4 | Refinamento LT | lider-tecnico | sonnet | 107624 | 42 | 359s |
| 5 | Dev Sub-A #139 | dev-dotnet | sonnet | 183618 | 115 | 724s |
