# UX/UI Spec — `FilterBar` (ISSUE-167 / Sub-D #171)

> Spec visual para o novo componente `FilterBar` da Home (`website/app/page.tsx`), em CSS puro,
> reaproveitando 100% dos tokens já definidos em `documentacoes/ISSUE-154-site-sem-css/ux-ui-spec.md`
> (`app/styles/tokens.css`). Nenhuma cor, tipografia, espaçamento ou raio novo é introduzido — só
> composição de layout e estados sobre o design system já implementado.

## 0. Nota sobre a fonte (Figma + base técnica)

Reconsultei `get_figma_data` no arquivo do design system da squad (`yi6YkNAy9HfHus2oiPi3G7`) — segue
no estado padrão de boas-vindas do Figma, nenhum token/componente novo desde a Issue #154. Não há
nada a extrair daqui além do já documentado (tipografia Work Sans). Confirma que a base de tokens
usada abaixo é a mesma da Issue #154, sem desvio.

**Base técnica:** o `website` é Next.js com **CSS puro** (sem biblioteca de componentes, sem Radix)
— confirmado pelo padrão já implementado na Issue #154 (`site-header__chip`, `deal-card`, etc., todos
CSS/BEM sobre elementos HTML nativos, sem `@radix-ng/primitives` nem React Native Reusables, que são
específicos dos stacks Angular/RN da squad, não deste projeto). Esta spec segue a **mesma convenção
já estabelecida no repo**: elementos HTML nativos e acessíveis (`<button>`, `<input type="range">`,
`<ul role="listbox">`) estilizados via classes BEM `filter-bar__*`, sem framework de UI novo.

