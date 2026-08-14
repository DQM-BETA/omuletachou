# Proposal — ISSUE-154: Estilização CSS do site público + validação visual automatizada (Playwright)

## Objetivo
Corrigir o gap crítico identificado na Issue #154: o site público (`website/`, Next.js 14+, já funcional em dados/rotas/SEO desde as Issues #12/#94/#95/#96/#117) renderiza como HTML puro, sem nenhuma regra CSS implementada — os arquivos `app/globals.css` e `page.module.css` contêm apenas boilerplate do `create-next-app`, nunca customizado, e as classes BEM já estruturadas em `DealCard.tsx`/`Header.tsx`/páginas não têm nenhuma regra correspondente. Esta issue implementa a camada visual completa (identidade de marca ancorada em `#e63946`, design system genérico do Figma da squad, conceito de site de ofertas/cupons de afiliado, mobile-first) para as 3 telas já existentes (Home, categoria, `deal-detail`), e configura `test:visual` (Playwright) em `website/` para que o Gate Visual do QA — hoje sempre `N/A` por ausência desse script — passe a funcionar de fato neste projeto.

Não há mudança de dados, rotas, integrações ou SEO nesta issue — é puramente a camada de apresentação (CSS) sobre uma estrutura de componentes já pronta, mais o setup de verificação visual automatizada.

