# UX/UI Spec — ISSUE-228 / Sub-issue #245: Filtros + cards agregados + tabela na tela Reports

> Contexto técnico: `especificacao-tecnica.md` (docs_path) e `design.md` (openspec_path). Este
> documento define **apenas** a camada visual — layout, componentes, estados, responsividade — para
> o dev (`stack:angular`) implementar T-04. Contrato de dados já fechado, não é re-decidido aqui.

## 0. Nota sobre o Design System no Figma

O arquivo Figma da squad (`yi6YkNAy9HfHus2oiPi3G7`) contém apenas o **kit de tokens genérico**
(paletas de cor, escala tipográfica, exemplos soltos de botão/cursor) — não há mockups dedicados
das telas `Products`/`Jobs`/`Reports` do dashboard. Não há, portanto, imagem de referência para
baixar. Diante disso, a spec abaixo:
- Reaproveita os **tokens de cor/tipografia** do Figma onde fazem sentido como acento (paletas
  `Iris`/`Fuschia`), sem inventar uma nova linguagem visual.
- Ancora a estrutura de componentes no **Angular Material já em uso no dashboard** (confirmado em
  `especificacao-tecnica.md` §4: "Angular Material, ng2-charts, Reactive Forms" — o mesmo padrão já
  usado em `ProductsComponent`/`JobsComponent`/no restante de `reports.component.ts`). Este projeto
  **não usa** radix-ng/Tailwind — a base técnica real é Angular Material, então o mapeamento abaixo
  amarra a componentes `mat-*` (equivalente ao papel que radix-ng/tailwind.config teriam num stack
  diferente): primitivos versionados do design system + tokens do tema Material, nunca CSS ad-hoc.
- Tokens de cor extraídos do Figma para uso pontual (chips de status, barras de breakdown):

| Token Figma | Hex | Uso sugerido nesta feature |
|---|---|---|
| Iris/100 | `#5D5FEF` | Acento primário (barras de breakdown, botão "Aplicar"/foco de filtro) — mapear para `primary` do tema Material já configurado, não um novo azul |
| Fuschia/100 | `#EF5DA8` | Acento secundário/destaque (badge "N filtros ativos") — mapear para `accent` do tema |
| Header text `#0E0E2C` | `#0E0E2C` | Títulos dos cards (`Header 2`, Work Sans 700 20px) |
| Body `Work Sans 400 13px` | — | Texto de suporte (contagens, legendas) |

Dev deve usar as variáveis SCSS do tema Material **já existentes** em `dashboard/src/styles` (não
criar paleta nova) — a tabela acima é só o de-para conceitual com o Figma, não uma paleta nova a
declarar.

## 1. Decisão de UX que resolve a ambiguidade "tabela/gráfico" (proposal §4, deixada para UX/técnico)

**Decisão: tabela paginada para o detalhe + gráfico de barras embutido nos cards de breakdown
(não um gráfico separado abaixo da tabela).**

Motivo: o endpoint de detalhe (`GET /api/products`, T-03) devolve **linhas individuais de produto**
paginadas — dado columnar, não agregado. Um gráfico de barras exige dado agregado, que já é
exatamente o que o endpoint de summary (`GET /api/reports/products/summary`, T-02) devolve. Logo:
- **Cards de resumo** = onde a dimensão "gráfico" vive — cada card de breakdown (Plataforma,
  Categoria, Status, Subcategoria) renderiza uma **mini barra horizontal proporcional** ao lado da
  contagem (mesmo princípio visual de um bar chart, sem precisar de um segundo componente
  `ng2-charts` novo, custo zero de request extra).
- **Tabela detalhada** = lista paginada de produtos individuais, com `mat-table` + `mat-paginator`
  (mesmo padrão da tela `Products`), não um gráfico.

Isso evita duplicar visualmente o mesmo dado agregado em dois lugares (card + gráfico separado) e
mantém a tabela fazendo o que só ela pode fazer: mostrar o produto individual.

