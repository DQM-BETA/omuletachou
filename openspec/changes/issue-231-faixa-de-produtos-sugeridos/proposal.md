# Proposal — ISSUE-231: Rastreio de cliques + faixa de produtos sugeridos (site público)

## Objetivo
Registrar cliques em produtos no site público (`website/`, Next.js) de forma anônima, e usar essa contagem para alimentar uma nova faixa/carrossel de "produtos sugeridos" na tela de listagem de produtos, baseada na categoria dos itens atualmente filtrados pelo usuário (com fallback para "mais clicados" quando o filtro atual não retorna nenhum produto).

**Fora de escopo desta issue:** o item 3 do pedido original (melhorias no grid de Products do dashboard — coluna de contagem de cliques, ordenação por cabeçalho, remoção da coluna Desconto) foi separado para a **Issue #275**, por ser independente e mais simples. Investigação técnica de `discount_pct` (dados reais de Amazon/Shopee) fica a critério do Arquiteto/LT, não é ação obrigatória desta issue.

## Usuários
- Visitante do site público (`omuletachou.com.br`) — navega pela listagem de produtos, filtra por categoria/preço, clica em cards de produto (leva ao destino atual, sem mudança), e passa a ver uma faixa de "produtos sugeridos" relacionada à categoria filtrada (ou aos mais clicados, se o filtro atual não retornar resultados).

## Casos de uso principais

### Item 2 — Rastreio de cliques (pré-requisito do item 1)
1. O visitante clica em um card de produto em qualquer lugar do site (listagem normal, resultado de busca, faixa de sugeridos) — um evento de clique é registrado de forma **anônima** (produto + timestamp, sem usuário/sessão), sem alterar o destino atual do clique (link de afiliado/página, o que já existe hoje).
2. O evento é contabilizado para o ranking de "mais clicados", usado tanto no fallback do item 1 quanto na ordenação por categoria (ver Regras de negócio).
3. Cliques disparados dentro do carrossel de produtos sugeridos contam exatamente como cliques disparados na listagem normal — mesmo evento, mesma contagem, sem distinção de origem.

### Item 1 — Faixa de produtos sugeridos (carrossel horizontal)
1. O visitante acessa a tela de listagem de produtos com um filtro de categoria aplicado (e esse filtro retorna ao menos 1 produto) — a faixa de sugeridos exibe produtos da(s) mesma(s) categoria(s) do filtro atual, ordenados por **mais clicados dentro da categoria** (ver Regras de negócio, decisão do Gerente).
2. O visitante aplica um filtro que não retorna nenhum produto (lista vazia) — a faixa de sugeridos exibe os produtos **mais clicados em geral** (fallback, todas as categorias), em vez de sugestões por categoria.
3. A faixa é exibida como um **carrossel horizontal**, com **setas de navegação** para a esquerda e para a direita, permitindo ao visitante percorrer os produtos sugeridos sem paginação de página inteira.
4. O visitante clica em um produto dentro da faixa de sugeridos — o comportamento de clique é idêntico ao de um card na listagem normal (mesmo destino atual, sem mudança de fluxo) e o clique é contabilizado (ver item 2.3).

## Casos de uso de exceção

### Item 2 (rastreio de cliques)
- Falha ao registrar o evento de clique (erro de rede/timeout no lado do rastreio) — não deve bloquear nem atrasar a navegação do usuário ao destino do clique; o registro do evento é best-effort/assíncrono do ponto de vista da experiência do visitante (não pode segurar o redirecionamento).
- Cliques duplicados/repetidos rapidamente no mesmo produto pelo mesmo visitante (ex.: duplo clique acidental) — comportamento de deduplicação (se houver) fica a critério do refinamento técnico; não é requisito de negócio desta issue impedir múltiplas contagens do mesmo visitante (evento é anônimo, sem estado de sessão para deduplicar de forma confiável).

### Item 1 (faixa de sugeridos)
- Categoria do filtro atual tem produtos, mas em quantidade menor que o padrão de exibição do carrossel (ver Regras de negócio, decisão de produto do PM) — a faixa exibe os produtos disponíveis daquela categoria (não força completar com outras categorias), respeitando o mínimo definido para a faixa aparecer.
- Não há produtos suficientes disponíveis nem para o fallback "mais clicados" (catálogo muito pequeno ou nenhum clique registrado ainda) — a faixa não é exibida (ver mínimo de exibição nas Regras de negócio), sem quebrar a tela de listagem.
- Filtro aplicado combina múltiplos critérios além de categoria (ex.: categoria + faixa de preço) — a faixa de sugeridos considera apenas a(s) categoria(s) do filtro atual para a sugestão (não replica os demais filtros, como preço, na seleção dos sugeridos); comportamento exato de "múltiplas categorias filtradas ao mesmo tempo" (se a UI permitir) fica a critério do refinamento técnico, desde que a faixa sempre tenha como base a(s) categoria(s) ativas.

## Regras de negócio (confirmadas no Gate 1 + decisões de produto do PM)

### Confirmadas pelo Gerente (Gate 1)
1. **Critério de ordenação dentro da categoria: mais clicados** — o ranking da faixa de sugeridos por categoria usa a contagem de cliques do próprio produto dentro daquela categoria (não AI Score, não mais recentes).
2. **Destino do clique não muda** — clicar no card do produto continua levando para onde já leva hoje (link de afiliado ou página existente); esta issue não altera esse fluxo.
3. **Evento de clique é anônimo** — sem ligar a usuário/sessão, apenas produto + timestamp.
4. **Cliques no carrossel de sugeridos contam igual** a cliques na listagem normal (mesmo evento, mesma contagem, sem distinção de origem).

