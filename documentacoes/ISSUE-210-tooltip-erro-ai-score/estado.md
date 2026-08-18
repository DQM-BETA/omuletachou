---
issue: 210
titulo: fix: tooltip de motivo do erro aparece ao passar o mouse no AI Score, não no Status
etapa_atual: Aguardando Aprovação (Gate 2)
ultimo_agente: lider-tecnico
openspec_change: ~
tech_stacks:
  - angular
repos:
  omuletachou: repos/omuletachou
repo_path: repos/omuletachou
docs_path: repos/omuletachou/documentacoes/ISSUE-210-tooltip-erro-ai-score
openspec_path: ~
sub_issues: []
desenv_tasks_merged: []
sub_issues_frontend: {}
pr_homologacao: 213
pr_release: 214
code_review_homolog_pr: 213
qa_status: aprovado
figma_url: ~
blockers: nenhum
status_comment_id: 5332289407
---

## Contexto
Na tela `Products`, quando um produto está com Status = `Error`, a mensagem explicando o motivo do erro (ex.: "Nenhuma rede social habilitada com credenciais válidas para publicar este produto.") aparece como tooltip ao passar o mouse sobre a coluna **AI Score**, não sobre a coluna **Status** — o que é contraintuitivo, já que o motivo é do erro (Status), não da nota da IA.

## Investigação
Componente da tela de produtos no dashboard (provavelmente `dashboard/src/app/pages/products/products.component.html`), procurar o binding do tooltip (`matTooltip` ou similar) e verificar em qual elemento/coluna ele está de fato anexado.

## Aceite
- [x] Dev reproduz ao vivo (`ng serve` ou via container em `localhost:8081`, logado)
- [x] Tooltip do motivo do erro movido para a coluna Status (ex.: badge "Error")
- [x] Comportamento mantido (mostrar `ai_reason` ao passar o mouse) no elemento correto
- [x] QA valida a mudança de posição do tooltip

## Merge feature→desenv
PR #211 (`feature/ISSUE-210-fix-tooltip-erro` → `desenv`) mesclado via squash em 2026-08-18 (commit `d82825a`). Testes reportados pelo Dev: 131/131.

## PR de homologação
PR #213 (`desenv` → `homolog`) aberto em 2026-08-18.

## Code Review (2026-08-18): APROVADO
PR #213 mesclado `desenv→homolog` via merge commit `adfcfea5ae7202f20553782968218d37d4d10cfd`. Evidência completa no comentário do PR (https://github.com/DQM-BETA/omuletachou/pull/213#issuecomment-5332589071). Resumo específico da Issue #210:
- Produtos reais com `status=Error` e `ai_reason` preenchido confirmados via `GET /api/products?status=Error` contra o Postgres real (ex.: `920cc7b3-...`, "Nenhuma rede social habilitada com credenciais validas para publicar este produto.").
- **Validação em browser real (Chromium via Playwright)**, logado com usuário seed, contra a app servida pelo container Docker (build `--no-cache`): hover no `[data-testid="status-badge"]` de um produto `Error` real → tooltip visível com o texto exato de `ai_reason`. Hover no `[data-testid="ai-score-badge"]` do mesmo produto → tooltip **não** aparece (`matTooltipDisabled` ativo quando `status==='Error'`). Screenshots capturados.
- Suíte Karma completa (compartilhada com Issue #209): 134/134 verdes, incluindo os 2 specs dedicados (`CA-B6`/`CA-B7`) que cobrem exatamente a troca de tooltip entre badges.
- `etapa_atual` → QA. Apto a seguir.

## QA (2026-08-18): APROVADO
`documentacoes/ISSUE-210-tooltip-erro-ai-score/relatorio-qa.md`. Produto ML real com `status=Error` validado via API e UI. Backend 441/441, dashboard 134/134 (incluindo CA-B6/CA-B7). Gate visual com screenshots (hover AI Score sem tooltip, hover Status com tooltip do `ai_reason` correto) e validação E2E manual via Playwright contra containers Docker reais, fluxo de login real. 100% dos critérios de aceite validados. Nenhuma issue encontrada.

## PR de release
PR #214 (`homolog` → `main`) aberto em 2026-08-18, cobrindo Issue #209 e Issue #210 (Closes #209, Closes #210). Merge commit `adfcfea5ae7202f20553782968218d37d4d10cfd` é a base validada em homolog. Aguardando aprovação do Gerente (Gate 2) — merge NÃO realizado pelo LT.

## Custo (ledger)
| # | Etapa | Agente | Modelo | Tokens | Ferramentas | Tempo (s) |
|---|---|---|---|---|---|---|
