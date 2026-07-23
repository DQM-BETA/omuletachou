# Design (resumido) — ISSUE-13: Dashboard Angular (Todas as Páginas Admin)

> PM Fase 2 concluiu sem ambiguidade arquitetural relevante (ver `estado.md` — investigação do contrato `PUT /api/settings/{key}` e avaliação da estratégia de sessão). Este design.md é resumido (sem revisão do Arquiteto), cobrindo visão geral da solução, componentes envolvidos, stack e fluxo de dados. Detalhamento de contratos/tipos em `especificacao-tecnica.md`.

## Visão geral
Evoluir o scaffold Angular 17 (Issue #17) para um dashboard administrativo completo: 7 telas (`/login`, `/products`, `/queue`, `/facebook-manual`, `/settings`, `/jobs`, `/reports`), autenticação JWT em `sessionStorage`, consumindo a API administrativa da Issue #11 (já em `main`) via proxy nginx `/api/`.

## Componentes/telas envolvidos
- **Auth (novo):** `AuthService`, `authGuard`/`loginGuard` (functional guards), `authInterceptor` (functional interceptor), `LoginComponent` (tela sem menu lateral), `ShellComponent` (layout com menu lateral para as 6 telas protegidas).
- **Products** (evolui stub): tabela paginada com filtros, badges de `ai_score`/status, ações aprovar/rejeitar.
- **Queue** (evolui stub): tabela/timeline por rede social, filtros, retry.
- **Facebook Manual** (evolui stub): cards de posts pendentes, copiar legenda, marcar publicado.
- **Settings** (evolui stub): formulário agrupado por seção, mascaramento de secrets resolvido no front (nenhum PUT para campo vazio).
- **Jobs** (novo): botões de disparo manual dos 6 jobs já expostos por `JobsController`.
- **Reports** (evolui stub): cards de totais, gráfico de 7 dias (Chart.js/ng2-charts), tabela de falhas recentes com retry.

## Stack
Angular 17 (standalone components, functional guards/interceptors, Signals), Angular Material (decisão de UI — ver `especificacao-tecnica.md` §0), `HttpClient` com `provideHttpClient(withInterceptors(...))`, Chart.js/ng2-charts para o gráfico de Reports, Jasmine/Karma (scaffold já traz) para testes unitários. Sem Playwright/e2e nesta issue (decisão do Gate 1).

## Fluxo de dados
Componente de página → `*Service` (injeta `HttpClient`) → `authInterceptor` anexa `Authorization: Bearer <token>` automaticamente → nginx (`/api/` → `http://api:8080/api/`) → API ASP.NET Core (Issue #11). Resposta 401 em qualquer chamada → `authInterceptor` limpa sessão e redireciona a `/login` (fluxo único, não replicado em cada componente).

## Gaps de contrato descobertos (ver `especificacao-tecnica.md` para o detalhamento e a decisão)
Durante o refinamento técnico, a inspeção direta dos controllers da Issue #11 (`main`) revelou 3 pontos onde o contrato existente não cobre integralmente os critérios de aceite desta issue. Todos resolvidos como **extensões aditivas de leitura/escrita, sem alterar comportamento de endpoints existentes**, decisão dentro da autoridade de refinamento técnico do LT (não são mudanças de comportamento em produção, diferente do padrão de ajuste retroativo das Issues #8/#9):
1. `ai_score`/`ai_reason` ausentes em `GET /api/products` (só existem no detalhe) — Sub-B estende `ProductListItemDto`.
2. Sem endpoint para marcar item `ManualPending → Published` — Sub-D adiciona `PATCH /api/queue/{id}/status`.
3. Sem endpoint de totais hoje/semana/mês para Reports — Sub-D adiciona `GET /api/reports/totals`. A tabela de falhas recentes (CA-D6) não precisa de endpoint novo — reaproveita `GET /api/queue?status=Failed` já existente.

## Decisão de UX/UI
Não escalado ao agente UX/UI da squad (diferente do padrão da Issue #12). Justificativa: ferramenta interna administrativa, desktop-only, sem exigência de identidade visual/marca; o Gate 1 já resolveu a decisão de design ("priorizando Angular Material/PrimeNG" — uso de biblioteca pronta, não layout customizado); os critérios de aceite já especificam comportamento visual suficiente para implementação direta (cores de badge/status exatas, agrupamento de formulário por seção, estrutura de cards). Segue direto para os Devs após este refinamento.