### Decisão de produto do PM (não especificado pelo Gerente — sujeita a ajuste no Code Review/QA)
> As regras abaixo foram definidas pelo PM como valores padrão razoáveis, na ausência de especificação do Gerente. Podem ser ajustadas no refinamento técnico, Code Review ou QA se se mostrarem inadequadas na prática.

5. **Quantidade de produtos por vez na faixa: 10 produtos.** O carrossel carrega até 10 produtos sugeridos por chamada (categoria ou fallback); a navegação por setas percorre esses 10 itens (paginação/carregamento incremental além dos 10, se necessário, fica a critério do refinamento técnico/UX).
6. **Escopo do fallback "mais clicados": geral, sem corte de disponibilidade/plataforma.** Quando o filtro atual não retorna produtos, o fallback considera todos os produtos ativos do catálogo (todas categorias, todas plataformas), ordenados por contagem de cliques decrescente — sem filtrar por plataforma específica ou outro corte adicional além de "produto ativo/disponível" (mesmo critério de disponibilidade já usado na listagem normal).
7. **Mínimo para a faixa aparecer: 4 produtos.** A faixa de sugeridos (por categoria ou fallback) só é exibida se houver pelo menos 4 produtos disponíveis para compor a lista; abaixo desse mínimo, a faixa não é renderizada (evita carrossel "vazio" ou com poucos itens, esteticamente pobre).
8. **Empate no ranking de cliques (0 cliques ou cliques iguais):** produtos com contagem de cliques igual (incluindo 0, especialmente relevante logo após o lançamento da funcionalidade, quando ainda não há histórico de cliques) são desempatados por critério já existente no catálogo (ex.: mais recentes primeiro), a critério do refinamento técnico — não é um requisito de negócio rígido desta issue, apenas garantir que a faixa não fique vazia por falta de cliques históricos.

## Integrações
- Nenhuma integração externa nova (sem serviço de analytics de terceiros). O rastreio de cliques é interno ao sistema (API própria já existente, `ASP.NET Core`), persistido em banco próprio (`PostgreSQL`) — decisão de modelagem (campo no `Product` vs. tabela de eventos separada) e contrato do endpoint são decisões arquiteturais, delegadas ao Arquiteto (ver seção de ambiguidade abaixo).
- A faixa de sugeridos consome um novo endpoint/contrato de API a ser definido no refinamento técnico (Arquiteto), consumido pelo `website/` (Next.js).

## Restrições
- Rota `normal` — pipeline completo (PM → Arquiteto → LT → Dev → Code Review → QA → Gate 2).
- Escopo restrito aos itens 1 e 2 (rastreio de cliques + faixa de sugeridos); item 3 (grid do dashboard) é tratado na Issue #275, sem dependência de bloqueio entre as duas issues — #231 pode ser entregue independentemente de #275.
- Investigação de `discount_pct` (dados reais de Amazon/Shopee vs. Mercado Livre) não é escopo obrigatório desta issue; se o Arquiteto/LT considerar relevante investigar durante o refinamento técnico (por eventual impacto de schema ao mexer no `Product`), pode registrar achados para uso futuro, mas não é bloqueante para a entrega de #231.
- Volume esperado de eventos de clique deve ser considerado na decisão de persistência (Arquiteto) — site com tráfego de afiliado pode gerar volume não-trivial de eventos; a modelagem deve suportar agregação eficiente de "mais clicados por categoria" sem degradar performance da listagem de produtos.

## Definição de pronto
Ver `documentacoes/ISSUE-231-faixa-de-produtos-sugeridos/criterios-aceite.md` para os critérios Given/When/Then completos. Resumo:
- **Item 2 (rastreio):** clique em qualquer card de produto (listagem normal ou carrossel de sugeridos) registra evento anônimo (produto + timestamp), sem alterar o destino atual do clique, sem bloquear a navegação do usuário em caso de falha no registro.
- **Item 1 (faixa de sugeridos):** carrossel horizontal com setas de navegação, exibindo até 10 produtos da categoria do filtro atual (ordenados por mais clicados na categoria) ou, em fallback (filtro vazio), os mais clicados em geral — só exibido se houver pelo menos 4 produtos disponíveis.

## Ambiguidade arquitetural avaliada pelo PM
**Há ambiguidade arquitetural que justifica o Arquiteto.** Pontos que exigem decisão técnica não-óbvia antes do refinamento do LT:
1. **Onde persistir a contagem de cliques** — novo campo agregado no `Product` (ex.: `click_count`) vs. tabela de eventos separada (ex.: `product_clicks` com timestamp por evento) — trade-off entre simplicidade/performance de leitura (campo agregado) e granularidade/auditabilidade/possibilidade de análises futuras (tabela de eventos), considerando volume esperado de escrita em produção.
2. **Como agregar "mais clicados por categoria" com performance aceitável** — se optar por tabela de eventos, é necessário decidir estratégia de agregação (query on-the-fly com índice vs. contador desnormalizado atualizado de forma assíncrona/job) para não degradar a listagem de produtos, que já é uma tela de alto tráfego.
3. **Contrato do endpoint da faixa de sugeridos** — payload de entrada (categoria(s) do filtro atual) e saída (lista de produtos + metadados), e se o endpoint de registro de clique é síncrono (fire-and-forget do client) ou passa por alguma fila/job (Hangfire, já usado no projeto) para não impactar a latência da navegação.
4. **Investigação de `discount_pct`** (mencionada na issue original) — decisão sobre necessidade/momento de investigar dados reais de Amazon/Shopee, caso relevante para a modelagem do `Product` neste refinamento.

Segue para o **Arquiteto** completar o `design.md`.
