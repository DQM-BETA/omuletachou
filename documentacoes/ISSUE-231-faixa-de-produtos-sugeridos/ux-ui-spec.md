# UX/UI Spec — ISSUE #280: Faixa/carrossel de produtos sugeridos (frontend)

Sub-issue de #231 (task_id T-05). Spec visual para `SuggestedProductsCarousel.tsx` — ver
`especificacao-tecnica.md` §4.3/§4.4/§4.5 para o esqueleto técnico já definido pelo LT (fetch
client-side, `overflow-x` + `scrollBy()`, reaproveitamento de `DealCard`/`DealCardLink`). Esta spec
cobre apenas a camada visual/interação — não redefine contratos de API nem componentes já
implementados em #279 (T-04).

## 0. Nota sobre o Design System consultado no Figma

Consultei `https://www.figma.com/design/yi6YkNAy9HfHus2oiPi3G7/Diego-Mulet-s-team-library` (frames
01-11). O arquivo contém **apenas o conteúdo padrão de template do Figma** (paleta de exemplo
Fuschia/Iris, tipografia genérica Inter/Work Sans, telas de onboarding "Publish your Team Library",
"Using your Team Library", cursores default/hover) — **não há nenhum frame do site público real do
omuletachou nem tokens específicos do projeto** (sem paleta de marca, sem componente `DealCard`/
`FilterBar` documentado). Não é um design system utilizável para este projeto no estado atual.

**Consequência para esta spec:** não crio nem hardcodo cores/hex novos. Uso **tokens semânticos**
(papel, não valor) e determino que o Dev reaproveite as variáveis/classes CSS **já existentes** no
`website/` (`DealCard.tsx`, `filter-bar.css`, estilos globais — fora do meu escopo de leitura, que é
restrito a `documentacoes/`, `openspec/`, `CLAUDE.md`). Nenhuma cor, fonte ou espaçamento novo deve
ser introduzido sem necessidade — o carrossel é uma nova *disposição* de cards já existentes, não um
novo sistema visual. Se o Dev identificar que os valores desta spec (larguras/gaps) colidem com o
que já existe em `DealCard`/`deals-grid`, o valor real do código prevalece — os números abaixo são a
referência de layout do carrossel (quantos itens cabem, gap, breakpoints), não uma redefinição das
dimensões do card em si.

Registrar esta lacuna como sugestão de melhoria (fora do escopo desta issue): `.claude/melhorias/`
deveria receber um apontamento para o Gerente popular o arquivo Figma com o design real do site
público, para specs futuras não precisarem operar sem referência visual.

## 1. Objetivo e posição na tela

**Posição: abaixo do grid principal de produtos, acima da paginação** (mesmo default já registrado
pelo LT em `especificacao-tecnica.md` §4.4 — confirmado aqui do ponto de vista de UX, não só como
fallback técnico).

Racional (heurística de Nielsen — combinar sistema e mundo real / minimalismo estético):
- O visitante veio para a listagem/filtro que ele mesmo montou — isso é a tarefa primária. Uma faixa
  de sugestões **acima** do grid competiria por atenção antes do conteúdo que o usuário pediu
  explicitamente, indo contra o modelo mental de "eu filtrei, quero ver o resultado do meu filtro
  primeiro".
- "Sugestões relacionadas" abaixo do conteúdo principal é o padrão já consolidado em e-commerce
  (reduz carga cognitiva — usuário já sabe que aquilo é conteúdo secundário/exploratório pela
  posição).
- Ficar **acima da paginação** (não no rodapé da página, depois de tudo) garante que o carrossel
  ainda esteja "dentro" do contexto da listagem, não pareça um bloco desconectado do rodapé do site.

Renderizado sempre que houver filtro ativo ou não (grid com resultado normal) e também no cenário de
fallback (grid vazio) — nesse segundo caso, a faixa é o único conteúdo "de produto" visível na área
principal, então também deve aparecer logo abaixo da mensagem de "nenhum produto encontrado" do
grid, na mesma posição relativa.

## 2. Título/label da faixa (varia conforme fallback ativo)

Duas variações de texto, sempre como `<h2>` (mesma hierarquia de heading da seção da listagem —
não usar `<h1>`, que pertence ao título da página):

| Cenário (CA) | Condição | Título |
|---|---|---|
| Categoria com resultado (CA 1.1, 1.6) | `hasResults=true` e `categories` preenchido | **"Em alta em {Categoria}"** — ex.: "Em alta em Eletrônicos" |
| Fallback geral (CA 1.2, sem filtro ou filtro vazio) | `hasResults=false` ou `categories` vazio | **"Em alta na loja"** |

