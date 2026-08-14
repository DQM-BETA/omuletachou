# Especificação Técnica — ISSUE-154: CSS do site público + `test:visual` (Playwright)

## 1. Abordagem CSS: CSS global puro (não CSS Modules, não nova dependência)

`website/` já tem CSS Modules disponível nativamente no Next.js (`page.module.css`), mas **todos os componentes/páginas afetados usam classes BEM como strings literais** (`className="deal-card__title"`, `className="site-header__chip"` etc.), não `styles.dealCardTitle`. Migrar para CSS Modules exigiria reescrever `DealCard.tsx`, `Header.tsx`, `DealDetail.tsx` e as 3 páginas para importar e referenciar um objeto `styles` — mudança de estrutura de componente que **CA-T2 explicitamente proíbe** ("não há mudança em... apenas CSS, estrutura de estilos e o setup de `test:visual`").

**Decisão: manter as classes BEM como estão e implementar CSS global**, sem introduzir nenhuma dependência nova (sem Tailwind/styled-components/CSS-in-JS). Isso é o caminho mais simples e coerente com o que já existe.

### 1.1 Organização dos arquivos

`app/globals.css` é o único ponto de entrada processado pelo Next.js (importado 1x no root layout — ver seção 2). Para não virar um arquivo monolítico de ~300+ linhas cobrindo ~35 seletores BEM em 3 templates, organizar em partials via `@import` nativo de CSS (suportado pelo pipeline PostCSS do Next.js, sem lib adicional):

```
app/
  globals.css                 # entry point — só @imports, nesta ordem
  styles/
    tokens.css                 # custom properties (cores, espaçamento, tipografia, raio)
    reset.css                  # o reset básico já existente em globals.css, movido para cá
    layout.css                 # <main>, site-header, deals-grid, deals-empty, deals-pagination
    deal-card.css               # .deal-card__*
    deal-detail.css              # .deal-detail__*
```

`app/globals.css` fica reduzido a:
```css
@import "./styles/tokens.css";
@import "./styles/reset.css";
@import "./styles/layout.css";
@import "./styles/deal-card.css";
@import "./styles/deal-detail.css";
```

Se o Dev preferir um único arquivo por simplicidade (squad é pequena, ~35 seletores não é um volume enorme), tudo bem também — a divisão acima é recomendação de organização, não requisito rígido. O que é obrigatório: **tudo global, nenhuma classe local de CSS Module nos componentes existentes**.

`app/page.module.css` está **órfão hoje** (`app/page.tsx` não o importa — é boilerplate do `create-next-app` nunca customizado). Remover este arquivo (dead code) faz parte do escopo de limpeza desta issue.

### 1.2 Tokens de design (`styles/tokens.css`)

Definir como CSS custom properties em `:root`. Os **nomes/estrutura** dos tokens são definidos aqui pelo LT; os **valores exatos** (paleta neutra, escala tipográfica, espaçamento, raio de borda) vêm da spec visual do UX/UI (próximo agente, a partir do Figma) — exceto a cor primária, que já está fixada pelo Gate 1/manifest PWA:

```css
:root {
  --color-primary: #e63946;      /* fixo — já em app/layout.tsx (theme-color) e manifest.json (Issue #117) */
  --color-primary-dark: ...;      /* UX/UI: tom mais escuro derivado, p/ hover/active */
  --color-neutral-900: ...;       /* UX/UI: texto principal */
  --color-neutral-600: ...;       /* UX/UI: texto secundário */
  --color-neutral-200: ...;       /* UX/UI: bordas/divisores */
  --color-neutral-100: ...;       /* UX/UI: fundos sutis (cards, chips inativos) */
  --color-surface: #ffffff;

  --font-family-base: ...;        /* UX/UI: conforme design system do Figma */
  --font-size-sm / base / lg / xl / 2xl;   /* UX/UI: escala tipográfica */

  --space-1 .. --space-6;         /* UX/UI: escala de espaçamento (ex. 4/8/12/16/24/32px) */
  --radius-sm / md / lg;          /* UX/UI: raio de borda de cards/botões/chips */
}
```

O Dev implementa a estrutura acima; a UX/UI entrega os valores concretos (provavelmente em `{docs_path}/spec-visual-ux.md` ou anexo equivalente — path exato a critério do UX/UI) antes do Dev começar.

### 1.3 Inventário de classes a cobrir (base para CA-3)

Levantamento via `grep -rn "className=" app components` (excluindo `*.test.tsx`). 100% destas precisam de pelo menos 1 regra CSS:

**Layout / páginas (`app/page.tsx`, `app/categoria/[categoria]/page.tsx`):**
`deals-empty`, `deals-grid`, `deals-pagination`

**`Header.tsx`:**
`site-header`, `site-header__brand`, `site-header__filters`, `site-header__chip`, `site-header__chip--active`