## 2. Layout geral do bloco (dentro de `reports.component.html`, abaixo do bloco existente)

```
┌─────────────────────────────────────────────────────────────────────┐
│  [Bloco existente: cards Hoje/Semana/Mês + gráfico "Publicações      │
│   por rede" — inalterado, T-04 não toca aqui]                        │
├─────────────────────────────────────────────────────────────────────┤
│  Relatório de produtos publicados                    (Header 2)      │
│  ┌───────────────────────────────────────────────────────────────┐  │
│  │ FILTROS (mat-card, elevação 1)                                 │  │
│  │ [Categoria ▾] [Subcategoria ▾] [Plataforma ⊞⊞⊞] [Status ▾]     │  │
│  │ [Data de coleta: de __/__/__ até __/__/__]      [Limpar filtros]│  │
│  │ Chips de filtros ativos: (Categoria: Eletrônicos ×) (Plataf: ML ×)│
│  └───────────────────────────────────────────────────────────────┘  │
│  ┌──────────┬──────────────┬──────────────┬───────────┬───────────┐ │
│  │  Total   │ Por Plataforma│ Por Categoria│ Por Status│Por Subcat.│ │
│  │  (hero)  │ (mini-barras) │(mini-barras) │(mini-bar.)│(mini-bar.)│ │
│  └──────────┴──────────────┴──────────────┴───────────┴───────────┘ │
│  ┌───────────────────────────────────────────────────────────────┐  │
│  │ TABELA DETALHADA (mat-table + mat-paginator)                   │  │
│  │ Produto | Categoria | Subcategoria | Plataforma | Status |     │  │
│  │ Preço | Data de coleta                                         │  │
│  └───────────────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────────┘
```

## 3. Filtros

Container: `mat-card` (mesma elevação/padding usado nos outros cards da tela), título "Filtros"
(`Header 2` ou `mat-card-title` padrão do dashboard).

### 3.1 Comportamento de aplicação — auto-apply, sem botão "Aplicar"

CA 2.9 exige recálculo "sem exigir ação adicional além da própria mudança do filtro". Decisão:
**sem botão "Aplicar filtros"** — cada controle dispara o recálculo sozinho:
- `mat-select` (Categoria, Subcategoria, Plataforma se for select, Status): `selectionChange` dispara
  na hora (não precisa debounce, é uma única escolha discreta).
- `mat-button-toggle-group`/chips (Plataforma, se optar por toggle em vez de select — ver 3.2):
  dispara no `change`.
- Faixa de data (`mat-date-range-input`): dispara **só quando as duas datas (início e fim) estão
  preenchidas** (evento `dateRangeChange`/ao fechar o range picker com ambas selecionadas) — nunca
  dispara com só uma data, para não gerar uma faixa inválida no meio da seleção.
- Toda chamada de recálculo passa por `debounceTime(150)` + `distinctUntilChanged` no
  `filterForm.valueChanges` do lado do componente, absorvendo cliques rápidos em sequência sem
  gerar requisições redundantes — detalhe de implementação do dev, mas o comportamento percebido
  deve ser "muda o filtro → resultado atualiza sozinho, sem lag perceptível de UI" (heurística de
  visibilidade do status, §7).

### 3.2 Campos