Por que "Em alta" (e não "Você também pode gostar" / "Mais produtos da categoria X" genéricos):
- É a mesma palavra nos dois cenários — cria identidade reconhecível para a faixa em qualquer
  contexto de navegação, sem o usuário precisar reler o padrão toda vez.
- Comunica o critério real (popularidade/cliques) em linguagem coloquial de português brasileiro,
  sem expor o termo técnico "mais clicados" (que soa como jargão de analytics, não como copy de
  produto).
- `{Categoria}` usa o nome de exibição da categoria tal como já formatado no `FilterBar` (mesma
  capitalização/rótulo — não inventar um novo formato de nome de categoria).

Nenhum subtítulo/descrição adicional — o título já é auto-explicativo dado o contexto (o usuário
acabou de aplicar o filtro).

## 3. Mapeamento de componentes (reaproveitamento, não recriação)

| Elemento | Componente | Observação |
|---|---|---|
| Card de produto dentro do carrossel | `DealCard` + `DealCardLink` (já existentes, de #279/T-04) | Não recriar — mesmo componente do grid, mesmas dimensões, mesmo conteúdo (imagem, título, preço, badge de desconto/plataforma, CTA "Ver oferta →") |
| Setas de navegação | Novo — `<button>` nativo, sem lib de carrossel (decisão já tomada em `especificacao-tecnica.md` §4.4: `scrollBy()` + `overflow-x: auto`) | Ver §5 (estados) |
| Trilho scrollável | Novo container (`<div>` com `overflow-x: auto`, `scroll-snap-type: x mandatory`) | `scroll-snap-align: start` em cada item, para o `scrollBy()` das setas parar alinhado a um card inteiro, não cortado |
| Título da faixa | Novo `<h2>` | Ver §2 |

Não há Radix/RNR/radix-ng aplicável aqui — `website/` é Next.js com CSS próprio (BEM-like,
`deal-card__cta`, `filter-bar.css`), sem Tailwind nem biblioteca de componentes headless. Manter o
mesmo padrão: HTML semântico + CSS próprio, sem introduzir dependência nova (alinhado à decisão já
registrada no refinamento técnico de não trazer lib de carrossel).

## 4. Layout — dimensões e itens visíveis por breakpoint

Cards mantêm a **mesma largura/altura já usadas no grid principal** (`DealCard` não é redimensionado
para o carrossel — é o mesmo componente, mesmo CSS de card). Os valores abaixo são a referência de
**quantos itens cabem por vez** e o **espaçamento do trilho**, calibrados para o Dev ajustar contra a
largura real do `DealCard` se divergir do assumido aqui (~240px, largura típica de card de e-commerce
em grid responsivo — não é uma medida travada).

| Breakpoint | Largura de viewport | Itens totalmente visíveis | Peek do próximo item | Gap entre cards | Padding lateral do trilho |
|---|---|---|---|---|---|
| Mobile | < 640px | 1 | ~30% do 2º card visível na borda direita | 12px | 16px (evita card colado na borda da tela) |
| Tablet | 640–1023px | 2 | ~40% do 3º card visível | 16px | 24px |
| Desktop | ≥ 1024px | 4 | ~15% do 5º card visível | 16px | mesmo padding do container do grid principal (não redefinir) |

O "peek" (fatia do próximo card visível na borda) é intencional em todos os breakpoints — sinaliza
visualmente que há mais conteúdo para rolar, mesmo antes do usuário interagir com a seta ou arrastar
(heurística de Nielsen — visibilidade do status do sistema: o usuário não precisa clicar para
descobrir que há mais itens).

Altura do trilho: `auto`, definida pela altura natural do `DealCard` — sem forçar altura fixa nem
`overflow-y` (evita cortar conteúdo do card em variações de título longo, já tratadas pelo próprio
`DealCard`).

## 5. Setas de navegação (decisão do Gerente — obrigatórias em todos os breakpoints)

Botões `<button type="button">` reais (não `<div onClick>` — foco/teclado/leitor de tela precisam
funcionar nativamente), um à esquerda e um à direita do trilho.

**Posicionamento:**
- Desktop/Tablet: sobrepostas nas bordas do trilho (posição absoluta, centralizadas verticalmente),
  metade do botão para fora do container do carrossel — padrão reconhecível de carrossel de
  e-commerce, não compete com o espaço dos cards.
- Mobile: mesma sobreposição, mas com leve puxão para dentro (o botão não pode invadir a área de
  toque do card vizinho) — 8px de margem da borda do trilho.

**Tamanho:** 40×40px (círculo), abaixo do mínimo recomendado de área de toque (44×44px do WCAG)
apenas visualmente — a área de toque real (`padding`/`hit area` do botão) deve ser 44×44px mesmo que
o círculo visível seja 40px, via `padding` invisível ou `::before` expandido.

**Ícone:** seta simples (chevron), sem texto visível — `aria-label` obrigatório (ver estados abaixo).

**Estados (todos obrigatórios — feedback de status, heurística de Nielsen):**

| Estado | Aparência | Comportamento |
|---|---|---|
| Default | Fundo neutro (reaproveitar cor de superfície/botão secundário já usada no site, ex. mesma base do botão de filtro), ícone com cor de texto padrão, sombra leve | Clicável, dispara `scrollBy({ left: ±cardWidth, behavior: 'smooth' })` |
| Hover (desktop) | Leve elevação (sombra maior) + mudança sutil de fundo (mesma transição já usada em outros hovers do site, se existir) | — |
| Focus (teclado) | Contorno de foco visível (`outline`), mesmo padrão de foco já usado nos demais elementos interativos do site — **não remover outline sem substituto visível** | Navegável via Tab, ativável via Enter/Space |
| Disabled (início/fim do trilho) | Opacidade reduzida (~40%), cursor `not-allowed`, ícone sem cor de destaque | `disabled` **nativo** do HTML (não só classe CSS) — impede clique/foco por teclado quando não há mais itens naquela direção. `aria-disabled="true"` redundante para leitores de tela mais antigos |
| Loading | N/A — setas só aparecem depois que a lista carrega (ver §6, esqueleto não tem setas) | — |

**Regra de desabilitação (CA 1.3):** seta esquerda desabilitada quando `scrollLeft === 0`; seta
direita desabilitada quando `scrollLeft + clientWidth >= scrollWidth` (com tolerância de 1-2px por
arredondamento de subpixel). Recalculado em todo evento `onScroll` do trilho (inclui arrasto manual/
touch, não só clique nas setas) e no primeiro render após os dados chegarem.

**Acessibilidade dos rótulos:**
- Seta esquerda: `aria-label="Ver produtos anteriores"`
- Seta direita: `aria-label="Ver mais produtos"`
- Trilho: `role="region"` + `aria-label="Produtos sugeridos"` (permite navegação por regiões em
  leitores de tela, sem precisar ler cada card fora de contexto)

## 6. Estado de carregamento (loading)

Não especificado nos critérios de aceite, mas obrigatório por heurística de Nielsen (visibilidade do
status do sistema) — a busca é client-side (`useEffect`, especificacao-tecnica.md §4.4), então há uma
janela real (rede) em que a página já renderizou mas a faixa ainda não tem dados.

**Skeleton, não espaço em branco nem spinner central.** Renderiza imediatamente (antes do fetch
resolver):
- Barra de título esqueleto (~160px × altura de linha do `<h2>`, cor de placeholder neutra,
  animação de shimmer se o site já tiver um padrão de skeleton em outro lugar — senão, opacidade
  pulsante simples).
- N cards esqueleto (mesma quantidade de itens totalmente visíveis do breakpoint — §4), cada um com
  a mesma largura/altura do `DealCard` real, retângulos cinza para imagem/título/preço.
- Sem setas de navegação durante o skeleton (evita usuário clicar em algo que ainda não tem para onde
  ir).

Transição: ao resolver o fetch, se `deals.length > 0` → skeleton é substituído pelo conteúdo real
(sem fade abrupto necessário, mas evitar layout shift — skeleton deve ocupar a mesma altura que o
conteúdo real ocupará). Se lista vazia/erro → skeleton é removido e **nada** é renderizado (§7).

Tempo mínimo de exibição: não artificial — não adicionar delay proposital só para "mostrar o
skeleton"; ele aparece pelo tempo real que o fetch levar (pode ser imperceptível em conexão rápida,
e isso é o comportamento correto).