**`DealCard.tsx`:**
`deal-card`, `deal-card__media`, `deal-card__image`, `deal-card__badge`, `deal-card__title`, `deal-card__price`, `deal-card__price-current`, `deal-card__price-strike`, `deal-card__cta`, `deal-card__cta--disabled`

**`DealDetail.tsx`:**
`deal-detail`, `deal-detail__media`, `deal-detail__image`, `deal-detail__info`, `deal-detail__title`, `deal-detail__category`, `deal-detail__price`, `deal-detail__price-current`, `deal-detail__price-strike`, `deal-detail__badge`, `deal-detail__cta`, `deal-detail__cta--disabled`, `deal-detail__related`, `deal-detail__related-grid`

Nota: modificadores como `--disabled`/`--active` combinam com a classe base no `className` (ex. `"deal-card__cta deal-card__cta--disabled"`) — o seletor CSS pode ser `.deal-card__cta--disabled` isolado (aplica-se sempre em conjunto com `.deal-card__cta`) ou `.deal-card__cta.deal-card__cta--disabled`, à escolha do Dev.

### 1.4 Estados a cobrir sem quebrar layout (casos de exceção do proposal.md)

- **Sem desconto** (`hasDiscount === false`): `deal-card__badge`/`deal-detail__badge` e `*__price-strike` não são renderizados — o CSS de `*__price`/`*__price-current` não pode depender da presença desses irmãos para o alinhamento ficar correto.
- **CTA indisponível** (`affiliateLink` ausente): renderiza `<span>` com `*__cta *__cta--disabled` em vez de `<a>` — precisa de estilo visualmente distinto (ex. opacidade reduzida, cursor `not-allowed`) mas mantendo a mesma caixa/dimensão do CTA ativo.
- **Estado vazio** (`deals-empty`): estilizado como bloco centralizado com mensagem, não texto solto (CA-7).
- **Imagem quebrada/placeholder** (já resolvido em `lib/format.ts` via `resolveDealImageUrl` — sempre retorna uma URL válida, incluindo placeholder): `deal-card__image`/`deal-detail__image` devem ter `object-fit: cover` + altura fixa/aspect-ratio para não colapsar o card quando a imagem é o placeholder.

## 2. Fix do bug raiz: importar `globals.css`

