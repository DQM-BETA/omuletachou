issue: 223
titulo: fix: nginx do dashboard derruba o disparo de jobs longos com timeout (504) antes do job terminar
etapa_atual: Concluído
ultimo_agente: coordenador
openspec_change: ~
tech_stacks: []
repos: {}
repo_path: repos/omuletachou
docs_path: repos/omuletachou/documentacoes/ISSUE-223-nginx-timeout
openspec_path: ~
sub_issues: []
desenv_tasks_merged: []
sub_issues_frontend: {}
pr_homologacao: 225
pr_release: 226
code_review_homolog_pr: 225
qa_status: aprovado
figma_url: ~
blockers: nenhum
status_comment_id: ~

## Notas
- Rota: rapido (pula PM, Arquiteto, UX/UI, Gate 1)
- Labels aplicados: bug, stack:angular, rapido
- Descrição técnica completa já fornecida na issue — pronta para Dev
- PR #224 (feature/ISSUE-223-nginx-timeout → desenv): squash mergeado por LT em 2026-08-18T22:39:13Z. Dev validou ao vivo (job real 338.8s, HTTP 200, 122 produtos persistidos, sem timeout prematuro).
- PR #225 (desenv → homolog): criado por LT, merge commit pendente (aguarda Code Review + QA + Gate 2). NÃO mergear ainda.
- **Code Review (2026-08-19) — APROVADO.** Diff confirmado como config-only (`dashboard/nginx.conf`, +12/-0 linhas: `proxy_connect_timeout`/`proxy_send_timeout`/`proxy_read_timeout` = 600s no bloco `location /api/`, com comentário explicando a decisão de aplicar a todo `/api/` e o trade-off aceito de erros de rede reais também levarem até 600s para retornar). Sem findings do plugin `/code-review` no PR (0 comentários/reviews).
  - **Compila e sobe:** `docker compose build --no-cache dashboard` — build limpo, `ng build` sem erros (só warnings de budget pré-existentes, não relacionados). `docker compose up -d dashboard` recriou o container a partir da imagem nova (container anterior estava rodando havia 13h com config antiga, então o rebuild era necessário para não aprovar em cima de imagem stale).
  - **Config real confirmada no container:** `docker exec afiliado_dashboard cat /etc/nginx/conf.d/default.conf` retornou o `nginx.conf` do diff, com os 3 timeouts de 600s presentes. `nginx -t` dentro do container: `syntax is ok` / `test is successful`.
  - **Boot/smoke test:** `GET http://localhost:8081/` → 200 (dashboard servindo). `GET http://localhost:8081/api/settings` (proxy) → 401, idêntico ao `GET http://localhost:8080/api/settings` (API direta) → 401 — confirma que o proxy `/api/` está roteando corretamente para o backend real através do novo bloco de config (mesma resposta de autenticação nos dois casos, sem 502/504/erro de conexão).
  - **Integração real (o próprio bug fixado):** não repetido nesta rodada — o Dev já validou ao vivo, documentado no corpo do PR #225: disparo real de `POST /api/jobs/collector/mercadolivre/trigger` através do nginx (porta 8081), 338.8s, HTTP 200, `{"count":122}`, 122 produtos confirmados persistidos via query direta no Postgres, sem 504 prematuro. Evidência suficiente e mais forte que uma repetição rápida do CR (o bug só se manifesta em jobs de vários minutos). Optado por não repetir o teste de minutos, conforme orientação explícita da tarefa.
  - **Conformidade com o spec:** a Issue #223 pedia exatamente isso — `proxy_connect_timeout`/`proxy_send_timeout`/`proxy_read_timeout` elevados para um valor generoso (~600s) no bloco `/api/`, com decisão do Dev sobre escopo (`/api/` inteiro vs. só `/api/jobs/`) documentada e justificada no comentário do próprio `nginx.conf`. 100% aderente.
  - **Sem teste-lixo / sem segredo commitado:** mudança é puramente config de infra, sem código de app tocado — não há testes novos a avaliar (nem deveria haver). Nenhum segredo no diff.
  - **`.first()/.nth()/.last()` em specs E2E:** não aplicável — PR não toca nenhum spec Playwright.
  - **Escopo do diff:** além do `nginx.conf`, o PR carrega commits de documentação pré-existentes da Issue #208 (estado.md, relatorio-qa.md, screenshots) que estavam em `desenv` aguardando o próximo sync `desenv→homolog` — puramente docs, sem risco, não introduzidos por esta issue.
  - PR #225 mesclado `desenv→homolog` via merge commit (`2d8d5a9`), conforme exigido para promoções entre branches de longa vida.
- **QA (2026-08-19) — APROVADO. 7/7 critérios (100%).** Detalhes completos em `relatorio-qa.md`. Resumo:
  - Branch sincronizada: `git fetch` + `checkout homolog` + `pull` → fast-forward, commit `2d8d5a9` confirmado no log.
  - Rebuild próprio sem cache (`docker compose build --no-cache dashboard` + `up -d`) a partir de `homolog`, eliminando qualquer dúvida de imagem stale.
  - Config real no container confirmada byte-a-byte com o diff + `nginx -t` ok.
  - Smoke test do proxy `/api/` ok (401 idêntico via proxy e via API direta).
  - **Validação integrada real repetida pelo QA (não apenas reaproveitada do Dev):** login real via `/api/auth/login` através do nginx → `POST /api/jobs/collector/mercadolivre/trigger` autenticado, aguardado de forma síncrona por **281s** → **HTTP 200**, `{"count":110}`, sem 504. Confirmado via query direta no Postgres: 84 produtos novos com `created_at` no horário exato do job. Logs de `afiliado_dashboard` e `afiliado_api` sem 504/`OperationCanceledException`/`TaskCanceledException` na janela. Containers estáveis (sem restart) durante e após.
  - E2E/screenshots: N/A — `dashboard/package.json` (componente tocado pelo diff) não define `test:visual`; `website/package.json` define, mas `website/` não foi tocado por este diff. Mudança é puramente infra/config, sem UI alterada.
  - Nenhuma issue encontrada. Nenhum finding de severidade alta/média/baixa.
- **PR release (2026-08-19) — PR #226 (`homolog→main`, merge commit) criado por LT.** Descreve o bug (timeout 60s do nginx cancelando `CancellationToken` no backend e descartando trabalho já feito em jobs longos) e a correção (timeouts de 600s no bloco `/api/`), referenciando PR #225 e `relatorio-qa.md` (job real de 281s, HTTP 200, produtos persistidos). Aguardando **Gate 2 (Gerente)** para aprovação do merge — LT NÃO mescla.
- **Gate 2 APROVADO — Merge finalizado (2026-08-19).** PR #226 mesclado via merge commit (`494da6e`) em `main`. Issue #223 fechada pelo Coordenador.

