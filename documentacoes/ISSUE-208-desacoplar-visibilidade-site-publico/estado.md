issue: 208
titulo: feat(discussão): avaliar desacoplar visibilidade no site público do requisito de rede social configurada
etapa_atual: Code Review
ultimo_agente: lider-tecnico
openspec_change: openspec/changes/issue-208-desacoplar-visibilidade-site-publico
tech_stacks: [dotnet, angular]
repos:
  omuletachou: ~
repo_path: repos/omuletachou
docs_path: repos/omuletachou/documentacoes/ISSUE-208-desacoplar-visibilidade-site-publico
openspec_path: repos/omuletachou/openspec/changes/issue-208-desacoplar-visibilidade-site-publico
sub_issues:
  - number: 215
    titulo: "Sub: ProcessorJob publica no site independente de rede social qualificada"
    stack: stack:dotnet
    task_id: T-01
  - number: 216
    titulo: "Sub: API do dashboard — campo Destinations agregado em ProductListItemDto"
    stack: stack:dotnet
    task_id: T-02
  - number: 217
    titulo: "Sub: Dashboard — tooltip de destinos na coluna Status"
    stack: stack:angular
    task_id: T-03
desenv_tasks_merged: [215, 216, 217]
sub_issues_frontend:
  217: stack:angular
pr_homologacao: 221
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
- Ambiguidade arquitetural identificada: modelagem do "status por destino" no domínio → encaminhado ao Arquiteto
- Arquiteto concluiu `design.md` em 2026-08-18: sem novo campo/tabela — `Product.Status == Published` passa a ser incondicional (só depende de aprovação + link de afiliado válido); `PublicationQueue` já existente vira fonte de verdade para o tooltip via campo aditivo `Destinations` em `ProductListItemDto`
- Refinamento técnico do LT concluído em 2026-08-18:
  - Confirmações ao vivo contra o código real (registradas em `especificacao-tecnica.md` §0): nomes/casing de `ProcessorJob`/`Product`/`PublicationQueue`/`ProductListItemDto`/enums conferem 100% com o design; serialização JSON já é camelCase por padrão (sem config custom) — confirma `destinations` sem `[JsonPropertyName]`.
  - Decisão de observabilidade do LT: adicionar `LogInformation` quando `queuedCount == 0` (produto publicado no site sem rede social qualificada) — não é warning, é comportamento esperado pós-fix.
  - Reset de dados (proposal Cenário 5.1): confirmado que **não existe** rotina de reset/truncate no `deploy.sh` nem no runbook de deploy atual — é ação manual pontual do Gerente, fora do escopo de código desta issue. Registrado como item de checklist em `tasks.md` (não sub-issue de código).
  - Decisão de formato do tooltip (delegada pelo Arquiteto): texto simples via `matTooltip`, mesmo padrão já usado nas colunas `aiScore`/`status` — não escalado para UX/UI (extensão pontual de tela existente, sem Issue de UI disparada).
  - `especificacao-tecnica.md` e `tasks.md` escritos em `openspec/changes/issue-208-desacoplar-visibilidade-site-publico/`.
  - 3 sub-issues criadas: #215 (T-01, backend `ProcessorJob`), #216 (T-02, backend API `Destinations`), #217 (T-03, frontend tooltip dashboard).
- Merge sub-issue #215 (T-01) em 2026-08-18: PR #219 squash-mergeado em `desenv` (commit `4e5dbba`), confirmado no remoto. Testes reportados pelo Dev: 448/448. Sub-issue #215 fechada.
- Merge sub-issue #216 (T-02) em 2026-08-18: PR #218 squash-mergeado em `desenv` (commit `32c8b16`), confirmado no remoto. Testes reportados pelo Dev: 447/447. Sub-issue #216 fechada.
- Merge sub-issue #217 (T-03) em 2026-08-18: PR #220 squash-mergeado em `desenv` (commit `6a38043`), confirmado no remoto via `git log --oneline -1 origin/desenv`. Testes reportados pelo Dev: 140/140. Sub-issue #217 fechada.
- **Todas as 3 sub-issues da Issue-pai #208 mescladas em `desenv`.** PR #221 (`desenv→homolog`, merge commit — NUNCA squash) criado em 2026-08-18, cobrindo T-01/T-02/T-03 (site publica independente de rede social + campo `Destinations` agregado + tooltip no dashboard). Referencia sub-issues #215/#216/#217 e PRs #219/#218/#220.
- **Próximo passo:** Code Review (análise `/code-review` + agente Code Review) no PR #221. PR #221 **não deve ser mesclado** até Code Review + QA + Gate 2 do Gerente.
