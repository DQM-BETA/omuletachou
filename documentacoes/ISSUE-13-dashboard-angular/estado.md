# Estado — ISSUE-13: Dashboard Angular (Todas as Paginas Admin)

## Campos principais
issue: 13
repo: omuletachou
titulo: feat: Dashboard Angular (Todas as Paginas Admin)
rota: normal
etapa_atual: Em Desenvolvimento
docs_path: repos/omuletachou/documentacoes/ISSUE-13-dashboard-angular
openspec_path: repos/omuletachou/openspec/changes/issue-13-dashboard-angular
ultimo_agente: lider-tecnico
status_comment_id: 5045887889
pr_homologacao: ~
code_review_homolog_pr: ~
pr_release: ~
closedAt: ~

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
sub_issues: [#103 (stack:angular, task_id:T-01, Sub-A Autenticação, bloqueante) — MERGED em desenv, #104 (stack:angular, task_id:T-02, Sub-B Products+Queue) — PR #110 (feature/104-products-queue → desenv) aberto, aguardando merge do LT, #105 (stack:angular, task_id:T-03, Sub-C Settings+Jobs manual) — desbloqueada, #106 (stack:angular, task_id:T-04, Sub-D Facebook Manual+Reports) — desbloqueada, backend do gap de contrato já mergeado (PR #109), parte Angular pendente]
desenv_tasks_merged: [#103]

## Merge e Encerramento
Aguardando fluxo da rota normal (Dev, LT, Code Review, QA, Gate 2) para Sub-B (#104), Sub-C (#105) e Sub-D (#106).

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

### Dev Sub-B #104 — Products + Queue (Angular) — Dev Angular
- `ProductsService`/`QueueService` (`dashboard/src/app/core/services/`) implementados conforme especificacao-tecnica.md §2.1/§2.2, reutilizando `AuthService`/`authInterceptor` da Sub-A (#103, já mergeada). `cleanParams()`/`PagedResult<T>` compartilhados em `core/services/paged-result.model.ts`.
- Confirmado por inspeção que o gap de contrato `ai_score`/`ai_reason` em `GET /api/products` já foi resolvido pelo fix backend #104 (PR #108, squash-merged pelo LT) — nenhum ajuste de backend necessário nesta etapa, apenas consumo do contrato já disponível em `desenv`.
- `ProductsComponent` (`/products`): `MatTable` com paginação/sort, filtros de plataforma/status (server-side, via query params) e data de coleta (client-side via `MatTableDataSource.filterPredicate`, já que `GET /api/products` não expõe filtro de data), badge de `ai_score` (verde ≥8, amarelo ≥6, vermelho <6) com `matTooltip` de `ai_reason`, ações de aprovar (`PATCH .../status` com `{status:"pending"}`) e rejeitar (`{status:"rejected"}`) recarregando a tabela após sucesso.
- `QueueComponent` (`/queue`): `MatTable` com filtros de rede/status, badge de status por cor (cinza=Scheduled, verde=Published, vermelho=Failed, laranja=ManualPending), botão "Retry" (`POST /api/queue/{id}/retry`) visível apenas em itens `Failed`. Sem `markPublished`/Facebook Manual (escopo da Sub-D, #106).
- Convenção Angular Material M2 mantida (mesma decisão do LT para Sub-A — ver seção acima).
- Testes (Jasmine/Karma): 58/58 passando — `ProductsService`, `QueueService`, `ProductsComponent`, `QueueComponent`, cobrindo CA-B1 a CA-B8.
- `ng build`: sem erros de TypeScript (warning de budget de bundle pré-existente, não bloqueante).
- Boot Docker real validado a partir do worktree `.worktrees/104-products-queue` (`docker compose up -d --build db api dashboard`, `.env` local com seed de usuário criado só para o smoke test, removido ao final): login via `POST /api/auth/login`, produto inserido via `psql` com `ai_score`/`ai_reason`, `GET /api/products` confirmando os dois campos na listagem, `PATCH /api/products/{id}/status` (rejeitar, 204, refletido na listagem), item de fila inserido via `psql` com status `Failed`, `GET /api/queue` confirmando o item, `POST /api/queue/{id}/retry` (204, status mudou para `Scheduled`), proxy nginx do container `dashboard` (porta 4200) validado roteando `/api/*` corretamente (401 sem token, 200 no login). Containers derrubados (`docker compose down -v`) ao final.
- PR #110 (`feature/104-products-queue` → `desenv`) aberto, aguardando merge do LT. Worktree `.worktrees/104-products-queue` removido após push.

### Dev Sub-C #105 — Settings + Jobs manual — Dev Angular
- `SettingsService`/`JobsService` (`dashboard/src/app/core/services/`) conforme especificacao-tecnica.md §2.3/§2.4 (contrato `GET/PUT /api/settings/{key}` por chave individual, `POST /api/jobs/*/trigger`).
- `SettingsComponent` (`dashboard/src/app/pages/settings/`): formulário agrupado por seção (Amazon, MercadoLivre, Shopee, Telegram, YouTube, Instagram, TikTok, Claude AI, Agendamentos, Redes habilitadas, Avançado — fallback para chaves não mapeadas, sem travar o build). Campo sensível (`_key`/`_secret`/`_token`/`_password`) sempre carregado vazio com placeholder mascarado (CA-C1), toggle show/hide (CA-C4), submit por seção via `forkJoin` só dos campos alterados/não vazios (CA-C2/C3/C5, erro em uma chave não bloqueia as demais), sem botão "Testar conexão" (CA-C6, fora de escopo confirmado no Gate 1).
- `JobsComponent` (`dashboard/src/app/pages/jobs/`, evoluído do stub da Sub-A): botões para os 6 jobs (collector geral + 3 por plataforma, processor, publisher), feedback de sucesso/erro da última execução disparada sem travar a UI (CA-C7/CA-C8).
- `paged-result.model.ts` (`core/services/`): helper `cleanParams()` compartilhado, criado nesta sub-issue (também usado por Sub-B/D).
- **Ajuste descoberto em smoke test Docker real** (não previsto na especificacao-tecnica.md): `GET /api/settings` pode retornar `value: null` para chaves ainda não configuradas no backend (não só o formato mascarado `****...`). `Setting.value` tipado como `string | null`; componente trata com placeholder amigável ("Nenhum valor configurado — digite para definir") em vez de "Valor atual: null".
- Testes: 62/62 passando (Jasmine/Karma) cobrindo CA-C1 a CA-C8 + casos de robustez (value null, chave não mapeada, erro parcial em seção com múltiplas chaves).
- Build (`ng build`): sem erros de TypeScript (mesmo warning de budget pré-existente da Sub-A, 757kb vs 500kb, não bloqueante).
- Boot Docker validado: stack isolada (`sub105_db`/`sub105_api`/`sub105_dashboard`, portas 5433/5001/4201) para não colidir com outra sub-issue rodando em paralelo (worktree de Sub-D usando os nomes/portas padrão simultaneamente). Smoke test real via `curl` contra a API real através do proxy nginx do dashboard: login, `GET/PUT /api/settings` com mascaramento confirmado (`****************real` após PUT), `POST /api/jobs/collector/amazon/trigger` retornou 500 real (credenciais Amazon não configuradas no ambiente de teste — tratado corretamente pela UI como erro), `POST /api/jobs/processor/trigger` 200, 401 sem token. `docker-compose.yml`/`.env` do worktree revertidos ao padrão após o teste (mudanças de porta/nome eram só locais, não commitadas). Containers e imagens de teste removidos ao final.
- PR: #111 (`feature/105-settings-jobs` → `desenv`), aguardando merge do LT. Branch pushada para o remoto.

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
