---
issue: 163
titulo: "chore: Substituir placeholder-deal.png (1x1px) por placeholder visual real"
etapa_atual: "QA (PR #165 desenv→homolog mergeado)"
rota: rapido
ultimo_agente: code-review
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
pr_release: ~
code_review_homolog_pr: 165
qa_status: ~
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

## Próximos passos
- QA

## Custo (ledger)
| # | Etapa | Agente | Modelo | Tokens | Tools | Tempo(s) |
|---|---|---|---|---|---|---|
| 1 | Preparação (Issue + estado.md) | Coordenador | Haiku | 18991 | 6 | 45s |
| 2 | Dev (SVG placeholder, PR #164) | Dev Node.js | Sonnet | 79709 | 64 | 1221s |
| 3 | LT — merge PR #164 + PR homologação #165 | Líder Técnico | Sonnet | 32843 | 9 | 50s |
| 4 | Code Review — validação PR #165 (build/boot/testes/visual, merge desenv→homolog) | Code Review | Sonnet | 84148 | 53 | 580s |

