# Estado — ISSUE-13: Dashboard Angular (Todas as Paginas Admin)

## Campos principais
issue: 13
repo: omuletachou
titulo: feat: Dashboard Angular (Todas as Paginas Admin)
rota: normal
etapa_atual: Preparacao — Issue criada, estado.md pronto
docs_path: repos/omuletachou/documentacoes/ISSUE-13-dashboard-angular
openspec_path: repos/omuletachou/openspec/changes/issue-13-dashboard-angular
ultimo_agente: coordenador
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
Dependência adicional: Issue #18 (Sub-C: Dashboard Angular — Scaffolding) — já concluída. O scaffold em `dashboard/` (Angular 17, TypeScript, estrutura de páginas stub em `dashboard/src/app/pages/`) já existe; esta issue evolui esse scaffold, NÃO recria.

**Nota técnica:** Esta é a primeira issue de frontend administrativo Angular com conteúdo real do projeto. O dashboard consumirá a API administrativa protegida por JWT (Issue #11), nunca expondo URLs internas ao browser. Scaffold de páginas (`/products`, `/queue`, `/facebook-manual`, `/settings`, `/reports`) e `services/` já estruturados; esta issue implementa os serviços e templates componentes reais.

## PM Fase 1 — levantamento
Aguardando PM Fase 1 (perguntas e contexto de negócio).

## Gate 1 — respostas do Gerente
Aguardando Gate 1 (decisões arquiteturais, escopo confirmado).

## PM Fase 2 — PRD consolidado
Aguardando PM Fase 2 (proposal.md, criterios-aceite.md).

## Refinamento Técnico (LT)
Aguardando LT (design.md, especificacao-tecnica.md, tasks.md, sub-issues).

## Sub-issues
sub_issues: []
desenv_tasks_merged: []

## Merge e Encerramento
Aguardando fluxo da rota normal (Dev, LT, Code Review, QA, Gate 2).

## Historico de etapas
| # | Etapa | Agente | Status |
|---|---|---|---|
| 1 | Preparacao | Coordenador | ativo — Issue preparada, estado.md criado, comentário 📍 Status criado, card adicionado ao board em 💻 Em Desenvolvimento |

## Custo (ledger)
| # | Etapa | Agente | Modelo | Tokens | Tools | Tempo_s |
|---|---|---|---|---|---|---|
| 1 | Preparacao | Coordenador | haiku-4.5 | 45607 | 37 | 210s |
