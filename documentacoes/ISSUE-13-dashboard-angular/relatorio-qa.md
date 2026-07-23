# Relatório QA — ISSUE-13: Dashboard Angular (Todas as Páginas Admin)

## Status: APROVADO

Branch validada: `homolog` (commit `9e49f37ae587b6aa35fb4fbcfe6c9492c5db8a7`, PR #113 desenv→homolog).

## Sincronização de branch
`git fetch origin && git checkout homolog && git pull origin homolog` executado antes de qualquer teste. Commit informado no spawn (merge do PR #113) confirmado em `git log --oneline -5` de `homolog`.

**Nota não-bloqueante:** os 2 commits de documentação do Code Review (`b61c53c`, `a67ac0a`) existem em `desenv` mas ainda não haviam sido promovidos a `homolog` no momento da validação (foram criados após o merge do PR #113). Isso afeta somente `estado.md` (documentação), não código de aplicação — confirmado via diff completo de `desenv`→`homolog`. Recomenda-se ao LT sincronizar essa documentação ao abrir o PR de release.

## Testes automatizados
- Backend (`dotnet test`): **290/290 passando**.
- Frontend (`ng test --watch=false --browsers=ChromeHeadless`): **105/105 passando**.
- Cobertura frontend (`--code-coverage`): Statements 92.43%, **Branches 81.6%** (>= 80%), Functions 92.3%, Lines 92.28%.
- `tsc --noEmit -p tsconfig.app.json`: sem erros (CA-T1).
- `npm run test:visual`: **não existe** em `dashboard/package.json` → E2E/screenshots: N/A (projeto sem Playwright configurado nesta issue, decisão de escopo confirmada no Gate 1/CA-T2).

## Validação integrada (Docker Compose real)
`docker compose up -d --build db api dashboard` a partir de `homolog` — subida sem erro. Todas as 7 rotas do dashboard (`/`, `/login`, `/products`, `/queue`, `/settings`, `/jobs`, `/facebook-manual`, `/reports`) responderam HTTP 200.

Fluxos ponta-a-ponta exercidos via curl/JWT/psql com dados de teste inseridos diretamente no banco:
- Login válido (200 + JWT) e inválido (401).
- Listagem de produtos com `ai_score`/`ai_reason` presentes; aprovar (`status:pending`) e rejeitar (`status:rejected`) via PATCH.
- Fila com os 4 status (Scheduled/Published/Failed/ManualPending); retry de item falho (Failed → Scheduled).
- Mascaramento de Settings: PUT com valor real seguido de GET confirmando mascaramento (`****************a1b2`).
- Disparo de job manual (`processor/trigger` → 200).
- Facebook Manual: item ManualPending listado com dados do produto (mídia/legenda), marcado como publicado via PATCH, removido da lista de pendentes.
- Reports: totais hoje/semana/mês e resumo por rede refletindo a publicação de teste.

## Tabela de critérios de aceite
Ver seção completa "QA — homolog" em `{docs_path}/estado.md` (29/29 critérios CA-A1 a CA-T4 aprovados, com evidência individual por critério).

## Issues encontradas
Nenhum blocker funcional. Único apontamento é a nota não-bloqueante de sincronização de documentação `desenv`→`homolog` (ver acima), a ser resolvida pelo LT ao promover para `main`.

## Containers
`docker compose down -v` executado ao final. `.env` restaurado ao estado original.

## Conclusão
QA aprovado. Próxima etapa: Líder Técnico abre PR de release `homolog→main` (Gate 2 — aprovação do Gerente).