| Campo | Componente Material | Opções/Comportamento |
|---|---|---|
| Categoria | `mat-select` | Lista de categorias existentes (mesma fonte de dados/endpoint já usado para popular o filtro de Categoria na tela `Products`, reaproveitar — não duplicar chamada). Placeholder "Todas as categorias". |
| Subcategoria | `mat-select` | Lista de subcategorias existentes, **independente** do filtro de Categoria (CA 2.2 permite filtrar só por Subcategoria, sem exigir Categoria antes — não implementar cascata obrigatória). Placeholder "Todas as subcategorias". |
| Plataforma | `mat-button-toggle-group` (3 opções fixas: Mercado Livre, Amazon, Shopee) + opção implícita "Todas" (nenhum toggle selecionado) | Toggle exclusivo (single-select). Ícone/cor por plataforma se já existir padrão visual em `Products` (reaproveitar os mesmos ícones de plataforma já usados na tabela de Products, não criar novos). |
| Status | `mat-select` | Lista de status do domínio (mesmos valores já usados na tela `Products`). **Sem valor pré-selecionado visualmente** — o placeholder mostra "Publicados (padrão)" para deixar claro ao operador que a ausência de escolha já filtra por `Published` (CA 1.1/2.4), evitando a leitura errada de "sem filtro = todos os status". |
| Faixa de data de coleta | `mat-date-range-input` (`mat-start-date` + `mat-end-date`, mesmo componente Material padrão) | Label "Data de coleta" com ícone de tooltip (ℹ) ao lado explicando "Data em que o produto foi coletado pelo pipeline — não é a data de publicação" (evita ambiguidade citada no proposal §"Casos de uso"). Validação: data final não pode ser anterior à inicial — Material já bloqueia isso nativamente no range picker. |
| Limpar filtros | `button mat-stroked-button` | Sempre visível (não só quando há filtro ativo, para não gerar layout shift) mas **`disabled` quando nenhum filtro está aplicado** (estado disabled real, não só visual — evita ação sem efeito, heurística de prevenção de erro). Ao clicar: reseta `filterForm`, dispara recálculo com filtros vazios (CA 2.8). |

### 3.3 Resumo de filtros ativos (chips)

Abaixo da linha de controles, uma linha de `mat-chip-set` mostrando cada filtro ativo como
`mat-chip` removível (ex.: `Categoria: Eletrônicos ✕`, `Plataforma: Mercado Livre ✕`). Clicar no
`✕` de um chip remove **só aquele filtro** e recalcula — atalho que evita ter que abrir o
`mat-select` de novo (heurística "flexibilidade e eficiência de uso", §7). Linha inteira some
(não renderiza vazia) quando não há filtro ativo.

### 3.4 Estados do bloco de filtros

| Estado | Comportamento visual |
|---|---|
| Default (sem filtro) | Todos os controles em placeholder/vazio; "Limpar filtros" disabled; sem chips. |
| Filtro aplicado | Controle(s) mostram o valor selecionado; chip(s) correspondente(s) aparecem; "Limpar filtros" enabled. |
| Carregando (requisição em voo) | `mat-progress-bar` modo `indeterminate` fino no topo do bloco de filtros (não bloqueia interação — operador pode already trocar outro filtro, que cancela/substitui a requisição anterior via `switchMap`, não `forkJoin` acumulando race conditions — detalhe de implementação a cargo do dev, mas o requisito visual é: nunca mostrar resultado de uma requisição desatualizada). Controles permanecem `enabled` (não travar o formulário). |
| Erro (requisição falhou) | Ver §6 "Estado de erro do bloco" — os controles de filtro continuam usáveis (o erro é do resultado, não do formulário) para permitir nova tentativa direta trocando o filtro, além do botão "Tentar novamente". |
| Disabled | N/A para os selects/toggle em si (sempre interativos) — único disabled real é "Limpar filtros" sem filtro ativo (3.2). |
| Readonly | N/A — não há modo somente-leitura nesta feature. |

## 4. Cards de resumo agregados

Grid responsivo (CSS Grid, `grid-template-columns: repeat(auto-fit, minmax(220px, 1fr))`, `gap`
consistente com o espaçamento já usado entre os cards existentes "Hoje/Semana/Mês"), 5 cards:

### 4.1 Card "Total"
- `mat-card` com número grande (`Header 1`-like, mas usando a escala tipográfica já aplicada nos
  cards existentes da tela — reaproveitar a classe/estilo do card "Hoje", não criar tipografia
  nova) + legenda "produtos publicados" (ou o status filtrado, ex. "produtos com status Pendente"
  quando Status ≠ Published, para refletir CA 2.4 sem confundir o operador).
