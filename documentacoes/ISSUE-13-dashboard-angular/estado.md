# Estado — ISSUE-13: Dashboard Angular (Todas as Paginas Admin)

## Campos principais
issue: 13
repo: omuletachou
titulo: feat: Dashboard Angular (Todas as Paginas Admin)
rota: normal
etapa_atual: Concluído — PR #115 mergeado em main (commit ec983ced), Issue fechada
docs_path: repos/omuletachou/documentacoes/ISSUE-13-dashboard-angular
openspec_path: repos/omuletachou/openspec/changes/issue-13-dashboard-angular
ultimo_agente: coordenador
status_comment_id: 5045887889
pr_homologacao: 113
code_review_homolog_pr: 113
pr_release: 115
merge_commit_main: ec983ced9f443b54c5282be3b29c9c62d054ae9d
createdAt: 2026-07-03T12:45:30Z
closedAt: 2026-07-23T17:43:03Z

## Contexto
Stack: Angular 17 + TypeScript + HttpClient
Repo: DQM-BETA/omuletachou
Branch base: desenv
Dependência: Issue #11 (REST API pública/administrativa: `POST /api/auth/login`, GET/PATCH `/api/products`, GET/POST `/api/queue`, GET/PUT `/api/settings`, GET `/api/reports`, POST `/api/jobs/retry`) — já entregue em main.
Dependência adicional: Issue #17 (Sub-B: Dashboard Angular — Scaffolding e Docker) — já concluída. O scaffold em `dashboard/` (Angular 17, TypeScript, estrutura de páginas stub em `dashboard/src/app/pages/`, Dockerfile, nginx.conf) já existe — confirmado por inspeção do diretório. Esta issue evolui esse scaffold, NÃO recria.

