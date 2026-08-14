# Relatório de QA — ISSUE-154: Estilização CSS do site público

**Status: REPROVADO**

Branch validada: `homolog` (commit `6e65564d8e4172c5d437af2bb99e00245ee26424`, PR #158 `desenv`→`homolog`, mergeado).

---

## 1. Testes automatizados (Jest)

```
npm test  (website/)
Test Suites: 14 passed, 14 total
Tests:       79 passed, 79 total
Time:        2.616s
```
Sem regressão — 79/79 conforme esperado.

## 2. Build / TypeScript (CA-T1)

`npm run build` executado dentro do build Docker (`docker compose build website`): `✓ Compiled successfully`, `Linting and checking validity of types...` sem erros. CA-T1 **PASS**.

Nota à parte (não bloqueante, fora de escopo): `npx tsc --noEmit` direto no host acusa erros em arquivos `*.test.tsx` (`toBeInTheDocument`/`toHaveAttribute` não reconhecidos — `tsconfig.json` não lista `@testing-library/jest-dom` em `compilerOptions.types`). Confirmado via `git log`/`git show 468f633` que `tsconfig.json` **não foi tocado** por este PR — é uma pré-existência de configuração (desde issues anteriores), e o gate oficial da issue (`npm run build`) passa limpo. Registrado como sugestão de melhoria futura, não é motivo de reprovação aqui (CA-T2 proíbe inclusive mexer fora do escopo).

## 3. Validação integrada — stack Docker real (obrigatória, d3)

Subida via `docker compose up -d --build db api website` a partir de `homolog`, com `.env` e `docker-compose.override.yml` (expondo portas 8080/3000) locais descartáveis, removidos ao final. Catálogo vazio no ambiente local → 5 produtos seedados via SQL direto na tabela `products` (categoria "Eletronicos" uma palavra só, para não esbarrar no bug conhecido #159; casos: com desconto, sem desconto, sem `affiliate_link`/CTA desabilitado, categoria alternativa "Casa").

- `curl http://localhost:3000/` confirmado servindo HTML com `<link rel="stylesheet" href="/_next/static/css/....css">`.
- CSS baixado via curl: 11032 bytes, contém `:root{--color-primary:#e63946;...}` — confirma CA-4 (CSS efetivamente importado/aplicado, não órfão).
- `curl http://localhost:8080/api/public/deals` confirmado retornando os produtos seedados.
- Stack subiu sem erros; todos os containers `healthy`. Ambiente removido ao final (`docker compose down -v`, `.env`/`docker-compose.override.yml` apagados) — sem resíduo.

## 4. Gate Visual obrigatório (d2) — `npm run test:visual`

**Primeira execução real do Gate Visual para `website`** (script `test:visual` existe desde o PR desta issue — CA-13/CA-15). Rodado com:
```
STAGING_URL=http://localhost:3000 SCREENSHOTS_DIR={docs_path}/screenshots-qa npm run test:visual
```
Resultado: **3/3 passed** (Home, Categoria, `deal-detail`), PNGs gerados em `documentacoes/ISSUE-154-site-sem-css/screenshots-qa/` (`home.png`, `categoria.png`, `deal-detail.png`). Um 4º screenshot ad-hoc (`categoria-vazia-adhoc.png`) capturado manualmente para validar CA-7 (estado vazio) com uma categoria inexistente, já que a execução oficial usou uma categoria real (havia produtos no catálogo seedado).

### Inspeção visual — achado crítico

- **Home (`home.png`)**: header (`site-header`) visível **1x**, sem duplicação. Grid de cards estilizado, badges `%OFF` vermelhos, preço riscado/atual, CTA vermelho, CTA desabilitado (cinza, "Indisponível" — produto sem `affiliate_link`), paginação "Página 1 de 1". Layout condiz com `ux-ui-spec.md` (cor `#e63946`, tipografia, espaçamento, cards com sombra/raio). **OK.**
- **Categoria (`categoria.png`)**: header visível **1x**. Mesmo grid/card da Home (CA-6). Título da categoria. **OK.**
- **Categoria vazia (`categoria-vazia-adhoc.png`)**: header visível **1x**. Bloco `deals-empty` estilizado (borda tracejada, ícone, mensagem "Nenhuma oferta encontrada nesta categoria.", link "Ver todas as ofertas"). CA-7 **OK.**
- **`deal-detail.png` — FALHA:** **o header (`site-header`) está completamente ausente** — não aparece nem 1x. Confirmado que não é falha de scroll/screenshot: `curl http://localhost:3000/oferta/fone-bluetooth-xpto-pro | grep -c "site-header"` → **0 ocorrências** no HTML renderizado. Inspecionado o código-fonte: `website/app/oferta/[slug]/page.tsx` não importa nem renderiza `<Header />` (diferente de `app/page.tsx` e `app/categoria/[categoria]/page.tsx`, que importam explicitamente), e `app/layout.tsx` (root layout) também não tem header global — cada página é responsável por renderizar o próprio `<Header />`, e a página de detalhe de oferta simplesmente nunca o fez.
  - **Não é regressão introduzida por esta issue**: `git log --oneline -- website/app/oferta/[slug]/page.tsx` mostra que o arquivo não foi tocado desde `84a24c8 feat(ISSUE-95)` (a página de detalhe nunca teve header). O CSS entregue nesta issue está correto — o bug é estrutural/JSX, pré-existente, só agora detectado porque **esta é a primeira vez que o Gate Visual do QA de fato executa** para `website` (conforme contexto da própria issue, CA-15).
  - Restante da tela `deal-detail` está corretamente estilizado: mídia em destaque, preço grande (`R$ 149,90` vs `R$ 299,90` riscado), badge `-50%`, CTA "Comprar agora →" proeminente com sombra, seção "Mais ofertas" em grid reaproveitando `.deal-card` (incluindo o card com CTA desabilitado "Indisponível").

**Consequência para os critérios de aceite:**
- **CA-1 (cor de marca consistente nas 3 telas, incl. "algum elemento de destaque do header"): FALHA** — na tela `deal-detail` não há nenhum elemento de header, logo o critério não é atendido nesta tela.
- **CA-8 (deal-detail estilizada): FALHA** — mídia/preço/badge/CTA/relacionados estão OK isoladamente, mas a tela como página completa está incompleta (sem navegação/marca), o que também viola o Gate Visual obrigatório do próprio QA (`Header visível exatamente 1x em cada tela`).

Dark mode: fora de escopo por decisão explícita documentada em `ux-ui-spec.md` §0 (Figma sem tokens semânticos definidos) — não avaliado, não é reprovação.

## 5. Tabela de critérios de aceite

| Critério | Resultado | Evidência |
|---|---|---|
| CA-1 — cor de marca consistente nas 3 telas | **FALHA** | `deal-detail.png` sem nenhum header — "elemento de destaque do header" ausente nesta tela |
| CA-2 — paleta/tipografia alinhadas ao design system | PASS | Work Sans + tokens documentados em `ux-ui-spec.md`, confirmados no CSS servido |
| CA-3 — 100% das classes BEM com regra CSS | PASS | Verificação programática do Dev (PR #157) + inspeção visual amostral confirma aplicação |
| CA-4 — CSS importado/aplicado no build | PASS | curl mostra `<link rel="stylesheet">` + conteúdo do CSS com tokens (`--color-primary:#e63946`) |
| CA-5 — Home estilizada | PASS | `home.png` |
| CA-6 — Categoria reaproveita estilo da Home | PASS | `categoria.png` |
| CA-7 — Estado vazio de categoria estilizado | PASS | `categoria-vazia-adhoc.png` |
| CA-8 — `deal-detail` estilizada | **FALHA** | `deal-detail.png` — header ausente (ver achado acima); demais elementos OK |
| CA-9 — sem overflow horizontal (mobile 375px) | PASS | Assert automatizado nas 3 páginas em `e2e/visual.spec.ts` (scrollWidth ≤ clientWidth), 3/3 passed |
| CA-10 — grid responsivo mobile-first | PASS | 1 coluna mobile confirmada (`home.png`/`categoria.png`); breakpoints `min-width` 640/1024/1280px no CSS entregue |
| CA-11 — área de toque adequada | PASS | Chips/CTAs com dimensões visíveis adequadas nos screenshots (min-height 40-52px conforme spec) |
| CA-12 — sem conflito de cor com manifest PWA | PASS | Única cor de marca usada é `#e63946` e derivações — nenhuma cor concorrente |
| CA-13 — `test:visual` existe e roda sem erro de config | PASS | `npm run test:visual` executou, 3/3 passed |
| CA-14 — cobertura mínima de 3 telas em screenshot | PASS | `home.png`, `categoria.png`, `deal-detail.png` gerados |
| CA-15 — Gate Visual do QA deixa de resolver N/A | PASS | Este próprio relatório é a evidência — Gate executou de verdade e **encontrou um bug real** (prova que o gate funciona) |
| CA-T1 — build sem erros de TS | PASS | Build Docker: "✓ Compiled successfully", type check OK |
| CA-T2 — nenhuma mudança de dados/rotas/API | PASS | Confirmado no diff revisado pelo LT (PR #157) — apenas CSS + setup Playwright |
| CA-T3 — nenhuma config de deploy alterada | PASS | Nenhum arquivo de produção alterado; `.env`/`docker-compose.override.yml` usados no QA são locais, descartáveis, não commitados |

**Resultado: 16/18 critérios PASS (13/15 dos CA-1..CA-15 + 3/3 transversais T1-T3) — 2 critérios reprovados (CA-1, CA-8), ambos pela mesma causa raiz (header ausente em `deal-detail`). QA exige 100% dos critérios — REPROVADO.**

## 6. Issue funcional identificada

**Header (`<Header />`) completamente ausente na página de detalhe de oferta (`/oferta/{slug}`)**, `website/app/oferta/[slug]/page.tsx`. Bug pré-existente (desde Issue #95), não introduzido pelo PR #157/#158 desta issue, mas bloqueante para a aprovação porque:
1. Viola CA-1 e CA-8 (usuário não tem nenhuma navegação/marca na página que provavelmente concentra o maior tráfego do site, a página de conversão de afiliado).
2. Viola o Gate Visual obrigatório do próprio processo de QA (regra d2: "Header visível exatamente 1x em cada tela").

**Sugestão de correção** (para o LT avaliar escopo/sub-issue): importar e renderizar `<Header />` em `app/oferta/[slug]/page.tsx`, análogo ao que já é feito em `app/page.tsx` e `app/categoria/[categoria]/page.tsx`. É uma mudança pequena e não deveria exigir novo ciclo de UX/UI (reaproveita componente e CSS já existentes/estilizados nesta mesma issue).

## 7. Ambiente

- Stack Docker (`db`, `api`, `website`) subida a partir de `homolog`, catálogo seedado via SQL, removida ao final (`docker compose down -v`).
- `.env` e `docker-compose.override.yml` locais descartáveis, removidos — sem resíduo no worktree.
- `repo_path` deixado em `desenv` ao final (conforme instrução).
