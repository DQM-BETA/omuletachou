# Estado — ISSUE-13: Dashboard Angular (Todas as Paginas Admin)

## Campos principais
issue: 13
repo: omuletachou
titulo: feat: Dashboard Angular (Todas as Paginas Admin)
rota: normal
etapa_atual: Gate 1 — aguardando resposta do Gerente
docs_path: repos/omuletachou/documentacoes/ISSUE-13-dashboard-angular
openspec_path: repos/omuletachou/openspec/changes/issue-13-dashboard-angular
ultimo_agente: pm-analista-negocios
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
Dependência adicional: Issue #18 (Sub-C: Dashboard Angular — Scaffolding) — já concluída. O scaffold em `dashboard/` (Angular 17, TypeScript, estrutura de páginas stub em `dashboard/src/app/pages/`, Dockerfile, nginx.conf) já existe — confirmado por inspeção do diretório. Esta issue evolui esse scaffold, NÃO recria.

**Nota técnica:** Esta é a primeira issue de frontend administrativo Angular com conteúdo real do projeto. O dashboard consumirá a API administrativa protegida por JWT (Issue #11), nunca expondo URLs internas ao browser. Scaffold de páginas (`/products`, `/queue`, `/facebook-manual`, `/settings`, `/reports`) e `services/` já estruturados; esta issue implementa os serviços e templates componentes reais.

## PM Fase 1 — levantamento
Concluído. Perguntas postadas na Issue #13 (comentário https://github.com/DQM-BETA/omuletachou/issues/13#issuecomment-5045926492), cobrindo:
1. Escopo fechado de páginas (confirmar 5 telas + necessidade de tela de Login dedicada / tela de Jobs manual)
2. Autenticação no front — armazenamento do JWT (sem refresh token, 24h) e comportamento ao expirar
3. Confirmação do scaffold já existente (Issue #18) — evoluir, não recriar
4. UX do mascaramento de secrets em `/settings` (campos vazios/mascarados, não pré-preenchidos)
5. Uso do Figma Design System da squad vs. UX/UI livre a partir de critérios funcionais
6. Responsividade — desktop-only vs. suporte a tablet/mobile
7. Endpoint/job de "Testar conexão" por integração — existe ou é novo escopo a alinhar com Arquiteto/LT
8. Preferência de fatiamento em sub-issues (por página/domínio) ou deixar a critério do LT
9. Definição de pronto — exigência ou não de testes e2e (Playwright) já nesta issue

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
| 2 | PM Fase 1 | pm-analista-negocios | concluído — perguntas de levantamento postadas na Issue, comentário 📍 Status atualizado para Gate 1 |

## Custo (ledger)
| # | Etapa | Agente | Modelo | Tokens | Tools | Tempo_s |
|---|---|---|---|---|---|---|
| 1 | Preparacao | Coordenador | haiku-4.5 | 45607 | 37 | 210s |
| 2 | PM Fase 1 | pm | sonnet | 32806 | 10 | 85s |