**Nota técnica:** Esta é a primeira issue de frontend administrativo Angular com conteúdo real do projeto. O dashboard consumirá a API administrativa protegida por JWT (Issue #11), nunca expondo URLs internas ao browser. Scaffold de páginas (`/products`, `/queue`, `/facebook-manual`, `/settings`, `/reports`) e `services/` já estruturados; esta issue implementa os serviços e templates componentes reais, mais as 2 telas novas definidas no Gate 1 (`/login`, `/jobs`).

## PM Fase 1 — levantamento
Concluído. Perguntas postadas na Issue #13 (comentário https://github.com/DQM-BETA/omuletachou/issues/13#issuecomment-5045926492).

## Gate 1 — respostas do Gerente
Concluído. Respostas completas no comentário https://github.com/DQM-BETA/omuletachou/issues/13#issuecomment-5046210646. Resumo:
1. Escopo: 7 telas — as 5 do scaffold + `/login` (dedicada, sem menu lateral) + `/jobs` (disparo manual de Collector/Processor/Publisher).
2. Auth: JWT em memória (`AuthService` singleton) + `sessionStorage` (não `localStorage`); `HttpInterceptor` captura 401 global, limpa token, redireciona `/login` com mensagem de sessão expirada; sem refresh token.
3. Scaffold da Issue #17 confirmado — evoluir, não recriar.
4. Mascaramento em Settings: placeholder mascarado, campo vazio no submit não é enviado no PUT.
5. Design livre (sem Figma), priorizando Angular Material/PrimeNG.
6. Desktop-only, sem responsividade mobile/tablet.
7. "Testar conexão": escopo novo, endpoint não existe — autorizado entregar Settings sem esse botão, vira follow-up.
8. Fatiamento sugerido: Sub-A Auth, Sub-B Products+Queue, Sub-C Settings+Jobs, Sub-D Facebook Manual+Reports (não fechado, LT decide).
9. Sem Playwright nesta issue — testes unitários Angular + validação manual.

## PM Fase 2 — PRD consolidado
Concluído.
- `proposal.md`: repos/omuletachou/openspec/changes/issue-13-dashboard-angular/proposal.md
- `criterios-aceite.md`: repos/omuletachou/documentacoes/ISSUE-13-dashboard-angular/criterios-aceite.md
- Sumário do PRD postado como comentário na Issue #13.

### Investigação do contrato `PUT /api/settings/{key}` (Issue #11) — decisão sobre ajuste retroativo
Lido `especificacao-tecnica.md` e `criterios-aceite.md` da Issue #11 (CA-C1 a CA-C6, seção 5 da espec técnica). Achado: `PUT /api/settings/{key}` já é um endpoint **por chave individual** (`{ "value": "novo-valor" }`), não um payload em lote. Logo, "campo vazio não sobrescreve" (exigência do Gate 1) é **inteiramente resolvida no front** — basta não disparar a chamada PUT para chaves deixadas em branco no formulário. **Nenhum ajuste retroativo no contrato da API #11 é necessário** — decisão documentada em `proposal.md` (seção Regras de negócio + Avaliação de ambiguidade arquitetural) e em CA-T4 de `criterios-aceite.md`. Diferente do padrão dos fixes retroativos das Issues #8/#9 (aqueles exigiam mudança de comportamento no backend em produção; aqui o contrato já suporta o comportamento desejado sem alteração).

### Avaliação de ambiguidade arquitetural (decisão: NÃO escalar ao Arquiteto)
Dois pontos foram ponderados explicitamente antes da decisão:
1. **Ajuste retroativo em `PUT /api/settings/{key}`**: investigado e descartado — ver acima. Não há mudança de contrato de API em produção, então não se aplica o mesmo cuidado redobrado das Issues #8/#9 (que alteravam comportamento já em `main`).
2. **Estratégia de sessão no browser (JWT em memória + `sessionStorage` + `HttpInterceptor` de 401)**: é a primeira feature de sessão de usuário do projeto, mas o padrão (interceptor + guard + storage) é prática consolidada e amplamente documentada do ecossistema Angular, não uma decisão de arquitetura não-óbvia ou com múltiplas stacks/integrações em jogo. Os riscos residuais já foram endereçados explicitamente pelo Gerente no Gate 1: `sessionStorage` (não `localStorage`) reduz superfície de XSS residual; ausência de refresh token foi aceita conscientemente; CSRF não se aplica porque o token vai em header `Authorization` anexado manualmente pelo interceptor, não em cookie enviado automaticamente pelo browser; múltiplas abas usam `sessionStorage` por aba (comportamento nativo esperado, sem necessidade de sincronização cross-tab nesta issue de uso interno por um único operador).
- **Conclusão**: sem ambiguidade arquitetural relevante. Segue direto para o **Líder Técnico** (design.md resumido + task breakdown), sem passar pelo Arquiteto.

## Refinamento Técnico (LT)
Concluído.
- `design.md` (resumido, PM roteou sem Arquiteto): openspec/changes/issue-13-dashboard-angular/design.md
- `especificacao-tecnica.md`: documentacoes/ISSUE-13-dashboard-angular/especificacao-tecnica.md
- `tasks.md`: openspec/changes/issue-13-dashboard-angular/tasks.md
- Decisão de UI: Angular Material (justificativa em especificacao-tecnica.md §0).
- Decisão UX/UI da squad: NÃO acionado — ferramenta interna desktop-only sem exigência de marca; Gate 1 já resolveu design ("priorizando Angular Material/PrimeNG"); critérios de aceite já detalham comportamento visual suficiente (cores de badge/status, agrupamento de seções). Segue direto para os Devs.
- 3 gaps de contrato descobertos por inspeção direta dos controllers da Issue #11 (`main`), resolvidos como extensões aditivas (sem reabrir a Issue #11 nem exigir aprovação do Gerente — não são mudanças de comportamento em produção): (1) `ai_score`/`ai_reason` ausentes em `GET /api/products` → Sub-B estende `ProductListItemDto`; (2) sem endpoint para marcar item da fila `ManualPending → Published` → Sub-D adiciona `PATCH /api/queue/{id}/status`; (3) sem endpoint de totais hoje/semana/mês em Reports → Sub-D adiciona `GET /api/reports/totals` (tabela de falhas recentes reaproveita `GET /api/queue?status=Failed`, já existente, sem endpoint novo).
- Sumário técnico postado na Issue #13: https://github.com/DQM-BETA/omuletachou/issues/13#issuecomment-5046388558
- Comentário 📍 Status atualizado para "Em Desenvolvimento": https://github.com/DQM-BETA/omuletachou/issues/13#issuecomment-5045887889

## Sub-issues
sub_issues: [#103 (stack:angular, task_id:T-01, Sub-A Autenticação, bloqueante) — MERGED em desenv, #104 (stack:angular, task_id:T-02, Sub-B Products+Queue) — MERGED em desenv (PR #110), sub-issue fechada, #105 (stack:angular, task_id:T-03, Sub-C Settings+Jobs manual) — MERGED em desenv (PR #111, squash, commit `53490d80ea0a3d020f7b37fcc830aea6b6151382`), sub-issue fechada, #106 (stack:angular, task_id:T-04, Sub-D Facebook Manual+Reports) — MERGED em desenv (PR #112, squash, commit `46927ad35c086e842c631f9c92ceb343a4ad634b`, mergedAt 2026-07-22T19:12:02Z), sub-issue fechada]
desenv_tasks_merged: [#103, #104, #105, #106]

## Merge e Encerramento
**As 4 sub-issues (#103-#106) estão completas e mergeadas em `desenv`.** PR de homologação #113 (`desenv` → `homolog`) criado pelo LT em 2026-07-22 (https://github.com/DQM-BETA/omuletachou/pull/113), resumindo a entrega completa das 4 sub-issues + os 2 gaps de contrato backend (#104: ai_score/ai_reason; #106: PATCH queue status + reports/totals). Aguardando `/code-review` + agente Code Review antes de avançar ao QA.

### Fix backend #104 — ai_score/ai_reason na listagem GET /api/products (gap de contrato §2.1.1) — MERGED em desenv (LT)
- Extensao aditiva de `ProductListItemDto` (`backend/src/AfiliadoBot.Api/Products/ProductDtos.cs`): campos `AiScore`/`AiReason` adicionados com `[JsonPropertyName("ai_score"/"ai_reason")]`, mesmo padrao do `ProductDetailDto`.
- `ProductsController.GetProducts` (`backend/src/AfiliadoBot.Api/Controllers/ProductsController.cs`) projeta os dois novos campos na listagem.
- Teste novo `GetProducts_ProdutoComScore_RetornaAiScoreEAiReasonNaListagem` (`backend/src/AfiliadoBot.Tests/Products/ProductsControllerTests.cs`) cobre produto com e sem score na listagem; teste de regressao do detalhe (`GetProduct_Existente_RetornaDetalheComAiScoreEAiReason`) confirmado intacto.
- `dotnet test`: 281/281 passando (100%).
- Boot Docker validado (`docker compose up -d --build db api`, worktree `.worktrees/fix-104-products-dto`) — API sobe sem excecao. Smoke test real: produto inserido via SQL com `ai_score`/`ai_reason`, `GET /api/products` autenticado confirmou os dois campos na resposta JSON. Containers derrubados (`docker compose down`) ao final.
- **PR #108 (`fix/104-products-ai-score-ai-reason` → `desenv`) squash-merged pelo LT em 2026-07-22T14:00:37Z, commit `8fddef562bfd070576309f8f92a856bd333ed6b8`.** Branch remota e local deletadas. Esta é uma extensão pontual dentro do escopo de #104 (backend); o dev Angular de #104 (Sub-B, telas Products/Queue) segue consumindo o contrato já com os campos disponíveis em `desenv`. Sub-issue #104 permanece **aberta** (parte de UI Angular ainda pendente) — comentário registrado em https://github.com/DQM-BETA/omuletachou/issues/104#issuecomment-5046973606.

### Fix backend #106 — PATCH /api/queue/{id}/status + GET /api/reports/totals (gaps de contrato §2.1.2 e §2.1.3) — MERGED em desenv (LT)
- `PublicationQueue.MarkAsPublishedManually()` (domínio): transição restrita `ManualPending → Published`.
- `QueueController`: novo `PATCH /api/queue/{id}/status` (400 transição inválida, 404 não encontrado, 409 estado incompatível, `[Authorize]`).
- `ReportsController`: novo `GET /api/reports/totals` (contagem hoje/semana ISO/mês de itens `Published`).
- Teste de regressão `Summary_ComTokenValido_...` corrigido para asserções por delta (gap de isolamento de teste pré-existente no InMemory DB compartilhado, exposto pelo novo teste de Totals).
- `dotnet test`: 289/289 passando (rodado 3x para descartar flakiness de ordenação).
- Boot Docker validado (worktree `.worktrees/fix-106-queue-reports`) + smoke test real via `curl`/JWT/`psql`: login, 401 sem token, transição manual válida (204), transição inválida (400), republicação (409), `reports/totals` refletindo a nova contagem, `queue/manual` confirmando remoção da fila manual. Containers derrubados ao final.
- **PR #109 (`fix/106-queue-status-reports-totals` → `desenv`) squash-merged pelo LT em 2026-07-22T14:00:59Z, commit `329e18cc7b67d205b9c120166e2c3aeb94a49144`.** Branch remota e local deletadas. Trabalho isolado dos arquivos de #104 (Sub-B em paralelo) — nenhum conflito. Sub-issue #106 permanece **aberta** (parte de UI Angular ainda pendente) — comentário registrado em https://github.com/DQM-BETA/omuletachou/issues/106#issuecomment-5046974107.
- `git pull origin desenv` no repo compartilhado: fast-forward `ed81377..329e18c`, sem conflitos. Worktrees `.worktrees/fix-104-products-dto` e `.worktrees/fix-106-queue-reports` removidos (`git worktree remove`) após o merge.

### Sub-A (#103) — Autenticação — MERGED em desenv (LT)
- Angular Material 17.3 instalado (`ng add @angular/material`). Nota técnica: a especificacao-tecnica.md §0 descreve a API M3 (`mat.theme(...)`), disponível a partir do Angular Material 18+; o scaffold está fixado em 17.3 (mesma major do restante do dashboard, Issue #17), cuja API estável é M2. Aplicado o mesmo espírito (tema light, paleta azul, sem guideline de marca) via `mat.define-light-theme` com `$blue-palette` em `src/styles.scss`. Registrado para o LT avaliar se aceita a divergência ou decide upgrade de major em issue futura.
- `AuthService`, `authGuard`/`loginGuard`, `authInterceptor` implementados conforme especificacao-tecnica.md §1.1-1.3, em `dashboard/src/app/core/auth/`.
- `ShellComponent` (`dashboard/src/app/core/shell/`) com `MatSidenav` + 6 itens de navegação (Products, Queue, Facebook Manual, Settings, Jobs, Reports) + botão de Logout.
- `LoginComponent` (`dashboard/src/app/pages/login/`) com formulário reativo Material, sem menu lateral (CA-A8), redirecionamento em sucesso e mensagem de erro em credenciais inválidas.
- `JobsComponent` criado como stub (rota `/jobs`, ainda sem lógica — escopo de Sub-C).
- `app.routes.ts`/`app.config.ts` atualizados: `provideHttpClient(withInterceptors([authInterceptor]))`, `provideAnimationsAsync()`, rotas com `authGuard`/`loginGuard`.
- `proxy.conf.json` criado para `ng serve` local (`/api` → `http://localhost:8080`), registrado em `angular.json` (`serve.options.proxyConfig`).
- Testes: 31/31 passando (Jasmine/Karma) cobrindo CA-A1 a CA-A7 (login sucesso/falha, storage, guard, interceptor 401 dentro/fora de `/api/auth/login`).
- Build (`ng build`): sem erros de TypeScript (1 warning de orçamento de bundle — 712kb vs budget de 500kb — não bloqueante, `maximumError` é 1mb).
- Boot Docker validado: `docker compose up -d --build db api dashboard` a partir do worktree — smoke test real via `curl` contra a API real (seed `admin@omuletachou.com.br`, Issue #11): login válido (200 + JWT), login inválido (401), chamada sem token a `/api/products` (401), chamada com token (200) — todos via proxy nginx do container `dashboard` (porta 4200). Containers derrubados (`docker compose down -v`) ao final.
- PR: #107 (feature/103-auth → desenv), squash merge por LT em 2026-07-22T13:45:05Z. Branch `feature/103-auth` deletada local e remotamente após o merge. `desenv` local atualizada via fast-forward (`git pull origin desenv`, 52e10f9..7ae9da1). Sub-issue #103 fechada (`completed`).

### Decisão técnica do LT: Angular Material M2 vs M3
Avaliada a divergência sinalizada pelo Dev (especificacao-tecnica.md §0 descreve a API M3 `mat.theme(...)`, mas Angular Material 17.3 — fixado nesta issue, mesma major do scaffold da Issue #17 — só suporta a API M2 `mat.define-light-theme`; M3 exige Angular Material 18+).
- **Decisão: M2 é aceitável para esta issue. Não é débito técnico bloqueante.** Justificativa: (1) dashboard é ferramenta interna administrativa, uso por operador único, sem exigência de marca/guideline visual (confirmado no Gate 1 — "design livre, sem Figma"); (2) M2 entrega tema customizável (paleta azul) e todos os componentes Material necessários (`MatSidenav`, formulários reativos, badges) sem qualquer lacuna funcional para os critérios de aceite desta issue; (3) upgrade de major do Angular (17→18) para ganhar M3 é uma mudança estrutural fora do escopo desta issue (afeta todas as Sub-B/C/D e o scaffold inteiro da Issue #17), com risco de regressão desproporcional ao ganho (puramente estético/tokens de design).
- **Registrado como melhoria não-bloqueante** (não débito técnico crítico): considerar upgrade para Angular 18+/Material M3 em issue futura de manutenção, caso a squad decida investir em um design system mais robusto para o dashboard. Não abre issue de rastreio agora — fica documentado aqui para referência caso o tema volte à tona.
- Sub-B, Sub-C e Sub-D devem seguir a mesma convenção M2 (`mat.define-light-theme`) já aplicada em `src/styles.scss`, sem tentar migrar para M3 isoladamente.

### Sub-D (#106) — Facebook Manual + Reports (backend) — Dev .NET
- Implementados os 2 endpoints aditivos identificados pelo LT no refinamento (especificacao-tecnica.md §2.2.1 e §2.5.1), sem alterar comportamento de nenhum endpoint existente da Issue #11:
  - `PATCH /api/queue/{id}/status` (`QueueController`): transicao explicita `ManualPending -> Published` via novo metodo de dominio `PublicationQueue.MarkAsPublishedManually()` (mesmo padrao de `Retry()`). 400 em transicao nao suportada (so aceita `status: "Published"`), 404 se o item nao existe, 409 se o item nao esta em `ManualPending`. `[Authorize]` herdado do controller.
  - `GET /api/reports/totals` (`ReportsController`): contagens agregadas `today`/`week`(ISO, segunda-feira UTC)/`month`(dia 1 do mes corrente UTC) de publicacoes com `Status=Published`, complementando `GET /api/reports/summary` (janela fixa de 7 dias, usada no grafico).
- Testes: dominio (`PublicationQueueTests`) + integracao real via `WebApplicationFactory` (`QueueControllerTests`, `ReportsControllerTests`) cobrindo sucesso/401/400/404/409. `dotnet test`: 289/289 passando (rodado 3x para descartar flakiness de ordem).
- Ajuste de teste pre-existente (Gate obrigatorio, nao é mudanca de comportamento): `ReportsControllerTests.Summary_ComTokenValido_...` convertido para asserts em delta (baseline antes/depois), pois `CustomWebApplicationFactory` compartilha o banco InMemory entre os testes da mesma classe — o novo teste de `Totals` tambem publica itens "hoje", o que quebrava a asserção de total absoluto do teste de `Summary` dependendo da ordem de execucao.
- Boot Docker validado: `docker compose up -d --build db api` a partir do worktree (`.env` local criado so para o smoke test, removido ao final) — API sobe sem excecao. Smoke test real via `curl` com JWT: login, `reports/totals` sem/com token, item `ManualPending` inserido via `psql`, `PATCH .../status` com transicao invalida (400), publicacao valida (204), nova tentativa (409), `reports/totals` refletindo a contagem, `queue/manual` confirmando saida da fila. Containers derrubados (`docker compose down -v`) ao final.
- Trabalho realizado em paralelo com outro Dev .NET (fix na Sub-B, `ProductsController`/DTO de produtos) — nenhum arquivo de `Products` tocado, sem sobreposicao.
- PR #109 squash-merged pelo LT em desenv — ver seção "Fix backend #106" acima.

### Dev Sub-B #104 — Products + Queue (Angular) — Dev Angular — MERGED em desenv (LT)
- `ProductsService`/`QueueService` (`dashboard/src/app/core/services/`) implementados conforme especificacao-tecnica.md §2.1/§2.2, reutilizando `AuthService`/`authInterceptor` da Sub-A (#103, já mergeada). `cleanParams()`/`PagedResult<T>` compartilhados em `core/services/paged-result.model.ts`.
- Confirmado por inspeção que o gap de contrato `ai_score`/`ai_reason` em `GET /api/products` já foi resolvido pelo fix backend #104 (PR #108, squash-merged pelo LT) — nenhum ajuste de backend necessário nesta etapa, apenas consumo do contrato já disponível em `desenv`.
- `ProductsComponent` (`/products`): `MatTable` com paginação/sort, filtros de plataforma/status (server-side, via query params) e data de coleta (client-side via `MatTableDataSource.filterPredicate`, já que `GET /api/products` não expõe filtro de data), badge de `ai_score` (verde ≥8, amarelo ≥6, vermelho <6) com `matTooltip` de `ai_reason`, ações de aprovar (`PATCH .../status` com `{status:"pending"}`) e rejeitar (`{status:"rejected"}`) recarregando a tabela após sucesso.
- `QueueComponent` (`/queue`): `MatTable` com filtros de rede/status, badge de status por cor (cinza=Scheduled, verde=Published, vermelho=Failed, laranja=ManualPending), botão "Retry" (`POST /api/queue/{id}/retry`) visível apenas em itens `Failed`. Sem `markPublished`/Facebook Manual (escopo da Sub-D, #106).
- Convenção Angular Material M2 mantida (mesma decisão do LT para Sub-A — ver seção acima).
- Testes (Jasmine/Karma): 58/58 passando — `ProductsService`, `QueueService`, `ProductsComponent`, `QueueComponent`, cobrindo CA-B1 a CA-B8.
- `ng build`: sem erros de TypeScript (warning de budget de bundle pré-existente, não bloqueante).
- Boot Docker real validado a partir do worktree `.worktrees/104-products-queue` (`docker compose up -d --build db api dashboard`, `.env` local com seed de usuário criado só para o smoke test, removido ao final): login via `POST /api/auth/login`, produto inserido via `psql` com `ai_score`/`ai_reason`, `GET /api/products` confirmando os dois campos na listagem, `PATCH /api/products/{id}/status` (rejeitar, 204, refletido na listagem), item de fila inserido via `psql` com status `Failed`, `GET /api/queue` confirmando o item, `POST /api/queue/{id}/retry` (204, status mudou para `Scheduled`), proxy nginx do container `dashboard` (porta 4200) validado roteando `/api/*` corretamente (401 sem token, 200 no login). Containers derrubados (`docker compose down -v`) ao final.
- **PR #110 (`feature/104-products-queue` → `desenv`) squash-merged pelo LT em 2026-07-22T18:32:30Z, commit `9d9d7d0f8b255fc477362d230226d78802f85ec7`.** Branch remota `feature/104-products-queue` deletada. `git pull origin desenv` no repo compartilhado: fast-forward `2fbfda8..9d9d7d0`, sem conflitos. Worktree `.worktrees/104-products-queue` já não existia (removido previamente pelo dev). Sub-issue #104 **fechada** (`completed`) — backend (PR #108) + UI Angular (PR #110) entregues, comentário de resumo postado em https://github.com/DQM-BETA/omuletachou/issues/104.

### Dev Sub-C #105 — Settings + Jobs manual — Dev Angular
- `SettingsService`/`JobsService` (`dashboard/src/app/core/services/`) conforme especificacao-tecnica.md §2.3/§2.4 (contrato `GET/PUT /api/settings/{key}` por chave individual, `POST /api/jobs/*/trigger`).
- `SettingsComponent` (`dashboard/src/app/pages/settings/`): formulário agrupado por seção (Amazon, MercadoLivre, Shopee, Telegram, YouTube, Instagram, TikTok, Claude AI, Agendamentos, Redes habilitadas, Avançado — fallback para chaves não mapeadas, sem travar o build). Campo sensível (`_key`/`_secret`/`_token`/`_password`) sempre carregado vazio com placeholder mascarado (CA-C1), toggle show/hide (CA-C4), submit por seção via `forkJoin` só dos campos alterados/não vazios (CA-C2/C3/C5, erro em uma chave não bloqueia as demais), sem botão "Testar conexão" (CA-C6, fora de escopo confirmado no Gate 1).
- `JobsComponent` (`dashboard/src/app/pages/jobs/`, evoluído do stub da Sub-A): botões para os 6 jobs (collector geral + 3 por plataforma, processor, publisher), feedback de sucesso/erro da última execução disparada sem travar a UI (CA-C7/CA-C8).
- `paged-result.model.ts` (`core/services/`): helper `cleanParams()` compartilhado, criado nesta sub-issue (também usado por Sub-B/D).
- **Ajuste descoberto em smoke test Docker real** (não previsto na especificacao-tecnica.md): `GET /api/settings` pode retornar `value: null` para chaves ainda não configuradas no backend (não só o formato mascarado `****...`). `Setting.value` tipado como `string | null`; componente trata com placeholder amigável ("Nenhum valor configurado — digite para definir") em vez de "Valor atual: null".
- Testes: 62/62 passando (Jasmine/Karma) cobrindo CA-C1 a CA-C8 + casos de robustez (value null, chave não mapeada, erro parcial em seção com múltiplas chaves).
- Build (`ng build`): sem erros de TypeScript (mesmo warning de budget pré-existente da Sub-A, 757kb vs 500kb, não bloqueante).
- Boot Docker validado: stack isolada (`sub105_db`/`sub105_api`/`sub105_dashboard`, portas 5433/5001/4201) para não colidir com outra sub-issue rodando em paralelo (worktree de Sub-D usando os nomes/portas padrão simultaneamente). Smoke test real via `curl` contra a API real através do proxy nginx do dashboard: login, `GET/PUT /api/settings` com mascaramento confirmado (`****************real` após PUT), `POST /api/jobs/collector/amazon/trigger` retornou 500 real (credenciais Amazon não configuradas no ambiente de teste — tratado corretamente pela UI como erro), `POST /api/jobs/processor/trigger` 200, 401 sem token. `docker-compose.yml`/`.env` do worktree revertidos ao padrão após o teste (mudanças de porta/nome eram só locais, não commitadas). Containers e imagens de teste removidos ao final.
- PR: #111 (`feature/105-settings-jobs` → `desenv`) — ver seções abaixo (resolução de conflito + merge final).

### Merge Sub-C #105 (LT) — BLOQUEADO por conflito (tentativa inicial)
- Revisão do diff do PR #111 concluída: o achado do dev (`GET /api/settings` podendo retornar `value: null` para chaves não configuradas) está tratado de forma sensata no front (`value: string | null` + placeholder amigável) — não é mudança de contrato de API, apenas robustez no consumo, confirmado adequado.
- `git pull origin desenv` (sem conflitos com o estado local no início desta invocação) e `gh pr merge 111 --squash` tentado: **não mergeável** ("the merge commit cannot be cleanly created").
- `gh pr update-branch 111` também falhou ("Cannot update PR branch due to conflicts").
- Teste de merge em worktree isolado (`git worktree add .../wt-105 feature/105-settings-jobs` + `git merge origin/desenv --no-commit --no-ff`) confirmou **conflito add/add** em `dashboard/src/app/core/services/paged-result.model.ts`: a Sub-C criou `cleanParams(params: Record<string, unknown>)` com `String(value)`, enquanto a Sub-B (#104, já mergeada em `desenv` via PR #110, commit `9d9d7d0`) criou `cleanParams(params: object)` sem conversão explícita — mesmo arquivo/contrato, assinaturas levemente diferentes. Merge de teste abortado (`git merge --abort`), worktree removido, nenhuma alteração de código feita pelo LT (fora do escopo da ferramenta).
- Comentário técnico postado no PR #111 (https://github.com/DQM-BETA/omuletachou/pull/111#issuecomment-5050016130) resumindo a revisão do achado `value: null` e pedindo ao Dev Angular para sincronizar com `desenv` e resolver o conflito (recomendação: manter a assinatura mais estrita `Record<string, unknown>` + `String(value)`, validando que nenhum caller da Sub-B dependia do comportamento anterior), rodar os testes das duas sub-issues e dar push.
- PR #111 **não foi mergeado nesta tentativa**; branch `feature/105-settings-jobs` não deletada; sub-issue #105 permaneceu aberta, aguardando push do dev com a resolução do conflito.

### Resolução do conflito Sub-C #105 (PR #111) — Dev Angular
- Worktree recriado (`.worktrees/111-fix-conflict`, base `feature/105-settings-jobs`) — o worktree original já havia sido removido pelo dev anterior.
- `git fetch origin && git merge origin/desenv`: conflito confirmado, tipo add/add, exclusivamente em `dashboard/src/app/core/services/paged-result.model.ts` (nenhum outro arquivo em conflito).
- Investigação do uso real antes de decidir: buscado (`grep`) todo uso de `cleanParams`/`PagedResult` nos dois branches. Achado: a versão da Sub-C (`Record<string, unknown>` + `String(value)`) nunca é chamada por nenhum código da própria Sub-C — `SettingsService`/`JobsService` não têm parâmetros de listagem paginada (`GET /api/settings` sem query params, `POST /api/jobs/*/trigger` sem params). O arquivo foi criado de forma especulativa/redundante, duplicando a abstração que a Sub-B já havia introduzido. Já a versão da Sub-B (`object` sem conversão explícita, já mergeada em `desenv` via PR #110) é ativamente usada por `products.service.ts` e `queue.service.ts` (confirmado via `grep`).
- Decisão: manter a versão canônica da Sub-B/desenv como está (não portar `String(value)`/`Record<string, unknown>` da Sub-C) — não há caller da Sub-C dependendo do comportamento diferente, e alterar a assinatura da versão já em uso por Products/Queue introduziria risco sem benefício comprovado. Resolvido via `git checkout --theirs` (incoming = `origin/desenv`) no arquivo em conflito.
- Commit de merge `6cbdc0c` criado (`git commit --no-edit`) trazendo também os arquivos da Sub-B (`products.service.ts`/`queue.service.ts`/specs) para dentro da branch de Sub-C, sem alterações adicionais de código.
- `npm ci` + `ng test --watch=false --browsers=ChromeHeadless`: 89/89 testes passando (união de Sub-B + Sub-C, nenhuma regressão). `ng build`: sucesso (apenas warning pré-existente de budget de bundle, não bloqueante).
- Push da branch `feature/105-settings-jobs` (`053767a..6cbdc0c`). `gh pr view 111 --json mergeable,mergeStateStatus` confirmou MERGEABLE/CLEAN após alguns segundos de recomputação do GitHub.
- Worktree `.worktrees/111-fix-conflict` removido ao final.

### Merge Sub-C #105 (PR #111) — MERGED em desenv (LT)
- `git pull origin desenv` no início da invocação: já atualizado (Sub-B/#104 já presente localmente).
- `gh pr view 111 --json mergeable,mergeStateStatus`: `MERGEABLE`/`CLEAN` confirmado (após a resolução de conflito do dev acima).
- `gh pr diff 111` revisado: nenhuma alteração remanescente em `paged-result.model.ts` (a branch já convergiu para a versão canônica de `desenv`); demais arquivos (jobs.service, settings.service, componentes Jobs/Settings) sãos.
- **PR #111 squash-merged em `desenv` em 2026-07-22T18:47:38Z, commit `53490d80ea0a3d020f7b37fcc830aea6b6151382`.** Branch remota `feature/105-settings-jobs` deletada (`--delete-branch`). `git pull origin desenv` no repo compartilhado: fast-forward `96643ab..53490d8`, sem conflitos. Sub-issue #105 **fechada** (`completed`), comentário de resumo postado.
- **Atenção para o próximo merge (Sub-D, PR #112):** conforme "Observação para o merge" registrada pelo próprio dev da Sub-D, `products.service.ts`/`queue.service.ts` seguem o mesmo contrato usado pela Sub-B — colisão adicional esperada nesses dois arquivos (união de métodos). Além disso, como a Sub-D também recria `paged-result.model.ts` (mesmo padrão especulativo da Sub-C), é **provável** que o PR #112 sofra o mesmo tipo de conflito add/add já visto no #111, exigindo nova rodada de resolução por um Dev Angular antes do merge.

### Dev Sub-D #106 — Facebook Manual + Reports (Angular) — Dev Angular
- Confirmado por inspeção que o backend de #106 (`PATCH /api/queue/{id}/status`, `GET /api/reports/totals`) já estava mergeado em `desenv` (PR #109, fix backend) — nenhum ajuste de backend necessário nesta etapa, apenas consumo do contrato já disponível.
- Novos services `core/services/`: `QueueService` (`list`/`listManualPending`/`retry`/`markPublished`), `ReportsService` (`summary`/`totals`), `ProductsService` (método `getById`, consumindo `GET /api/products/{id}` já existente desde a Issue #11 — necessário para exibir preview de mídia + legenda completa do produto associado a cada item `ManualPending`). `paged-result.model.ts` (`PagedResult<T>`/`cleanParams`) recriado (mesmo contrato documentado — possível colisão trivial no merge com a Sub-B/#104, que também cria este arquivo).
- `FacebookManualComponent` (`/facebook-manual`): cards de posts pendentes filtrados por `status=ManualPending` + `socialNetwork='Facebook'` (`GET /api/queue/manual`), preview de mídia (imagem/vídeo, detecção por extensão) e legenda completa via `ProductsService.getById`, botão "Copiar legenda" (`navigator.clipboard.writeText`) e "Marcar como publicado" (`QueueService.markPublished` → remove o card da lista em sucesso). Feedback de loading/erro/vazio.
- `ReportsComponent` (`/reports`): 3 cards de totais hoje/semana/mês (`ReportsService.totals()`), gráfico de barras de publicações por rede nos últimos 7 dias (`ng2-charts`+`Chart.js`, `ReportsService.summary()`), tabela de falhas recentes com botão Retry reaproveitando `QueueService.list({status:'Failed'})`/`retry()` — sem endpoint novo, conforme especificacao-tecnica.md §2.5.2. Dependências novas: `ng2-charts`, `chart.js`.
- Convenção Angular Material M2 mantida (mesma decisão do LT para Sub-A).
- Testes (Jasmine/Karma): 51/51 passando, cobertura 96.44% statements / 85.71% branches — cobrindo CA-D1 a CA-D6 + estados de loading/erro/vazio.
- `ng build`: sem erros de TypeScript (mesmo warning de budget de bundle pré-existente, não bloqueante).
- Boot Docker real validado: stack isolada (`sub106_db`/`sub106_api`/`sub106_dashboard`, portas 5434/5002/4202, via `docker-compose.override.yml` local não commitado) para não colidir com a Sub-C (#105) rodando em paralelo. Smoke test via `curl` com JWT real: login, `GET /api/reports/totals` (200, contagens zeradas em banco vazio), `GET /api/queue/manual` (200), `GET /api/queue?status=Failed` (200), `PATCH /api/queue/{id}/status` com id inexistente (404, esperado). Containers/volumes/override removidos ao final (`docker compose down -v`).
- **Observação para o merge (LT):** `core/services/products.service.ts` e `queue.service.ts` seguem o mesmo contrato documentado em especificacao-tecnica.md §2.1/§2.2 usado pela Sub-B (#104) — colisão esperada no merge, resolução trivial (união de métodos, sem divergência de assinatura), registrada no corpo do PR.
- PR: #112 (`feature/106-facebook-reports` → `desenv`), aguardando merge do LT. Branch pushada para o remoto. Worktree `.worktrees/106-facebook-reports` removido após o push.

### Resolução do conflito Sub-D #106 (PR #112) — Dev Angular
- Worktree criado (`.worktrees/112-fix-conflict`, base `feature/106-facebook-reports`). `git fetch origin && git merge origin/desenv`: conflito add/add confirmado em 3 arquivos: `paged-result.model.ts`, `products.service.ts` e `queue.service.ts` (+ conflito trivial nos respectivos `.spec.ts`).
- **Diferença deste caso vs. Sub-C (#105, PR #111):** na Sub-C a versão duplicada não era usada por nenhum caller (descartada sem prejuízo). Aqui a Sub-D **usa ativamente** as APIs — `FacebookManualComponent` chama `QueueService.listManualPending()`/`markPublished()` e `ProductsService.getById()`; `ReportsComponent` chama `QueueService.list()`/`retry()`. Investigação (leitura de `facebook-manual.component.ts`/`reports.component.ts`) mostrou que `listManualPending`, `markPublished`, `list` e `retry` **já existiam** na versão canônica (desenv/Sub-B), com o mesmo contrato. O único método ausente na versão canônica era `ProductsService.getById(id)`.
- Decisão: manter a versão canônica de `desenv` (Sub-B) em todos os 3 arquivos como base, **estendendo-a de forma aditiva** com `ProductsService.getById(id): Observable<ProductDetail>` (comentário no código referenciando CA-D1 e o contrato `GET /api/products/{id}` já existente desde a Issue #11). `paged-result.model.ts` e `queue.service.ts` mantidos inalterados (nenhum método novo necessário). A duplicata da Sub-D nesses 3 arquivos foi descartada. Nos `.spec.ts`, mantidos os specs canônicos da Sub-B e adicionado o teste `getById()` (mock de `ProductDetail`, `GET /api/products/{id}`) trazido da Sub-D.
- **Problema adicional encontrado (não relacionado ao conteúdo do conflito, mas bloqueava o Gate de build):** após o merge, `ng build` passou a **falhar** (erro, não mais warning) por estouro do budget de bundle inicial (`1.13MB` vs `maximumError: 1mb`) — causado pela combinação cumulativa do crescimento das páginas já mergeadas (Sub-B/Sub-C) com as novas dependências da Sub-D (`chart.js`+`ng2-charts` no `ReportsComponent`), com todas as rotas carregadas eagerly em `app.routes.ts` (import direto de componente, sem lazy loading). Confirmado por isolamento: a Sub-D sozinha (antes do merge) buildava com apenas warning (978KB); o build passou a dar erro só após a soma com o restante de `desenv`.
- Correção aplicada (mecânica, sem mudança de comportamento): convertidas todas as rotas de `app.routes.ts` de `component: X` (eager) para `loadComponent: () => import(...)` (lazy, padrão standalone do Angular), isolando `chart.js`/`ng2-charts` (usado só por `ReportsComponent`) em chunk lazy próprio. Resultado: bundle inicial caiu para 785.84KB (mesma faixa de warning não-bloqueante já documentada para Sub-A/B/C), sem erro.
- `npm ci` + `ng test --watch=false --browsers=ChromeHeadless`: **105/105 testes passando** (união de Sub-B + Sub-C + Sub-D, nenhuma regressão). `ng build`: sucesso (apenas warning pré-existente de budget de bundle, não bloqueante — mesmo padrão documentado desde a Sub-A).
- Commit de merge criado, push da branch `feature/106-facebook-reports`. `gh pr view 112 --json mergeable,mergeStateStatus` confirmado MERGEABLE/CLEAN.
- Worktree `.worktrees/112-fix-conflict` removido ao final.

## Historico de etapas
| # | Etapa | Agente | Status |
|---|---|---|---|
| 1 | Preparacao | Coordenador | ativo — Issue preparada, estado.md criado, comentário 📍 Status criado, card adicionado ao board em 💻 Em Desenvolvimento |
| 2 | PM Fase 1 | pm-analista-negocios | concluído — perguntas de levantamento postadas na Issue, comentário 📍 Status atualizado para Gate 1 |
| 3 | PM Fase 2 | pm-analista-negocios | concluído — proposal.md e criterios-aceite.md escritos, investigação do contrato PUT /api/settings/{key} concluída (sem ajuste retroativo necessário), sem ambiguidade arquitetural identificada, sumário do PRD postado na Issue, comentário 📍 Status atualizado para Refinamento Técnico |
| 4 | Refinamento Técnico | lider-tecnico | concluído — design.md/especificacao-tecnica.md/tasks.md escritos, 4 sub-issues criadas (#103-#106), 3 gaps de contrato com a API #11 resolvidos como extensões aditivas, UX/UI da squad não acionado (justificativa registrada), sumário postado na Issue, comentário 📍 Status atualizado para Em Desenvolvimento |
| 5 | Dev Sub-A #103 | dev-angular | concluído — PR #107 (feature/103-auth → desenv), 31/31 testes, boot Docker validado com login real contra API #11 |
| 6 | Merge Sub-A #103 | lider-tecnico | concluído — PR #107 squash-merged em desenv, sub-issue #103 fechada, decisão M2/M3 documentada (M2 aceito, não bloqueante), Sub-B/C/D desbloqueadas, branch feature/103-auth removida |
| 7 | Fix backend #104 (ai_score/ai_reason listagem) | dev-dotnet | concluído (dev) — PR #108 (fix/104-products-ai-score-ai-reason → desenv) aberto, 281/281 testes, boot Docker + smoke test real validados, aguardando merge do LT |
| 8 | Fix backend #106 (status manual da fila + totais de reports) | dev-dotnet | concluído (dev) — PR #109 (fix/106-queue-status-reports-totals → desenv) aberto, 289/289 testes, boot Docker + smoke test real validados, aguardando merge do LT |
| 9 | Merge fix backend #104 (PR #108) | lider-tecnico | concluído — squash-merged em desenv (commit 8fddef5) em 2026-07-22T14:00:37Z, branch deletada, comentário postado na sub-issue #104 (permanece aberta — UI Angular pendente) |
| 10 | Merge fix backend #106 (PR #109) | lider-tecnico | concluído — squash-merged em desenv (commit 329e18c) em 2026-07-22T14:00:59Z, branch deletada, comentário postado na sub-issue #106 (permanece aberta — UI Angular pendente), git pull origin desenv fast-forward sem conflitos, worktrees fix-104/fix-106 removidos |
| 11 | Dev Sub-B #104 (Angular) | dev-angular | concluído (dev) — PR #110 (feature/104-products-queue → desenv) aberto, 58/58 testes, boot Docker + smoke test real validados (login, ai_score/ai_reason, aprovar/rejeitar, retry), aguardando merge do LT |
| 12 | Dev Sub-C #105 (Settings + Jobs) | dev-angular | concluído (dev) — PR #111 (feature/105-settings-jobs → desenv) aberto, 62/62 testes, boot Docker + smoke test real validados (login, mascaramento GET/PUT settings, jobs trigger sucesso/erro real), aguardando merge do LT |
| 13 | Dev Sub-D #106 (Facebook Manual + Reports) | dev-angular | concluído (dev) — PR #112 (feature/106-facebook-reports → desenv) aberto, 51/51 testes, boot Docker + smoke test real validados (login, reports/totals, queue/manual, queue?status=Failed, PATCH status 404 esperado), aguardando merge do LT |
| 14 | Tentativa de merge Sub-C #105 (PR #111) | lider-tecnico | bloqueado — PR #110 (Sub-B) já mergeado em desenv (commit 9d9d7d0) tornou o PR #111 não-mergeável: conflito add/add em `paged-result.model.ts` (assinaturas divergentes de `cleanParams` entre Sub-B e Sub-C); `gh pr merge`/`gh pr update-branch` falharam, conflito confirmado em worktree isolado e revertido sem tocar código (fora do escopo de ferramentas do LT); comentário técnico postado no PR #111 pedindo ao Dev Angular resolver e dar push; sub-issue #105 permanece aberta |
| 15 | Merge Sub-B #104 (PR #110) | lider-tecnico | concluído — squash-merged em desenv (commit 9d9d7d0) em 2026-07-22T18:32:30Z, branch feature/104-products-queue deletada, git pull origin desenv fast-forward (2fbfda8..9d9d7d0) sem conflitos, sub-issue #104 fechada (completed) com comentário de resumo. Nota: este merge é o mesmo commit 9d9d7d0 referenciado na linha 14 (tentativa de merge de Sub-C, executada por outra invocação paralela do LT) — Sub-C (#105) segue bloqueada por conflito aguardando push do dev; Sub-D (#106) tem PR #112 aberto, ainda sem tentativa de merge |
| 16 | Resolução do conflito Sub-C #105 (PR #111) | dev-angular | concluído — merge de origin/desenv na branch feature/105-settings-jobs, conflito add/add em paged-result.model.ts resolvido mantendo a versão canônica da Sub-B (já em uso por Products/Queue; versão da Sub-C nunca era chamada), 89/89 testes passando, ng build ok, push (053767a..6cbdc0c), PR #111 confirmado MERGEABLE, aguardando merge do LT |
| 17 | Merge Sub-C #105 (PR #111) | lider-tecnico | concluído — squash-merged em desenv (commit 53490d8) em 2026-07-22T18:47:38Z, branch feature/105-settings-jobs deletada, git pull origin desenv fast-forward (96643ab..53490d8) sem conflitos, sub-issue #105 fechada (completed) com comentário de resumo. Sub-D (#106, PR #112) ainda pendente — risco de conflito adicional em paged-result.model.ts/products.service.ts/queue.service.ts sinalizado para a próxima rodada |
| 18 | Resolução do conflito Sub-D #106 (PR #112) | dev-angular | concluído — merge de origin/desenv na branch feature/106-facebook-reports, conflito add/add em paged-result.model.ts/products.service.ts/queue.service.ts resolvido mantendo a versão canônica da Sub-B estendida aditivamente com ProductsService.getById() (único método ausente); corrigido também um erro de budget de bundle (1.13MB > 1MB) surgido da soma cumulativa das sub-issues, via conversão das rotas de app.routes.ts para lazy loading (loadComponent); 105/105 testes passando, ng build ok (apenas warning pré-existente), push, PR #112 confirmado MERGEABLE, aguardando merge do LT |
| 19 | Merge Sub-D #106 (PR #112) | lider-tecnico | concluído — squash-merged em desenv (commit 46927ad) em 2026-07-22T19:12:02Z, branch feature/106-facebook-reports deletada, sub-issue #106 fechada (completed) com comentário de resumo. **As 4 sub-issues da Issue #13 estão completas.** PR de homologação #113 (desenv→homolog) criado, resumindo a entrega completa |
| 20 | Code Review — PR #113 (desenv→homolog) | code-review | concluído — build/boot/testes executados (backend 290/290, frontend 105/105, `ng build` prod ok), smoke test real via curl/JWT dos 3 gaps de contrato (ai_score/ai_reason, PATCH queue status, reports/totals), boot Docker completo com as 7 rotas do dashboard respondendo 200, sem innerHTML/segredo commitado, plugin `/code-review` sem achados — **aprovado**, PR #113 merged (commit 9e49f377) |
| 21 | QA — homolog | qa | concluído — **aprovado**, todos os 29 critérios de aceite (CA-A1 a CA-T4) validados por execução real (Docker Compose completo, curl/JWT/psql) e/ou leitura de código + testes automatizados (backend 290/290, frontend 105/105, tsc --noEmit ok, cobertura de branches 81.6%); ver seção "QA — homolog" acima |
| 22 | Sincronização de docs desenv→homolog (PR #114) | lider-tecnico | concluído — confirmado via `git log`/`git diff --stat` que o diff `homolog...desenv` continha só arquivos de documentação (`estado.md`, `relatorio-qa.md`, 2 arquivos/147 linhas), nenhum código de aplicação. PR #114 (`desenv`→`homolog`, título "docs: sincronizar registros de Code Review e QA em homolog") criado e mergeado via `gh pr merge --merge` (merge commit, sem squash, sem nova rodada de Code Review por ser só doc) — merge commit `3fbf123a6f88989967e2212617039959d0a96c75`, mergedAt 2026-07-23T15:45:40Z |
| 23 | PR de release homolog→main (#115) | lider-tecnico | concluído — PR #115 criado (`homolog`→`main`, merge commit, NUNCA squash), descrição resumindo as 4 sub-issues (#103-#106), os 2 gaps de contrato backend resolvidos, aprovação do Code Review (PR #113) e do QA (29/29 CAs). **PR mergeado** — merge commit `ec983ced9f443b54c5282be3b29c9c62d054ae9d`, mergedAt 2026-07-23T17:43:03Z (coincide com closedAt da Issue #13, auto-fechada pelo `Closes #13` do PR) |
| 24 | Encerramento — Consolidação de custo | Coordenador | concluído — PR #115 mergeado em main, Issue #13 fechada, estado.md atualizado (closedAt, etapa_atual=Concluído, merge_commit_main), tabela de consolidação de custo postada na Issue (#💰 Custo), comentário 📍 Status atualizado marcando todas as etapas como concluídas, campos do board atualizados (Custo/Tempo), commit/push em desenv |

## Custo (ledger)
| # | Etapa | Agente | Modelo | Tokens | Tools | Tempo_s |
|---|---|---|---|---|---|---|
| 1 | Preparacao | Coordenador | haiku-4.5 | 45607 | 37 | 210s |
| 2 | PM Fase 1 | pm | sonnet | 32806 | 10 | 85s |
| 3 | PM Fase 2 | pm | sonnet | 66779 | 26 | 256s |
| 4 | Refinamento LT | lt | sonnet | 109958 | 49 | 458s |
| 5 | Dev Sub-A #103 (PR #107) | dev-angular | sonnet | 116556 | 95 | 946s |
| 6 | Merge Sub-A #103 (PR #107) | lt | sonnet | 48717 | 13 | 135s |
| 7 | Fix backend #104 (PR #108) | dev-dotnet | sonnet | 76498 | 44 | 450s |
| 8 | Fix backend #106 (PR #109) | dev-dotnet | sonnet | 114084 | 60 | 623s |
| 9 | Dev Sub-B #104 (PR #110) | dev-angular | sonnet | 129394 | 92 | 988s |
| 10 | Dev Sub-C #105 (PR #111) | dev-angular | sonnet | 156389 | 114 | 1186s |
| 11 | Dev Sub-D #106 (PR #112) | dev-angular | sonnet | 152428 | 112 | 1372s |
| 12 | Merge Sub-B #104 (PR #110) | lt | sonnet | 189952 | 29 | 808s |
| 13 | Fix conflito paged-result.model.ts (PR #111) | dev-angular | sonnet | 59483 | 29 | 218s |
| 14 | Merge Sub-C #105 (PR #111) | lt | sonnet | 76349 | 18 | 296s |
| 15 | Fix conflito products/queue.service.ts + lazy loading (PR #112) | dev-angular | sonnet | 144598 | 106 | 1068s |
| 16 | Merge Sub-D #106 (PR #112) + PR homologação #113 | lt | sonnet | (agente caiu por erro de conexão antes do HANDOFF final — usage não capturado; trabalho real confirmado via gh/git) | — | — |
| 17 | Code Review — PR #113 (desenv→homolog) | code-review | sonnet | 93208 | 47 | 965s |
| 18 | QA — homolog (29/29 CAs) | qa | sonnet | 180158 | 84 | 1310s |
| 19 | Sync docs (PR #114) + PR release #115 (homolog→main) | lt | sonnet | 94765 | 19 | 334s |

**Consolidação (quiescência — Issue #13 fechada, todos os gates aprovados):**
- Tokens totais (capturados): 1.687.775 (nota: linha 16 não capturada — agente LT caiu por erro de conexão; trabalho real confirmado via `gh`/`git`)
- Tempo total de processamento: 11.712 segundos = 195,2 minutos (aproximadamente 3 horas 15 minutos)
- Tempo decorrido (criação → fechamento): 2026-07-03T12:45:30Z até 2026-07-23T17:43:03Z = 20 dias 4 horas 57 minutos
- Merge commit final: ec983ced9f443b54c5282be3b29c9c62d054ae9d
