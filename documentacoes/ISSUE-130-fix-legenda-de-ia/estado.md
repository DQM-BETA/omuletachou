issue: 130
titulo: "fix: Legenda de IA nunca é persistida — todo post sai sem legenda"
etapa_atual: Refinamento Técnico
ultimo_agente: pm-analista-negocios
openspec_change: openspec/changes/issue-130-fix-legenda-de-ia
tech_stacks:
  - dotnet
  - angular
repos:
  omuletachou: "repos/omuletachou"
repo_path: repos/omuletachou
docs_path: repos/omuletachou/documentacoes/ISSUE-130-fix-legenda-de-ia
openspec_path: repos/omuletachou/openspec/changes/issue-130-fix-legenda-de-ia
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

## PM Fase 2
Gerente respondeu ao Gate 1 (comentário https://github.com/DQM-BETA/omuletachou/issues/130#issuecomment-5169630106):
1. Novo campo `Caption` em `PublicationQueue` — fonte de verdade para publicação. `Product.AiCaption` pode ser removido ou mantido só para propósitos não-autoritativos. Migration: `ALTER TABLE publication_queue ADD COLUMN caption TEXT NOT NULL DEFAULT ''`.
2. Geração mantida no `ProcessorJob` (não move para `PublisherJob`) — evita multiplicar chamadas pagas à API Claude em retries e preserva separação de responsabilidades.
3. Facebook Manual no escopo: `ProductDetailDto` (backend) + `ProductDetail` (frontend) passam a expor/consumir a caption real da rede Facebook.
4. Sem backfill/retrocompatibilidade — só uma linha de changelog no PR.
5. Confirmado: `ProcessorJobTests.cs` corrigido para validar persistência (`PublicationQueue.Caption`), não apenas chamada de mock.

PRD consolidado e postado (comentário https://github.com/DQM-BETA/omuletachou/issues/130#issuecomment-5169632533):
- `openspec/changes/issue-130-fix-legenda-de-ia/proposal.md`
- `documentacoes/ISSUE-130-fix-legenda-de-ia/criterios-aceite.md` (CA1–CA18)

**Ambiguidade arquitetural:** nenhuma. Todas as decisões de design vieram definidas pelo Gerente no Gate 1 (campo, ponto de geração, escopo do Facebook Manual, migration aditiva simples). Sem múltiplas stacks em conflito, integração externa nova, ou trade-off de arquitetura não-óbvio. Segue direto para o Líder Técnico (refinamento técnico / task breakdown), sem passar pelo Arquiteto.

## Custo (ledger)
| # | Etapa | Agente | Modelo | Tokens | Tools | Tempo (s) |
|---|-------|--------|--------|--------|-------|-----------|
| 1 | Preparacao (compartilhada com #130/#131/#132/#133) | coordenador | haiku-4.5 | 34090 | 19 | 271s |
| 2 | PM Fase 1 | pm-analista-negocios | sonnet | 29010 | 14 | 95s |
| 3 | PM Fase 2 | pm-analista-negocios | sonnet | 44474 | 21 | 188s |