## 7. Estado "sem sugestões" (fallback também vazio) — decisão

**A faixa inteira desaparece (não renderiza nada, nenhuma mensagem).** Confirma e mantém a decisão
já tomada no refinamento técnico (`especificacao-tecnica.md` §4.4: `return null`), com o racional de
UX explícito aqui:

- CA 1.5/1.8 já definem isso do lado de contrato de dados/robustez técnica; do lado de UX, mostrar
  uma faixa vazia com mensagem tipo "Nenhuma sugestão no momento" adicionaria um bloco de conteúdo
  vazio numa página que já pode estar mostrando "nenhum produto encontrado" no grid principal (CA
  1.2, cenário de filtro vazio) — duas mensagens de "nada aqui" empilhadas pioram a experiência em
  vez de ajudar (heurística de minimalismo estético: não adicionar informação irrelevante/redundante
  à tela).
- Erro de rede (CA 1.8) e "lista vazia por regra de negócio" (corte mínimo de 4, CA 1.5) têm o
  **mesmo resultado visual** (nada aparece) — não é necessário nem desejável o usuário distinguir os
  dois casos; ambos significam "não há sugestão relevante agora".

Não há necessidade de reservar espaço/altura para essa faixa quando ausente — o layout da página
colapsa normalmente para o próximo elemento (paginação), sem gap vazio.

