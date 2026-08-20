# Proposal — ISSUE-228: Relatório de produtos com filtros na tela Reports

## Objetivo
Adicionar à tela `Reports` do dashboard (Angular, admin interno) um **relatório de quantos produtos estão publicados no site** (`Status = Published`, visível em `GET /api/public/deals`), com filtros combináveis por Categoria, Plataforma, Status, Subcategoria e Faixa de data de coleta. O relatório combina **cards de resumo agregados** (contagens por dimensão) com uma **tabela/gráfico detalhado** abaixo, recalculado on-demand a cada aplicação/mudança de filtro — sem exportação nesta versão.

Motivação: desde a Issue #208, a visibilidade de um produto no site público foi desacoplada de ter rede social configurada — um produto pode estar `Published` e visível no site sem nunca ter passado pela fila de publicação social (`publication_queue`). Os cards/gráfico atuais da tela Reports ("Hoje/Semana/Mês", "Publicações por rede") são baseados exclusivamente em `publication_queue` e não refletem essa realidade: hoje não existe nenhum indicador operacional de "quantos produtos estão de fato publicados e visíveis no site", nem forma de segmentar esse número por categoria/plataforma/status/subcategoria/data de coleta.

## Usuários
- Operador/administrador (dashboard Angular, uso interno) — usa o relatório para acompanhar volume de produtos publicados no site e refinar a visão por dimensão (ex.: "quantos produtos de Eletrônicos do Mercado Livre foram publicados na última semana"). **Não é feature do site público** (`omuletachou.com.br`) nem do visitante final.

## Casos de uso principais
1. O operador acessa a tela `Reports` e vê, sem aplicar nenhum filtro, o relatório completo: cards de resumo agregados (total de produtos publicados no site + quebras por dimensão) e a tabela/gráfico detalhado abaixo, refletindo todos os produtos publicados no site.
2. O operador aplica um filtro (ex.: Categoria = "Eletrônicos"). Os cards de resumo e a tabela/gráfico são recalculados on-demand para refletir apenas os produtos que atendem ao filtro.
3. O operador combina múltiplos filtros simultaneamente (ex.: Plataforma = "Mercado Livre" AND Categoria = "Eletrônicos" AND Faixa de data de coleta = últimos 7 dias). Os filtros se combinam em AND — o relatório mostra a interseção.
4. O operador limpa os filtros (ou os deixa todos em branco) e volta a ver o relatório completo (equivalente ao caso de uso 1).
5. O operador troca de filtro (ex.: de Categoria = "Eletrônicos" para Categoria = "Moda") sem recarregar a página — o relatório recalcula automaticamente a nova combinação.

## Casos de uso de exceção
- Nenhum produto atende à combinação de filtros aplicada — os cards de resumo mostram zero/vazio e a tabela/gráfico exibe estado vazio (sem erro), não uma tela quebrada.
- Filtros com dimensões que não existem para nenhum produto no momento (ex.: uma Subcategoria cadastrada mas sem produtos publicados) — mesmo comportamento de estado vazio acima, sem erro.
- Falha de comunicação com o backend ao aplicar filtro — o relatório deve indicar erro de carregamento de forma clara (sem exibir dado desatualizado como se fosse atual), permitindo nova tentativa.

