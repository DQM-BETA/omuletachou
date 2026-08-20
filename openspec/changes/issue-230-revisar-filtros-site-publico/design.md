# Design — ISSUE-230: Revisar filtros da tela de produtos do site público

## Visão geral
Mudança contida no componente `FilterBar` (`website/components/FilterBar.tsx`, Next.js 14 App Router,
Client Component) e na Server Component `website/app/page.tsx` que o consome. Sem novas integrações,
sem mudança de contrato de API além do que já existe para `minPrice`/`maxPrice`.

## Investigação do bug do item 2 (causa raiz)

### Reprodução
Reprodução ao vivo (rodar o app) está fora do escopo de ferramentas deste agente (LT não executa
código de aplicação). A causa raiz abaixo foi determinada por **tracing estático completo** do
caminho de execução (evento → estado → rede → renderização), cobrindo os mesmos pontos que uma
reprodução ao vivo confirmaria. **A sub-issue do bugfix (T-02) exige, como critério de aceite, um
teste e2e Playwright que reproduz o crash na versão atual (arrasto rápido simulado via múltiplos
eventos `pointermove`/`input` em sucessão) antes de aplicar a correção, e comprova a ausência do
crash depois — essa é a confirmação empírica que valida (ou refuta) a hipótese abaixo.**

### Cadeia causal identificada
1. `PriceGroup` usa **dois `<input type="range">` controlados** cujo `value` vem direto da URL
   (`searchParams` via `useSearchParams`), não de estado local do componente.
2. Cada evento `onChange` do range chama `handleMinPriceChange`/`handleMaxPriceChange` →
   `updateParams()` → **`router.push()` síncrono, sem debounce/throttle** (`FilterBar.tsx:151-157,
   116-130`).
3. Arrastar um `<input type="range">` dispara dezenas de eventos `input`/`change` por segundo
   (um por frame de movimento do mouse/touch). Um arrasto rápido gera uma rajada de chamadas
   `router.push()` em poucos segundos.
4. `router.push()` do App Router do Next.js, para uma navegação client-side de mesma rota (só
   querystring muda), aciona `history.pushState()` do browser.
5. **Chrome/Edge (Chromium) limitam `history.pushState`/`replaceState` a ~100 chamadas por 10
   segundos** (throttle de proteção contra abuso, desde Chrome 89). Excedido o limite, a chamada
   lança `SecurityError: Attempt to use history.pushState() more than N times per 10 seconds`.
   Um arrasto rápido e contínuo de ~1-2s facilmente ultrapassa essa contagem (múltiplos `onChange`
   por frame × handles do slider).
6. Essa exceção é lançada **de forma síncrona dentro do handler de evento do React**
   (`onChange` → `updateParams` → `router.push`) — não é capturada em lugar nenhum do código.
7. `website/app/` **não tem `error.tsx` nem `global-error.tsx`** (confirmado — nenhum arquivo desse
   nome existe na árvore `app/`). Sem um Error Boundary de rota, uma exceção não tratada que escapa
   do fluxo de eventos/render do React derruba a árvore de componentes montada; o Next.js cai no
   **fallback genérico de erro** ("Application error: a client-side exception has occurred" em
   produção) — exatamente a "página de erro sem mensagem clara" relatada pelo Gerente.

### Por que só ao arrastar **rápido**
- Um clique único no trilho (CA 2.2) ou um arrasto lento (CA 2.1) geram poucos eventos `onChange`
  (às vezes um só), muito abaixo do limiar de throttle — nunca aciona o `SecurityError`.
- Arrastar rápido é o único gesto que gera dezenas de `router.push()` em sequência apertada,
  batendo no limite do browser.

### Por que não é (só) o cruzamento min/max
Os dois `<input type="range">` são independentes (cada um com `min={0} max={5000}`, sem
`clamp` entre si) — é possível o handle de mínimo ultrapassar o de máximo mesmo com arrasto lento.
Isso já é um problema de UX/dado (endereçado no item 3, validação), mas **não é, isoladamente, a
causa do crash**: um `minPrice > maxPrice` na URL apenas produz uma faixa visual "invertida" no CSS
(`left`/`right` da `.filter-bar__price-range`) e, se refletido na Server Component (`page.tsx`),
uma resposta da API de listagem vazia ou 400 — que **seria** um erro tratável (try/catch +
mensagem), não o crash de router genérico observado. A causa determinante do crash relatado
("arrastar rápido") é o throttle do History API do item acima.

### Correção (T-02) — não silenciar o sintoma
A correção ataca a causa raiz, não apenas envolve a chamada num try/catch genérico:
1. **Desacoplar o valor visual do slider (durante o arrasto) da navegação/URL.** O `<input
   type="range">` passa a refletir um **estado local** (`useState`) atualizado em todo `onChange`
   (sem custo — é só re-render local, não navegação). A navegação (`updateParams`/URL) só é
   disparada:
   - ao soltar o gesto (`onPointerUp`/`onMouseUp`/`onTouchEnd`/`onKeyUp`, cobrindo mouse, touch e
     teclado — a11y), e/ou
   - com **debounce** (ex.: 200-300ms de inatividade) como rede de segurança adicional para o
     caso de o navegador não disparar o evento de soltura de forma confiável em todos os browsers.
   Isso reduz o volume de `router.push()` de "um por frame de movimento" para "um por gesto
   completo (ou por pausa)" — ordens de magnitude abaixo do limiar de throttle do browser.
