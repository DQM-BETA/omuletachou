# Proposal — ISSUE-167: Categorização unificada de produtos + remoção de distinção de plataforma

## Objetivo
Substituir a taxonomia rasa e por-plataforma dos produtos (hoje 6 categorias genéricas, nascendo sempre "Geral" na coleta) por uma taxonomia unificada de 2 níveis (Categoria + Subcategoria), aplicada da mesma forma independentemente da plataforma de origem (Amazon, MercadoLivre, Shopee). Paralelamente, remover o campo `Platform` do contrato de dados público (`/api/public/deals/*`) por higiene de segurança — evitar que terceiros mapeiem, via inspeção do JSON público, a estratégia de curadoria por plataforma. O usuário final do site passa a navegar por categoria/subcategoria e filtros (preço, desconto, ordenação), sem nunca precisar saber de qual marketplace o produto veio.

## Usuários
- Visitante do site público (`omuletachou.com.br`) — navega, filtra e busca ofertas por categoria/subcategoria.
- Sistema (jobs `AmazonCollector`/`MercadoLivreCollector`/`ShopeeCollector`, `ProcessorJob`, `ClaudeAiService`) — classifica produtos automaticamente na coleta e, como fallback, via IA pós-aprovação.
- Operador/administrador (dashboard Angular) — continua vendo `Platform` normalmente (uso interno, não afetado).

## Casos de uso principais
1. Um produto é coletado por qualquer um dos 3 collectors. Durante `CollectAsync`, o dicionário de categorias (`CategoryDetector` expandido) roda sobre título/descrição e atribui `Category` + `Subcategory` (ou "Geral" sem subcategoria, se nenhuma palavra-chave der match) — sem custo de IA, para todo produto, aprovado ou não.
2. O produto segue o fluxo normal de scoring (`ScoreProductAsync`, inalterado — não recebe responsabilidade de categorização).
3. Se o produto for aprovado (`Status == Queued`) e ainda estiver em "Geral" (dicionário não encontrou match) e o orçamento mensal de IA (`claude.monthly_budget_limit_brl`) não tiver estourado, o `ProcessorJob` aciona o fallback via Claude para tentar classificar (`category`/`subcategory`) antes de gerar o slug/legenda.
4. Visitante acessa a Home e usa a barra de filtros: dropdown de Categoria, dropdown dependente de Subcategoria (populado conforme a Categoria escolhida), slider de faixa de preço, botões de desconto mínimo (10%+/30%+/50%+) e seletor de ordenação (relevância/menor preço/maior desconto/mais recente). Os filtros são combináveis e opcionais; sem nenhum filtro aplicado, o comportamento é idêntico ao atual (ordenado por `AiScore`).
5. Frontend consulta `GET /api/public/categories` para montar os dropdowns dinamicamente (árvore Categoria → [Subcategorias] com contagem de produtos ativos), sem hardcode de taxonomia no client.
6. Frontend consulta `GET /api/public/deals` com os parâmetros de filtro selecionados; a resposta (`PublicDealDto`) não contém mais o campo `Platform`.

## Casos de uso de exceção
- Produto sem match no dicionário e IA desativada (orçamento do mês estourado) → permanece em "Geral", sem subcategoria; comportamento aceito (item de backlog separado se o volume for relevante pós-lançamento).
- Produto sem match no dicionário, mas `Status != Queued` (rejeitado no score) → nunca aciona a camada de IA (economia intencional).
- Falha na chamada de IA do fallback (timeout/erro da API Claude) → produto permanece com a classificação do dicionário (ou "Geral"), sem bloquear o `ProcessorJob`; comportamento de erro/retry segue o padrão já existente no job para outras chamadas à IA.
- Categoria/subcategoria não reconhecida no filtro da querystring (ex.: valor livre digitado via URL) → retorna lista vazia (nenhum match), não erro 400; contrato tolerante a valores desconhecidos, já que `Category`/`Subcategory` são `VARCHAR` livre.
- Rota antiga `GET /api/public/deals/category/{categoria}` — decisão de convivência/substituição fica com o Arquiteto/LT (ver seção de ambiguidade).

## Regras de negócio (confirmadas no Gate 1)
1. **Taxonomia v1 fechada**: 9 categorias, 3-5 subcategorias cada (~35 subcategorias totais): Eletrodomésticos, Climatização, Ferramentas, Eletrônicos, Casa e Cozinha, Beleza, Moda, Brinquedos, Geral (fallback, sem subcategoria). Ponto de partida do lançamento, não fechada para sempre.
2. **`Category`/`Subcategory` são `VARCHAR` livre, NÃO enum.** O dicionário de palavras-chave é tratado como "config versionada" (dado), não schema — novas categorias/subcategorias podem ser adicionadas editando o dicionário, sem migration/deploy de schema.
3. **Sem recategorização retroativa.** Produtos antigos (`Category = "Geral"` das 6 categorias legadas) não recebem backfill — são upsertados por `(Platform, ExternalId)` a cada ciclo do `CollectorJob`, e ofertas têm ciclo de vida curto o suficiente para se atualizarem naturalmente ou saírem de circulação. Volume residual relevante em "Geral" pós-lançamento é item de backlog separado, fora desta issue.
4. **Arquitetura de classificação em 2 camadas, camada 1 realocada para a coleta:**
   - Camada 1 (dicionário/keyword match, sem custo) roda em `CollectAsync`, nos 3 collectors, para todo produto — aprovado ou não.
   - Camada 2 (IA, fallback) permanece restrita ao `ProcessorJob`, só para produtos já aprovados no score (`Status == Queued`) — igual à posição da camada de dicionário hoje.
   - **Camada 2 NÃO é combinada com `ScoreProductAsync`** — evita gastar chamada paga em produtos que serão rejeitados/descartados. `ScoreProductAsync` não ganha responsabilidade de categorização.
