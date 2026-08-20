# UX/UI Spec — Sub-issue #254: Tag de plataforma no `DealCard`

> Issue-pai #229 · Sub-issue #254 (`stack:nodejs`, task_id T-02) · Componente: `website/components/DealCard.tsx`
> Consumida pela sub-issue #253 (backend) apenas como referência de contrato — nenhuma alteração de backend aqui.

## 0. Nota sobre o Figma consultado
O arquivo do Design System (`yi6YkNAy9HfHus2oiPi3G7`) foi inspecionado e contém **apenas o boilerplate padrão de "team library" do Figma** (página tutorial "Start here", sem componentes/estilos publicados específicos do `omuletachou`/`DealCard`). Não há token de cor, tipografia ou componente de badge customizado para extrair de lá.
→ A fonte de verdade de consistência visual usada nesta spec são os **design tokens já em produção** no próprio `website/app/styles/deal-card.css`, conforme documentado pelo LT em `especificacao-tecnica.md`/`design.md`: `--color-neutral-*`, `--color-primary` (reservado para preço/CTA — não usar aqui), `--font-size-xs`, `--space-*`, `--radius-sm`, e o padrão de badge já existente `.deal-card__badge` (usado hoje para o percentual de desconto). O Dev deve reutilizar os valores literais desses tokens já presentes no arquivo — esta spec não inventa novos tokens.