2. **Trocar `router.push()` por `router.replace()`** nas atualizações de preço (`minPrice`/
   `maxPrice`): ajustar a faixa de preço é refinamento contínuo do mesmo filtro, não deve empilhar
   uma entrada de histórico por ajuste (efeito colateral positivo, já que o usuário não quer
   apertar "voltar" dezenas de vezes para sair da tela após mexer no slider). `replace()` também
   está sujeito ao mesmo throttle do browser, mas como (1) já derruba drasticamente a frequência de
   chamadas, a combinação elimina o gatilho do bug.
3. **Clamping defensivo**: ao commitar o valor (soltar o gesto/digitar), garantir
   `minPrice <= maxPrice` no próprio componente antes de montar a URL (não depende só da correção
   de throttle — remove também a faixa visual invertida citada acima). Ver item 3 para a mesma
   regra aplicada aos campos de texto.
4. **Defesa em profundidade — `app/error.tsx`**: criar um Error Boundary de rota
   (`website/app/error.tsx`, Client Component conforme contrato do App Router) com uma mensagem
   amigável ("Algo deu errado. Tentar novamente.") e botão de reset. Isso não corrige a causa raiz
   (item 1-3 acima corrigem), mas é rede de segurança: qualquer exceção futura não tratada em
   Server/Client Component dessa árvore de rotas deixa de cair no fallback genérico sem mensagem —
   trata a classe inteira de sintoma ("página de erro sem mensagem clara"), não só esta ocorrência.
   Justificativa de incluir mesmo com a causa raiz corrigida: CA 2.4 exige also "se qualquer
   condição inesperada ocorrer... ela é tratada de forma controlada" — é o requisito de resiliência
   do item, não apenas o fix pontual.

## Item 1 — Remoção do filtro de desconto mínimo
Remover de `FilterBar.tsx`: `DISCOUNT_OPTIONS`, componente interno `DiscountGroup`, entrada
`'minDiscount'` de `RESTRICTIVE_KEYS`, leitura de `minDiscount` de `searchParams`,
`handleDiscountToggle`, o bloco de pílula de `minDiscount` em `Pills()`, e as duas chamadas de
`<DiscountGroup />` (desktop e drawer mobile). Remover `.filter-bar__discount-group`,
`.filter-bar__discount-btn` e variantes de `app/styles/filter-bar.css` (CSS morto). Em
`app/page.tsx`: remover `minDiscount` de `HomePageProps.searchParams`, `buildFilters`,
`buildPaginationQuery` e `hasActiveFilters`. Em `lib/api.ts`: `DealFilters.minDiscount` e o bloco
que o envia para a API **só** devem ser removidos se nenhum outro consumidor os usa (checar
dashboard/backend antes — se o backend/`PublicController` ainda expõe o parâmetro, tudo bem
mantê-lo lá; o que sai é só a UI e o encadeamento client→server que o alimenta a partir do
`website/`). Ajustar/remover os testes de `FilterBar.test.tsx` e `page.test.tsx` relativos a
`minDiscount`.

## Item 3 — Campos de texto min/max
Dois `<input type="number">` (ou `type="text" inputMode="numeric"` para melhor controle de
formatação — decisão do dev, ambos aceitáveis) ao lado/abaixo dos valores exibidos em
`.filter-bar__price-values`, reaproveitando os tokens de `filter-bar.css` (mesma altura/borda dos
`.filter-bar__dropdown-trigger`, sem CSS novo fora do padrão já estabelecido — não requer UX/UI
dedicado, é composição do design system existente). Compartilham o **mesmo estado local +
commit-on-blur/debounce** descrito na correção do item 2 (mesmo mecanismo, uma só fonte de verdade
para o valor "em edição" antes de ir para a URL) — os dois inputs de texto e os dois `range`
escrevem/leem o mesmo par de estados locais `[minPriceDraft, maxPriceDraft]`, sincronizados nos
dois sentidos com a URL. Validação (CA 3.4-3.7): min > max, negativo, não numérico/vazio, fora dos
limites do catálogo — todas tratadas no ponto de commit (blur/Enter/debounce), nunca deixando o
`fetch` do server component ser chamado com um filtro inválido; mensagem de erro visível junto aos
campos (não `alert()`/silencioso).

## Sub-issues (avaliação de split)
Itens 2 e 3 tocam a **mesma região de código** (`PriceGroup`, mesmo mecanismo de estado
local→commit→URL) — separar em sub-issues distintas criaria dependência forte e risco real de
conflito/retrabalho (quem fizer o item 3 precisaria reimplementar ou aguardar o mecanismo do item
2). Ficam numa única sub-issue (T-02). O item 1 é isolado (outro bloco de UI, `DiscountGroup`, sem
overlap de código com `PriceGroup`) — sub-issue própria (T-01), paralelizável sem risco.

## Fora de escopo / não afetado
- Backend (`PublicController`, `AfiliadoBot.*`) — sem mudança de contrato esperada; se o backend já
  valida `minPrice > maxPrice` com um 400, isso é aceitável e complementar (defesa em profundidade
  no servidor), mas a correção do bug do slider é 100% client-side.
- Dashboard Angular — não usa `FilterBar` do site público.
- Item 4 (busca inteligente) — Issue #260, sem dependência.