5. **Teto de gasto mensal de IA**: `claude.monthly_budget_limit_brl` em `app_settings`, valor padrão **R$ 30/mês**. Se o gasto acumulado do mês ultrapassar o teto, a camada 2 (fallback de categorização via IA) é desativada automaticamente até o dia 1 do mês seguinte — produtos sem match no dicionário ficam em "Geral" nesse período. Scoring e legenda (funções core do produto) **nunca** são desativados por esse teto — o teto rege exclusivamente o fallback de categorização.
6. **Ordenação padrão do site continua por `AiScore`** (relevância), inalterada. Filtros e ordenações alternativas (categoria, subcategoria, preço, desconto mínimo, menor preço, maior desconto, mais recente) são adicionais/opcionais — o usuário escolhe, o padrão sem filtros não muda.
7. **Remoção de `Platform` do DTO público é higiene de contrato de dados**, motivada por segurança/privacidade estratégica (não expor a origem/estratégia de curadoria por plataforma via scraping do endpoint público) — não é mudança visual (confirmado na Fase 1: nenhum componente do site renderiza badge de plataforma hoje). `Platform` continua existindo normalmente no banco de dados e nos DTOs internos (dashboard), usado para gerar `AffiliateLink`.

## Integrações
- Anthropic Claude API (`ClaudeAiService`) — nenhuma mudança na integração externa em si; o fallback de categorização (camada 2) é uma nova chamada/prompt, mas usa a integração já existente. Precisa de contabilização de custo estimado por chamada, persistida em `app_settings`, para o teto mensal.
- Nenhuma integração externa nova.

## Restrições
- **Migration aditiva**: `Subcategory` (VARCHAR, nullable ou com default) adicionado a `products`, sem quebrar dados existentes (produtos antigos ficam com `Subcategory = NULL`/vazio até serem naturalmente reprocessados por um novo ciclo de coleta).
- `app_settings` precisa suportar `claude.monthly_budget_limit_brl` (configurável, default R$30) e um contador de gasto acumulado do mês corrente, resetado no dia 1.
- Dicionário expandido precisa cobrir as 9 categorias / ~35 subcategorias, aplicado de forma idêntica nos 3 collectors (Amazon, MercadoLivre, Shopee) — sem lógica duplicada ou divergente por plataforma.
- Sem prazo explícito além do fluxo normal do pipeline.
- Decisões técnicas específicas (onde calcular/persistir o custo estimado por chamada; estrutura de índices compostos para os novos filtros; se o endpoint novo de filtros substitui ou convive com `/api/public/deals/category/{categoria}`) ficam para a etapa de Arquitetura (ver seção abaixo) — não são bloqueantes de negócio, mas afetam o desenho técnico.

## Definição de pronto
Ver `documentacoes/ISSUE-167-categorizacao-unificada/criterios-aceite.md` para os critérios Given/When/Then completos. Resumo:
- Migration aditiva aplicada (`Subcategory` em `products`), sem quebrar dados/produtos existentes.
- Dicionário expandido (9 categorias / ~35 subcategorias) rodando em `CollectAsync` nos 3 collectors, sem custo de IA.
- Fallback via IA no `ProcessorJob`, condicionado a `Status == Queued` E dicionário sem match E orçamento do mês não estourado.
- Contador de custo/orçamento em `app_settings` (`claude.monthly_budget_limit_brl`, default R$30), resetado mensalmente, desativando automaticamente a camada 2 sem afetar scoring/legenda.
- `Platform` removido de `PublicDealDto` (contrato público), mantido nos DTOs internos/dashboard.
- Novo endpoint `GET /api/public/deals` com filtros combináveis (`category`, `subcategory`, `minPrice`, `maxPrice`, `minDiscount`, `sort`) e `GET /api/public/categories` (árvore com contagem).
- Frontend (Home): dropdowns dependentes categoria→subcategoria, slider de preço, botões de desconto mínimo, seletor de ordenação — ordenação padrão inalterada (`AiScore`).

## Ambiguidade arquitetural avaliada pelo PM
**Existe ambiguidade real que exige o Arquiteto.** A sequência de negócio (dicionário na coleta, IA restrita ao pós-aprovação, sem combinar com scoring) já foi decidida pelo Gerente no Gate 1 — não é mais uma decisão em aberto. Mas restam decisões técnicas não-óbvias, de integração/infraestrutura, sem resposta única de negócio:
1. **Onde calcular e persistir o custo estimado de cada chamada à Claude API** para alimentar o contador de orçamento em `app_settings` — no próprio `ClaudeAiService` (camada transversal a todas as chamadas: scoring, legenda, categorização) ou só no ponto de fallback de categorização? Precisa decidir granularidade de contabilização (por chamada, por token, estimativa fixa por tipo de prompt) e onde fica o "cofre" do contador (linha única em `app_settings` vs. tabela dedicada de uso mensal).
2. **Estrutura de índices compostos** para suportar os novos filtros combináveis (`category`, `subcategory`, `sale_price`, `discount_pct`) com performance aceitável em `PublicController`.
3. **Convivência ou substituição da rota atual** `GET /api/public/deals/category/{categoria}` frente ao novo endpoint `GET /api/public/deals?category=&subcategory=&...` — decisão de versionamento/depreciação de contrato de API.

Essas são decisões de arquitetura/integração, não de negócio — encaminhado ao Arquiteto antes do refinamento técnico do LT.