## 1. Direção estética e decisão de posicionamento
- **Tag de texto, não ícone** (decisão do Gerente) — reforça reconhecimento pelo nome real da plataforma (heurística "Match entre sistema e mundo real"), sem depender de logo/ícone de marca (evita questões de uso de marca de terceiro sem licença).
- **Estilo neutro único, sem distinção de cor por plataforma.** Motivo: (a) CA 8 exige texto/estilo idêntico entre telas — um estilo único elimina qualquer ambiguidade de implementação; (b) "bem discreta" pede baixo contraste/baixa saliência — cores diferentes por marca (ex. amarelo Amazon, azul-claro ML, laranja Shopee) aumentariam saliência e competiriam visualmente com o preço/CTA, o que o Gerente/CA pedem para evitar; (c) evita a tag virar pseudo-filtro visual por cor (radar da decisão da #167 sobre não reintroduzir mecanismo de distinção/filtro).
- **Posição: acima da linha de preço, dentro do bloco `.deal-card__price`** (primeiro filho, antes do preço), não inline ao lado dos números do preço.
  - Por quê não inline ao lado do preço: em mobile, cards compactos já acomodam preço original (riscado), preço com desconto e badge de `%` na mesma área — colocar a tag de plataforma na mesma linha arrisca quebra/corte (violaria CA6). Empilhar a tag numa linha própria, acima do preço, garante largura total do card disponível ao texto, sem disputar espaço horizontal.
  - Ainda assim satisfaz "próxima ao preço" (CA1–3): é o elemento imediatamente adjacente ao bloco de preço, mesmo agrupamento visual.
  - Não fica sobre a imagem nem disputa espaço com o badge de desconto existente (`.deal-card__badge`, tipicamente overlay na imagem) — são elementos visualmente distintos, sem sobreposição.

## 2. Mapeamento enum → texto de exibição
Fonte do enum: `backend/src/AfiliadoBot.Domain/Enums/Platform.cs` (`Amazon | MercadoLivre | Shopee`), serializado como string bruta pela sub-issue #253 (`product.Platform.ToString()`).

| Valor bruto da API (`deal.platform`) | Texto exibido no card | Observação |
|---|---|---|
| `"Amazon"` | `Amazon` | Nome próprio da marca, grafia oficial |
| `"MercadoLivre"` | `Mercado Livre` | Espaço inserido — nome completo, não abreviar para "ML" (ver justificativa abaixo) |
| `"Shopee"` | `Shopee` | Nome próprio da marca, grafia oficial |
| `null` / `undefined` / ausente | *(tag não renderizada)* | CA 4 |
| Qualquer outro valor (ex. `"Aliexpress"`, string vazia, valor futuro não mapeado) | *(tag não renderizada)* | CA 5 — nunca exibir o valor cru |

**Por que nome completo e não abreviação ("ML"):** com a tag posicionada em linha própria acima do preço (não espremida ao lado dos números), não há restrição real de largura que justifique abreviar. "Mercado Livre" (13 caracteres) em `--font-size-xs` cabe confortavelmente até em cards de 2 colunas no menor breakpoint mobile do grid atual. Abreviar reduziria reconhecimento (heurística "Recognition rather than recall" — nem todo visitante decodifica "ML" instantaneamente) sem ganho de espaço que compense.

Estrutura de implementação sugerida (já alinhada com o `especificacao-tecnica.md`, valores agora definidos):
```ts
const PLATFORM_LABELS: Record<string, string> = {
  Amazon: 'Amazon',
  MercadoLivre: 'Mercado Livre',
  Shopee: 'Shopee',
};
```
Regra de renderização: `const label = deal.platform ? PLATFORM_LABELS[deal.platform] : undefined;` → renderizar o `<span>` **somente se `label` truthy**. Nunca fazer fallback para o valor bruto (`deal.platform`) quando ausente do mapeamento.

## 3. Especificação visual do componente `.deal-card__platform`

Elemento: `<span className="deal-card__platform">{label}</span>`, primeiro filho dentro do container `.deal-card__price` (antes do markup de preço).

| Propriedade | Valor (token) | Justificativa |
|---|---|---|
| `display` | `inline-block` | ocupa só a largura do texto, não força largura total da linha |
| `font-size` | `var(--font-size-xs)` | mesma escala tipográfica menor já usada no card (consistência com metadados secundários) |
| `font-weight` | `500` (medium) — usar o peso "medium" já disponível na escala tipográfica do projeto, se existir variável; senão `font-weight: 500` literal | discreto mas legível, sem competir com o peso do preço (que deve ser mais forte) |
| `color` | `var(--color-neutral-600)` (ou o tom neutro médio mais próximo já usado para texto secundário no card, ex. legenda/categoria) | texto secundário discreto, contraste suficiente para leitura (mín. AA em fundo branco/neutro claro) |
| `background-color` | `var(--color-neutral-100)` (neutro bem claro) | leve destaque de "chip" sem saliência, nunca `--color-primary` (reservado para preço/CTA, conforme já indicado pelo LT) |
| `border-radius` | `var(--radius-sm)` | consistente com o badge de desconto já existente (`.deal-card__badge`) |
| `padding` | `var(--space-1) var(--space-2)` (ou o par de espaçamentos equivalente já usado no `.deal-card__badge`) | chip compacto, não avantajado |
| `margin-bottom` | `var(--space-1)` | separa visualmente da linha de preço logo abaixo, mantendo a leitura "tag → preço" |
| `line-height` | `1.2` | compacto, evita esticar a altura do bloco de preço |
| `white-space` | `nowrap` | nomes de plataforma nunca devem quebrar em duas linhas dentro do chip |
| `text-transform` | nenhum (manter grafia normal: "Mercado Livre", não uppercase) | uppercase soaria mais "alerta/promocional", contra o pedido de "bem discreta"; grafia normal do nome próprio favorece reconhecimento imediato |
| `cursor` | `default` (herdado — **não** `pointer`) | reforça CA 7: não é interativo |
| `hover`/`focus`/`active` | nenhum estilo definido (sem transição, sem outline de foco, sem `tabindex`) | elemento não interativo — nenhum estado de interação deve existir |

### Estados (checklist obrigatório)
| Estado | Comportamento |
|---|---|
| **Default** (plataforma mapeada presente) | `<span className="deal-card__platform">` renderizado com o texto da tabela de mapeamento, estilo acima. |
| **Oculto** (`platform` ausente/`null`/`undefined`) | `<span>` **não é renderizado** — sem elemento vazio, sem placeholder, sem altura reservada. `.deal-card__price` não muda de altura/posicionamento (CA 4). |
| **Oculto** (`platform` presente mas fora da tabela de mapeamento) | mesmo tratamento do estado "Oculto" acima — nunca renderizar o valor bruto (CA 5). |
| **Loading** | Não se aplica — o dado de plataforma chega junto com o resto do `Deal` na renderização SSR do card (não há fetch assíncrono isolado nem skeleton específico para a tag). |
| **Erro** | Não se aplica — não há chamada de rede própria da tag; se `deal.platform` vier malformado, cai no tratamento "Oculto" acima (robustez sem exibir erro visível ao usuário, heurística "prevenção de erros"/"design robusto a dados ausentes"). |
| **Disabled / Readonly** | Não se aplica — elemento estático de texto, nunca interativo em nenhum estado (CA 7). |
| **Hover/Focus/Active** | Deliberadamente ausentes (ver tabela de estilo acima) — sinaliza visual e semanticamente que não é clicável. |

### Acessibilidade
- `<span>` puro, sem `role="button"`, sem `href`, sem `onClick`, sem `tabindex` (não entra na ordem de tabulação).
- Texto legível por leitor de tela como conteúdo normal do card (não precisa de `aria-label` adicional — o texto já é o nome da plataforma).
- Contraste texto/fundo deve atender AA (verificar o par `--color-neutral-600` sobre `--color-neutral-100` — se o par não atingir 4.5:1 em corpo de texto pequeno, usar o próximo tom mais escuro da escala neutra disponível).

## 4. Responsividade
Não introduzir novos breakpoints — reutilizar os breakpoints já definidos em `website/app/styles/deal-card.css` para o grid de cards (mobile / tablet / desktop).
- **Mobile (viewport estreito, grid compacto):** tag em linha própria acima do preço garante que o texto nunca precisa competir por espaço horizontal com os números do preço/desconto. `white-space: nowrap` + comprimento máximo de 13 caracteres ("Mercado Livre") cabe dentro da largura mínima de card do grid atual sem cortar.
- **Tablet/Desktop:** mesmo comportamento — o chip não escala com a largura do card (tamanho fixo baseado em `--font-size-xs`), mantendo a discrição pedida em qualquer breakpoint.
- **Fallback defensivo (não deve disparar em uso normal, mas por robustez):** se algum layout futuro comprimir o card abaixo do necessário para "Mercado Livre" em uma linha, aplicar `overflow: hidden; text-overflow: ellipsis; max-width: 100%;` no chip em vez de permitir corte abrupto/sobreposição — nunca deixar o texto vazar do card ou sobrepor outro elemento (CA 6).

## 5. Fluxo de navegação
Não há fluxo novo. A tag é um elemento de exibição estático, presente (ou ausente) no mesmo ciclo de renderização do card, em home/categoria/oferta — sem estado de rota, sem interação, sem navegação/filtro (CA 7). O comportamento é idêntico nas 3 telas por construção: as três reutilizam o mesmo `DealCard.tsx` e a mesma tabela `PLATFORM_LABELS` (CA 8).

## 6. Heurísticas de Nielsen → critérios verificáveis
| Heurística | Critério verificável nesta feature |
|---|---|
| Match entre sistema e mundo real | Texto exibido é sempre o nome comercial da plataforma ("Mercado Livre", "Amazon", "Shopee") — nunca o valor bruto do enum (`"MercadoLivre"` sem espaço nunca aparece na tela) |
| Consistência e padrões | Mesma classe CSS, mesmo texto, mesmo posicionamento nas 3 telas (home/categoria/oferta) — verificável comparando o mesmo produto renderizado em duas telas (CA 8) |
| Reconhecimento em vez de memorização | Nome completo da plataforma, sem abreviação — usuário não precisa decodificar sigla |
| Estética e design minimalista | Chip discreto (`--color-neutral-*`), nunca usa `--color-primary`; não compete visualmente com preço/CTA |
| Controle e liberdade do usuário / prevenção de erros | Tag nunca é interativa — nenhum clique/toque produz navegação, filtro ou efeito colateral (CA 7); ausência de `hover`/`cursor:pointer` sinaliza isso visualmente |
| Design robusto a dados ausentes/inválidos | `platform` ausente ou fora do mapeamento → tag simplesmente não renderiza, sem erro visível, sem quebra de layout, sem vazar valor técnico (CA 4, CA 5) |
| Feedback de status | Não aplicável a este componente estático (sem ação do usuário que produza mudança de estado) — documentado explicitamente para não deixar heurística "esquecida": não há nada a dar feedback aqui pois não há interação |

## 7. Resumo para o Dev (`DealCard.tsx`)
1. `PLATFORM_LABELS` com os 3 valores exatos da seção 2.
2. `<span className="deal-card__platform">` como primeiro filho de `.deal-card__price`, renderização condicional (`label &&`).
3. Nova classe `.deal-card__platform` em `deal-card.css` com os valores da seção 3 (tokens já existentes no arquivo — `--color-neutral-*`, `--font-size-xs`, `--space-*`, `--radius-sm`; nunca `--color-primary`).
4. Nenhum `href`/`onClick`/`role`/`tabindex` no `<span>`.
5. Testes (`DealCard.test.tsx`) devem cobrir os 3 estados da tabela "Estados" (default com cada plataforma mapeada, oculto com `null`, oculto com valor não mapeado) + ausência de atributos interativos.
