# Critérios de Aceite — ISSUE-167: Categorização unificada + remoção de distinção de plataforma

## 1. Migration aditiva (`Subcategory`)

**Cenário 1.1 — Coluna adicionada sem quebrar dados existentes**
- Given o banco de produção/homolog tem produtos existentes com `Category` preenchido (livre, incluindo as 6 categorias legadas) e nenhum `Subcategory`
- When a migration é aplicada (`ALTER TABLE products ADD COLUMN subcategory VARCHAR(100) NULL` ou com default vazio)
- Then todos os produtos existentes continuam legíveis e utilizáveis, com `Subcategory = NULL` (ou vazio), sem erro de aplicação e sem necessidade de backfill

**Cenário 1.2 — `Category`/`Subcategory` permanecem VARCHAR livre**
- Given o schema após a migration
- When um novo valor de categoria/subcategoria (fora das ~35 subcategorias v1) é persistido por qualquer camada (dicionário ou IA)
- Then a persistência funciona sem erro de constraint/enum — nenhuma validação de schema restringe os valores possíveis (a lista v1 é config, não schema)

## 2. Dicionário expandido rodando na coleta

**Cenário 2.1 — Dicionário roda em `CollectAsync`, todos os collectors**
- Given um produto sendo coletado por `AmazonCollector`, `MercadoLivreCollector` ou `ShopeeCollector`
- When `CollectAsync` executa e o título/descrição do produto contém palavras-chave mapeadas para uma das 9 categorias/~35 subcategorias v1
- Then o produto é criado já com `Category` e `Subcategory` corretos, atribuídos pelo dicionário, sem nenhuma chamada à API do Claude
- And o comportamento é idêntico independentemente de qual dos 3 collectors originou o produto (mesma lógica de classificação, sem divergência por plataforma)

**Cenário 2.2 — Sem match no dicionário**
- Given um produto cujo título/descrição não contém nenhuma palavra-chave mapeada
- When `CollectAsync` executa a classificação
- Then o produto é criado com `Category = "Geral"` e `Subcategory = NULL`/vazio

**Cenário 2.3 — Cobertura das 9 categorias**
- Given o dicionário expandido
- When avaliado contra uma amostra de produtos reais de teste cobrindo cada uma das 9 categorias (Eletrodomésticos, Climatização, Ferramentas, Eletrônicos, Casa e Cozinha, Beleza, Moda, Brinquedos, Geral)
- Then cada categoria não-"Geral" tem ao menos uma palavra-chave por subcategoria mapeada, e os testes automatizados de `CategoryDetector` cobrem ao menos um caso de match por categoria e por subcategoria

## 3. Fallback via IA no `ProcessorJob`

**Cenário 3.1 — Fallback acionado (condições atendidas)**
- Given um produto com `Status == Queued` (aprovado no score) e `Category == "Geral"` (dicionário não encontrou match) e o gasto acumulado do mês corrente está abaixo de `claude.monthly_budget_limit_brl`
- When `ProcessorJob` processa o produto
- Then a camada de IA é acionada para tentar classificar (`category`/`subcategory`), antes da geração de slug/legenda, e o resultado (se houver classificação) sobrescreve `Category`/`Subcategory` do produto

**Cenário 3.2 — Fallback NÃO acionado: produto não aprovado**
- Given um produto com `Status != Queued` (rejeitado no score) e `Category == "Geral"`
- When `ProcessorJob` processa (ou não processa) o produto
- Then a camada de IA de categorização nunca é chamada para esse produto — nenhum custo é gerado

**Cenário 3.3 — Fallback NÃO acionado: dicionário já classificou**
- Given um produto com `Status == Queued` e `Category != "Geral"` (dicionário já classificou na coleta)
- When `ProcessorJob` processa o produto
- Then a camada de IA de categorização não é chamada para esse produto

**Cenário 3.4 — Fallback NÃO combinado com `ScoreProductAsync`**
- Given a implementação do fluxo de scoring e classificação
- When se inspeciona o código/testes de `ScoreProductAsync`
- Then `ScoreProductAsync` não recebe nem retorna responsabilidade de categoria/subcategoria — a chamada de scoring permanece com sua assinatura/escopo atual, sem `needsAiCategory` ou campos de categoria no retorno

## 4. Contador de custo / orçamento em `app_settings`

**Cenário 4.1 — Configuração do teto**
- Given `app_settings` após a migration/seed
- When consultado o valor de `claude.monthly_budget_limit_brl`
- Then o valor default é R$ 30,00 (configurável sem deploy, via `app_settings`)

**Cenário 4.2 — Contabilização de custo por chamada**
- Given uma chamada de fallback de categorização via Claude é executada com sucesso
- When a chamada retorna
- Then o custo estimado dessa chamada é somado ao contador acumulado do mês corrente em `app_settings`

**Cenário 4.3 — Desativação automática da camada 2 ao estourar o teto**
- Given o contador acumulado do mês corrente é maior ou igual a `claude.monthly_budget_limit_brl`
- When um produto elegível ao fallback (Status == Queued, Category == "Geral") é processado pelo `ProcessorJob`
- Then o fallback de IA NÃO é acionado, o produto permanece em "Geral", e nenhuma nova chamada de categorização é feita até o reset mensal

