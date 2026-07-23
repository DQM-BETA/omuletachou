# Tasks — ISSUE-13: Dashboard Angular (Todas as Páginas Admin)

Contexto técnico completo (tipos, endpoints, gaps de contrato): `documentacoes/ISSUE-13-dashboard-angular/especificacao-tecnica.md`. Design resumido: `openspec/changes/issue-13-dashboard-angular/design.md`. Critérios de aceite completos: `documentacoes/ISSUE-13-dashboard-angular/criterios-aceite.md`.

**Ordem obrigatória: Sub-A deve ser mergeada em `desenv` ANTES de Sub-B/C/D iniciarem** (ver especificacao-tecnica.md §6 — `ShellComponent`/`authInterceptor`/`provideHttpClient` são pré-requisitos físicos, sem stub razoável).

---

## T-01 — Sub-A: Autenticação (bloqueante, primeiro)

**O que fazer:**
- `ng add @angular/material` (tema M3, `provideAnimationsAsync`) — especificacao-tecnica.md §0.
- `provideHttpClient(withInterceptors([authInterceptor]))` em `app.config.ts`.
- `AuthService` (`core/auth/auth.service.ts`) — signal de token, `login()`, `logout()`, `getToken()`, `isAuthenticated` — §1.1.
- `authGuard`/`loginGuard` (`core/auth/auth.guard.ts`) — §1.2.
- `authInterceptor` (`core/auth/auth.interceptor.ts`) — anexa `Authorization`, trata 401 global (exceto na própria chamada de login) — §1.3.
- `LoginComponent` (`pages/login/`, novo) — formulário email/senha, chama `AuthService.login()`, exibe erro em credenciais inválidas, redireciona em sucesso.
- `ShellComponent` (`core/shell/`, novo) — `MatSidenav` com menu lateral (6 itens + logout) + `router-outlet`.
- Reestruturar `app.routes.ts` conforme §1.4 (rota `/login` fora do shell; demais 6 rotas dentro, protegidas por `authGuard`).
- `dashboard/proxy.conf.json` + ajuste em `angular.json` (`serve.options.proxyConfig`) — §1.5.

**Critérios de aceite:** CA-A1 a CA-A8 (criterios-aceite.md).

**Contexto técnico:** especificacao-tecnica.md §0, §1, §4 (estrutura de pastas), §5 (testes: `AuthService`, `authInterceptor`, guards). Stack: Angular 17 standalone, Angular Material, Signals.

---

## T-02 — Sub-B: Products + Queue (após merge de T-01)

**O que fazer:**
- `ProductsService`/`QueueService` (`core/services/`) conforme §2.1/§2.2 (sem `markPublished`, que é da Sub-D).
- **Ajuste aditivo no backend** (`backend/src/AfiliadoBot.Api/Products/ProductDtos.cs` + `Controllers/ProductsController.cs`): incluir `ai_score`/`ai_reason` em `ProductListItemDto` — §2.1.1 (gap de contrato). Ajustar teste correspondente em `AfiliadoBot.Tests`.
- `ProductsComponent`: tabela `MatTable` com paginação/sort, badges de plataforma/status/`ai_score` (verde ≥8, amarelo ≥6, vermelho <6), tooltip de `ai_reason`, filtros de plataforma/status/data, ações aprovar/rejeitar (`PATCH`).
- `QueueComponent`: tabela/timeline por rede social, cores de status (cinza/verde/vermelho/laranja), filtros rede/status, botão Retry em itens `Failed`.
- `cleanParams()` helper compartilhado (`core/services/paged-result.model.ts`) — usado também por Sub-C/D.

**Critérios de aceite:** CA-B1 a CA-B8.

**Contexto técnico:** especificacao-tecnica.md §2.1 (incl. §2.1.1 — gap `ai_score`/`ai_reason`), §2.2.

---

## T-03 — Sub-C: Settings + Jobs manual (após merge de T-01, paralelo a T-02/T-04)

**O que fazer:**
- `SettingsService` (§2.3) e `JobsService` (§2.4).
- `SettingsComponent`: formulário agrupado por seção (mapeamento fixo de prefixo → seção, §3), campo sensível com placeholder mascarado + toggle show/hide (nunca populado com valor mascarado/real), submit por seção só disparando `PUT` para campos preenchidos (`forkJoin`, erro por chave não bloqueia as demais).
- `JobsComponent` (novo): botões para os 6 jobs (`collector` geral + 3 por plataforma, `processor`, `publisher`), exibe resultado (sucesso/erro) da última execução disparada sem travar a UI.

**Critérios de aceite:** CA-C1 a CA-C8.

**Contexto técnico:** especificacao-tecnica.md §2.3, §2.4, §3 (detalhamento completo do formulário de Settings).

---

## T-04 — Sub-D: Facebook Manual + Reports (após merge de T-01, paralelo a T-02/T-03)

**O que fazer:**
- **Novo endpoint aditivo no backend** (`backend/src/AfiliadoBot.Api/Domain/Entities/PublicationQueue.cs` — método `MarkAsPublishedManually()`; `Controllers/QueueController.cs` — `PATCH /api/queue/{id}/status`) — §2.2.1 (gap de contrato). Incluir teste em `AfiliadoBot.Tests/Queue/QueueControllerTests.cs`.
- **Novo endpoint aditivo no backend** (`Controllers/ReportsController.cs` — `GET /api/reports/totals`, contagens hoje/semana/mês) — §2.5.1 (gap de contrato). Incluir teste correspondente.
- `QueueService.markPublished()` (§2.2) e `ReportsService.totals()`/`summary()` (§2.5).
- `FacebookManualComponent`: cards com preview de mídia/legenda, botão "Copiar legenda" (`navigator.clipboard`), botão "Marcar como publicado" (`QueueService.markPublished`).
- `ReportsComponent`: 3 cards de totais (`ReportsService.totals()`), gráfico de barras 7 dias (Chart.js/ng2-charts, `ReportsService.summary()`), tabela de falhas recentes reaproveitando `QueueService.list({ status: 'Failed' })` + botão Retry (mesmo fluxo de T-02) — §2.5.2 (sem endpoint novo aqui).

**Critérios de aceite:** CA-D1 a CA-D6.

**Contexto técnico:** especificacao-tecnica.md §2.2 (incl. §2.2.1), §2.5 (incl. §2.5.1/§2.5.2).

---

## Transversal (todas as sub-issues)

- **CA-T1:** `npm run build` (dashboard) sem erros de TypeScript ao final de cada sub-issue.
- **CA-T2:** testes unitários dos services tocados por cada sub-issue (ver especificacao-tecnica.md §5).
- **CA-T3:** nenhuma responsividade mobile/tablet — não adicionar breakpoints.
- **CA-T4:** nenhuma alteração no contrato de `PUT /api/settings/{key}` (Sub-C) — comportamento de campo vazio é só no front.
