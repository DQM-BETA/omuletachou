# Estado — ISSUE-12: Site Publico Next.js (SSR + SEO)

## Campos principais
issue: 12
repo: omuletachou
titulo: feat: Site Publico Next.js (SSR + SEO)
rota: normal
etapa_atual: Gate 1 (aguardando resposta do Gerente)
docs_path: repos/omuletachou/documentacoes/ISSUE-12-site-publico
openspec_path: repos/omuletachou/openspec/changes/ISSUE-12-site-publico
ultimo_agente: pm-analista-negocios
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

## PM Fase 1 — levantamento postado
Comentário de perguntas postado na Issue #12 (2026-07-20): https://github.com/DQM-BETA/omuletachou/issues/12#issuecomment-5025528041

Eixos levantados:
1. Estrutura do projeto — greenfield dentro do repo `omuletachou` (novo diretório/workspace) vs. estrutura já existente
2. Páginas e rotas — confirmar escopo (Home, `/oferta/[slug]`, `/categoria/[categoria]`) e estratégia de renderização (SSR vs. ISR na Home)
3. SEO — meta tags dinâmicas, Open Graph, Schema.org JSON-LD; sitemap.xml/robots.txt nesta issue ou issue futura
4. PWA/Push — confirmar que fica fora do escopo (delegado à Issue #14, backlog), evitando duplicação
5. Domínio/Deploy — serviço `website` no docker-compose existente vs. hospedagem separada; deploy real em produção é escopo da Issue #15 (backlog)
6. Design/UI — existe referência Figma ou UX/UI da squad define o layout a partir dos critérios funcionais já na issue
7. CORS — confirmar domínios já liberados na Issue #11 (`omuletachou.com.br`, `www.omuletachou.com.br`, `localhost:3000`) são suficientes

Aguardando resposta do Gerente para seguir à Fase 2 (PRD + critérios de aceite).

## Sub-issues
sub_issues: []
desenv_tasks_merged: []

(A serem criadas durante PM Fase 2 e refinadas pelo LT. Possivelmente: Home SSR + filtros, página de produto com OG, categoria, componentes de card, etc.)

## Historico de etapas
| # | Etapa | Agente | Status |
|---|---|---|---|
| 1 | Preparacao | Coordenador | concluido |
| 2 | PM Fase 1 | pm-analista-negocios | concluido |

## Custo (ledger)
| # | Etapa | Agente | Modelo | Tokens | Tools | Tempo_s |
|---|---|---|---|---|---|---|
| 1 | Preparacao | Coordenador | haiku-4.5 | 32474 | 33 | 185s |
