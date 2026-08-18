issue: 199
titulo: fix: MercadoLivreCollector falha ao salvar produto com slug maior que 300 caracteres (perde o ciclo inteiro de coleta)
etapa_atual: Aguardando aprovação do Gerente (Gate 2) — PR release #203 aberto
ultimo_agente: lider-tecnico
openspec_change: ~
tech_stacks: [".NET 8", "EF Core", "PostgreSQL"]
repos:
  omuletachou: repos/omuletachou
repo_path: repos/omuletachou
docs_path: repos/omuletachou/documentacoes/ISSUE-199-fix-mercadolivre-slug
openspec_path: ~
sub_issues: []
desenv_tasks_merged: []
sub_issues_frontend: {}
pr_feature: 200
pr_feature_2: 202
pr_feature_merge_commit: 0130af11783a633ed9be4b2c4e6193d9dc4475bb
pr_feature_2_merge_commit: 12331e8c8ca66eda081ebd8953d81683b19f968d
pr_homologacao: 201
pr_release: 203
code_review_homolog_pr: 201
qa_status: aprovado — ver etapa 8 do histórico
figma_url: ~
blockers: nenhum
status_comment_id: 5328791206
rota: rapido

## Custo (ledger)
| # | Etapa | Agente | Modelo | Tokens | Tools | Tempo (s) | Notas |
|---|---|---|---|---|---|---|---|
| 1 | Preparação | Coordenador | Haiku | 20665 | 8 | 70s | Issue criada, estado.md preparado |
| 2 | Dev .NET (fix slug) | Dev .NET | Sonnet | 86383 | 53 | 395s | Truncagem do slug preservando sufixo do externalId. TDD RED→GREEN, 437/437 testes, boot real contra Postgres validado. PR #200 feature→desenv aberto. |
| 3 | Merge feature→desenv + PR desenv→homolog | Líder Técnico | Sonnet | 30751 | 12 | 78s | PR #200 squash mergeado em desenv (437/437 testes, boot real Postgres validado); PR #201 (desenv→homolog, merge commit) aberto, não mesclado — aguarda Code Review/QA/Gate 2 |
| 4 | Code Review (tentativa 1, inconclusiva) | Code Review | Sonnet | 57668 | 46 | 381s | Agente travou aguardando notificação de job em background que nunca chega para subagentes — não devolveu HANDOFF nem mesclou o PR. Sessão principal assumiu a validação ao vivo diretamente: disparou o coletor real contra a API do Mercado Livre e reproduziu o MESMO erro `varchar(300)` — mas descobriu que o container rodando ainda usava a imagem Docker anterior ao merge do fix (rebuild necessário). Após rebuild+restart, disparou de novo e encontrou um **segundo bug real, diferente**: `Product.UpdateAiResult`/`MarkAsError` gravam `AiReason` (também `varchar(300)`) sem truncar — a resposta real da IA (Claude, não mock) pode passar de 300 caracteres. Afeta as 3 plataformas (Amazon/Shopee/MercadoLivre), não só ML. Adicionando essa correção na mesma Issue antes de reacionar Code Review/QA. |
| 5 | Dev .NET (fix ai_reason, 2ª correção) | Dev .NET | Sonnet | 73198 | 44 | 365s | Truncagem centralizada em `SetAiReason(string? reason)`, reaproveitado por `UpdateAiResult` e `MarkAsError`. TDD RED→GREEN (reason 350 chars truncado p/ 300; textos dentro do limite preservados). 441/441 testes, boot real Docker+Postgres validado. PR #202 feature→desenv aberto. |
| 6 | Merge feature→desenv (2ª correção) | Líder Técnico | Sonnet | ~ | ~ | ~ | PR #202 squash mergeado em desenv (441/441 testes, boot real validado). PR #201 (desenv→homolog) confirmado atualizado automaticamente — headRefOid passou a refletir o novo HEAD de desenv (12331e8c...) sem necessidade de intervenção manual. PR #201 permanece aberto, aguardando Code Review/QA/Gate 2. |
| 7 | Code Review (PR #201, homologação) | code-review | sonnet | 107660 | 55 | 1115s | **APROVADO.** Build real (`dotnet build -c Release` OK). Container `afiliado_api` estava stale (imagem anterior ao commit `12331e8`, 2º fix) — `docker compose down` + `build --no-cache api` + `up -d db api`: boot real confirmado (migrations aplicadas, Hangfire iniciado, `/health` 200). `dotnet test -c Release --no-build`: **441/441** ✅. Coverage: line-rate 89.4%, branch-rate 80.8% (≥80%). **Integração real ao vivo**: login real (`/api/auth/login`) + trigger real do coletor (`POST /api/jobs/collector/mercadolivre/trigger` contra API real do ML, 334s) → **HTTP 200, count:117** (antes: HTTP 500, zero produtos salvos). Confirmado no Postgres: 117 produtos MercadoLivre persistidos, `MAX(LENGTH(slug))=211` (nenhum >300), e — evidência forte do 2º bug — 1 produto com `ai_reason` truncado exatamente em 300 chars (resposta real do Claude cortada em `...avaliaç`), confirmando a correção do `SetAiReason` funcionando com dados reais, não só mock/teste unitário. Logs sem exceções não tratadas; único erro (404 em `MLB75526622/items`) é isolamento de falha por item pré-existente, não regressão. Sem achados do `/code-review` estático (0 comentários/reviews no PR). Sem `.first()`/`.nth()` — N/A (PR 100% backend, sem specs E2E tocados). Evidência completa: https://github.com/DQM-BETA/omuletachou/pull/201#issuecomment-5331273381. PR #201 mesclado `desenv→homolog` via merge commit `16bf895a62aa69590c9dfd061eecd03572a731c3`. `code_review_homolog_pr` = 201, `etapa_atual` = QA. |
| 8 | QA (homolog) | qa | sonnet | 57492 | 42 | 619s | **APROVADO — 100% dos critérios.** `homolog` sincronizado (fast-forward, commit `16bf895a...`). Rebuild sem cache (evitando o mesmo problema de imagem stale do CR) + boot real (`/health` 200). Suíte completa: 441/441, cobertura 89,4%/80,8%. Integração real ao vivo: login + trigger do coletor real (126s) → HTTP 200, count:117 (antes: 500/zero produtos). Zero ocorrências de `varchar(300)`/`22001` nos logs. Confirmado no Postgres: `MAX(LENGTH(slug))=211`, `MAX(LENGTH(ai_reason))=300` (nenhum acima do limite, com evidência de truncagem real). Gate Visual/Playwright: N/A justificado (diff 100% backend). Relatório: `relatorio-qa.md`. Comentário: https://github.com/DQM-BETA/omuletachou/issues/199#issuecomment-5331391205 |
| 9 | PR release (homolog→main) | Líder Técnico | Sonnet | ~ | ~ | ~ | Sem divergência entre local e `origin/desenv` (ambos em `62d1693`). Aberto PR #203 `homolog→main` (merge commit, `Closes #199`), descrevendo os dois bugs reais (slug e ai_reason > varchar(300), ambos derrubando o ciclo inteiro de coleta pois `SaveChangesAsync` roda uma única vez ao fim do loop) e a validação real (coletor real disparado contra a API do ML, HTTP 500→200, 117 produtos persistidos). Referencia PRs #200, #202, #201 e `relatorio-qa.md`. PR não mesclado — aguarda Gate 2 (Gerente). |
