# Especificação Técnica — ISSUE-230: Revisar filtros da tela de produtos do site público

## Componentes afetados
- `website/components/FilterBar.tsx` (Client Component) — principal.
- `website/app/styles/filter-bar.css` — remoção de CSS do desconto.
- `website/app/page.tsx` (Server Component) — `buildFilters`/`buildPaginationQuery`/
  `HomePageProps.searchParams`, remoção de `minDiscount`.
- `website/lib/api.ts` — `DealFilters.minDiscount` (avaliar remoção; ver design.md §Item 1).
- **Novo:** `website/app/error.tsx` (Error Boundary de rota, Client Component).
- Testes: `website/components/FilterBar.test.tsx`, `website/app/page.test.tsx`, novo
  `website/e2e/filter-bar-price.spec.ts` (Playwright).

## Não há contrato de API novo
Nenhuma rota do backend (`PublicController`) muda. Os parâmetros `minPrice`/`maxPrice` já existem
e continuam com o mesmo formato (`number`, querystring). Nenhuma migration, nenhuma mudança de
schema.

## Padrões obrigatórios

### Estado do slider de preço (item 2 + item 3)
- Estado local (`useState`) de "rascunho" para `minPrice`/`maxPrice` — inicializado a partir da
  URL, mas **não** commitado na URL a cada `onChange` do range.
- Commit para a URL (via `router.replace`, nunca `router.push`, para não empilhar histórico a
  cada ajuste de preço) acontece em:
  - soltura do gesto do slider (`onPointerUp`/`onMouseUp`/`onTouchEnd`/`onKeyUp`), **e/ou**
  - debounce de 200-300ms após a última mudança (rede de segurança cross-browser).
  Implementação exata (qual dos dois, ou ambos) é decisão do dev — o critério de aceite é
  observável: nenhuma sequência de arrasto rápido gera mais que uma pequena constante de chamadas
  de navegação (não uma por frame).
- Antes de commitar: `minPrice <= maxPrice` sempre garantido (clamp ou bloqueio + mensagem —
  ver validação abaixo). Nunca escrever na URL um par invertido.
- Os inputs de texto (novos, item 3) leem/escrevem o **mesmo** estado local de rascunho — nunca
  duas fontes de verdade divergentes entre slider e texto na tela (CA 3.3).

### Validação (item 3, CA 3.4-3.7)
| Caso | Comportamento |
|---|---|
| min > max | Bloquear commit à URL; mensagem de erro visível junto aos campos (ex.: "O valor mínimo não pode ser maior que o máximo") |
| valor negativo | Não commitar valor negativo; normalizar para 0 ou bloquear com mensagem — decisão do dev, mas nunca silencioso (algum feedback visual) |
| vazio / não numérico | Não lança exceção; não commita filtro inválido; reverte ao último valor válido ao perder foco (comportamento simples e previsível) |
| fora dos limites do catálogo (`PRICE_MIN`/`PRICE_MAX`, hoje `0`/`5000` — constantes do componente) | Clamp ao limite mais próximo, sem erro |

### Error Boundary (`app/error.tsx`)
Client Component (`'use client'`) padrão do Next.js App Router: recebe `error: Error & { digest?:
string }` e `reset: () => void`; renderiza mensagem amigável (reutilizar tokens de
`filter-bar.css`/`tokens.css`, sem novo design system) + botão que chama `reset()`. Não precisa de
lógica adicional — é rede de segurança genérica da rota `app/`, não específica do slider.

## Item 1 — checklist de remoção sem código órfão
- `FilterBar.tsx`: `DISCOUNT_OPTIONS`, `DiscountGroup`, `minDiscount` de `RESTRICTIVE_KEYS`,
  `handleDiscountToggle`, leitura de `searchParams.get('minDiscount')`, pílula de desconto em
  `Pills()`, as 2 chamadas de `<DiscountGroup />` (linha do row desktop e do drawer mobile).
- `filter-bar.css`: `.filter-bar__discount-group`, `.filter-bar__discount-btn`,
  `.filter-bar__discount-btn--active` e seus estados `:hover`/`:active`.
- `page.tsx`: `minDiscount` de `HomePageProps.searchParams`, `buildFilters`,
  `buildPaginationQuery`, `hasActiveFilters`.
- `lib/api.ts`: `DealFilters.minDiscount` e o `if (filters?.minDiscount !== undefined)` em
  `fetchDeals` — remover **somente se** nenhum outro ponto do `website/` os referencia após a
  limpeza acima (checar com grep antes de remover; se o backend ainda aceitar o parâmetro, não há
  problema em não enviá-lo mais — a API trata como ausente).
- Testes: remover/ajustar todos os casos de `minDiscount` em `FilterBar.test.tsx` e
  `page.test.tsx` (não deixar teste quebrado nem `it.skip`).

## Teste e2e obrigatório (item 2)
`website/e2e/filter-bar-price.spec.ts` (Playwright, `test:visual`):
1. Reproduzir o crash na versão **antes** da correção (ou documentar a reprodução em texto no PR
   se o dev preferir não deixar um teste que falha propositalmente no histórico — mínimo aceitável:
   evidência escrita no PR de que a hipótese de causa raiz foi confirmada) simulando um arrasto
   rápido: disparar múltiplos eventos (`page.locator(...).evaluate` + `dispatchEvent('input', ...)`
   em loop apertado, ou `mouse.move` em muitos passos pequenos e rápidos) suficientes para
   ultrapassar o throttle do browser (na ordem de 100+ eventos em poucos segundos).
2. Após a correção: mesmo gesto não deve navegar para página de erro — assert que
   `page.locator('[data-testid="filter-bar"]')` (ou `deals-grid`) segue presente/visível, sem texto
   de erro genérico do Next.js.
3. Cobrir também CA 2.1-2.3 (arrasto lento, clique único, extremos) se ainda não cobertos.

## Cobertura de testes
Manter o threshold já configurado no projeto (`website/package.json`/`jest.config`). PR não pode
reduzir cobertura de `FilterBar.tsx`.
