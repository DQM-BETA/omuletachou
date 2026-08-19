# Relatório de QA — Issue #223 (fix: nginx do dashboard derruba jobs longos com 504)

**Status: ✅ APROVADO**

Rota: `rapido` (sem `criterios-aceite.md` de PM — critérios derivados diretamente do corpo da Issue #223, seção "Correção esperada").

## Sincronização de branch (pré-requisito)
- `git fetch origin` + `git checkout homolog` + `git pull origin homolog` → fast-forward `249439e..2d8d5a9`.
- Commit `2d8d5a9` (merge commit do PR #225, `desenv→homolog`) confirmado no topo de `git log --oneline`:
  ```
  2d8d5a9 Merge pull request #225 from DQM-BETA/desenv
  3cf66ff chore(ISSUE-223): estado.md - PR desenv->homolog #225 criado
  7472dd9 feat(ISSUE-223): corrige timeout do nginx que derrubava jobs longos (504)
  ```
- Diff da mudança (`dashboard/nginx.conf`, commit `7472dd9`): `+12` linhas no bloco `location /api/` — `proxy_connect_timeout 600s`, `proxy_send_timeout 600s`, `proxy_read_timeout 600s`, com comentário justificando o escopo (todo `/api/`, não só `/api/jobs/`).

## Critérios de aceite validados

| # | Critério (derivado da Issue #223) | Evidência | Resultado |
|---|---|---|---|
| 1 | `dashboard/nginx.conf` define `proxy_connect_timeout`/`proxy_send_timeout`/`proxy_read_timeout` = 600s no bloco `/api/` | `docker exec afiliado_dashboard cat /etc/nginx/conf.d/default.conf` (container rebuildado sem cache a partir de `homolog`) retornou os 3 timeouts de 600s idênticos ao diff | ✅ |
| 2 | Config nginx válida | `docker exec afiliado_dashboard nginx -t` → `syntax is ok` / `test is successful` | ✅ |
| 3 | Proxy `/api/` roteia corretamente para o backend (sem regressão) | `GET http://localhost:8081/api/settings` (via proxy) → `401`, idêntico a `GET http://localhost:8080/api/settings` (API direta) → `401`. `GET http://localhost:8081/` → `200` | ✅ |
| 4 | Job real do coletor Mercado Livre (2-6min) completa via nginx **sem 504 prematuro** | `POST http://localhost:8081/api/jobs/collector/mercadolivre/trigger` autenticado (login real via `/api/auth/login` através do proxy) → **HTTP 200** em **281s** (4min41s) — muito além do antigo default de 60s do nginx, sem timeout. Resposta: `{"count":110}` | ✅ |
| 5 | Produtos coletados são persistidos (não descartados por cancelamento do `CancellationToken`) | Query direta no Postgres (`afiliado_db`): `SELECT count(*) FROM products WHERE created_at > NOW() - interval '20 minutes'` → **84 produtos novos** com `created_at` no horário exato do job disparado nesta rodada (diferença entre 110 no retorno e 84 novos = upserts de produtos já existentes, atualizados em vez de inseridos — comportamento esperado do upsert) | ✅ |
| 6 | Nenhum 504/timeout/cancelamento nos logs durante a execução | `docker logs afiliado_dashboard` (access log) sem nenhuma linha `" 504 "` na janela do job. `docker logs afiliado_api` sem `OperationCanceledException`/`TaskCanceledException`/"was canceled" na janela do job | ✅ |
| 7 | Containers permanecem estáveis (sem crash/restart) durante e após o job | `docker ps` ao final: `afiliado_dashboard`, `afiliado_api`, `afiliado_db` todos `Up`, sem restart | ✅ |

**7/7 critérios validados = 100%.**

## Validação integrada (execução real, não mock)
1. `git checkout homolog && git pull` (branch remota sincronizada, commit `2d8d5a9` confirmado).
2. `docker compose build --no-cache dashboard` — rebuild completo a partir do código de `homolog` (evita aprovar em cima de imagem stale; container anterior já tinha sido recriado pelo Code Review, mas o rebuild próprio do QA elimina qualquer dúvida). Build limpo, `ng build` sem erros (só warnings de budget pré-existentes, não relacionados à mudança).
3. `docker compose up -d dashboard` — container recriado a partir da imagem nova.
4. Inspeção do arquivo real dentro do container + `nginx -t` (critérios 1-2 acima).
5. Smoke test do proxy `/api/` (critério 3).
6. **Fluxo ponta a ponta real**: login autenticado via `POST /api/auth/login` (através do nginx, porta 8081) → obtenção de JWT real → `POST /api/jobs/collector/mercadolivre/trigger` (através do nginx) com o token, aguardado **de forma síncrona por 281s** (chamada real ao Mercado Livre, sem mock) → HTTP 200 → confirmação de persistência via query direta no Postgres real (critérios 4-7).

Este é exatamente o bug original: o nginx antigo (timeout padrão de 60s) derrubava a conexão com 504 antes do job terminar, cancelando o `CancellationToken` no backend e descartando todo o trabalho já feito. Nesta rodada, o job levou 281s — quase 5x o timeout antigo — e completou com sucesso, sem 504 e com produtos persistidos, provando que o fix resolve o bug relatado.

## E2E / Screenshots (Playwright)
`E2E/screenshots: N/A (projeto sem UI tocada)`.
- `dashboard/package.json` (componente efetivamente modificado por este diff) **não define** script `test:visual` — o Angular dashboard usa apenas `"test": "ng test"` (unit/Karma). Não há tooling de visual regression configurado para o dashboard neste repo.
- `website/package.json` define `test:visual` (Playwright), mas o `website/` (Next.js) **não foi tocado** por este diff (a mudança é 100% restrita a `dashboard/nginx.conf`) — não se aplica a este QA.
- A mudança é puramente configuração de infraestrutura (timeouts de proxy), sem alteração de nenhum componente visual/tela. Não há screenshot a arquivar.

## Testes automatizados
- Diff da issue toca exclusivamente `dashboard/nginx.conf` (infra/config) — nenhum código de aplicação (TS/C#) foi alterado, portanto não há suíte de unit/integration test nova ou impactada a rodar.
- `ng build` (parte do `docker compose build --no-cache`) executado com sucesso, validando que o restante do bundle Angular compila normalmente (nenhuma quebra colateral).

## Issues encontradas
Nenhuma. Nenhum finding de severidade alta/média/baixa nesta rodada.

## Observação lateral (fora de escopo, já registrada na Issue original)
A Issue #223 já registra como nota lateral (fora do escopo desta correção pontual) que jobs longos disparados de forma síncrona via HTTP é um padrão frágil a médio prazo — o ideal seria resposta imediata (202 Accepted) + polling de status via Hangfire. Não é bloqueador para esta aprovação.