## Usuários afetados
- Visitantes do site público `omuletachou.com.br` (a maioria em mobile, conforme decisão do Gerente) — hoje veem texto corrido sem layout, tornando o site inutilizável.
- Google/crawlers e WhatsApp/Facebook (link preview) — não afetados diretamente (SEO/Open Graph já funcionam via Issue #12), mas a percepção de qualidade do link compartilhado piora sem estilo visual.
- Pipeline de QA/Code Review da squad — passa a ter um Gate Visual funcional (script `test:visual` existente), eliminando o ponto cego que permitiu o bug chegar a produção sem detecção em ~8 rodadas anteriores.

## Casos de uso principais
1. Visitante acessa `/` (Home) → grade de cards de ofertas estilizada (imagem, título, preço riscado, preço com desconto, badge `%OFF`, CTA) em layout responsivo mobile-first, com header/navegação e filtros visualmente coerentes com a identidade de marca (`#e63946`).
2. Visitante acessa `/categoria/{categoria}` → mesma estilização de grade da Home, aplicada consistentemente (reaproveita os mesmos componentes/classes BEM já existentes, ex. `deals-grid`, `deals-empty`, `deals-pagination`).
3. Visitante acessa `/oferta/{slug}` (`deal-detail`) → página de produto estilizada: mídia em destaque, preço grande, badge de desconto, CTA principal visualmente proeminente, seção "Mais ofertas" em grid.
4. Dev/CI executa `npm run test:visual` em `website/` → Playwright abre as 3 telas (Home, categoria, `deal-detail`) em viewport mobile (e idealmente desktop) e captura screenshots, permitindo comparação visual e detecção de regressão de CSS.
5. QA/Code Review de qualquer PR futura em `website/` → Gate Visual do checklist (`.claude/agents/qa.md`, regra d2) encontra `test:visual` no `package.json` e efetivamente executa a verificação visual, em vez de cair em `N/A`.

## Casos de exceção
1. **Classe BEM referenciada no componente sem uso real de dados** (ex. `deal-card__badge` quando não há desconto): o CSS deve prever o estado "sem badge" sem quebrar o grid/alinhamento do card.
2. **Imagem de produto ausente/quebrada** (já tratada na Issue #12 com placeholder): o CSS deve estilizar corretamente o estado de placeholder, sem furo de layout.
3. **Tela muito estreita (< 360px) ou muito larga (desktop grande)**: o layout mobile-first deve degradar graciosamente nos extremos (sem overflow horizontal, sem elementos cortados).
4. **`npm run test:visual` falha por diferença de screenshot** (regressão visual introduzida por um PR futuro): deve falhar de forma clara e acionável no output do Playwright, permitindo ao Code Review/QA identificar a tela e o componente afetado.
5. **Categoria/estado vazio ("Nenhuma oferta encontrada")**: precisa de estilização própria (não pode renderizar como texto corrido dentro de um grid vazio).

## Regras de negócio
- **Identidade visual**: sem brand book formal. Âncora obrigatória: `theme-color: #e63946` (já commitado em `app/layout.tsx`/manifest da PWA). Paleta e componentes derivam dessa cor + design system genérico do Figma da squad (https://www.figma.com/design/yi6YkNAy9HfHus2oiPi3G7/Diego-Mulet-s-team-library) + conceito visual de site de ofertas/cupons de afiliado (ênfase em preço, desconto, urgência/CTA — não em design elaborado, alinhado à Issue #12: "objetivo final é converter clique no link de afiliado").
- **Escopo de telas**: as 3 páginas existentes (Home, `/categoria/{categoria}`, `/oferta/{slug}`) — mesma aplicação de design system nas 3, sem fatiamento por prioridade (decisão do Gerente).
- **Mobile-first obrigatório**: CSS deve ser escrito partindo do viewport mobile como base, com breakpoints progressivos para tablet/desktop (não o inverso). Maioria do tráfego de site de cupom/afiliado é mobile.
- **Consistência com o manifest PWA (Issue #117)**: o CSS novo não pode introduzir uma cor de marca diferente de `#e63946` nem conflitar com os ícones/tema já registrados no manifest — é uma continuidade da identidade já declarada, não uma nova decisão de marca.
- **Sem alteração de estrutura de componentes/dados**: as classes BEM já existentes em `DealCard.tsx`, `Header.tsx` e nas páginas são o contrato a ser estilizado — não é escopo desta issue renomear classes, mudar a árvore de componentes ou alterar a lógica de fetch/dados (isso pertence às Issues #12/#94/#95/#96/#117, já entregues).
- **Setup de verificação visual**: `test:visual` (Playwright) deve ser configurado em `website/` cobrindo, no mínimo, as 3 telas em screenshot. Não inclui configurar o mesmo setup em `dashboard` (Angular, stack diferente) — isso é a Issue #155, em rota `backlog`.

## Integrações externas
Nenhuma integração externa nova. Reaproveita 100% dos dados/API já entregues nas Issues #11/#12 (não há mudança de contrato de API, endpoints ou fetch). O único "consumo" novo é do design system do Figma da squad (referência visual estática, não uma integração técnica).

## Restrições / prazo
- Sem prazo explícito informado na Issue — mas classificada como crítica (site inutilizável para usuários finais no estado atual).
- Base já existente e funcional: dados, rotas, SEO, PWA (Issues #12/#94/#95/#96/#117) — este trabalho é aditivo (camada de apresentação), não reconstrução.
- Sem ambiguidade arquitetural: não há decisão de arquitetura de sistema em jogo (ex. escolha de framework, integração externa nova, infraestrutura). A abordagem de CSS (CSS Modules puro, já usado no boilerplate do projeto — `page.module.css` — vs. estilos globais em `globals.css`) é uma decisão de implementação dentro da stack já definida (Next.js 14+ App Router), não uma escolha de arquitetura de sistema. Avaliação do PM (Fase 2): **não é necessário Arquiteto** — segue direto para o Líder Técnico, que decide a organização técnica dos arquivos CSS (module vs. global vs. escopo por componente) e o task breakdown, incluindo o setup do Playwright.
- Dependência: nenhuma (todas as issues de base já estão em `main`).

## Definição de pronto
- As 3 telas (Home, `/categoria/{categoria}`, `/oferta/{slug}`) renderizam com layout visual completo (grid, cores, tipografia, espaçamento) — não mais texto corrido.
- Toda classe BEM referenciada nos componentes/páginas afetados (`DealCard.tsx`, `Header.tsx`, páginas Home/categoria/oferta) possui pelo menos 1 regra CSS correspondente aplicada (ver critério objetivo em `criterios-aceite.md`).
- Cor de marca `#e63946` aplicada de forma consistente como cor primária/destaque (ex. CTA, badges, header) nas 3 telas, alinhada ao `theme-color` do manifest PWA.
- Layout mobile-first verificável: viewport mobile (ex. 375px) sem overflow horizontal, sem elementos cortados/sobrepostos, em nenhuma das 3 telas.
- `npm run test:visual` (Playwright) configurado e executável em `website/`, capturando screenshot das 3 telas com sucesso.
- `npm run build` continua completando sem erros de TypeScript (nenhuma regressão introduzida no build existente).
- Nenhuma alteração de dados, rotas, contrato de API ou lógica de negócio — apenas CSS + config de teste visual.
