---
issue: 163
titulo: "chore: Substituir placeholder-deal.png (1x1px) por placeholder visual real"
etapa_atual: "Aguardando Aprovação — Gate 2 (PR #166 homolog→main criado)"
rota: rapido
ultimo_agente: lt
openspec_change: ~
tech_stacks:
  - nodejs
repos:
  omuletachou: repos/omuletachou
repo_path: repos/omuletachou
docs_path: repos/omuletachou/documentacoes/ISSUE-163-placeholder-deal
openspec_path: ~
sub_issues: []
desenv_tasks_merged: []
sub_issues_frontend: {}
pr_homologacao: "165 (desenv -> homolog, merge commit af6810f, MERGEADO)"
pr_release: "166 (homolog -> main, aguardando Gate 2)"
code_review_homolog_pr: 165
qa_status: aprovado
figma_url: ~
blockers: nenhum
createdAt: "2026-08-14"
status_comment_id: ~
---

## Síntese
Achado incidental: placeholder-deal.png é 1x1px (68 bytes) desde Issue #94. Renderiza como bloco preto sólido quando não há media_url. Substituir por placeholder visual real (SVG ou PNG conforme avaliação do Dev).

## Implementação (Dev)
Solução escolhida: SVG (markup, sem ferramentas de raster; leve, nítido em qualquer resolução).
- `website/public/placeholder-deal.svg`: novo — ícone "imagem indisponível" (moldura + foto), cores da paleta neutra do design system (`documentacoes/ISSUE-154-site-sem-css/ux-ui-spec.md`: `--color-neutral-100` #f5f3f6 fundo, `--color-neutral-200` #e4e0e6 borda/ícone, `--color-neutral-400` #a29aa8 traço), sem cor de marca (estado vazio ≠ CTA).
- `website/public/placeholder-deal.png`: removido (stub órfão de 1x1px).
- `website/lib/format.ts`: `resolveDealImageUrl` retorna `/placeholder-deal.svg`.
- `website/components/DealCard.test.tsx` (CA-A7): assertiva atualizada para `.svg`.
- Gate de testes: grep confirmou `DealCard.test.tsx` e `format.ts` como únicas referências ao asset no código — nenhum outro teste ficou desalinhado.

### Validação
- `npm test`: 80/80 passando (14 suites).
- `npm run build`: sucesso.
- Docker: stack `db+api+website` subida em worktree isolado (container_names/portas dedicados via override local, para não colidir com stack pré-existente `afiliado_*` do repo principal — parada e restaurada ao estado original ao final). Produto inserido via SQL direto (`media_url`/`media_local_path` NULL); API confirmou `mediaUrl: null`; Home e `/oferta/{slug}` servem `<img src="/placeholder-deal.svg">` (200, `image/svg+xml`); renderização confirmada como ícone visual (não bloco preto). `docker compose down -v` ao final — ambiente limpo, sem containers/volumes/networks residuais do teste.
- Worktree removido; `repo_path` em `desenv`.

PR: https://github.com/DQM-BETA/omuletachou/pull/164 (`chore/ISSUE-163-placeholder-real` → `desenv`) — MERGED (squash) por LT.

## Merge (Líder Técnico)
- Diff do PR #164 revisado: escopo confere (novo `.svg`, remoção do `.png` 1x1px, `lib/format.ts` e `DealCard.test.tsx` atualizados, sem mudança de lógica de negócio).
- `gh pr merge 164 --squash --delete-branch`: merge confirmado (`mergedAt: 2026-08-14T17:32:51Z`), branch remota `chore/ISSUE-163-placeholder-real` deletada.
- PR de homologação criado: https://github.com/DQM-BETA/omuletachou/pull/165 (`desenv` → `homolog`, merge commit).
- `repo_path` checked out em `desenv`, atualizado (fast-forward).

## Code Review — PR #165

Segunda camada de gate (validação ao vivo, execução real). `/code-review` (plugin Anthropic) já havia rodado no PR sem achados: https://github.com/DQM-BETA/omuletachou/pull/165#issuecomment-5296317817.

**Execução realizada:**

1. `git fetch && git checkout desenv && git pull origin desenv` — já em `desenv`, `9dff926` (up to date).
2. `gh pr diff 165` revisado: escopo confere ao esperado — `website/public/placeholder-deal.png` (deletado), `website/public/placeholder-deal.svg` (novo, 13 linhas de markup), `website/lib/format.ts` (`resolveDealImageUrl` → `.svg`), `website/components/DealCard.test.tsx` (assertiva `.svg`), `documentacoes/ISSUE-163-placeholder-deal/estado.md` (novo). Diff também trouxe arquivos de `documentacoes/ISSUE-154-site-sem-css/*` (docs/ledger/screenshots) — confirmado via `git merge-base`/`git log` que é drift de documentação já mesclada em `main` via PR #162, não código funcional; sem risco.
3. `npm test` (`website/`): **80/80 passando** (14 test suites).
4. `npm run build` (`website/`): `✓ Compiled successfully`, sem erros de tipo, 5 rotas geradas.
5. **Stack Docker real (isolada)**: como já havia uma stack persistente `afiliado_*` (projeto `omuletachou-local`) rodando no host com código antigo (ainda com o `.png` de 68 bytes) nas portas 3000/8080, subi uma stack **isolada** via override ad-hoc (`docker-compose.crvalidation.yml`, descartável, removido ao final) com nomes/portas/rede/volumes próprios (`cr165_db`, `cr165_api:18080`, `cr165_website:13000`, rede `cr165_net`) para não colidir — `docker compose -f docker-compose.yml -f docker-compose.crvalidation.yml up -d --build db api website`: build completo, `cr165_db` healthy, `cr165_api` healthy, `cr165_website` up.
6. Produto inserido via SQL direto (`INSERT INTO products ...`, schema conferido via `\d products`) com `media_url`/`media_local_path`/`image_url` NULL (`slug=produto-teste-cr165`, `status=2` Published, `platform=0` Amazon).
7. `curl http://localhost:18080/api/public/deals` confirmou o produto real com `"mediaUrl":null,"mediaLocalPath":null`.
8. `curl -o /dev/null -w "status=%{http_code} content-type=%{content_type}" http://localhost:13000/placeholder-deal.svg` → **`status=200 content-type=image/svg+xml`**.
9. `curl http://localhost:13000/` e `curl http://localhost:13000/oferta/produto-teste-cr165` — ambos referenciam `placeholder-deal.svg` no HTML servido (confirmado via grep).
10. **Confirmação visual real**: screenshot via Playwright (Chromium headless) do SVG isolado e da página `/oferta/produto-teste-cr165` renderizada — ícone "imagem indisponível" (moldura + foto/montanha + sol) em tons neutros, claramente distinto de um bloco preto sólido. Confirmado visualmente (não apenas por leitura de código).
11. Checklist de veto:
    - **Sem segredos commitados**: grep no diff do PR por `password|secret|api[_-]?key|token` só retorna ocorrências em texto de documentação (relato de outras rodadas de QA/CR), não em código/config; `.env`/`docker-compose.override.yml` locais nunca staged.
    - **Conformidade com `repos/omuletachou/CLAUDE.md`**: commit `chore(ISSUE-163): substitui placeholder-deal.png (1x1px) por SVG visual real (#164)` segue convenção; merge feature→desenv foi squash (PR #164); merge desenv→homolog foi merge commit (nunca squash, PR #165).
    - **Integração real**: banco Postgres real, API .NET real, website Next.js real servindo o asset estático — nenhum mock no caminho testado.
    - **Sem teste-lixo**: alteração em `DealCard.test.tsx` é assertiva legítima (`.png` → `.svg`), não trivial/vazia.
    - **`.first()`/`.nth()`/`.last()`**: grep em `website/e2e/` não retornou nenhuma ocorrência — sem veto aplicável (PR não toca specs E2E).
    - **Diff mínimo e contido**: exatamente o escopo da Issue (asset + 2 referências de código), sem mudança de lógica de negócio.
12. Limpeza: produto de teste removido do banco (`DELETE FROM products WHERE external_id='ext-cr165-001'`), `docker compose -f docker-compose.yml -f docker-compose.crvalidation.yml down -v` (containers/volumes/rede `cr165_*` removidos, incluindo rede órfã `omuletachou_omuletachou_net` de uma tentativa inicial que colidiu com a stack persistente), imagens `omuletachou-website`/`omuletachou-api` locais removidas (`docker rmi`), arquivos ad-hoc (`docker-compose.crvalidation.yml`, script de screenshot) apagados. Stack persistente `afiliado_*` (`omuletachou-local`) confirmada intacta e não tocada. `git status --short` limpo (exceto `docker-compose.override.yml`, arquivo pré-existente não criado por este Code Review).

**Veredito: aprovado.** Merge executado: `gh pr merge 165 --repo DQM-BETA/omuletachou --merge` (merge commit `af6810fcaa89279b041173d6b848ef159bb35627`, `desenv` → `homolog`, `mergedAt: 2026-08-14T17:42:44Z`). `repo_path` deixado checked out em `desenv`.

## QA — homolog

Validação em `homolog` (commit `af6810fcaa89279b041173d6b848ef159bb35627`, confirmado presente via `git log` após `git fetch && git checkout homolog && git pull`).

**Execução:**

1. `npm test` (`website/`): **80/80 passando** (14 suites) — sem regressão.
2. `npm run build` (`website/`): `✓ Compiled successfully`, type-check interno do Next OK, 5 rotas geradas sem erros.
3. **Stack Docker isolada a partir de `homolog`**: já havia stack persistente `afiliado_*` (portas 3000/8080) rodando no host com código antigo — subida stack isolada via override ad-hoc (`docker-compose.qa163.yml`, descartável, removido ao final), projeto Compose `qa163` (containers `qa163_db`/`qa163_api:18081`/`qa163_website:13001`, rede/volumes próprios): `docker compose -p qa163 -f docker-compose.yml -f docker-compose.qa163.yml up -d --build db api website` — build completo, `qa163_db` healthy, `qa163_api` healthy, `qa163_website` up.
4. Populados 2 produtos via SQL direto na tabela `products`: `produto-qa163-sem-midia` (`media_url`/`media_local_path` NULL, `status=2` Published) e `produto-qa163-com-midia` (`media_url=https://picsum.photos/id/237/300/200.jpg`).
5. `curl http://localhost:18081/api/public/deals` confirmou `mediaUrl: null, mediaLocalPath: null` para o produto sem mídia e `mediaUrl` preenchida para o produto com mídia.
6. `curl -o /dev/null -w "status=%{http_code} content-type=%{content_type}" http://localhost:13001/placeholder-deal.svg` → **`status=200 content-type=image/svg+xml`**.
7. `curl` em `/` (Home) e `/oferta/produto-qa163-sem-midia` confirmaram `src="/placeholder-deal.svg"` no HTML servido; `curl` em `/oferta/produto-qa163-com-midia` confirmou `src="https://picsum.photos/id/237/300/200.jpg"` (produto COM mídia não usa o fallback).
8. **E2E Playwright** (`test:visual` existe no `package.json` → obrigatório rodar): `STAGING_URL=http://localhost:13001 SCREENSHOTS_DIR={docs_path}/screenshots npm run test:visual` — **3/3 passando** (Home, Categoria, Detalhe de oferta). Screenshots arquivados em `documentacoes/ISSUE-163-placeholder-deal/screenshots/` (`home.png`, `categoria.png`, `deal-detail.png`).
9. **Gate visual (inspeção real das screenshots):**
   - `categoria.png` e `deal-detail.png`: card "Produto QA163 sem midia" exibe o ícone SVG "imagem indisponível" (moldura + montanha + sol, tons neutros) — **confirmado que não é mais um bloco preto sólido** (CA-4).
   - `deal-detail.png` e `categoria.png`: card "Produto QA163 com midia" exibe a foto real (cachorro preto) normalmente — **sem regressão em produtos COM mídia** (CA-5).
   - `home.png`: o card "sem mídia" mostra o ícone corretamente; o card "com mídia" aparece sem imagem carregada nesse print específico — investigado: artefato de timing do `loading="lazy"` do `<img>` + `waitForLoadState('networkidle')` naquela captura específica (o HTML confirmado via curl já tinha o `src` correto, e os outros dois screenshots — que renderizam o mesmo card — carregaram a foto normalmente). Não é regressão de código; não bloqueia o critério.
   - Header (`site-header`) aparece exatamente 1x em cada screenshot, sem duplicação. Sem footer capturado (fora do escopo desta issue, viewport mobile não rolou até o fim — não é critério desta mudança).
10. Limpeza: produtos de teste removidos (`DELETE FROM products WHERE external_id IN (...)`), `docker compose -p qa163 -f docker-compose.yml -f docker-compose.qa163.yml down -v` (containers/volumes/rede `qa163_*` removidos), imagens `qa163-api`/`qa163-website` removidas (`docker rmi`), `docker-compose.qa163.yml` apagado. Stack persistente `afiliado_*` confirmada intacta. `repo_path` deixado checked out em `desenv` (atualizado).

**Critérios de aceite — resultado:**

| # | Critério | Resultado | Evidência |
|---|---|---|---|
| 1 | `npm test` sem regressão | ✅ | 80/80 passando |
| 2 | `npm run build` sem erros | ✅ | Compiled successfully, 5 rotas |
| 3 | Produto sem mídia renderiza `/placeholder-deal.svg` (200, `image/svg+xml`) na Home e em `/oferta/{slug}` | ✅ | curl + HTML grep, ambas as rotas |
| 4 | Ícone real (moldura + foto), não bloco preto | ✅ | `categoria.png`, `deal-detail.png` |
| 5 | Produtos COM mídia continuam mostrando a imagem real | ✅ | `categoria.png`, `deal-detail.png` (foto real do cachorro) |
| — | E2E/screenshots | Rodado (projeto COM UI) — 3/3 passando, arquivados em `documentacoes/ISSUE-163-placeholder-deal/screenshots/` |

**Veredito: aprovado.** Todos os 5 critérios objetivos passaram com evidência de execução real (Docker isolado, banco Postgres real, API .NET real, Next.js real).

## Release (Líder Técnico)
- `git fetch && git checkout homolog && git pull origin homolog`: HEAD confirmado em `af6810fcaa89279b041173d6b848ef159bb35627` (merge commit do PR #165), working tree limpo (exceto `docker-compose.override.yml`, pré-existente, não relacionado).
- PR de release criado: https://github.com/DQM-BETA/omuletachou/pull/166 (`homolog` → `main`), descrevendo escopo, pipeline (Dev #164, Code Review/QA #165) e os 5 critérios de aceite aprovados. **Não mergeado** — aguardando Gate 2 (Gerente).
- `repo_path` checked out de volta em `desenv`, atualizado (`git pull origin desenv`).

## Próximos passos
- **Gate 2 (Gerente)**: aprovar merge do PR #166 (`homolog` → `main`, merge commit — nunca squash).
- Após aprovação: Coordenador executa `gh pr merge 166 --merge` e fecha a Issue #163. Card permanece em "Em Desenvolvimento" até o Gerente arrastar manualmente para "Concluído".

## Custo (ledger)
| # | Etapa | Agente | Modelo | Tokens | Tools | Tempo(s) |
|---|---|---|---|---|---|---|
| 1 | Preparação (Issue + estado.md) | Coordenador | Haiku | 18991 | 6 | 45s |
| 2 | Dev (SVG placeholder, PR #164) | Dev Node.js | Sonnet | 79709 | 64 | 1221s |
| 3 | LT — merge PR #164 + PR homologação #165 | Líder Técnico | Sonnet | 32843 | 9 | 50s |
| 4 | Code Review — validação PR #165 (build/boot/testes/visual, merge desenv→homolog) | Code Review | Sonnet | 84148 | 53 | 580s |
| 5 | QA — validação homolog (build/testes/E2E/visual, 5 critérios) | QA | Sonnet | 71605 | 52 | 484s |
| 6 | LT — PR release #166 (homolog→main) | Líder Técnico | Sonnet | 42416 | 7 | 89s |
