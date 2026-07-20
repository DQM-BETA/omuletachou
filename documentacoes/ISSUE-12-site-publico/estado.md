# Estado — ISSUE-12: Site Publico Next.js (SSR + SEO)

## Campos principais
issue: 12
repo: omuletachou
titulo: feat: Site Publico Next.js (SSR + SEO)
rota: normal
etapa_atual: Aguardando PM Fase 1
docs_path: repos/omuletachou/documentacoes/ISSUE-12-site-publico
openspec_path: repos/omuletachou/openspec/changes/ISSUE-12-site-publico
ultimo_agente: coordenador
status_comment_id: 5025494280
pr_homologacao: ~
code_review_homolog_pr: ~
pr_release: ~

## Contexto
Stack: Next.js 14 + TypeScript + SSR (App Router)
Repo: DQM-BETA/omuletachou
Branch base: desenv
Dependência: Issue #11 (REST API pública: `/api/public/deals`, `/api/public/deals/{slug}`, `/api/public/deals/category/{categoria}`, `/api/public/push/subscribe`) — já entregue em main.

**Nota técnica:** Esta é a primeira issue de frontend público Next.js do projeto. Todo trabalho anterior (#6-#11) foi backend .NET/REST API. O site consumirá a API pública já disponível via Docker (rede interna: `http://api:8080`), nunca expondo URLs internas ao browser.

## Sub-issues
sub_issues: []
desenv_tasks_merged: []

(A serem criadas durante PM Fase 1 e refinadas pelo LT. Possivelmente: Home SSR + filtros, página de produto com OG, categoria, componentes de card, etc.)

## Historico de etapas
| # | Etapa | Agente | Status |
|---|---|---|---|
| 1 | Preparacao | Coordenador | concluido |

## Custo (ledger)
| # | Etapa | Agente | Modelo | Tokens | Tools | Tempo_s |
|---|---|---|---|---|---|---|
| 1 | Preparacao | Coordenador | haiku-4.5 | a preencher | a preencher | a preencher |
