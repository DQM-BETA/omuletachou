issue: 208
titulo: feat(discussão): avaliar desacoplar visibilidade no site público do requisito de rede social configurada
etapa_atual: Refinamento de Negócio — PM Fase 1 concluída, aguardando resposta do Gerente (Gate 1)
ultimo_agente: pm-analista-negocios
openspec_change: ~
tech_stacks: []
repos:
  omuletachou: ~
repo_path: repos/omuletachou
docs_path: repos/omuletachou/documentacoes/ISSUE-208-desacoplar-visibilidade-site-publico
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
status_comment_id: ~

## 📝 Notas

- Demanda de negócio registrada pelo Gerente após teste end-to-end em produção local (Issue #182/#199/#204)
- Descoberta: 111 produtos foram aprovados pela IA e geraram links de afiliado reais, mas ficaram em status `Error` porque nenhuma rede social estava configurada
- Pergunta central: site público deve funcionar independentemente das redes sociais configuradas?
- Se a resposta for SIM (desacoplar), envolve mudanças arquiteturais em `Product.MarkAsPublished` / `ProductStatus` — escopo para refinamento pelo PM
- PM Fase 1 concluída em 2026-08-18: levantamento de requisitos postado como comentário na Issue #208 (comentário: https://github.com/DQM-BETA/omuletachou/issues/208#issuecomment-5332294378)
  - Eixos cobertos: problema de negócio, regra de negócio central (Published direto vs. status intermediário), escopo de plataformas, reprocessamento retroativo dos 111 produtos em Error, casos de exceção/qualidade, restrições/prazo, definição de pronto
- Aguardando respostas do Gerente para avançar à Fase 2 (proposal.md + critérios de aceite)