- Estado vazio: número `0` (nunca escutar "sem dado" — é uma contagem, zero é uma resposta válida
  e clara, CA 1.3).

### 4.2 Cards de breakdown (Por Plataforma / Por Categoria / Por Status / Por Subcategoria)
- `mat-card` com título (nome da dimensão) + lista de linhas `dimensão — contagem`, cada linha com
  uma barra horizontal preenchida proporcionalmente a `count / max(count na lista)` (cor `primary`
  do tema, opacidade decrescente ou única cor sólida — decisão de implementação, manter
  consistência entre os 4 cards).
- Ordenação: decrescente por contagem (dimensão mais frequente primeiro) — facilita leitura rápida
  (heurística "reconhecimento, não recall").
- **Truncamento:** se a lista tiver mais de 5 itens (esperado para Categoria/Subcategoria, que
  podem ter muitos valores), mostrar os 5 primeiros + linha "+ N outras" clicável que expande a
  lista completa inline (`mat-expansion-panel` ou toggle simples, sem modal) — evita cards
  gigantes quebrando o grid.
- Estado vazio (nenhum item na lista de breakdown — mesma combinação de filtros sem produtos): card
  mostra mensagem curta "Nenhum dado" em vez de lista vazia em branco (CA 2.7).

### 4.3 Estados do bloco de cards

| Estado | Comportamento |
|---|---|
| Loading | Skeleton (retângulos cinza-claro pulsantes, `@angular/material` não tem skeleton nativo — usar `div` com classe de shimmer já existente no projeto se houver, senão CSS simples) no lugar do número/barras, mesmo formato do card final para não gerar layout shift. |
| Sucesso | Números/barras atualizados. Transição suave (fade, ~150ms) na troca de valor — sinaliza "isto é novo", sem exagerar em animação. |
| Vazio (CA 1.3/2.7) | Total = `0`; cards de breakdown com "Nenhum dado"; **sem** ícone de erro, cor neutra (não vermelho) — é um resultado válido, não uma falha. |
| Erro | Cards não renderizam número velho — ver §6, o bloco inteiro (cards + tabela) entra em estado de erro compartilhado. |
| Disabled/Readonly | N/A. |

## 5. Tabela detalhada

`mat-table` dentro de `mat-card`, colunas (nomes de exibição, mapeadas ao `ProductListItemDto` já
existente + `Subcategory` novo — reaproveitar exatamente as colunas/formatação já usadas na tabela
da tela `Products` para consistência, este dev não inventa uma tabela do zero visualmente):

| Coluna | Formatação |
|---|---|
| Produto | Nome/título do produto (truncar com `...` + `title` tooltip se muito longo) |
| Categoria | Texto simples |
| Subcategoria | Texto simples (novo campo) — `—` quando `null` |
| Plataforma | Badge/ícone (mesmo padrão já usado em `Products`) |
| Status | `mat-chip` colorido (reaproveitar o mapeamento de cor por status já existente na tela `Products` — Published=sucesso/verde, Pending=alerta/âmbar, Error=erro/vermelho, demais=neutro — não redefinir aqui, só reaproveitar) |
| Preço | Formatado em `R$` (mesma pipe já usada em `Products`) |
| Data de coleta | `CreatedAt` formatado `dd/MM/yyyy` |

- `mat-paginator`: mesmas opções de `pageSize` já configuradas em `ProductsComponent` (reaproveitar
  o mesmo default, não inventar um novo). Trocar de página **não** recalcula os cards (design.md
  §2.1) — só a tabela mostra loading local (spinner pequeno sobre a área da tabela, cards
  permanecem estáveis).
- Ordenação: `ORDER BY CreatedAt DESC` fixo (mesmo padrão do backend, design.md §3) — sem
  `mat-sort` interativo nesta v1 (não pedido nos critérios de aceite; não inventar escopo).

### 5.1 Estados da tabela

