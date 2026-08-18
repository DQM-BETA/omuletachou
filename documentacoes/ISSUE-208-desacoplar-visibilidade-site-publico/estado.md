issue: 208
titulo: feat(discussão): avaliar desacoplar visibilidade no site público do requisito de rede social configurada
etapa_atual: Refinamento Técnico — PM Fase 2 concluída, aguardando Arquiteto (ambiguidade de modelagem de domínio)
ultimo_agente: pm-analista-negocios
openspec_change: openspec/changes/issue-208-desacoplar-visibilidade-site-publico
tech_stacks: []
repos:
  omuletachou: ~
repo_path: repos/omuletachou
docs_path: repos/omuletachou/documentacoes/ISSUE-208-desacoplar-visibilidade-site-publico
openspec_path: repos/omuletachou/openspec/changes/issue-208-desacoplar-visibilidade-site-publico
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

## 📝 Notas

- Demanda de negócio registrada pelo Gerente após teste end-to-end em produção local (Issue #182/#199/#204)
- Descoberta: 111 produtos foram aprovados pela IA e geraram links de afiliado reais, mas ficaram em status `Error` porque nenhuma rede social estava configurada
- PM Fase 1 concluída em 2026-08-18: levantamento de requisitos postado como comentário na Issue #208 (comentário: https://github.com/DQM-BETA/omuletachou/issues/208#issuecomment-5332294378)
- Gate 1 respondido pelo Gerente em 2026-08-18:
  1. Site deve funcionar independente de rede social configurada (aprovado + link de afiliado válido).
  2. Manter status separado por destino (site vs. cada rede social); dashboard exibe simplificado "Published" com tooltip detalhando os destinos efetivos.
  3. Vale para todas as plataformas de origem (Mercado Livre, Amazon, Shopee) e todas as redes sociais.
  4. Sem reprocessamento retroativo — dados atuais (incluindo os 111 produtos em Error) serão apagados para recomeçar do zero.
  5. Sem exceções de bloqueio adicionais; regra nova vale só para produtos novos/atualizados quando uma rede social futura for configurada (sem retroatividade).
  6. Sem urgência, rota normal.
- PM Fase 2 concluída em 2026-08-18: `proposal.md` e `criterios-aceite.md` escritos incorporando as decisões do Gate 1
- Ambiguidade arquitetural identificada: modelagem do "status por destino" no domínio (hoje `Product`/`ProductStatus` tem campo `Status` único), nome de campos/enum novos, como `ProcessorJob` deve separar a decisão "publicar no site" de "enfileirar publicação social", como a tooltip do dashboard deve agregar os dados de status por destino, e critério técnico para evitar retroatividade em rede social configurada no futuro → encaminhado ao Arquiteto antes do refinamento técnico do LT