## 8. Interação — resumo do fluxo de navegação

1. Página de listagem renderiza → grid principal aparece imediatamente (server-rendered, já existe).
2. Abaixo do grid (ou da mensagem de "nenhum resultado"), `SuggestedProductsCarousel` aparece em
   estado skeleton (§6) enquanto busca os dados.
3. Fetch resolve:
   - ≥ 4 produtos → título (§2) + trilho de cards + setas aparecem. Seta esquerda nasce desabilitada
     (início do trilho); seta direita habilitada se houver mais itens que cabem na viewport.
   - < 4 produtos ou erro → nada aparece (§7), sem transição visível (sem "pisca" de conteúdo).
4. Usuário clica na seta direita → trilho rola suavemente por ~1 "página" de itens (largura visível
   do trilho, não item a item) → ao chegar ao fim, seta direita desabilita.
5. Usuário clica em um card (dentro do trilho) → mesmo comportamento de um card do grid principal
   (navega para o destino do link de afiliado, registra clique) — nenhuma diferença visual ou de
   interação entre um card do carrossel e um card do grid (CA 1.4).
6. Usuário arrasta o trilho manualmente (touch/trackpad) → setas recalculam estado a cada `onScroll`
   (§5), mesmo comportamento de quem usou as setas.

## 9. Responsividade — resumo

| Breakpoint | Título | Cards visíveis | Setas | Gap/padding |
|---|---|---|---|---|
| Mobile (< 640px) | `<h2>` mesmo tamanho de fonte já usado em headings de seção mobile do site | 1 + peek 30% | 40×40px, hit area 44×44px, 8px de margem interna | 12px gap / 16px padding lateral |
| Tablet (640–1023px) | idem | 2 + peek 40% | idem, posição semi-sobreposta | 16px gap / 24px padding lateral |
| Desktop (≥ 1024px) | idem | 4 + peek 15% | idem, mais afastadas do trilho (sobrepostas na borda) | 16px gap / padding igual ao container do grid |

Tipografia do título e dos cards: **reaproveitar exatamente** a escala/fonte já usada nos headings de
seção e no `DealCard` existentes — esta spec não introduz fonte nova.

## 10. Checklist de heurísticas de Nielsen aplicadas (verificável pelo QA)

- [ ] **Visibilidade do status do sistema:** skeleton visível durante carregamento (§6); setas
  desabilitadas refletem o estado real do scroll, atualizadas em tempo real (§5).
- [ ] **Correspondência sistema/mundo real:** copy "Em alta" em linguagem natural, sem jargão técnico
  exposto ao usuário (§2).
- [ ] **Controle e liberdade do usuário:** navegação por seta E por arrasto/touch, ambos funcionais e
  sincronizados (§5, §8.6); foco por teclado funcional nas setas (Tab + Enter/Space).
- [ ] **Consistência e padrões:** card idêntico ao do grid principal (mesmo componente, sem variação
  visual) — CA 1.4 (§3, §8.5).
- [ ] **Prevenção de erros:** seta desabilitada nativamente (`disabled`) nos extremos — impossível
  disparar scroll além do conteúdo disponível (§5).
- [ ] **Reconhecimento em vez de memorização:** peek do próximo card sinaliza "há mais conteúdo" sem
  o usuário precisar descobrir por tentativa (§4).
- [ ] **Design minimalista e estético:** nenhuma mensagem/bloco vazio quando não há sugestões (§7);
  nenhum subtítulo redundante (§2).
- [ ] **Ajuda ao reconhecer/diagnosticar erros:** falha de rede é silenciosa para o usuário (a faixa
  some, CA 1.8) — não há erro para o usuário "reconhecer", por design (best-effort, não crítico).

## 11. Fora de escopo desta spec

- Contrato do endpoint, lógica de fallback categoria/geral, corte mínimo de 4 — já decidido e
  implementado no backend (T-01/T-03, `especificacao-tecnica.md` §3.3). Este componente só consome e
  renderiza na ordem recebida.
- `DealCard`/`DealCardLink` (visual do card em si, registro de clique) — já especificado e
  implementado em #279 (T-04). Esta spec só define o container/carrossel ao redor.
