# UX/UI Spec — ISSUE-154: CSS do site público "O Mulet Achou"

> Spec visual para implementação em CSS puro (`website/app/globals.css` + partials), a partir da estrutura definida em `especificacao-tecnica.md` (LT). Cobre as 3 telas: Home, Categoria, Detalhe de oferta (`deal-detail`).

## 0. Nota sobre a fonte do design system (Figma)

Consultado `get_figma_data` no arquivo do design system da squad (`yi6YkNAy9HfHus2oiPi3G7`). **Achado:** o arquivo está no estado padrão de boas-vindas do Figma ("Start here ↓", "Build your own team library" etc.) — nunca foi customizado pelo time com paleta/tokens reais do produto. Os únicos artefatos nomeados reaproveitáveis são os **estilos de texto**:

| Estilo Figma | Font family | Peso | Tamanho |
|---|---|---|---|
| `Header 1` (node `1:87`) | Work Sans | Bold (700) | 34px |
| `Header 2` (node `1:88`) | Work Sans | Bold (700) | 20px |
| `Body` (node `1:89`) | Work Sans | Regular (400) | 13px |

As cores nomeadas (`Fuschia/*`, `Iris/*`) são o par de cores placeholder do tutorial padrão do Figma — **não usadas aqui**, pois conflitam com a decisão do Gate 1 (âncora `#e63946`). Consequência prática para CA-2: **tipografia** (família Work Sans) tem rastreabilidade direta ao Figma da squad; **paleta, espaçamento e raio** foram definidos por mim seguindo boas práticas de design de e-commerce/ofertas, ancorados na cor de marca fixada pelo Gerente, já que o Figma não tem esses tokens customizados. Isso é consistente com o texto do próprio CA-2 ("é o design system genérico"). Dark mode: o Figma não tem nenhum token semântico (claro/escuro) definido — não há nada a registrar nem como nice-to-have; fica de fora do escopo.

Direção estética escolhida: site de ofertas/cupons precisa comunicar "urgência de desconto" sem virar poluição visual — vermelho de marca concentrado em 3 pontos de alta atenção (badge de desconto, preço atual, CTA) sobre uma base neutra quase-branca com leve tom quente (não cinza puro, foge do "cinza de template"), cards com sombra sutil e cantos arredondados médios (não pill genérico), tipografia Work Sans (geométrica, legível em telas pequenas).

---

## 1. Tokens CSS (`app/styles/tokens.css`)

Bloco pronto para uso — nomes seguem a estrutura definida pelo LT em `especificacao-tecnica.md` §1.2, com extensões (`-light`/`-darker`, `xs`/`3xl`, `--space-7`, sombras) documentadas inline.