## Regras de negócio (confirmadas no Gate 1)
1. **Ferramenta operacional interna**: o relatório vive no dashboard admin (Angular), não é uma feature do site público — não há requisito de SEO, cache de borda ou exposição pública.
2. **Escopo de dados**: o universo do relatório é `products` com `Status = Published` (produtos de fato visíveis no site, pós #208) — não é baseado em `publication_queue` (que mede publicação social, dimensão diferente e já coberta pelos cards/gráfico existentes da tela Reports, que permanecem inalterados).
3. **Filtros v1 (combináveis, lógica AND)**:
   - Categoria
   - Subcategoria
   - Plataforma (Mercado Livre, Amazon, Shopee)
   - Status
   - Faixa de data de coleta (data em que o produto foi coletado pelo pipeline, não data de publicação)
   - **Faixa de desconto está fora de escopo desta versão** (confirmado pelo Gerente — não implementar).
4. **Formato de exibição**: combinação de (a) cards de resumo com contagens agregadas — no mínimo: total de produtos publicados, quebra por Plataforma, quebra por Categoria — e (b) tabela/gráfico detalhado abaixo, ambos respeitando os filtros ativos. A composição exata das agregações dos cards (quais quebras adicionais, se por Status ou Subcategoria também) e o tipo de visual da tabela/gráfico detalhado (tabela paginada, gráfico de barras, ou ambos) ficam para o refinamento técnico/UX — não são decisão de negócio bloqueante, desde que cubram no mínimo total + Plataforma + Categoria.
5. **Sem exportação/impressão nesta versão** — uso é somente consulta em tela. Fora de escopo (pode virar melhoria futura).
6. **Atualização on-demand**: os dados são recalculados no momento em que o operador aplica ou muda um filtro (ou ao carregar a tela pela primeira vez). Não há requisito de atualização em tempo real, polling ou websocket.

## Integrações
- Nenhuma integração externa nova. Consome dados já existentes em `products` (schema pós-#208: `Status`, `Category`, `Subcategory`, `Platform`/plataforma de origem, data de coleta) via API interna do backend ASP.NET Core consumida pelo dashboard Angular.
- Não há impacto nos publishers de rede social nem no site público (`Next.js`) — mudança isolada ao dashboard admin e ao endpoint(s) de relatório que o alimentam.

## Restrições
- Sem prazo formal definido — segue o pipeline normal de priorização (rota `normal`).
- Deve conviver com os cards/gráfico existentes da tela Reports ("Hoje/Semana/Mês", "Publicações por rede") sem removê-los ou alterá-los — é uma adição à tela, não uma substituição.
- Volume de dados e performance de agregação com múltiplos filtros combinados (query em `products` com várias condições simultâneas) é uma preocupação técnica válida a avaliar no refinamento — não há requisito de negócio de SLA específico, mas a experiência deve ser responsiva (sem múltiplos segundos de espera perceptível em uso normal).
- Decisões técnicas específicas (contrato do endpoint de relatório — parâmetros de filtro, formato de resposta agregada vs. paginada; necessidade de índice novo em `products` para as colunas de filtro, especialmente `Subcategory` e data de coleta se ainda não indexadas; se as agregações são calculadas em SQL/EF Core diretamente ou exigem uma camada de agregação/cache) ficam para a etapa de Arquitetura/Refinamento Técnico — ver avaliação de ambiguidade abaixo.

## Definição de pronto
Ver `documentacoes/ISSUE-228-exibir-quantidade-produtos-publicados/criterios-aceite.md` para os critérios Given/When/Then completos. Resumo:
- Tela `Reports` exibe um novo relatório de produtos publicados no site, com cards de resumo agregados (no mínimo: total, por Plataforma, por Categoria) + tabela/gráfico detalhado abaixo.
- Filtros combináveis (lógica AND) por Categoria, Subcategoria, Plataforma, Status e Faixa de data de coleta, todos aplicáveis simultaneamente.
- Alterar/aplicar qualquer filtro recalcula o relatório on-demand (sem necessidade de reload de página, sem polling).
- Combinação de filtros sem resultados exibe estado vazio, não erro.
- Sem exportação/impressão nesta versão.
- Cards/gráfico existentes da tela Reports (baseados em `publication_queue`) permanecem inalterados.

## Ambiguidade arquitetural avaliada pelo PM
**Existe ambiguidade técnica que justifica o Arquiteto antes do refinamento do LT.** As regras de negócio (escopo de dados, filtros v1, formato combinado, sem exportação, on-demand) já foram decididas pelo Gerente no Gate 1. Mas restam decisões técnicas não-óbvias, de impacto em performance e contrato de API:
1. **Contrato do endpoint de relatório**: um único endpoint parametrizado por filtros retornando cards + dados detalhados juntos, ou endpoints separados (um para agregados dos cards, outro para a tabela/gráfico detalhado)? Afeta como o Angular consome e como o backend calcula/cacheia.
2. **Performance de agregação com múltiplos filtros combinados**: `products` pode ter volume relevante; contagens agregadas por Categoria/Plataforma/Status/Subcategoria com filtro de faixa de data de coleta simultâneo geram queries com múltiplas condições — decidir se EF Core/SQL direto é suficiente ou se é necessário índice composto novo (especialmente em `Subcategory` e na coluna de data de coleta, se ainda não indexadas para esse padrão de leitura).
3. **Se as agregações são recalculadas em tempo real a cada request (mais simples, dado o requisito de on-demand) ou exigem alguma camada de cache/materialização** — trade-off simplicidade vs. performance percebida, a decidir com base no volume real de dados.
4. **Formato do dado de "data de coleta"** e se o filtro de faixa (`from`/`to`) já é suportado nativamente pelo schema atual de `products` ou precisa de ajuste de tipo/índice.

Essas são decisões de arquitetura/performance, não de negócio — encaminhado ao Arquiteto antes do refinamento técnico do LT.