| Estado | Comportamento |
|---|---|
| Loading (filtro aplicado, primeira página) | Skeleton de linhas (5-8 linhas cinza pulsante) no lugar das linhas de dado. |
| Loading (troca de página, filtro igual) | Spinner pequeno centralizado sobre a tabela (overlay semi-transparente), cards não afetados. |
| Sucesso | Linhas populadas, paginador ativo. |
| Vazio (CA 1.3/2.7) | `mat-card` com ícone neutro (ex. caixa vazia) + texto "Nenhum produto encontrado com os filtros aplicados" + botão secundário "Limpar filtros" (atalho direto para sair do estado vazio, heurística de controle e liberdade do usuário) — só aparece esse CTA quando há filtro ativo; se vazio sem filtro (nenhum produto Published no sistema), mensagem é "Nenhum produto publicado no momento" sem CTA de limpar (não faria sentido). |
| Erro | Ver §6. |
| Disabled/Readonly | N/A. |

## 6. Estado de erro do bloco (cards + tabela compartilham, CA 5.1)

Quando `forkJoin` (summary + list) falha (rede/timeout/5xx) — em qualquer uma das duas chamadas:
- Cards e tabela **não mostram o dado da consulta anterior** (CA 5.1 explícito: "não exibe
  silenciosamente o dado antigo como se fosse atualizado"). A área inteira do bloco de resultado
  (cards + tabela) é substituída por um estado de erro único:
  - `mat-card` com ícone de alerta (cor `warn` do tema), texto "Não foi possível carregar o
    relatório. Verifique sua conexão e tente novamente." e botão primário "Tentar novamente" que
    reexecuta a última combinação de filtros aplicada.
- Filtros permanecem visíveis e usáveis (§3.4) — o operador pode tentar de novo tanto pelo botão
  quanto trocando o filtro.
- Erro é local ao bloco — nunca um `alert()`/modal bloqueante, nunca derruba o resto da tela
  `Reports` (cards Hoje/Semana/Mês continuam funcionando independente, CA 1.2).

## 7. Heurísticas de Nielsen → critérios verificáveis

| Heurística | Critério verificável nesta feature |
|---|---|
| Visibilidade do status do sistema | Toda troca de filtro produz feedback visual em ≤ 300ms (progress bar/skeleton) — nunca uma tela "congelada" sem indicação entre a ação do operador e o resultado. Loading da tabela (troca de página) é visualmente distinto do loading de recálculo completo (cards+tabela), para o operador entender o que está sendo atualizado. |
| Correspondência sistema-mundo real | Nomenclatura idêntica à já usada em `Products` (Categoria/Subcategoria/Plataforma/Status) — sem sinônimos novos. Tooltip explícito no filtro de data esclarecendo "data de coleta ≠ data de publicação". |
| Controle e liberdade do usuário | "Limpar filtros" sempre acessível; chips individuais removíveis; botão "Tentar novamente" no erro; nenhuma ação destrutiva sem volta fácil (não há ação destrutiva nesta feature — só consulta). |
| Consistência e padrões | Componentes, cores de status, formatação de preço/data idênticos aos já usados em `ProductsComponent` — zero componente visual novo sem precedente no dashboard. |
| Prevenção de erros | Faixa de data não permite fim < início (bloqueio nativo do `mat-date-range-input`); "Limpar filtros" desabilitado quando não há o que limpar. |
| Reconhecimento em vez de recall | Chips mostram os filtros ativos sempre visíveis (operador não precisa reabrir cada `mat-select` para lembrar o que está filtrado); breakdowns ordenados por relevância (contagem desc). |
| Flexibilidade e eficiência de uso | Remoção de filtro individual via chip `✕` (atalho) além do "Limpar filtros" geral. |
| Estética e design minimalista | Breakdowns longos truncados a 5 itens + expandir sob demanda — cards não crescem descontroladamente. |
| Ajudar a reconhecer/diagnosticar/recuperar de erros | Mensagem de erro específica e acionável (não "erro genérico") + retry. |
| Ajuda e documentação | Tooltip inline no filtro de data (não requer documentação externa para entender o campo mais ambíguo do form). |

## 8. Responsividade

Breakpoints alinhados ao `BreakpointObserver`/grid já padrão do Angular Material (mobile <599px,
tablet 600–959px, desktop ≥960px):

| Breakpoint | Filtros | Cards | Tabela |
|---|---|---|---|
| Desktop (≥960px) | Linha única (6 controles + "Limpar filtros" à direita), chips em linha abaixo | Grid 5 colunas (`auto-fit minmax(220px,1fr)`) | Todas as colunas visíveis, paginador padrão |
| Tablet (600–959px) | Grid 2 colunas (`mat-select`s e toggle quebram em 2 linhas), "Limpar filtros" ocupa linha própria à direita | Grid 2–3 colunas | Colunas mantidas; se não couber, `overflow-x: auto` na tabela (scroll horizontal) em vez de esconder colunas silenciosamente |
| Mobile (<600px) | Bloco de filtros colapsado por padrão em `mat-expansion-panel` ("Filtros" com badge de contagem de filtros ativos, ex. "Filtros (2)"), expande ao toque; controles empilhados verticais dentro do panel | Coluna única (cards empilhados) | Tabela mantém scroll horizontal (não vira lista de cards nesta v1 — fora de escopo, mesma decisão que a tabela de `Products` já usa hoje, se aplicável; reaproveitar o padrão existente, não inventar um novo) |

## 9. Fluxo de navegação

Não há rota nova nem modal — tudo acontece dentro de `ReportsComponent`, já montado sob o Layout
existente (Header + Sidenav), conforme "Contrato de componentes globais" do `design.md`.

```
Operador no Sidenav → clica "Reports" (rota já existente)
  → ReportsComponent carrega
      → Bloco existente (Hoje/Semana/Mês + gráfico) carrega como hoje, sem mudança
      → Novo bloco "Relatório de produtos publicados" carrega em paralelo
          (forkJoin summary+list, status=Published default)
          → sucesso: cards + tabela populados
          → vazio: cards zerados + tabela com mensagem
          → erro: card de erro único, filtros usáveis
  → Operador ajusta filtro(s) (select/toggle/date range/chip ✕)
      → recálculo automático (sem navegação, sem reload) — permanece na mesma tela
  → Operador troca de página da tabela
      → só a tabela recarrega, cards e filtros inalterados
  → Operador clica "Limpar filtros"
      → volta ao estado inicial (universo completo Published)
```

Nenhum deep-link de estado de filtro na URL é exigido pelos critérios de aceite desta versão — não
implementar (evitar escopo não pedido).

## 10. Rastreabilidade aos critérios de aceite

| CA | Onde a spec cobre |
|---|---|
| 1.1 | §2 layout padrão sem filtro; §4.1 Total; §3.2 Status placeholder "Publicados (padrão)" |
| 1.2 | §2 bloco existente preservado, não tocado |
| 1.3 | §4.1/4.2/5.1 estado vazio sem erro |
| 2.1–2.5 | §3.2 cada campo de filtro |
| 2.6 | §3.1 auto-apply cumulativo (todos os campos do mesmo `filterForm`, combinação AND é responsabilidade do backend já especificada — a UI só envia todos os valores preenchidos) |
| 2.7 | §4.3/§5.1 estado vazio compartilhado cards+tabela |
| 2.8 | §3.2 "Limpar filtros" |
| 2.9 | §3.1 auto-apply sem botão |
| 4.1 | Nenhum botão de exportar/imprimir especificado em nenhuma seção (ausência intencional) |
| 5.1 | §6 estado de erro compartilhado, sem dado antigo, com retry |

---

Handoff técnico: dev (`stack:angular`, sub-issue #245) implementa a partir desta spec + contrato de
dados em `especificacao-tecnica.md` §4. Qualquer decisão de negócio nova que surgir na implementação
(ex.: mudança de escopo de campos) volta ao PM, não é decidida ad-hoc pelo dev.