```css
:root {
  /* Cor de marca — fixa, já em app/layout.tsx (theme-color) e manifest.json (Issue #117). NÃO alterar. */
  --color-primary: #e63946;
  --color-primary-dark: #c41e2c;    /* hover/active de CTA e chip ativo — L reduzida ~12% em HSL, mesma hue/sat */
  --color-primary-darker: #a11723;  /* pressed/active state, uso pontual */
  --color-primary-light: #fce4e6;   /* tint pálido — fundo sutil de destaque, NÃO usar como fundo de texto corrido */
  --color-primary-contrast: #ffffff; /* texto sobre fundo --color-primary */

  /* Neutros — leve tom quente (não cinza puro), harmoniza com o vermelho de marca */
  --color-neutral-900: #1a1523;  /* texto principal (títulos, preço) */
  --color-neutral-700: #4a4453;  /* texto secundário */
  --color-neutral-600: #6b6473;  /* texto terciário/meta (categoria, legendas) */
  --color-neutral-400: #a29aa8;  /* texto desabilitado, preço riscado */
  --color-neutral-200: #e4e0e6;  /* bordas, divisores */
  --color-neutral-100: #f5f3f6;  /* fundo sutil (chip inativo, placeholder de imagem) */
  --color-neutral-50:  #fbfafc;  /* fundo de página */
  --color-surface: #ffffff;      /* fundo de card/superfície elevada */
  --color-border: var(--color-neutral-200);

  /* Tipografia — família extraída do Figma (Work Sans); fallback de sistema garante mobile-first sem custo de rede se o Dev optar por não carregar a webfont */
  --font-family-base: 'Work Sans', -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
  --font-size-xs: 12px;    /* badges, legendas de paginação */
  --font-size-sm: 14px;    /* texto secundário, preço riscado, chips, categoria */
  --font-size-base: 16px;  /* corpo de texto padrão */
  --font-size-lg: 18px;    /* preço em destaque no card, subtítulos */
  --font-size-xl: 22px;    /* título do deal-detail (mobile) */
  --font-size-2xl: 28px;   /* título do deal-detail (desktop) */
  --font-size-3xl: 32px;   /* preço em destaque do deal-detail */
  --line-height-tight: 1.2;
  --line-height-base: 1.5;
  --font-weight-regular: 400;
  --font-weight-semibold: 600;
  --font-weight-bold: 700;
  --font-weight-black: 800;

  /* Espaçamento — escala 8pt */
  --space-1: 4px;
  --space-2: 8px;
  --space-3: 12px;
  --space-4: 16px;
  --space-5: 24px;
  --space-6: 32px;
  --space-7: 48px;

  /* Raio de borda */
  --radius-sm: 6px;   /* badges, chips, botões de paginação */
  --radius-md: 10px;  /* cards, CTA */
  --radius-lg: 16px;  /* mídia de destaque do deal-detail */
  --radius-full: 999px;

  /* Elevação — sombra sutil, evitar exagero */
  --shadow-sm: 0 1px 2px rgba(26, 21, 35, 0.06);
  --shadow-md: 0 4px 12px rgba(26, 21, 35, 0.10);

  /* Container */
  --container-max-width: 1200px;
}
```

**Breakpoints (mobile-first, `min-width`, aplicar literalmente nas media queries — não são custom properties usáveis dentro de `@media`):**
- Base: `< 640px` (mobile)
- `@media (min-width: 640px)`: tablet
- `@media (min-width: 1024px)`: desktop
- `@media (min-width: 1280px)`: desktop largo (opcional, só para `deals-grid` em 4 colunas)