**Cenário 4.4 — Scoring e legenda não são afetados pelo teto**
- Given o contador acumulado do mês corrente ultrapassou `claude.monthly_budget_limit_brl`
- When um produto qualquer é coletado e processado
- Then `ScoreProductAsync` (scoring) e a geração de legenda continuam funcionando normalmente, sem qualquer restrição — o teto rege exclusivamente o fallback de categorização

**Cenário 4.5 — Reset mensal**
- Given o dia 1 de um novo mês
- When o contador de gasto é avaliado
- Then o contador acumulado do mês corrente é resetado para zero e a camada 2 volta a ficar disponível (se estava desativada por orçamento)

## 5. Remoção de `Platform` do DTO público

**Cenário 5.1 — `Platform` ausente do contrato público**
- Given um cliente consumindo `GET /api/public/deals` ou `GET /api/public/deals/{id}` (ou equivalente)
- When a resposta JSON é inspecionada
- Then o campo `platform` (ou qualquer campo equivalente que identifique a plataforma de origem) não está presente em `PublicDealDto`

**Cenário 5.2 — `Platform` preservado internamente**
- Given o dashboard (Angular) ou qualquer chamada de API interna/autenticada que retorna o DTO interno de produto
- When a resposta é inspecionada
- Then o campo `Platform` continua presente e correto, sem alteração de comportamento

**Cenário 5.3 — `AffiliateLink` não afetado**
- Given um produto de qualquer plataforma
- When o `AffiliateLink` é gerado (fluxo interno)
- Then a geração continua usando `Platform` normalmente — nenhuma regressão na geração de link de afiliado

## 6. Novos endpoints de filtro e categorias

**Cenário 6.1 — Filtros combináveis, todos opcionais**
- Given o endpoint `GET /api/public/deals`
- When chamado sem nenhum parâmetro de filtro
- Then o comportamento é equivalente ao endpoint atual sem filtros (mesma paginação, mesma ordenação padrão por `AiScore`)

**Cenário 6.2 — Filtro por categoria e subcategoria**
- Given produtos com diferentes combinações de `Category`/`Subcategory`
- When `GET /api/public/deals?category=Eletrônicos&subcategory=Celulares` é chamado
- Then somente produtos com `Category == "Eletrônicos" AND Subcategory == "Celulares"` são retornados

**Cenário 6.3 — Filtro por faixa de preço e desconto mínimo**
- Given produtos com diferentes preços e percentuais de desconto
- When `GET /api/public/deals?minPrice=100&maxPrice=500&minDiscount=30` é chamado
- Then somente produtos com preço de venda entre 100 e 500 (inclusive) E desconto >= 30% são retornados

**Cenário 6.4 — Ordenação alternativa via `sort`**
- Given o parâmetro `sort` com valores suportados (ex.: `price_asc`, `discount_desc`, `recent`, ou equivalente)
- When `GET /api/public/deals?sort=price_asc` é chamado
- Then os resultados vêm ordenados por preço crescente, e demais valores de `sort` produzem a ordenação correspondente

**Cenário 6.5 — Ordenação padrão inalterada**
- Given `GET /api/public/deals` chamado sem parâmetro `sort`
- When a resposta é avaliada
- Then a ordenação é por `AiScore` decrescente, igual ao comportamento atual

**Cenário 6.6 — Filtro com valor não reconhecido**
- Given `GET /api/public/deals?category=CategoriaInexistente`
- When chamado
- Then a resposta é 200 com lista vazia (nenhum produto casa o filtro), não erro 400/500

**Cenário 6.7 — Endpoint de árvore de categorias**
- Given produtos ativos distribuídos entre categorias/subcategorias
- When `GET /api/public/categories` é chamado
- Then a resposta retorna uma árvore `Category > [Subcategory]` contendo apenas categorias/subcategorias com ao menos 1 produto ativo, cada uma com a contagem de produtos ativos correspondente

## 7. Frontend — filtros na Home

**Cenário 7.1 — Dropdowns dependentes**
- Given a Home carregada, consumindo `GET /api/public/categories`
- When o usuário seleciona uma Categoria no primeiro dropdown
- Then o dropdown de Subcategoria é populado apenas com as subcategorias daquela categoria (ou fica desabilitado/vazio se a categoria não tiver subcategorias, ex. "Geral")

**Cenário 7.2 — Slider de preço e botões de desconto**
- Given a Home carregada
- When o usuário ajusta o slider de faixa de preço e/ou clica em um botão de desconto mínimo (10%+/30%+/50%+)
- Then a lista de ofertas é atualizada refletindo os filtros aplicados, combinados com quaisquer outros filtros já ativos (categoria/subcategoria)

**Cenário 7.3 — Seletor de ordenação**
- Given a Home carregada com a ordenação padrão (relevância/`AiScore`)
- When o usuário troca a ordenação (menor preço, maior desconto, mais recente)
- Then a lista é reordenada conforme a opção escolhida, sem alterar os filtros já aplicados

**Cenário 7.4 — Sem badge de plataforma**
- Given a Home ou a página de detalhe de um produto
- When renderizada
- Then nenhum elemento visual identifica a plataforma de origem do produto (confirma achado técnico da Fase 1: já não havia badge; garantir que nenhuma regressão introduza um)

**Cenário 7.5 — Estado sem resultados**
- Given uma combinação de filtros que não retorna nenhum produto
- When aplicada na Home
- Then é exibido um estado vazio claro (mensagem "nenhuma oferta encontrada" ou equivalente), sem erro visual/quebra de layout
