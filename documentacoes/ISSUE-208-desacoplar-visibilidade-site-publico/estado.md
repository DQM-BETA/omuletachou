issue: 208
titulo: feat(discussão): avaliar desacoplar visibilidade no site público do requisito de rede social configurada
etapa_atual: Aguardando aprovação do Gerente (Gate 2) — PR release #222 aberto
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
pr_release: 222
code_review_homolog_pr: 221
qa_status: aprovado — ver relatorio-qa.md e ledger etapa 15
figma_url: ~
blockers: nenhum
status_comment_id: ~

## Custo (ledger)
| # | Etapa | Agente | Modelo | Tokens | Tools | Tempo (s) | Notas |
|---|---|---|---|---|---|---|---|
| 1 | Preparação | coordenador | haiku | 20960 | 7 | 48s | Issue #208 criada no backlog |
| 2 | PM Fase 1 | pm-analista-negocios | sonnet | 25772 | 5 | 60s | Levantamento postado, aguardando Gate 1 |
| 3 | PM Fase 2 | pm-analista-negocios | sonnet | 46657 | 14 | 153s | PRD + criterios-aceite.md, ambiguidade arquitetural encaminhada |
| 4 | Arquiteto | arquiteto-engenheiro | sonnet | 88525 | 26 | 200s | design.md — sem novo campo/tabela, reaproveita PublicationQueue |
| 5 | Líder Técnico (refinamento) | lider-tecnico | sonnet | 113472 | 44 | 290s | especificacao-tecnica.md + tasks.md, 3 sub-issues criadas (#215/#216/#217) |
| 6 | Dev .NET T-01 #215 (tentativa 1) | dev-dotnet | sonnet | ~ | ~ | ~ | Falhou por limite de gasto mensal do agente; edições incompletas descartadas (`git checkout --`) |
| 7 | Dev .NET T-02 #216 (tentativa 1) | dev-dotnet | sonnet | ~ | ~ | ~ | Falhou por limite de gasto mensal do agente; edições incompletas descartadas |
| 8 | Dev .NET T-01 #215 (retomada) | dev-dotnet | sonnet | 89328 | 47 | 291s | ProcessorJob.MarkAsPublished incondicional, 448/448 testes. PR #219. |
| 9 | Dev .NET T-02 #216 (retomada) | dev-dotnet | sonnet | 85584 | 36 | 255s | Campo Destinations agregado, sem N+1, 447/447 testes. PR #218. |
| 10 | Líder Técnico (merge #219 + #218) | lider-tecnico | sonnet | 43430 | 22 | 116s | Ambos squash-mergeados em desenv, sequencial |
| 11 | Dev Angular T-03 #217 | dev-angular | sonnet | 102311 | 65 | 503s | Tooltip de destinos no badge Status, 140/140 testes. PR #220. |
| 12 | Líder Técnico (merge #220 + PR homolog) | lider-tecnico | sonnet | 36852 | 13 | 77s | PR #220 squash-mergeado; PR #221 desenv→homolog aberto |
| 13 | `/code-review` (sessão principal, camada estática) | orquestrador (multi-agente) | sonnet+haiku | 714416 | 97 | 870s | 10 sub-invocações (elegibilidade, CLAUDE.md, resumo, 5 agentes de auditoria, 1 de scoring). Único achado (imprecisão de atribuição causal no design.md) pontuou 0 — abaixo do corte de 80, nenhum comentário postado. |
| 14 | Code Review (PR #221, homologação) | code-review | sonnet | 120740 | 59 | 591s | **APROVADO.** Build/boot real, 454/454 backend + 140/140 dashboard. Validação E2E real: produto ML antes em Error reprocessado, virou Published, apareceu no site público — tudo sem nenhuma rede social configurada. Merge desenv→homolog via 249439e. |
| 15 | QA (homolog) | qa | sonnet | 148529 | 94 | 978s | **APROVADO — 100% dos 20 critérios de aceite.** homolog sincronizado (249439e). Rebuild sem cache. 454/454 backend + 140/140 dashboard + 5/5 Playwright (website). Revalidou de ponta a ponta o produto ML real já Published (site público, `destinations` corretos, fila social vazia sem regressão). Não conseguiu disparar uma nova transição ao vivo (Claude API key não configurada no ambiente + política da sessão bloqueou mutação SQL direta) — compensado com revalidação completa do estado persistido + leitura dos testes dedicados (Theory 3 plataformas, não-retroatividade). Relatório: `relatorio-qa.md`. Comentário: https://github.com/DQM-BETA/omuletachou/issues/208#issuecomment-5334665276 |
| 16 | Líder Técnico (PR release) | lider-tecnico | sonnet | 45384 | 12 | 106s | PR #222 (`homolog→main`, merge commit) criado, cobrindo Issue-pai #208 completa (Closes #208) + sub-issues #215/#216/#217 (todas CLOSED). Aguardando Gate 2 do Gerente. |

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
- **Código Review (2026-08-18) — APROVADO.** Build/boot real via Docker (`docker compose build --no-cache` + `docker compose up -d` para db/api/dashboard/website — todos healthy, sem npm/nginx-proxy-manager por conflito de porta 80 local, irrelevante à verificação). Suíte completa executada: backend `dotnet test` 454/454 (acima dos ~448 esperados — sub-issues somaram novos testes), dashboard `ng test` (ChromeHeadless) 140/140.
  - **Validação E2E real contra a app rodando:** produto ML real (`5e910e71-0d33-4d02-ae6e-03ff4172623f`, antes `Error` — exatamente a causa raiz das Issues #182/#199/#204, com link de afiliado real já resolvido) movido para `Queued` via SQL direto (dado de teste local, não código) e reprocessado disparando `POST /api/jobs/processor/trigger` autenticado de verdade. Log real da API confirmou o novo comportamento (`"ProcessorJob: produto ... publicado no site sem nenhuma rede social qualificada."`), produto virou `Published` sem nenhuma `PublicationQueue` criada (ambiente sem nenhuma rede social com credenciais — confirmado via `GET /api/settings`), apareceu em `GET /api/public/deals` e renderizou na página SSR real (`localhost:3000/oferta/...`, HTTP 200). `GET /api/products` (dashboard) retornou `destinations` corretos (Site: Published, as 5 redes: NotApplicable) — payload no formato exato que `buildDestinationsTooltip()` consome (confirmado também pelos testes unitários do componente, que cobrem esse mesmo formato).
  - **Não-regressão da fila social:** confirmado por leitura de código que `CreatePublicationQueueEntriesAsync` não mudou de lógica (só o XML doc comment) e continua sendo chamado antes de `MarkAsPublished()` incondicional — reforçado pelo teste real `ExecuteAsync_MarcaPublished_ECriaFila_QuandoRedeQualificada` (Theory, 3 plataformas) que passou na suíte, provando que rede qualificada ainda entra em `PublicationQueue` normalmente.
  - **Checklist de veto:** compila e sobe (OK); integração real ponta a ponta validada manualmente contra containers reais, além dos testes de integração via `WebApplicationFactory`+HTTP real (nota: DB desses testes de integração é EF InMemory, padrão pré-existente do repo, não introduzido por este PR — mitigado pela validação manual contra Postgres real acima); conformidade com `design.md`/`especificacao-tecnica.md`/`criterios-aceite.md` confirmada linha a linha no diff; sem teste-lixo; sem segredo commitado (varredura no diff); `.first()/.nth()/.last()` não aplicável (PR não toca specs E2E Playwright).
  - `/code-review` (plugin Anthropic) não postou comentários/reviews no PR #221 (nenhum achado de alta confiança) — nada a incorporar.
  - PR #221 mesclado `desenv→homolog` via merge commit (commit `249439e`), conforme exigido para promoções entre branches de longa vida.
- **QA (2026-08-18) — APROVADO, 100% dos 20 critérios de aceite.** `homolog` sincronizado no commit `249439e`, rebuild sem cache. 454/454 backend + 140/140 dashboard + 5/5 Playwright website. Validação end-to-end real do produto ML `5e910e71-0d33-4d02-ae6e-03ff4172623f` (Error→Published→visível no site público) revalidada de ponta a ponta, `GET /api/settings` confirmou nenhuma rede social qualificada. Relatório completo: `relatorio-qa.md`. Comentário na Issue: https://github.com/DQM-BETA/omuletachou/issues/208#issuecomment-5334665276
- **PR de release (2026-08-18):** PR #222 (`homolog→main`, merge commit — NUNCA squash) criado, `Closes #208`. Descreve a mudança de regra de negócio, as 3 sub-issues (#215/#216/#217, todas CLOSED), e a validação real do QA. Referencia PR #221 (homologação) e `relatorio-qa.md`. **Não mesclado** — aguardando Gate 2 (aprovação humana do Gerente).
- **Próximo passo:** GATE 2 — aprovação do Gerente para merge `homolog→main` no PR #222.
