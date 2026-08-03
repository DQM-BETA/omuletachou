issue: 130
titulo: "fix: Legenda de IA nunca é persistida — todo post sai sem legenda"
etapa_atual: Gate 1
ultimo_agente: pm-analista-negocios
openspec_change: ~
tech_stacks:
  - dotnet
  - angular
repos:
  omuletachou: "repos/omuletachou"
repo_path: repos/omuletachou
docs_path: repos/omuletachou/documentacoes/ISSUE-130-fix-legenda-de-ia
openspec_path: ~
sub_issues: []
desenv_tasks_merged: []
sub_issues_frontend: {}
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

## Custo (ledger)
| # | Etapa | Agente | Modelo | Tokens | Tools | Tempo (s) |
|---|-------|--------|--------|--------|-------|-----------|
| 1 | Preparacao (compartilhada com #130/#131/#132/#133) | coordenador | haiku-4.5 | 34090 | 19 | 271s |