`app/layout.tsx` hoje **não importa nenhum CSS** — causa raiz do bug (#154). Adicionar no topo do arquivo:

```tsx
import './globals.css';
```

Único import necessário — `globals.css` puxa os demais partials via `@import` (se o Dev optar pela organização em `styles/`).

## 3. Responsividade mobile-first

Base (sem media query) = layout mobile. Breakpoints progressivos via `min-width` (nunca `max-width` como base):

- **Base (mobile, <640px):** `deals-grid` em 1 coluna (`grid-template-columns: 1fr`); `site-header__filters` com scroll horizontal ou wrap; CTAs/chips com área de toque mínima de 44x44px (WCAG/CA-11).
- **`@media (min-width: 640px)`** (tablet): `deals-grid` em 2 colunas.
- **`@media (min-width: 1024px)`** (desktop): `deals-grid` em 3–4 colunas; `deal-detail` pode ir de layout empilhado para layout em 2 colunas (mídia à esquerda, info à direita), a critério do UX/UI.

Alternativa aceitável: grid auto-responsivo sem breakpoints fixos (`grid-template-columns: repeat(auto-fill, minmax(160px, 1fr))`), que também satisfaz CA-9/CA-10 (sem overflow, se adapta ao viewport) — decisão final de granularidade fica com o Dev/UX-UI, desde que a base seja mobile e não haja overflow horizontal em 375px.

`html, body { max-width: 100vw; overflow-x: hidden; }` já existe em `globals.css` — manter.

## 4. Setup do Playwright (`test:visual`)

### 4.1 Dependência
`@playwright/test` não está em `devDependencies` hoje (aparece só transitivamente no lockfile). Adicionar:
```
npm install -D @playwright/test
npx playwright install chromium
```

### 4.2 Config — `website/playwright.config.ts`

Adaptado do padrão usado em `repos/dqm-digital-app/playwright.config.ts` (STAGING_URL first, webServer local como fallback), trocando Expo Web por Next.js:

```ts
import { defineConfig, devices } from '@playwright/test';

const STAGING_URL = process.env.STAGING_URL;
const LOCAL_URL = 'http://localhost:3000';
const BASE_URL = STAGING_URL ?? LOCAL_URL;

export default defineConfig({
  testDir: './e2e',
  outputDir: './screenshots',
  timeout: 60000,
  retries: 0,
  reporter: [['list'], ['html', { open: 'never', outputFolder: 'playwright-report' }]],
  use: {
    baseURL: BASE_URL,
    screenshot: 'only-on-failure',
    viewport: { width: 375, height: 812 }, // mobile-first (CA-14 pede viewport mobile)
  },
  projects: [
    { name: 'mobile-chromium', use: { ...devices['Pixel 7'] } },
  ],
  webServer: STAGING_URL
    ? undefined
    : {
        command: 'npm run dev',
        url: LOCAL_URL,
        reuseExistingServer: true,
        timeout: 60000,
      },
});
```

**Nota de infraestrutura de teste (dependência de dados reais):** diferente do `dqm-digital-app` (API .NET local sem dados de terceiros), aqui o catálogo de ofertas vem de scraping real (sem seed fixo — ver `backend/src/AfiliadoBot.Infrastructure/Migrations`, não há seed de `deals`). Rodar `test:visual` localmente exige a stack completa no ar com dados reais (`docker compose up -d db api website` ou equivalente) e `API_INTERNAL_URL`/rede acessível ao website. Em CI, o modo recomendado é `STAGING_URL` apontando para homolog (que já tem dados reais via o pipeline desenv→homolog). Isso é setup de ambiente, não infra transversal da squad — o Dev resolve inline (ver CLAUDE.md, fronteira DevOps).

### 4.3 Descoberta de slug/categoria reais (sem hardcode)

Como não há dado fixo de seed, os testes de categoria/detalhe **não devem hardcodar** um slug/categoria. Usar o `sitemap.xml` já existente (`app/sitemap.ts`, gera `/categoria/{categoria}` e `/oferta/{slug}` reais a partir do catálogo ativo) como fonte:

```ts
// e2e/helpers.ts
export async function getRealCategoriaAndSlug(baseURL: string) {
  const res = await fetch(`${baseURL}/sitemap.xml`);
  const xml = await res.text();
  const categoria = xml.match(/\/categoria\/([^<]+)</)?.[1];
  const slug = xml.match(/\/oferta\/([^<]+)</)?.[1];
  return { categoria, slug };
}
```

- Se `categoria`/`slug` existirem → navegar e capturar screenshot do estado "com dados" (CA-5/CA-6/CA-8).
- Se o catálogo estiver vazio (ambiente sem dados) → usar uma categoria inexistente fixa (ex. `/categoria/categoria-teste-e2e-vazia`) para cobrir o estado vazio estilizado (CA-7) — não faz sentido pular o teste, mas documentar no teste que a cobertura do estado "com dados" depende de haver catálogo.

### 4.4 Testes — `website/e2e/visual.spec.ts`

3 telas mínimas (CA-14), viewport mobile (config acima já fixa 375px via device Pixel 7):

```ts
import { test, expect } from '@playwright/test';
import { getRealCategoriaAndSlug } from './helpers';

test.describe('Visual — Site público', () => {
  test('Home', async ({ page, baseURL }) => {
    await page.goto('/');
    await page.waitForLoadState('networkidle');
    await page.screenshot({ path: 'screenshots/home.png', fullPage: true });
  });

  test('Categoria', async ({ page, baseURL }) => {
    const { categoria } = await getRealCategoriaAndSlug(baseURL!);
    await page.goto(categoria ? `/categoria/${categoria}` : '/categoria/categoria-teste-e2e-vazia');
    await page.waitForLoadState('networkidle');
    await page.screenshot({ path: 'screenshots/categoria.png', fullPage: true });
  });

  test('Detalhe de oferta', async ({ page, baseURL }) => {
    const { slug } = await getRealCategoriaAndSlug(baseURL!);
    test.skip(!slug, 'Nenhuma oferta ativa no catálogo — não há /oferta/{slug} navegável.');
    await page.goto(`/oferta/${slug}`);
    await page.waitForLoadState('networkidle');
    await page.screenshot({ path: 'screenshots/deal-detail.png', fullPage: true });
  });
});
```

### 4.5 `package.json`

```json
"scripts": {
  "test:visual": "playwright test"
}
```

### 4.6 `.gitignore`

Adicionar (mesmo padrão do `dqm-digital-app`):
```
/screenshots
/playwright-report
/test-results
```

## 5. Checklist de build/regressão (CA-T1)

Nenhuma mudança em `lib/api.ts`, rotas, lógica de fetch/ISR. `npm run build` deve continuar passando — CSS puro e `layout.tsx` (só o `import`) não afetam TypeScript. Rodar `npm run build` e `npm test` antes do PR.

## 6. Task breakdown — uma única sub-issue

CSS (globals + tokens + responsividade) e o setup do `test:visual` são a mesma mudança de projeto (`website/`), sem fronteira de PR ou teste independente entre si — mesmo raciocínio já usado na Issue #15 ("3 artefatos compartilham as mesmas variáveis"). Aqui: implementar o CSS e não ter `test:visual` funcionando deixaria a issue sem forma objetiva de provar CA-5..CA-11 (visual) — as duas partes só fazem sentido entregues juntas, no mesmo PR. **Uma sub-issue** (stack: nodejs).