**Contraste/acessibilidade:** `--color-primary` (#e63946) sobre `--color-surface` (branco) atende AA para texto grande (≥18px bold ou ≥24px regular) e para uso como fundo sólido com texto branco (`--color-primary-contrast`). Não usar `--color-primary` como cor de texto em `font-size-sm`/`xs` sobre fundo branco (contraste insuficiente para texto pequeno) — nesses casos usar `--color-primary-dark`.

**Foco de teclado (global, obrigatório — heurística "visibilidade do status do sistema" + navegação por teclado):**
```css
:focus-visible {
  outline: 2px solid var(--color-primary);
  outline-offset: 2px;
  border-radius: var(--radius-sm);
}
```

---

## 2. Estilos base (`app/styles/reset.css` + regras globais em `layout.css`)

```css
html, body {
  max-width: 100vw;
  overflow-x: hidden; /* já existente — manter, é o que garante CA-9 */
}

body {
  background: var(--color-neutral-50);
  color: var(--color-neutral-900);
  font-family: var(--font-family-base);
  font-size: var(--font-size-base);
  line-height: var(--line-height-base);
  -webkit-font-smoothing: antialiased;
}

a { color: inherit; text-decoration: none; }

.container {
  width: 100%;
  max-width: var(--container-max-width);
  margin-inline: auto;
  padding-inline: var(--space-4); /* 16px mobile */
}
@media (min-width: 1024px) {
  .container { padding-inline: var(--space-6); } /* 32px desktop */
}
```

`<main>` das 3 páginas deve usar `.container` (ou equivalente já existente) para centralizar/limitar largura — se a estrutura atual não tiver essa classe, aplicar o mesmo `max-width`/`padding-inline` diretamente em `deals-grid`, `deals-pagination`, `deal-detail`.

---

## 3. Mapeamento de componentes por classe BEM

### 3.1 Header (`site-header`)

| Classe | Regra visual |
|---|---|
| `.site-header` | `position: sticky; top: 0; z-index: 50;` `display: flex; align-items: center; justify-content: space-between; gap: var(--space-3);` `height: 56px;` (`64px` em `@media (min-width:1024px)`) `padding-inline: var(--space-4)` (`var(--space-6)` desktop) `background: var(--color-surface); border-bottom: 1px solid var(--color-border);` |
| `.site-header__brand` | `display: flex; align-items: center; gap: var(--space-2);` `font-size: var(--font-size-lg); font-weight: var(--font-weight-bold); color: var(--color-primary); letter-spacing: -0.01em; white-space: nowrap;` — wordmark "O Mulet Achou". Estado: link para `/`, sem hover distinto além do cursor pointer padrão. |
| `.site-header__filters` | `display: flex; align-items: center; gap: var(--space-2);` `overflow-x: auto; -webkit-overflow-scrolling: touch; scrollbar-width: none;` (`::-webkit-scrollbar { display: none; }`) `padding-block: var(--space-1);` **Base mobile:** scroll horizontal (não quebra linha). `@media (min-width: 1024px)`: `flex-wrap: wrap; overflow-x: visible;` |
| `.site-header__chip` | Estado **default**: `display: inline-flex; align-items: center; justify-content: center;` `min-height: 40px; padding: 0 var(--space-4);` (garante área de toque ≈44px somando borda/line-height — CA-11) `flex-shrink: 0; white-space: nowrap;` `background: var(--color-neutral-100); color: var(--color-neutral-700);` `font-size: var(--font-size-sm); font-weight: var(--font-weight-semibold);` `border: 1px solid transparent; border-radius: var(--radius-full);` `transition: background-color .15s ease, color .15s ease;` |
| `.site-header__chip` — **hover** | `@media (hover: hover) { .site-header__chip:hover { background: var(--color-neutral-200); } }` |
| `.site-header__chip--active` | Combina com a classe base: `background: var(--color-primary); color: var(--color-primary-contrast); font-weight: var(--font-weight-bold);` `@media (hover:hover) { &:hover { background: var(--color-primary-dark); } }` |

### 3.2 Layout / listagem (`app/page.tsx`, `app/categoria/[categoria]/page.tsx`)

| Classe | Regra visual |
|---|---|
| `.deals-grid` | **Base (mobile, <640px):** `display: grid; grid-template-columns: 1fr; gap: var(--space-4); padding-block: var(--space-5);` `@media (min-width:640px)`: `grid-template-columns: repeat(2, 1fr); gap: var(--space-5);` `@media (min-width:1024px)`: `grid-template-columns: repeat(3, 1fr); gap: var(--space-6);` `@media (min-width:1280px)`: `grid-template-columns: repeat(4, 1fr);` |
| `.deals-empty` | `display: flex; flex-direction: column; align-items: center; justify-content: center; text-align: center;` `min-height: 240px; margin-block: var(--space-5);` `padding: var(--space-7) var(--space-5);` `background: var(--color-neutral-100); border: 1px dashed var(--color-neutral-200); border-radius: var(--radius-lg);` `color: var(--color-neutral-600); font-size: var(--font-size-base);` Reforço visual **sem alterar o JSX** (CA-T2): `.deals-empty::before { content: "🔎"; display: block; font-size: 32px; margin-bottom: var(--space-3); }` |
| `.deals-pagination` | `display: flex; align-items: center; justify-content: center; gap: var(--space-2); padding-block: var(--space-5);` Itens filhos (links/botões de número de página — estilizar como elementos diretos ou via seletor descendente `.deals-pagination a`/`.deals-pagination button`, conforme o que o Dev encontrar no JSX): `min-width: 40px; min-height: 40px; display: inline-flex; align-items: center; justify-content: center;` `border: 1px solid var(--color-border); border-radius: var(--radius-sm); background: var(--color-surface); color: var(--color-neutral-700); font-size: var(--font-size-sm); font-weight: var(--font-weight-semibold);` **hover** (hover-capable): `border-color: var(--color-primary); color: var(--color-primary);` **página atual** (se houver um modificador tipo `.deals-pagination__page--active`, ou `[aria-current="page"]`): `background: var(--color-primary); border-color: var(--color-primary); color: var(--color-primary-contrast);` **desabilitado** (prev/next nas bordas): `opacity: .4; pointer-events: none; cursor: not-allowed;` |

### 3.3 Card de oferta (`DealCard.tsx`)

| Classe | Regra visual |
|---|---|
| `.deal-card` | **default**: `display: flex; flex-direction: column;` `background: var(--color-surface); border: 1px solid var(--color-border); border-radius: var(--radius-md); overflow: hidden;` `box-shadow: var(--shadow-sm);` `transition: transform .15s ease, box-shadow .15s ease;` `height: 100%;` (para grid alinhar alturas) |
| `.deal-card` — **hover** | `@media (hover: hover) { .deal-card:hover { transform: translateY(-2px); box-shadow: var(--shadow-md); } }` |
| `.deal-card__media` | `position: relative; width: 100%; aspect-ratio: 4 / 3; background: var(--color-neutral-100); overflow: hidden;` |
| `.deal-card__image` | `width: 100%; height: 100%; object-fit: cover; display: block;` (garante que o placeholder de `resolveDealImageUrl` nunca colapse o card — não depende de a imagem existir de fato) |
| `.deal-card__badge` | `position: absolute; top: var(--space-2); left: var(--space-2); z-index: 2;` `background: var(--color-primary); color: var(--color-primary-contrast);` `font-size: var(--font-size-xs); font-weight: var(--font-weight-bold); line-height: 1;` `padding: var(--space-1) var(--space-2); border-radius: var(--radius-sm);` `box-shadow: var(--shadow-sm);` **Não renderizado quando `hasDiscount === false`** — sem regra CSS condicional necessária, é ausência de nó no DOM. |
| `.deal-card__title` | `padding: var(--space-3) var(--space-3) 0;` `font-size: var(--font-size-base); font-weight: var(--font-weight-semibold); color: var(--color-neutral-900); line-height: var(--line-height-tight);` `display: -webkit-box; -webkit-line-clamp: 2; -webkit-box-orient: vertical; overflow: hidden;` `min-height: calc(var(--font-size-base) * var(--line-height-tight) * 2);` (reserva 2 linhas mesmo com título curto, alinha cards entre si) |
| `.deal-card__price` | `display: flex; align-items: baseline; gap: var(--space-2); flex-wrap: wrap;` `padding: var(--space-2) var(--space-3) 0;` — **não depende de `__price-strike`/`__badge` estarem presentes** (flex row simples, sem grid-template fixo) |
| `.deal-card__price-current` | `font-size: var(--font-size-lg); font-weight: var(--font-weight-bold); color: var(--color-primary);` |
| `.deal-card__price-strike` | `font-size: var(--font-size-sm); font-weight: var(--font-weight-regular); color: var(--color-neutral-400); text-decoration: line-through;` |
| `.deal-card__cta` | **default**: `display: flex; align-items: center; justify-content: center;` `margin: var(--space-3); margin-top: auto;` (empurra para o rodapé do card, `.deal-card` é flex column) `min-height: 44px; padding: 0 var(--space-4);` `background: var(--color-primary); color: var(--color-primary-contrast);` `font-size: var(--font-size-sm); font-weight: var(--font-weight-bold);` `border-radius: var(--radius-md); text-align: center;` `transition: background-color .15s ease;` |
| `.deal-card__cta` — **hover** | `@media (hover: hover) { .deal-card__cta:hover { background: var(--color-primary-dark); } }` |
| `.deal-card__cta` — **active/pressed** | `.deal-card__cta:active { background: var(--color-primary-darker); }` |
| `.deal-card__cta--disabled` | Combina com `.deal-card__cta` (elemento é `<span>`, não `<a>`, quando `affiliateLink` ausente): `background: var(--color-neutral-200); color: var(--color-neutral-600); cursor: not-allowed;` — mesma caixa/dimensão do CTA ativo (herda `min-height`/`padding`/`margin` da classe base, só sobrescreve cor). |

### 3.4 Detalhe de oferta (`DealDetail.tsx`)

| Classe | Regra visual |
|---|---|
| `.deal-detail` | **Base (mobile)**: `display: flex; flex-direction: column; gap: var(--space-5);` `padding-block: var(--space-5);` (usa `.container` para largura, ou `max-width: 800px; margin-inline: auto; padding-inline: var(--space-4);` se `.container` não existir) `@media (min-width: 1024px)`: `display: grid; grid-template-columns: minmax(320px, 480px) 1fr; gap: var(--space-6); align-items: start; max-width: var(--container-max-width);` |
| `.deal-detail__media` | `position: relative; border-radius: var(--radius-lg); overflow: hidden; background: var(--color-neutral-100); aspect-ratio: 1 / 1; box-shadow: var(--shadow-sm);` |
| `.deal-detail__image` | `width: 100%; height: 100%; object-fit: cover; display: block;` |
| `.deal-detail__badge` | Igual a `.deal-card__badge`, maior: `position: absolute; top: var(--space-3); left: var(--space-3); z-index: 2;` `background: var(--color-primary); color: var(--color-primary-contrast);` `font-size: var(--font-size-sm); font-weight: var(--font-weight-bold);` `padding: var(--space-2) var(--space-3); border-radius: var(--radius-sm); box-shadow: var(--shadow-sm);` |
| `.deal-detail__info` | `display: flex; flex-direction: column; gap: var(--space-3);` |
| `.deal-detail__category` | `font-size: var(--font-size-sm); font-weight: var(--font-weight-semibold); text-transform: uppercase; letter-spacing: .04em; color: var(--color-neutral-600);` |
| `.deal-detail__title` | `font-size: var(--font-size-xl); font-weight: var(--font-weight-bold); color: var(--color-neutral-900); line-height: var(--line-height-tight);` `@media (min-width:1024px) { font-size: var(--font-size-2xl); }` |
| `.deal-detail__price` | `display: flex; align-items: baseline; gap: var(--space-3); flex-wrap: wrap; margin-block: var(--space-2);` (mesma independência de `__price-strike`/`__badge` do card) |
| `.deal-detail__price-current` | `font-size: var(--font-size-2xl); font-weight: var(--font-weight-black); color: var(--color-primary);` `@media (min-width:1024px) { font-size: var(--font-size-3xl); }` |
| `.deal-detail__price-strike` | `font-size: var(--font-size-base); color: var(--color-neutral-400); text-decoration: line-through;` |
| `.deal-detail__cta` | **default**: `display: flex; align-items: center; justify-content: center; width: 100%;` `min-height: 52px; padding: var(--space-3) var(--space-5);` `background: var(--color-primary); color: var(--color-primary-contrast);` `font-size: var(--font-size-base); font-weight: var(--font-weight-bold);` `border-radius: var(--radius-md); margin-top: var(--space-3); box-shadow: var(--shadow-md);` `transition: background-color .15s ease, transform .15s ease;` |
| `.deal-detail__cta` — **hover** | `@media (hover: hover) { .deal-detail__cta:hover { background: var(--color-primary-dark); transform: translateY(-1px); } }` |
| `.deal-detail__cta` — **active** | `.deal-detail__cta:active { transform: translateY(0); background: var(--color-primary-darker); }` |
| `.deal-detail__cta--disabled` | `background: var(--color-neutral-200); color: var(--color-neutral-600); box-shadow: none; cursor: not-allowed;` |
| `.deal-detail__related` | `margin-top: var(--space-7); padding-top: var(--space-5); border-top: 1px solid var(--color-border);` `@media (min-width:1024px) { grid-column: 1 / -1; }` (ocupa as 2 colunas do grid desktop, abaixo do bloco mídia+info). Título "Mais ofertas" dentro deste bloco (`h2` ou similar, sem classe própria no inventário): `font-size: var(--font-size-lg); font-weight: var(--font-weight-bold); margin-bottom: var(--space-4);` |
| `.deal-detail__related-grid` | `display: grid; grid-template-columns: 1fr; gap: var(--space-4);` `@media (min-width:640px) { grid-template-columns: repeat(2, 1fr); }` `@media (min-width:1024px) { grid-template-columns: repeat(3, 1fr); }` — os cards dentro reaproveitam 100% o estilo de `.deal-card` (mesma classe, CA-8). |

---

## 4. Estados — checklist por componente (heurística de Nielsen: visibilidade do status do sistema)

| Componente | default | hover | active/pressed | disabled | erro | vazio | loading | readonly |
|---|---|---|---|---|---|---|---|---|
| `site-header__chip` | fundo neutro-100 | fundo neutro-200 (hover-capable) | — | N/A (todo chip é clicável) | N/A | N/A | N/A (SSR, sem estado assíncrono) | N/A |
| `deal-card` | sombra sm, borda neutra | `translateY(-2px)` + sombra md | N/A (não é clicável como um todo, só o CTA) | N/A | imagem quebrada já resolvida a montante (`resolveDealImageUrl` sempre retorna placeholder válido) — `object-fit: cover` evita colapso do layout | N/A (ausência de cards é tratada por `deals-empty`, fora do card) | N/A | N/A |
| `deal-card__cta` / `deal-detail__cta` | fundo `--color-primary` | fundo `--color-primary-dark` | fundo `--color-primary-darker` (+ leve translateY no detail) | `--cta--disabled`: fundo neutro-200, texto neutro-600, `cursor: not-allowed` | N/A | N/A | N/A | N/A (é link ou span, nunca input) |
| `deals-pagination` (itens) | borda neutra, fundo branco | borda/texto primary | N/A | opacidade .4 + `pointer-events: none` nas bordas (sem próxima/anterior página) | N/A | N/A | N/A | N/A |
| `deals-empty` | bloco centralizado com ícone `::before` + mensagem | N/A (não interativo) | N/A | N/A | N/A | **é o próprio estado vazio** — coberto acima | N/A | N/A |

Não há estados de "sucesso" (não há submissão de formulário nesta issue) nem "loading" assíncrono no cliente — todas as 3 páginas são SSR/ISR (Next.js), o HTML já chega completo; portanto **N/A é a resposta correta e intencional**, não uma omissão.

---

## 5. As 3 telas — composição

### 5.1 Home (`/`)
1. `.site-header` fixo no topo: brand à esquerda, `.site-header__filters` à direita (scroll horizontal em mobile).
2. `.deals-grid`: 1 coluna mobile → 2 (640px) → 3 (1024px) → 4 (1280px), cada célula um `.deal-card` completo (mídia + badge + título + preço + CTA).
3. `.deals-pagination` centralizada abaixo do grid.
4. Se não houver ofertas (cenário raro na Home, mas o CSS de `.deals-empty` deve funcionar aqui também): mesmo tratamento da categoria.

### 5.2 Categoria (`/categoria/{categoria}`)
Reaproveita 100% do grid/card da Home (CA-6) — mesma `.deals-grid`/`.deal-card`, sem CSS específico adicional além do já mapeado. Diferença é só de conteúdo (filtro server-side), não de estilo.
- **Estado vazio** (CA-7): `.deals-empty` centralizado, com a mensagem "Nenhuma oferta encontrada nesta categoria" + ícone `::before`, dentro de um bloco com fundo neutro-100 e borda tracejada — visualmente distinto de um "card vazio" ou texto solto.

### 5.3 Detalhe de oferta (`/oferta/{slug}`)
1. `.deal-detail` — mobile: mídia em destaque no topo (`aspect-ratio 1:1`), depois `.deal-detail__info` (categoria → título → preço → CTA) empilhados. Desktop (≥1024px): 2 colunas (mídia à esquerda, info à direita).
2. CTA principal (`.deal-detail__cta`) com altura maior (52px) e sombra `--shadow-md` para se destacar claramente como ação primária da página (diferente do CTA do card, mais discreto).
3. `.deal-detail__related` abaixo, com título "Mais ofertas" + `.deal-detail__related-grid` reaproveitando `.deal-card`.

---

## 6. Responsividade — resumo por breakpoint

| Breakpoint | `deals-grid` | `deal-detail` | `site-header__filters` |
|---|---|---|---|
| Base (<640px) | 1 coluna | empilhado (mídia → info) | scroll horizontal |
| ≥640px (tablet) | 2 colunas | empilhado | scroll horizontal (ainda cabe pouco) |
| ≥1024px (desktop) | 3 colunas | 2 colunas (mídia \| info) | `flex-wrap: wrap`, sem scroll |
| ≥1280px (desktop largo) | 4 colunas | 2 colunas | idem |

Touch targets (CA-11): `site-header__chip` (min-height 40px + padding chega a ~44px de área tocável), `deal-card__cta`/`deal-detail__cta` (44px/52px), itens de `deals-pagination` (40x40px mínimo, com `gap: var(--space-2)` entre eles evitando toques acidentais).

---

## 7. Heurísticas de Nielsen aplicadas (critérios verificáveis)

1. **Visibilidade do status do sistema** — todo elemento interativo (chip, CTA, item de paginação) tem estado `:hover`/`:active`/`disabled` visualmente distinto do `default` (tabela §4). Verificável: inspecionar CSS computado ao interagir.
2. **Correspondência sistema-mundo real** — convenções já estabelecidas de e-commerce: preço riscado + preço atual + badge `%OFF` em vermelho, CTA com verbo de ação. Nenhuma metáfora nova a aprender.
3. **Consistência e padrões** — `.deal-card` é a única definição de card, reusada em Home, Categoria e `deal-detail__related-grid` (CA-6/CA-8) — nenhuma variação de estilo entre essas 3 ocorrências.
4. **Prevenção de erros** — `.deal-card__cta--disabled`/`.deal-detail__cta--disabled` usam cor neutra + `cursor: not-allowed`, deixando claro que a ação não está disponível (em vez de um CTA vermelho normal que sugere clicável).
5. **Reconhecimento em vez de memorização** — `.site-header__chip--active` destacado em vermelho sólido mostra o filtro ativo sem exigir que o usuário lembre o que selecionou.
6. **Estética e design minimalista** — hierarquia tipográfica clara (título 2 linhas com `line-clamp`, preço em destaque, CTA como ação final), sem elementos decorativos supérfluos.
7. **Ajudar a reconhecer e se recuperar de "erros"** — `.deals-empty` estilizado como bloco claramente delimitado (borda tracejada + ícone + mensagem), não texto solto perdido na página (CA-7).
8. **Flexibilidade e eficiência de uso** — grid responsivo aproveita mais espaço em telas maiores (mais colunas) sem exigir interação extra do usuário mobile.

---

## 8. Fluxo de navegação (para contexto do Dev — sem mudança de rotas, CA-T2)

`.site-header__brand` → `/` · `.site-header__chip` → filtra por plataforma/categoria (rota já existente) · `.deal-card` (imagem/título) → `/oferta/{slug}` · `.deal-card__cta` / `.deal-detail__cta` → link externo de afiliado (nova aba) · `.deals-pagination` → paginação da mesma listagem · `.deal-detail__related-grid` → outro `/oferta/{slug}`.

---

## 9. Referência rápida — nenhuma classe do inventário sem regra (checklist CA-3)

`deals-empty` ✓ · `deals-grid` ✓ · `deals-pagination` ✓ · `site-header` ✓ · `site-header__brand` ✓ · `site-header__filters` ✓ · `site-header__chip` ✓ · `site-header__chip--active` ✓ · `deal-card` ✓ · `deal-card__media` ✓ · `deal-card__image` ✓ · `deal-card__badge` ✓ · `deal-card__title` ✓ · `deal-card__price` ✓ · `deal-card__price-current` ✓ · `deal-card__price-strike` ✓ · `deal-card__cta` ✓ · `deal-card__cta--disabled` ✓ · `deal-detail` ✓ · `deal-detail__media` ✓ · `deal-detail__image` ✓ · `deal-detail__info` ✓ · `deal-detail__title` ✓ · `deal-detail__category` ✓ · `deal-detail__price` ✓ · `deal-detail__price-current` ✓ · `deal-detail__price-strike` ✓ · `deal-detail__badge` ✓ · `deal-detail__cta` ✓ · `deal-detail__cta--disabled` ✓ · `deal-detail__related` ✓ · `deal-detail__related-grid` ✓

---

## 10. Nota de escopo (fora desta spec)

- `--color-primary`/`--color-primary-dark` etc. são os únicos tokens de cor de marca — não introduzir nenhuma outra cor de marca (CA-12).
- Nenhuma estrutura de componente (`.tsx`) precisa mudar para aplicar esta spec — todas as regras usam apenas as classes já existentes no inventário (`especificacao-tecnica.md` §1.3), inclusive o ícone do estado vazio via `::before` (CSS puro, sem novo markup).
- Fonte Work Sans é uma **melhoria opcional** (via `next/font/google`, a critério do Dev/LT) — o fallback de sistema já cobre 100% dos critérios de aceite; não bloquear a issue por causa da webfont.