Novo arquivo sugerido: `website/app/styles/filter-bar.css` (importado em `globals.css`, mesmo padrão
dos demais partials da Issue #154).

---

## 1. Decisão de UX — comportamento responsivo (justificativa)

O `FilterBar` tem 5 controles (categoria, subcategoria, faixa de preço, desconto mínimo, ordenação) —
significativamente mais denso que os chips do `site-header` (que só filtravam por 1 dimensão e cabiam
em scroll horizontal de pílulas). Colocar os 5 controles em linha/scroll horizontal no mobile força o
usuário a "descobrir" filtros arrastando a barra lateralmente, viola **heurística 6 (estética e design
minimalista)** ao competir por espaço vertical logo no topo da Home, e cada controle (dropdown,
slider) precisa de área de toque generosa — scroll horizontal comprime isso.

**Decisão: painel/drawer no mobile e tablet (`<1024px`), barra em linha no desktop (`≥1024px`)** —
mesmo breakpoint desktop já usado por `deals-grid`/`deal-detail` na Issue #154, mantendo consistência
de "onde o layout desktop começa" no projeto.

- **Mobile/tablet (`<1024px`)**: `.filter-bar` colapsa numa **barra-resumo compacta** (`filter-bar__summary`)
  logo abaixo do `.site-header`: botão "Filtros" (com badge de contagem de filtros ativos) + seletor de
  ordenação inline (ação mais frequente, fica sempre visível sem abrir o painel — **heurística 8,
  flexibilidade e eficiência de uso**). Tocar "Filtros" abre um **painel inferior (`bottom sheet`)**
  com os 4 demais controles empilhados verticalmente, mais botões "Limpar" e "Ver resultados" fixos no
  rodapé do painel. Um **FAB de reabertura** (`filter-bar__fab`) fica ancorado no canto inferior
  direito da tela durante a rolagem do grid, para reabrir o painel sem precisar rolar de volta ao topo
  (**heurística 3, controle e liberdade do usuário**).
- **Desktop (`≥1024px`)**: todos os 5 controles ficam em **uma única linha** (`flex-wrap` se a janela
  for estreita entre 1024–1279px), sem painel/drawer — espaço horizontal sobra, então esconder atrás
  de um botão "Filtros" seria fricção desnecessária (contradiria a heurística 8 no sentido inverso).

Essa é uma decisão de UX registrada aqui — não estrutura de dados/API (o Dev implementa o
comportamento; o contrato de filtros combináveis via `searchParams` já está fechado pelo LT).

---

## 2. Tokens reaproveitados (`app/styles/tokens.css`, Issue #154 — nenhum novo)

| Uso no `FilterBar` | Token |
|---|---|
| Fundo da barra/summary/drawer | `--color-surface` (#ffffff) |
| Fundo da página por trás do overlay | `--color-neutral-50` |
| Texto de label/placeholder | `--color-neutral-600` |
| Texto de valor selecionado | `--color-neutral-900` |
| Borda de inputs/dropdowns | `--color-border` (= `--color-neutral-200`) |
| Fundo de controle inativo (botão de desconto, chip) | `--color-neutral-100` |
| Cor de destaque — filtro ativo, thumb do slider, botão "Aplicar" | `--color-primary` (#e63946) |
| Hover de elemento ativo | `--color-primary-dark` |
| Pressed/active | `--color-primary-darker` |
| Fundo de pílula de filtro ativo (tint pálido, uso pontual conforme já ressalvado na Issue #154) | `--color-primary-light` |
| Texto sobre fundo `--color-primary` | `--color-primary-contrast` |
| Tipografia | `--font-family-base`, `--font-size-xs/sm/base`, `--font-weight-semibold/bold` |
| Espaçamento | `--space-1` a `--space-6` (escala 8pt) |
| Raio | `--radius-sm` (controles/pílulas), `--radius-md` (drawer/cards), `--radius-full` (FAB, badge de contagem) |
| Sombra | `--shadow-sm` (barra/summary), `--shadow-md` (drawer, FAB, dropdown aberto) |
| Foco de teclado | `:focus-visible` global já definido (outline `--color-primary`) — reaproveitado sem alteração |
| Breakpoint desktop | `@media (min-width: 1024px)` — mesmo limiar de `deals-grid`/`site-header` |

---

## 3. Estrutura de classes BEM (árvore do componente)

```
.filter-bar                              (container — Home, app/page.tsx, acima de .deals-grid)
├── .filter-bar__summary                 (visível <1024px; oculto ≥1024px)
│   ├── .filter-bar__toggle              (botão "Filtros", abre o drawer)
│   │   └── .filter-bar__toggle-badge    (contador de filtros ativos, se > 0)
│   └── .filter-bar__sort                (seletor de ordenação inline, sempre visível)
│
├── .filter-bar__row                     (visível ≥1024px; oculto <1024px — linha única desktop)
│   ├── .filter-bar__group (categoria)
│   ├── .filter-bar__group (subcategoria)
│   ├── .filter-bar__group (faixa de preço)
│   ├── .filter-bar__group (desconto mínimo)
│   ├── .filter-bar__group (ordenação)
│   └── .filter-bar__clear               ("Limpar filtros")
│
├── .filter-bar__active-pills            (visível em qualquer largura, só quando há filtro ativo)
│   └── .filter-bar__pill (× N)
│
├── .filter-bar__drawer                  (mobile/tablet, oculto por padrão)
│   ├── .filter-bar__drawer-overlay
│   ├── .filter-bar__drawer-panel
│   │   ├── .filter-bar__drawer-header   (título "Filtros" + botão fechar)
│   │   ├── .filter-bar__drawer-body     (os mesmos 4 .filter-bar__group, empilhados)
│   │   └── .filter-bar__drawer-footer   (botões "Limpar" + "Ver resultados")
│
└── .filter-bar__fab                     (mobile/tablet, reabre o drawer durante o scroll do grid)
```

Controles reutilizados dentro de `.filter-bar__group` (mesma marcação em desktop-row e drawer-body):

```
.filter-bar__group
├── .filter-bar__label                   ("Categoria", "Subcategoria", "Preço", "Desconto mínimo", "Ordenar por")
└── (um dos abaixo, conforme o grupo)
    ├── .filter-bar__dropdown            (categoria / subcategoria / ordenação)
    │   ├── .filter-bar__dropdown-trigger
    │   └── .filter-bar__dropdown-panel  (role="listbox", só no DOM quando aberto)
    │       └── .filter-bar__dropdown-option (× N, role="option")
    ├── .filter-bar__price                (faixa de preço)
    │   ├── .filter-bar__price-values     ("R$ 50 — R$ 500")
    │   └── .filter-bar__price-slider
    │       ├── .filter-bar__price-track
    │       ├── .filter-bar__price-range  (trecho preenchido entre os 2 thumbs)
    │       └── input[type=range].filter-bar__price-input (× 2 — min/max, sobrepostos)
    └── .filter-bar__discount-group        (botões de desconto)
        └── .filter-bar__discount-btn (× 3 — "10%+", "30%+", "50%+")
```

---

## 4. Layout — desktop (`≥1024px`)

`.filter-bar` renderiza só `.filter-bar__row` (summary/drawer/fab ficam `display: none`):

| Classe | Regra visual |
|---|---|
| `.filter-bar` | `background: var(--color-surface); border-bottom: 1px solid var(--color-border); padding-block: var(--space-4);` Não é `sticky` (decisão §1 — evita 2 barras sticky competindo com `.site-header` já sticky). |
| `.filter-bar__row` | `display: flex; align-items: flex-end; flex-wrap: wrap; gap: var(--space-4); max-width: var(--container-max-width); margin-inline: auto; padding-inline: var(--space-6);` (mesmo container das demais seções) |
| `.filter-bar__group` | `display: flex; flex-direction: column; gap: var(--space-1); min-width: 160px;` — grupo "faixa de preço" ganha `min-width: 220px` (slider precisa de mais largura útil) |
| `.filter-bar__label` | `font-size: var(--font-size-xs); font-weight: var(--font-weight-semibold); color: var(--color-neutral-600); text-transform: uppercase; letter-spacing: .03em;` |
| `.filter-bar__clear` | `margin-left: auto; align-self: center;` — ver §6 (estados) |

## 5. Layout — mobile/tablet (`<1024px`)

`.filter-bar` renderiza `.filter-bar__summary` + `.filter-bar__active-pills` (se houver) +
`.filter-bar__drawer` (montado no DOM só quando aberto, ou sempre montado com `visibility`/transform
controlado via classe `--open`) + `.filter-bar__fab`. `.filter-bar__row` fica `display: none`.

| Classe | Regra visual |
|---|---|
| `.filter-bar` | `position: sticky; top: 56px;` (altura do `.site-header` mobile) `z-index: 40;` (abaixo do header, `z-index: 50`) `background: var(--color-surface); border-bottom: 1px solid var(--color-border); box-shadow: var(--shadow-sm);` **Aqui sim é sticky** — só a barra-resumo (compacta, 1 linha, ~48px) fica fixa, não o drawer inteiro; custo de espaço vertical é baixo e o ganho de sempre poder reordenar/reabrir filtros compensa. |
| `.filter-bar__summary` | `display: flex; align-items: center; justify-content: space-between; gap: var(--space-3); padding: var(--space-2) var(--space-4); min-height: 48px;` |
| `.filter-bar__toggle` | `display: inline-flex; align-items: center; gap: var(--space-2); min-height: 40px; padding: 0 var(--space-4); background: var(--color-neutral-100); color: var(--color-neutral-900); font-size: var(--font-size-sm); font-weight: var(--font-weight-semibold); border-radius: var(--radius-sm); border: 1px solid transparent;` Ícone sugerido antes do texto: `::before { content: "☰"; }` ajustável para ícone de biblioteca de ícones do Dev, se houver — sem dependência de assets aqui. Texto: **"Filtros"**. |
| `.filter-bar__toggle-badge` | `display: inline-flex; align-items: center; justify-content: center; min-width: 18px; height: 18px; padding-inline: 4px; background: var(--color-primary); color: var(--color-primary-contrast); font-size: 11px; font-weight: var(--font-weight-bold); border-radius: var(--radius-full);` Só renderiza quando `filtrosAtivos > 0` (ausência de nó, não `display:none` condicional). |
| `.filter-bar__sort` | Mesmo `.filter-bar__dropdown` do §6, versão compacta: `min-width: 140px; flex-shrink: 0;` sem label visível (label vira `aria-label="Ordenar por"`, texto do trigger já mostra a opção atual, ex. "Relevância"). |

### 5.1 Drawer (bottom sheet)

| Classe | Regra visual |
|---|---|
| `.filter-bar__drawer-overlay` | `position: fixed; inset: 0; background: rgba(26,21,35,.4); z-index: 60; opacity: 0; pointer-events: none; transition: opacity .2s ease;` **Aberto**: `.filter-bar__drawer--open &` → `opacity: 1; pointer-events: auto;` |
| `.filter-bar__drawer-panel` | `position: fixed; left: 0; right: 0; bottom: 0; z-index: 61; max-height: 85vh; display: flex; flex-direction: column;` `background: var(--color-surface); border-radius: var(--radius-lg) var(--radius-lg) 0 0; box-shadow: var(--shadow-md);` `transform: translateY(100%); transition: transform .25s ease;` **Aberto**: `.filter-bar__drawer--open &` → `transform: translateY(0);` |
| `.filter-bar__drawer-header` | `display: flex; align-items: center; justify-content: space-between; padding: var(--space-4); border-bottom: 1px solid var(--color-border);` Título: `font-size: var(--font-size-lg); font-weight: var(--font-weight-bold); color: var(--color-neutral-900);` texto **"Filtros"**. Botão fechar: `min-width: 40px; min-height: 40px;` ícone `✕`, `color: var(--color-neutral-600)`, hover `color: var(--color-neutral-900)`. |
| `.filter-bar__drawer-body` | `overflow-y: auto; padding: var(--space-4); display: flex; flex-direction: column; gap: var(--space-5);` (`.filter-bar__group` empilhados, `width: 100%` cada) |
| `.filter-bar__drawer-footer` | `display: flex; gap: var(--space-3); padding: var(--space-4); border-top: 1px solid var(--color-border); background: var(--color-surface);` (sticky dentro do painel, sempre visível mesmo com `body` rolando) |
| `.filter-bar__drawer-footer .filter-bar__clear` | `flex: 1; min-height: 48px; border: 1px solid var(--color-border); border-radius: var(--radius-md); color: var(--color-neutral-700); font-weight: var(--font-weight-semibold);` texto **"Limpar filtros"** |
| `.filter-bar__drawer-footer .filter-bar__apply` | `flex: 2; min-height: 48px; background: var(--color-primary); color: var(--color-primary-contrast); border-radius: var(--radius-md); font-weight: var(--font-weight-bold);` texto **"Ver resultados"** (microcopy orientada a resultado, não "Aplicar" genérico — reforça que o clique fecha o drawer e já mostra o grid atualizado) hover: `background: var(--color-primary-dark)`. |

Comportamento (para o Dev, não é CSS): abrir o drawer bloqueia o scroll do `<body>` (`overflow: hidden`
enquanto `--open`); fechar via botão `✕`, clique no overlay, ou tecla `Esc` — os 3 caminhos devem
convergir para o mesmo estado (**heurística 3, controle do usuário**). Os filtros já se aplicam ao
grid em tempo real conforme o usuário interage (mesma URL/`searchParams`), então "Ver resultados" é
principalmente um fechamento explícito do drawer, não um "confirmar" bloqueante — se o usuário fechar
pelo `✕`/overlay, o filtro já aplicado permanece (não há estado de "rascunho descartável").

### 5.2 FAB de reabertura

| Classe | Regra visual |
|---|---|
| `.filter-bar__fab` | `position: fixed; right: var(--space-4); bottom: var(--space-4); z-index: 45;` `display: inline-flex; align-items: center; gap: var(--space-2); min-height: 48px; padding: 0 var(--space-4);` `background: var(--color-primary); color: var(--color-primary-contrast); border-radius: var(--radius-full); box-shadow: var(--shadow-md);` `font-size: var(--font-size-sm); font-weight: var(--font-weight-bold);` **Visibilidade**: oculto por padrão (`opacity: 0; pointer-events: none;`); mostrado via classe `--visible` quando o usuário rola o grid para além do `.filter-bar` original (`IntersectionObserver` no Dev) — evita 2 pontos de acesso a filtro simultâneos na primeira dobra. Contém o mesmo `.filter-bar__toggle-badge` quando houver filtro ativo. |
| `.filter-bar__fab` — hover | `@media (hover:hover) { .filter-bar__fab:hover { background: var(--color-primary-dark); } }` |

---

## 6. Controles — especificação por tipo (default/hover/active/disabled/aberto-fechado)

### 6.1 Dropdown (categoria, subcategoria, ordenação)

Elemento custom (`<button role="combobox">` + `<ul role="listbox">`), não `<select>` nativo — necessário
para estilizar o painel aberto (browser não permite customizar o popup de `<select>`), mesma decisão
já implícita na Issue #154 para todo controle interativo do site.

| Estado | Classe/seletor | Regra visual |
|---|---|---|
| **Default** (fechado, sem valor) | `.filter-bar__dropdown-trigger` | `display: flex; align-items: center; justify-content: space-between; gap: var(--space-2); width: 100%; min-height: 44px; padding: 0 var(--space-3);` `background: var(--color-surface); border: 1px solid var(--color-border); border-radius: var(--radius-sm);` `font-size: var(--font-size-sm); color: var(--color-neutral-600);` (placeholder, ex. "Todas as categorias") Seta: `::after { content: "▾"; color: var(--color-neutral-600); }` |
| **Default com valor selecionado** | `.filter-bar__dropdown-trigger--filled` | Mesma caixa; `color: var(--color-neutral-900); font-weight: var(--font-weight-semibold);` |
| **Hover** | `.filter-bar__dropdown-trigger:hover` (hover-capable) | `border-color: var(--color-neutral-400);` |
| **Foco/teclado** | `:focus-visible` | Reaproveita o global (`outline: 2px solid var(--color-primary)`) |
| **Aberto** | `.filter-bar__dropdown-trigger--open` | `border-color: var(--color-primary); box-shadow: 0 0 0 3px var(--color-primary-light);` seta `::after { transform: rotate(180deg); }` |
| **Painel aberto** | `.filter-bar__dropdown-panel` | `position: absolute; z-index: 55; margin-top: var(--space-1); min-width: 100%; max-height: 280px; overflow-y: auto;` `background: var(--color-surface); border: 1px solid var(--color-border); border-radius: var(--radius-sm); box-shadow: var(--shadow-md);` (drawer mobile: o painel pode abrir como lista inline dentro do próprio `.filter-bar__group`, sem `position: absolute`, já que o drawer inteiro já rola) |
| **Opção — default** | `.filter-bar__dropdown-option` | `display: flex; align-items: center; justify-content: space-between; min-height: 40px; padding: 0 var(--space-3); font-size: var(--font-size-sm); color: var(--color-neutral-900);` texto da subcategoria + contagem opcional (`(12)`, se o Dev optar por expor a contagem de `GET /api/public/categories`) em `color: var(--color-neutral-600); font-size: var(--font-size-xs);` |
| **Opção — hover/foco** | `.filter-bar__dropdown-option:hover`, `:focus-visible` | `background: var(--color-neutral-100);` |
| **Opção — selecionada** | `.filter-bar__dropdown-option--selected` | `background: var(--color-primary-light); color: var(--color-primary-dark); font-weight: var(--font-weight-semibold);` marcador `::after { content: "✓"; }` |
| **Disabled** (subcategoria antes de escolher categoria) | `.filter-bar__dropdown-trigger--disabled` | `background: var(--color-neutral-100); color: var(--color-neutral-400); border-color: var(--color-border); cursor: not-allowed; pointer-events: none;` seta em `color: var(--color-neutral-400)`. Placeholder do trigger muda para **"Escolha uma categoria"** (texto orientativo, não só cinza — CA 7.1 e heurística 4, prevenção de erros: o usuário entende *por que* está desabilitado, não só *que* está). `aria-disabled="true"` no elemento (a11y). |
| **Reabilitado** (categoria já escolhida, sem subcategoria ainda selecionada) | `.filter-bar__dropdown-trigger` (default) | Placeholder volta a **"Todas as subcategorias"**; visual idêntico ao default de categoria. |

Microcopy dos triggers (default/placeholder): Categoria = **"Todas as categorias"** · Subcategoria
(habilitado) = **"Todas as subcategorias"** · Subcategoria (desabilitado) = **"Escolha uma categoria"**
· Ordenação = **"Relevância"** (opção padrão, primeira da lista, nunca vazia).

Opções do dropdown de Ordenação, nesta ordem: **Relevância** (padrão) · **Menor preço** · **Maior
desconto** · **Mais recente**.

### 6.2 Slider de faixa de preço

Implementado com 2 `<input type="range">` nativos sobrepostos sobre uma trilha customizada (mantém
navegação por teclado/acessibilidade nativa de `<input range>` "de graça", só a trilha visual é CSS
custom — mesmo espírito de não reinventar semântica nativa da Issue #154, ex. `<select>`/`<a>`/`<button>`
usados sempre que possível).

| Estado | Classe/seletor | Regra visual |
|---|---|---|
| **Valores atuais** | `.filter-bar__price-values` | `display: flex; justify-content: space-between; font-size: var(--font-size-sm); font-weight: var(--font-weight-semibold); color: var(--color-neutral-900); margin-bottom: var(--space-1);` Formato: `R$ {min}` à esquerda, `R$ {max}` à direita (ou `R$ {max}+` se `max` == teto superior do catálogo, indicando "sem limite superior"). |
| **Trilha (default)** | `.filter-bar__price-track` | `position: relative; height: 4px; border-radius: var(--radius-full); background: var(--color-neutral-200);` |
| **Trecho preenchido** | `.filter-bar__price-range` | `position: absolute; height: 4px; border-radius: var(--radius-full); background: var(--color-primary);` (posicionado via `left`/`right` calculados em JS a partir dos 2 valores — comportamento do Dev) |
| **Thumb (default)** | `input[type=range].filter-bar__price-input::-webkit-slider-thumb` (+ equivalente `::-moz-range-thumb`) | `width: 20px; height: 20px; border-radius: var(--radius-full); background: var(--color-surface); border: 2px solid var(--color-primary); box-shadow: var(--shadow-sm); cursor: pointer;` |
| **Thumb — hover/drag** | `:hover`, `:active` (mesmos pseudo-elementos vendor) | `border-color: var(--color-primary-dark); box-shadow: 0 0 0 6px var(--color-primary-light);` (halo de destaque durante o arraste — feedback claro de manipulação direta, heurística 1) |
| **Thumb — foco (teclado, setas ajustam o valor)** | `:focus-visible` | Reaproveita o outline global (`--color-primary`), somado ao halo acima |
| **Faixa numérica/limites** | — | `min`/`max` vêm do catálogo real (menor/maior `SalePrice` ativo, obtido via a listagem já carregada ou endpoint dedicado — decisão do Dev/LT, fora do escopo visual); a spec não fixa valores fixos de R$0–R$5000 para não divergir do catálogo real. |

### 6.3 Botões de desconto mínimo (10%+/30%+/50%+)

Grupo de **seleção única com toggle** (não é multi-seleção nem dropdown): clicar num botão já ativo
o desativa (limpa `minDiscount`); clicar em outro botão troca a seleção. Reaproveita a mesma linguagem
visual dos chips do `.site-header` (Issue #154), adaptado com classe própria para não colidir com o
namespace do header.

| Estado | Classe | Regra visual |
|---|---|---|
| `.filter-bar__discount-group` | container | `display: flex; gap: var(--space-2);` |
| **Default** | `.filter-bar__discount-btn` | `display: inline-flex; align-items: center; justify-content: center; min-height: 40px; padding: 0 var(--space-3); flex: 1;` `background: var(--color-neutral-100); color: var(--color-neutral-700);` `font-size: var(--font-size-sm); font-weight: var(--font-weight-semibold);` `border: 1px solid transparent; border-radius: var(--radius-sm); transition: background-color .15s ease, color .15s ease;` texto: **"10%+"**, **"30%+"**, **"50%+"** |
| **Hover** | `.filter-bar__discount-btn:hover` (hover-capable) | `background: var(--color-neutral-200);` |
| **Active/selecionado** | `.filter-bar__discount-btn--active` | `background: var(--color-primary); color: var(--color-primary-contrast); font-weight: var(--font-weight-bold);` hover: `background: var(--color-primary-dark)` — mesmo padrão de `.site-header__chip--active` já validado na Issue #154 (**heurística 5, reconhecimento em vez de memorização** — reaproveitar o mesmo código de cor para "ativo" em toda a Home) |
| **Pressed** | `.filter-bar__discount-btn:active` | `background: var(--color-primary-darker);` |
| **Disabled** | Não se aplica — os 3 botões estão sempre habilitados, independente de outros filtros (não há combinação que os invalide). |

### 6.4 Botão "Limpar filtros"

| Estado | Classe | Regra visual |
|---|---|---|
| **Habilitado** (≥1 filtro ativo) | `.filter-bar__clear` | `display: inline-flex; align-items: center; gap: var(--space-1); min-height: 40px; padding: 0 var(--space-3); background: transparent; color: var(--color-primary-dark); font-size: var(--font-size-sm); font-weight: var(--font-weight-semibold); border: 1px solid var(--color-primary-dark); border-radius: var(--radius-sm);` texto: **"Limpar filtros"** (ícone opcional `✕` antes do texto) |
| **Hover** | `.filter-bar__clear:hover` | `background: var(--color-primary-light);` |
| **Disabled** (nenhum filtro ativo — estado inicial da Home) | `.filter-bar__clear--disabled` (ou atributo `disabled` nativo) | `opacity: .4; border-color: var(--color-border); color: var(--color-neutral-400); cursor: not-allowed; pointer-events: none;` — **não esconder o botão**, mantê-lo visível e desabilitado (heurística 4, prevenção de erros/clareza: usuário vê que a ação existe mas não há nada a limpar, em vez de "sumir" e gerar dúvida). |

Ordenação (sort) **não conta** para o estado de "Limpar filtros"/badge de contagem — trocar a
ordenação não é "aplicar um filtro" no sentido de restringir resultados, é reordenar o mesmo conjunto;
`.filter-bar__clear` reseta `category`/`subcategory`/`minPrice`/`maxPrice`/`minDiscount`, mas mantém
`sort` como estava (decisão de UX — evita a frustração de "limpei filtro de preço e a ordenação também
mudou sem eu pedir").

### 6.5 Pílulas de filtro ativo (`.filter-bar__active-pills`)

Linha abaixo do `.filter-bar` (visível em qualquer largura), só renderizada quando há ≥1 filtro
restritivo ativo (`category`/`subcategory`/`minPrice`/`maxPrice`/`minDiscount` — `sort` não gera pílula,
mesma razão do item acima).

| Classe | Regra visual |
|---|---|
| `.filter-bar__active-pills` | `display: flex; flex-wrap: wrap; gap: var(--space-2); padding: var(--space-2) var(--space-4);` (`var(--space-6)` inline no desktop, dentro do `.container`) `background: var(--color-neutral-50);` |
| `.filter-bar__pill` | `display: inline-flex; align-items: center; gap: var(--space-1); min-height: 32px; padding: 0 var(--space-1) 0 var(--space-3);` `background: var(--color-primary-light); color: var(--color-primary-dark); font-size: var(--font-size-xs); font-weight: var(--font-weight-semibold); border-radius: var(--radius-full);` texto: label legível do filtro, ex. `"Eletrônicos"`, `"R$ 100 – R$ 500"`, `"30% OFF+"` |
| `.filter-bar__pill-remove` | `display: inline-flex; align-items: center; justify-content: center; width: 24px; height: 24px; margin-left: var(--space-1); border-radius: var(--radius-full); color: var(--color-primary-dark);` conteúdo `✕`; hover: `background: rgba(230,57,70,.15)` (tint do próprio `--color-primary` em baixa opacidade, sem novo token) |

Remover uma pílula individual remove só aquele filtro (ex. remover a pílula de subcategoria mantém a
categoria) — reforça **heurística 3 (controle e liberdade do usuário)** com granularidade maior que o
botão "Limpar filtros" (que reseta tudo de uma vez).

---

## 7. Estado vazio (reaproveitamento de `.deals-empty`, CA 7.5)

Nenhum componente novo — quando `PagedResult.items` vem vazio após aplicar filtros, `app/page.tsx`
renderiza o **mesmo** `.deals-empty` já especificado e implementado na Issue #154, no lugar do
`.deals-grid`. Único ajuste de conteúdo (mensagem, sem mudança de CSS): trocar o texto padrão
("Nenhuma oferta encontrada") por uma variante que reconhece o contexto de filtro E oferece saída
imediata — **heurística 7 (ajudar a reconhecer e se recuperar de erros)**:

> 🔎 **Nenhuma oferta encontrada com esses filtros.**
> Tente ajustar a faixa de preço ou o desconto mínimo.
> **[Ver todas as ofertas]**

O link/botão **"Ver todas as ofertas"** (CTA explícito no critério de aceite) usa a classe já existente
`.deal-card__cta` (mesmo componente de CTA vermelho já mapeado na Issue #154, sem criar uma 3ª variante
de botão) e, ao ser clicado, remove todos os filtros ativos (equivalente a `.filter-bar__clear`, mesma
ação, gatilho diferente). Estrutura sugerida dentro de `.deals-empty` (sem alterar a classe raiz):

```
.deals-empty
├── (ícone já existente via ::before)
├── texto da mensagem (2 linhas acima)
└── a.deal-card__cta  →  "Ver todas as ofertas"
```

---

## 8. Responsividade — resumo por breakpoint

| Breakpoint | `.filter-bar` | Controles visíveis |
|---|---|---|
| Base (`<640px`) | `.filter-bar__summary` sticky (48px) + `.filter-bar__drawer` sob demanda + `.filter-bar__fab` durante scroll | Resumo: botão "Filtros" + ordenação inline. Drawer: categoria, subcategoria, preço, desconto |
| `≥640px` (tablet) | Idem ao mobile — mesma densidade de controles não cabe em linha ainda | Idem |
| `≥1024px` (desktop) | `.filter-bar__row` em linha única (`flex-wrap` se necessário), sem drawer/summary/fab | Todos os 5 grupos + "Limpar filtros" na mesma linha |

Área de toque (consistente com CA-11 da Issue #154): `.filter-bar__toggle` (40px), `.filter-bar__dropdown-trigger`
(44px), `.filter-bar__discount-btn` (40px), thumbs do slider (20px visual + área de toque ampliada via
`padding`/hit-area maior no `<input type="range">`, mínimo 44px de área clicável mesmo com thumb visual
menor — técnica padrão: aumentar a altura do próprio `<input>` sem aumentar a trilha visível),
`.filter-bar__fab` (48px), `.filter-bar__drawer-footer` botões (48px).

---

## 9. Fluxo de navegação / estado (contexto para o Dev — sem prescrever implementação)

Todos os controles refletem e leem de `useSearchParams` (Next.js App Router, já definido pelo LT em
`especificacao-tecnica.md` §9.4) — mudar um filtro atualiza a URL (`router.push`/`replace`) e
`app/page.tsx` repassa os `searchParams` para `fetchDeals`. Consequência visual: **a URL é a fonte de
verdade do estado ativo** (permite compartilhar link filtrado, F5 preserva os filtros) — os estados
`--active`/`--selected`/`--filled` desta spec devem refletir o valor lido da URL no primeiro render
(SSR), não só interação client-side.

Navegação: `.filter-bar__toggle` → abre `.filter-bar__drawer` (mobile) · `.filter-bar__dropdown-trigger`
→ abre `.filter-bar__dropdown-panel` (fecha ao selecionar opção, clicar fora, ou `Esc`) ·
`.filter-bar__pill-remove` / `.filter-bar__clear` → atualiza `searchParams` e permanece na Home ·
nenhum controle deste componente navega para outra rota.

---

## 10. Heurísticas de Nielsen aplicadas (critérios verificáveis)

1. **Visibilidade do status do sistema** — filtro ativo sempre destacado em `--color-primary`
   (dropdown com valor, botão de desconto `--active`, pílula em `.filter-bar__active-pills`, badge de
   contagem no botão "Filtros" mobile). Verificável: nenhum filtro aplicado fica visualmente idêntico
   ao estado "nenhum filtro".
2. **Controle e liberdade do usuário** — 3 níveis de "desfazer": pílula individual remove 1 filtro,
   `.filter-bar__clear` remove todos, e o drawer fecha por 3 caminhos equivalentes (✕, overlay, `Esc`).
3. **Consistência e padrões** — cor "ativo" (`--color-primary`) e forma de pílula reaproveitam
   exatamente `.site-header__chip--active` (Issue #154); CTA do estado vazio reaproveita
   `.deal-card__cta`; nenhum componente visual novo fora do necessário.
4. **Prevenção de erros** — dropdown de subcategoria desabilitado com texto explicativo ("Escolha uma
   categoria") em vez de simplesmente cinza sem contexto; botão "Limpar filtros" some do fluxo de
   engano (visível porém desabilitado, nunca oculto).
5. **Reconhecimento em vez de memorização** — pílulas mostram o valor exato de cada filtro ativo em
   texto legível (não códigos), sem exigir que o usuário abra os dropdowns de novo para lembrar o que
   escolheu.
6. **Estética e design minimalista** — drawer isola a densidade de 5 controles do primeiro scroll da
   Home no mobile; FAB só aparece quando relevante (depois que o usuário já rolou passado a barra
   original), evitando poluir a tela o tempo todo.
7. **Ajuda a reconhecer e se recuperar de erros** — estado vazio com filtros explica a causa provável
   ("ajuste a faixa de preço ou desconto") e oferece saída de 1 clique ("Ver todas as ofertas").
8. **Flexibilidade e eficiência de uso** — ordenação (ação mais frequente) fica sempre acessível no
   resumo mobile sem precisar abrir o drawer completo; desktop expõe tudo em linha para usuários com
   mais espaço de tela, sem esconder atrás de clique extra.

---

## 11. Referência rápida — checklist de classes

`filter-bar` ✓ · `filter-bar__summary` ✓ · `filter-bar__toggle` ✓ · `filter-bar__toggle-badge` ✓ ·
`filter-bar__sort` ✓ · `filter-bar__row` ✓ · `filter-bar__group` ✓ · `filter-bar__label` ✓ ·
`filter-bar__clear` ✓ (+ `--disabled`) · `filter-bar__active-pills` ✓ · `filter-bar__pill` ✓ ·
`filter-bar__pill-remove` ✓ · `filter-bar__drawer` ✓ (+ `--open`) · `filter-bar__drawer-overlay` ✓ ·
`filter-bar__drawer-panel` ✓ · `filter-bar__drawer-header` ✓ · `filter-bar__drawer-body` ✓ ·
`filter-bar__drawer-footer` ✓ · `filter-bar__apply` ✓ · `filter-bar__fab` ✓ (+ `--visible`) ·
`filter-bar__dropdown-trigger` ✓ (+ `--filled`, `--open`, `--disabled`) · `filter-bar__dropdown-panel` ✓ ·
`filter-bar__dropdown-option` ✓ (+ `--selected`) · `filter-bar__price` ✓ · `filter-bar__price-values` ✓ ·
`filter-bar__price-slider` ✓ · `filter-bar__price-track` ✓ · `filter-bar__price-range` ✓ ·
`filter-bar__price-input` ✓ · `filter-bar__discount-group` ✓ · `filter-bar__discount-btn` ✓ (+ `--active`)

Reaproveitadas sem alteração (Issue #154): `deals-empty`, `deal-card__cta`, `site-header` (`sticky`
`top:0`), `:focus-visible` global.

---

## 12. Fora de escopo desta spec

- Estrutura de dados/API, nomes de parâmetros de `sort`/`minDiscount` — contrato já fechado por
  `especificacao-tecnica.md` §8/§9 (LT/Arquiteto).
- Curadoria do conteúdo do dropdown de categorias/subcategorias (vem de `GET /api/public/categories`).
- Biblioteca de ícones (☰, ▾, ✕, 🔎 usados como placeholders textuais/emoji nesta spec, mesmo padrão
  já aceito na Issue #154 para `.deals-empty::before`) — se o Dev preferir um icon-set SVG, a
  substituição é 1:1 sem alterar layout/dimensões especificadas aqui.
- Implementação de acessibilidade fina (ordem de tab, anúncios de `aria-live` ao atualizar o grid) —
  a spec define a estrutura semântica mínima (`role="combobox"`/`listbox`/`option`, `aria-disabled`),
  o Dev completa conforme os padrões WCAG já seguidos no restante do projeto.
