# Tasks — ISSUE-230: Revisar filtros da tela de produtos do site público

## T-01 — Remover filtro de desconto mínimo
**Sub-issue:** (preenchida após `gh issue create`)

### O que fazer
Remover por completo o seletor "Desconto mínimo" (10%+/30%+/50%+) do `FilterBar.tsx` e todo código
relacionado exclusivamente a ele (state, handler, CSS, referências em `page.tsx`/`lib/api.ts`) —
ver checklist completo em `especificacao-tecnica.md` §"Item 1 — checklist de remoção".

### Critérios de aceite (Given/When/Then)
- CA 1.1: seletor não é exibido, layout íntegro (desktop e mobile/drawer).
- CA 1.2: sem código órfão (state, handlers, CSS, referências em `page.tsx`/`lib/api.ts`/testes).

### Contexto técnico
- `especificacao-tecnica.md` §"Item 1"
- `design.md` §"Item 1 — Remoção do filtro de desconto mínimo"
- Arquivos: `website/components/FilterBar.tsx`, `website/app/styles/filter-bar.css`,
  `website/app/page.tsx`, `website/lib/api.ts`, `website/components/FilterBar.test.tsx`,
  `website/app/page.test.tsx`
- Stack: Next.js 14, React 18, TypeScript, Jest + Testing Library

---

## T-02 — Corrigir bug do slider de preço + campos de digitação min/max
**Sub-issue:** (preenchida após `gh issue create`)

### O que fazer
1. Investigar/confirmar a causa raiz documentada em `design.md` §"Investigação do bug do item 2"
   (hipótese: rajada de `router.push()` sem debounce durante arrasto rápido excede o throttle de
   `history.pushState` do browser → `SecurityError` não tratado → sem `error.tsx` → página de erro
   genérica). Escrever teste e2e Playwright que reproduz e depois valida a correção (ver
   `especificacao-tecnica.md` §"Teste e2e obrigatório").
2. Corrigir: estado local de rascunho para o slider, commit à URL via `router.replace` no
   soltar do gesto e/ou debounce (não a cada `onChange`), clamp de `minPrice <= maxPrice`.
3. Criar `website/app/error.tsx` (Error Boundary de rota) como defesa em profundidade.
4. Adicionar campos `<input>` numéricos de preço min/max, sincronizados nos dois sentidos com o
   slider (mesmo estado local de rascunho), com validação completa (CA 3.4-3.7).
5. Documentar a causa raiz confirmada no PR (obrigatório — CA 2.5).

### Critérios de aceite (Given/When/Then)
- CA 2.1, 2.2, 2.3: arrasto lento, clique único, valores extremos — sem erro.
- CA 2.4: arrasto rápido não navega para página de erro; gesto responde consistentemente ou, no
  mínimo, não trava; qualquer condição inesperada é tratada de forma controlada.
- CA 2.5: causa raiz documentada (PR/design.md/comentário técnico); correção resolve a causa raiz,
  não apenas suprime o sintoma.
- CA 3.1, 3.2: digitar min/max move o slider e aplica o filtro.
- CA 3.3: arrastar o slider atualiza os campos de texto; nunca divergem na tela.
- CA 3.4: min > max bloqueado com mensagem clara.
- CA 3.5: valor negativo não aplicado, com indicação ao usuário.
- CA 3.6: entrada vazia/não numérica não gera exceção nem filtro inválido.
- CA 3.7: valor fora dos limites reais do catálogo tratado sem erro (clamp ou lista vazia).

### Contexto técnico
- `especificacao-tecnica.md` §"Padrões obrigatórios", §"Validação", §"Error Boundary", §"Teste
  e2e obrigatório"
- `design.md` §"Investigação do bug do item 2 (causa raiz)", §"Item 3 — Campos de texto min/max"
- Arquivos: `website/components/FilterBar.tsx`, novo `website/app/error.tsx`,
  `website/components/FilterBar.test.tsx`, novo `website/e2e/filter-bar-price.spec.ts`
- Stack: Next.js 14 (App Router), React 18, TypeScript, Jest + Testing Library, Playwright
  (`npm run test:visual`)
- **Nota de dependência:** T-01 e T-02 tocam o mesmo arquivo (`FilterBar.tsx`) em regiões
  diferentes (`DiscountGroup` vs `PriceGroup`) — sem overlap funcional, mas o LT funde
  sequencialmente (nunca dois merges de sub-issue em paralelo no mesmo repo).

## Repo / branches
- Repo: `repos/omuletachou`. Branch base: `desenv`.
- `feature/ISSUE-<NNN>-descricao` onde NNN = número da sub-issue (T-01 e T-02, respectivamente).
