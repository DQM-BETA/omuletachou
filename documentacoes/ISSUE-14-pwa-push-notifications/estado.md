issue: 14
titulo: "feat: PWA + Push Notifications"
rota: normal
etapa_atual: Gate 1 - aguardando resposta do Gerente
ultimo_agente: pm-analista-negocios
status_comment_id: 5061626934
openspec_change: ~
tech_stacks:
  - Next.js (next-pwa)
  - ASP.NET Core (WebPush NuGet)
repos:
  omuletachou: https://github.com/DQM-BETA/omuletachou
repo_path: repos/omuletachou
docs_path: repos/omuletachou/documentacoes/ISSUE-14-pwa-push-notifications
openspec_path: repos/omuletachou/openspec/changes/issue-14-pwa-push-notifications
sub_issues: []
desenv_tasks_merged: []
sub_issues_frontend: {}
pr_homologacao: ~
pr_release: ~
code_review_homolog_pr: ~
qa_status: ~
figma_url: ~
blockers: nenhum
closedAt: ~

## Levantamento (PM Fase 1)
Escopo tecnico ja veio detalhado do Gerente na Issue. Perguntas de negocio postadas em
https://github.com/DQM-BETA/omuletachou/issues/14#issuecomment-5061649138 cobrindo:
1. Armazenamento das VAPID keys (app_settings vs. secrets)
2. Migration/granularidade da tabela push_subscriptions
3. Rate-limit/anti-abuso no endpoint publico de subscribe
4. Placeholder de icones do manifest (design vs. dev)
5. Conteudo da notificacao push (titulo+link vs. imagem do produto)
6. Frequencia/throttling do PublisherJob ao disparar push
7. Confirmacao de que a dependencia de HTTPS (Issue #15) nao bloqueia dev/teste local (fallback ja previsto)
Aguardando resposta do Gerente (Gate 1) antes de seguir para PM Fase 2 (PRD/proposal.md).

## Custo (ledger)
| # | Etapa | Agente | Modelo | Tokens | Tools | Tempo (s) |
|---|-------|--------|--------|--------|-------|-----------|
| 1 | Preparacao | Coordenador | haiku-4.5 | 26844 | 21 | 133s |
