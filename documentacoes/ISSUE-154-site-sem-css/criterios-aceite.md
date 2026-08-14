# Critérios de aceite — ISSUE-154: Estilização CSS do site público + validação visual automatizada

Organizados por tema. O LT usará esta base para o task breakdown técnico (provavelmente uma única sub-issue de Dev, dado que é uma implementação de CSS coesa sobre estrutura já pronta — LT decide se vale fatiar).

---

## Identidade visual e cor de marca

**CA-1 — Cor de marca `#e63946` aplicada consistentemente**
Given o `theme-color: #e63946` já definido em `app/layout.tsx`/manifest da PWA
When as 3 telas (Home, categoria, `deal-detail`) são renderizadas
Then a cor `#e63946` (ou uma variação de tom dela definida em variável CSS, ex. `--color-primary`) aparece de forma consistente em pelo menos: CTA principal, badge de desconto e algum elemento de destaque do header — sem nenhuma outra cor de marca concorrente sendo usada como primária.

**CA-2 — Paleta e tipografia seguem o design system do Figma da squad**
Given o design system genérico do Figma (https://www.figma.com/design/yi6YkNAy9HfHus2oiPi3G7/Diego-Mulet-s-team-library)
When o CSS das 3 telas é implementado
Then tokens de cor, espaçamento e tipografia usados (ex. escala de fontes, cores neutras/cinzas, raio de borda) são consistentes com os tokens definidos no Figma (validação por inspeção visual comparativa no Code Review/QA, já que não há Figma file específico do produto — é o design system genérico).

---

## Cobertura de classes CSS (critério objetivo)

**CA-3 — Toda classe BEM referenciada nos componentes tem regra CSS correspondente**
Given a lista de classes BEM já estruturadas nos componentes afetados (`deal-card__title`, `deal-card__media`, `deal-card__badge`, `site-header__brand`, `site-header__filters`, `site-header__chip`, `deals-grid`, `deals-empty`, `deals-pagination`, `deal-detail__*`, entre outras usadas em `DealCard.tsx`, `Header.tsx` e nas páginas)
When o CSS final é revisado (ex. `grep` das classes usadas em `.tsx` cruzado com os seletores definidos em `globals.css`/`*.module.css`)
Then 100% das classes referenciadas no JSX possuem pelo menos 1 seletor CSS correspondente definindo alguma regra visual (não apenas presente no markup sem estilo) — critério verificável objetivamente pelo QA/Code Review sem depender de julgamento estético.

**CA-4 — CSS efetivamente importado e aplicado no build**
Given os arquivos de estilo (`globals.css` e/ou CSS Modules por componente, conforme decisão do LT)
When `app/layout.tsx` e/ou os componentes são renderizados
Then as folhas de estilo estão corretamente importadas (sem CSS "órfão" não referenciado) e o HTML resultante carrega com as regras aplicadas (verificável via `curl`/view-source mostrando `<link>`/`<style>` correspondente, ou via Playwright screenshot mostrando o layout estilizado).

---

## Estilização por tela

**CA-5 — Home estilizada**
Given a rota `/`
When acessada
Then exibe: header com marca/navegação estilizados, grid de cards de ofertas (não lista de texto corrido), cada card com imagem, título, preço riscado, preço com desconto, badge `%OFF` e CTA visualmente distintos, filtros de plataforma/categoria estilizados como controles interativos (não texto puro), e paginação visualmente clara.

**CA-6 — Página de categoria estilizada**
Given a rota `/categoria/{categoria}`
When acessada
Then reaproveita a mesma estilização de grade/cards da Home (CA-5), com o mesmo nível de acabamento visual — sem divergência de estilo entre as duas telas.

**CA-7 — Estado vazio de categoria estilizado**
Given uma categoria sem ofertas ativas
When `/categoria/{categoria}` é acessada
Then a mensagem "Nenhuma oferta encontrada nesta categoria" (`deals-empty`) é exibida com estilização própria (não texto corrido solto no meio da página vazia).

**CA-8 — Página de detalhe de oferta (`deal-detail`) estilizada**
Given a rota `/oferta/{slug}`
When acessada
Then exibe: mídia em destaque estilizada, preço grande com destaque visual, badge de desconto, CTA principal proeminente (visualmente destacado como ação primária da página) e seção "Mais ofertas" em formato de grid (reaproveitando o estilo de card das telas anteriores).

---

## Mobile-first / responsividade

**CA-9 — Sem overflow horizontal em viewport mobile**
Given viewport mobile (375px de largura, referência comum)
When qualquer uma das 3 telas é carregada
Then não há scroll horizontal nem elementos cortados/sobrepostos — todo o conteúdo se ajusta à largura da viewport.

**CA-10 — Grid de cards se adapta ao viewport**
Given a Home ou página de categoria
When visualizada em mobile (1 coluna ou 2, conforme decisão do LT/CSS) versus desktop (múltiplas colunas)
Then o grid de cards se reorganiza de forma responsiva (mobile-first: a base é 1-2 colunas, expandindo progressivamente via `min-width` media queries para telas maiores — nunca o inverso).

**CA-11 — Elementos interativos com área de toque adequada em mobile**
Given CTAs, filtros e paginação nas 3 telas
When visualizados em mobile
Then possuem área de toque suficiente para uso confortável em touchscreen (sem elementos minúsculos ou colados uns aos outros).

---

## Consistência com o manifest PWA

**CA-12 — Nenhum conflito de cor de marca com o manifest**
Given o `theme-color`/`background_color` já definidos no manifest da PWA (Issue #117)
When o CSS novo é revisado
Then não introduz nenhuma cor de marca primária diferente de `#e63946` (ou tons derivados dela) que crie inconsistência visual entre a barra de tema do navegador/PWA instalada e o site em si.

---

## Setup de validação visual (Playwright / `test:visual`)

**CA-13 — Script `test:visual` existe e é executável**
Given o `package.json` de `website/`
When `npm run test:visual` é executado
Then o comando existe, roda via Playwright e finaliza sem erro de configuração (independentemente de haver ou não regressão visual a ser detectada nesta primeira execução).

**CA-14 — Cobertura mínima de 3 telas em screenshot**
Given a suíte `test:visual`
When executada
Then captura screenshot de, no mínimo, as 3 telas: Home (`/`), categoria (`/categoria/{categoria-existente}`) e detalhe de oferta (`/oferta/{slug-existente}`), preferencialmente em viewport mobile (alinhado à decisão mobile-first).

**CA-15 — Gate Visual do QA deixa de resolver `N/A` para `website`**
Given o Gate Visual do checklist de QA (`.claude/agents/qa.md`, regra d2), que decide executar verificação visual com base na existência do script `test:visual`
When o QA avalia um PR de `website/` após esta issue
Then o Gate Visual encontra o script e executa a verificação real (screenshot/comparação), não mais caindo no fallback de inspeção de HTML cru via curl.

---

## Transversal

**CA-T1 — Build sem erros de TypeScript**
Given o código completo desta issue integrado
When `npm run build` é executado em `website/`
Then o build completa sem erros de TypeScript (nenhuma regressão introduzida no build já existente da Issue #12).

**CA-T2 — Nenhuma alteração de dados, rotas ou contrato de API**
Given o escopo desta issue (camada visual + teste)
When o código é revisado
Then não há mudança em `lib/api.ts`, nos endpoints consumidos, na lógica de fetch/ISR ou nas rotas já existentes — apenas CSS, estrutura de estilos e o setup de `test:visual`.

**CA-T3 — Nenhuma configuração de deploy/produção alterada**
Given o `docker-compose.yml` e variáveis de ambiente já existentes
When o código desta issue é revisado
Then nenhuma variável de produção, domínio ou configuração de deploy é introduzida ou alterada (fora de escopo, pertence à Issue #15).
