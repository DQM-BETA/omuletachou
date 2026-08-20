---
issue: 229
titulo: 'feat: exibir tag pequena de plataforma de origem nos cards de produto do site público'
etapa_atual: Refinamento Técnico
ultimo_agente: pm-analista-negocios
openspec_change: repos/omuletachou/openspec/changes/issue-229-exibir-tag-plataforma
tech_stacks: [nextjs]
repos:
  omuletachou: null
repo_path: repos/omuletachou
docs_path: repos/omuletachou/documentacoes/ISSUE-229-exibir-tag-pequena-de-plataforma
openspec_path: repos/omuletachou/openspec/changes/issue-229-exibir-tag-plataforma
sub_issues: []
desenv_tasks_merged: []
sub_issues_frontend: {}
pr_homologacao: ~
pr_release: ~
code_review_homolog_pr: ~
qa_status: ~
figma_url: ~
blockers: nenhum
status_comment_id: ~
rota: normal

## Custo (ledger)
| # | Etapa | Agente | Modelo | Tokens | Tools | Tempo (s) |
|---|---|---|---|---|---|---|
| 1 | Preparação | Coordenador | haiku | — | — | — |
| 2 | PM Fase 1 (levantamento) | PM Analista de Negócios | sonnet-5 | ~ | ~ | ~ |
| 3 | PM Fase 2 (PRD) | PM Analista de Negócios | sonnet-5 | ~ | ~ | ~ |

## Notas
- **Rota:** promovida de `backlog` para `normal` pelo Gerente no Gate 1 (2026-08-20) — segue o pipeline completo a partir daqui.
- **Gate 1 respondido (2026-08-20):** confirmado sem conflito com Issue #167 (sinalização visual, não filtro); posição próxima ao preço, discreta; formato texto (não ícone); aparece em todas as telas (home/categoria/oferta). Comentário: https://github.com/DQM-BETA/omuletachou/issues/229#issuecomment-5357600715
- **PRD (Fase 2) concluído (2026-08-20):** `proposal.md` e `criterios-aceite.md` escritos cobrindo exibição em todas as telas, tratamento de produto sem plataforma identificada/valor não mapeado (tag oculta, sem erro), e legibilidade em mobile.
- **Avaliação de ambiguidade arquitetural:** sem ambiguidade — mudança de exibição em componente já existente do `website/` (Next.js), dado de plataforma já existe no domínio do produto. Única pendência técnica (se o campo já está exposto na API pública) é detalhe de implementação, não decisão de arquitetura. Segue direto para o **Líder Técnico** (com apoio do UX/UI para texto/estilo da tag).
