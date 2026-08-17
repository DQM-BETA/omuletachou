# Critérios de Aceite — ISSUE-182: MercadoLivreCollector quebrado — reconstruir com Highlights API

## 1. Mapeamento de categorias internas → categorias reais do Mercado Livre

**Cenário 1.1 — Cobertura das 8 categorias**
- Given a lista curada de categorias internas (Eletrodomésticos, Climatização, Ferramentas, Eletrônicos, Casa e Cozinha, Beleza, Moda, Brinquedos)
- When o mapeamento técnico é aplicado
- Then cada uma das 8 categorias internas possui ao menos um ID de categoria real do Mercado Livre (`MLB####`) associado, documentado (código ou config), obtido a partir de `GET /sites/MLB/categories`

**Cenário 1.2 — Categoria interna sem correspondência clara**
- Given uma categoria interna cujo mapeamento para o Mercado Livre não é 1:1 óbvio (ex. abrange mais de uma categoria de topo)
- When o mapeamento é definido
- Then a decisão (agregação de múltiplos `category_id` ou escolha do mais representativo) está documentada com a justificativa, não apenas implementada silenciosamente

## 2. Coleta via Highlights API por categoria

**Cenário 2.1 — Top 10 por categoria**
- Given uma categoria mapeada com produtos em destaque disponíveis
- When `MercadoLivreCollector.CollectAsync` chama `GET /highlights/MLB/category/{category_id}`
- Then até 10 IDs de produto são obtidos, ordenados por `position` (do Highlights API), sem paginação adicional

**Cenário 2.2 — Ciclo cobre todas as categorias mapeadas**
- Given as 8 categorias mapeadas e a Highlights API disponível para todas
- When um ciclo completo de `CollectAsync` roda
- Then a Highlights API é chamada uma vez para cada uma das 8 categorias no ciclo

## 3. Resolução de detalhes via multi-get (`/items?ids=...`)

**Cenário 3.1 — IDs do Highlights resolvidos em produtos completos**
- Given uma lista de IDs de produto obtida do Highlights API para uma ou mais categorias
- When o collector chama `GET /items?ids=...` (em um ou mais lotes, conforme o limite real confirmado da API)
- Then cada ID é resolvido em um objeto com título, preço, imagem e link original (`permalink`), usados para montar o `Product`

**Cenário 3.2 — Respeito ao limite de IDs por chamada**
- Given um ciclo com mais IDs coletados do total do que o limite máximo de IDs suportado por uma única chamada do `/items?ids=...` (limite confirmado durante o refinamento técnico)
- When o collector monta as chamadas de multi-get
- Then os IDs são agrupados em lotes que respeitam esse limite, sem nenhuma chamada rejeitada por excesso de IDs

**Cenário 3.3 — Item não resolvido no multi-get**
- Given um ID retornado pelo Highlights que não é encontrado/retornado pelo multi-get (ex. removido entre as duas chamadas)
- When o collector processa a resposta do multi-get
- Then esse ID é ignorado (log de aviso), sem interromper o processamento dos demais IDs do lote/ciclo

## 4. Mapeamento para `Product` e upsert

**Cenário 4.1 — Produto mapeado com os mesmos campos dos demais collectors**
- Given um produto resolvido via multi-get, pertencente a uma categoria mapeada
- When o collector monta o `Product` correspondente
- Then os campos (preço, desconto quando aplicável, imagem, `SourceUrl`, `ExternalId`, `Platform = MercadoLivre`, categoria interna de origem) são preenchidos na mesma estrutura já usada pelos collectors de Amazon/Shopee

**Cenário 4.2 — Upsert reaproveitado sem lógica nova**
- Given um produto já existente no banco com o mesmo `(Platform, ExternalId)`
- When o novo fluxo de coleta encontra esse produto novamente em um ciclo
- Then `UpdateFromCollector` é chamado normalmente (comportamento já existente), atualizando preço/mídia, sem nenhuma lógica de deduplicação nova introduzida por esta issue

**Cenário 4.3 — Mesmo produto em destaque em mais de uma categoria no mesmo ciclo**
- Given um produto cujo ID aparece nos resultados de Highlights de duas categorias mapeadas diferentes no mesmo ciclo
- When o collector processa ambas as ocorrências
- Then o upsert por `(Platform, ExternalId)` resulta em um único registro do produto, sem duplicação e sem erro

## 5. Isolamento de falha por categoria

**Cenário 5.1 — Falha em uma categoria não aborta o ciclo**
- Given uma categoria cuja chamada à Highlights API falha (erro HTTP, timeout ou rate limit)
- When `CollectAsync` executa o ciclo completo
- Then a categoria com falha é pulada (log de erro registrado) e as demais categorias do ciclo são processadas normalmente, com produtos coletados delas

**Cenário 5.2 — Falha em um lote de multi-get não aborta o ciclo**
- Given um lote de IDs cuja chamada a `/items?ids=...` falha
- When o restante do ciclo continua
- Then apenas os produtos daquele lote específico não são coletados neste ciclo (log de erro), demais lotes e categorias seguem normalmente

**Cenário 5.3 — Falha total (nenhuma categoria disponível) não derruba o `CollectorJob`**
- Given todas as categorias mapeadas falharem no mesmo ciclo (cenário extremo)
- When `CollectorJob` executa
- Then o job termina o ciclo sem exceção não tratada, registra os erros e outras plataformas (Amazon, Shopee) continuam coletando normalmente no mesmo ciclo — mesmo padrão de isolamento já existente entre plataformas

## 6. Frequência inalterada

**Cenário 6.1 — Cron mantido**
- Given a configuração `schedule.collector_cron` já existente em `app_settings`
- When o novo `MercadoLivreCollector` é implantado
- Then a coleta de Mercado Livre continua rodando 1x/dia, no mesmo horário/cadência configurada, sem nenhuma mudança de schedule necessária para esta issue

## 7. Validação end-to-end do link de afiliado (requisito crítico)

**Cenário 7.1 — Link de afiliado real, não o link original**
- Given um produto de Mercado Livre coletado pelo novo fluxo, aprovado no score (`Status == Queued`) e processado pelo `ProcessorJob`
- When `EnsureAffiliateLinkAsync` gera o `AffiliateLink` (via `affiliate-tools/links`, endpoint já existente)
- Then o `AffiliateLink` resultante é validado manualmente/ao vivo (ambiente local, credenciais reais) como um link de afiliado real do Mercado Livre — formato/domínio reconhecível de link de afiliado, contendo identificação rastreável da conta/tag do Gerente — e é diferente do `permalink`/link original do produto

**Cenário 7.2 — Validação não se satisfaz apenas com HTTP 200**
- Given a chamada a `affiliate-tools/links` responde HTTP 200 com um link no corpo da resposta
- When a validação desta issue é realizada
- Then a validação exige inspecionar o conteúdo do link retornado (não apenas o status HTTP) para confirmar a presença da tag/identificação de afiliado — um 200 com um link sem tag de afiliado reconhecível é tratado como reprovação deste critério, não como sucesso

**Cenário 7.3 — Achado de defeito na geração do link é tratado como problema separado**
- Given a validação end-to-end revela que `EnsureAffiliateLinkAsync`/`affiliate-tools/links` não está de fato gerando um link com tag de afiliado válida
- When esse achado ocorre durante o desenvolvimento ou QA desta issue
- Then o achado é documentado e reportado (ex. `.claude/melhorias/` ou nova Issue), sem que a correção desse componente (fora do escopo original desta issue, que é a coleta) seja feita silenciamente dentro desta issue sem sinalização

## 8. Sem regressão no restante do pipeline

**Cenário 8.1 — Scoring inalterado**
- Given um produto de Mercado Livre coletado pelo novo fluxo
- When `ScoreProductAsync` processa o produto
- Then o comportamento de scoring é idêntico ao aplicado a produtos de Amazon/Shopee, sem lógica especial introduzida para Mercado Livre

**Cenário 8.2 — Categorização (Issue #167) inalterada**
- Given um produto de Mercado Livre coletado pelo novo fluxo
- When a classificação por dicionário (`CategoryDetector`) roda em `CollectAsync`
- Then a categorização funciona da mesma forma que para os demais collectors, sem regressão da funcionalidade entregue na Issue #167

**Cenário 8.3 — Fila de publicação inalterada**
- Given um produto de Mercado Livre aprovado e com `AffiliateLink` gerado
- When o produto segue para a fila de publicação
- Then o comportamento de enfileiramento/publicação é idêntico ao já existente para as demais plataformas, sem alteração nesta issue

**Cenário 8.4 — Amazon e Shopee não afetados**
- Given o deploy da correção do `MercadoLivreCollector`
- When um ciclo completo do `CollectorJob` roda para as 3 plataformas
- Then `AmazonCollector` e `ShopeeCollector` continuam funcionando exatamente como antes desta issue, sem nenhuma alteração de comportamento ou regressão
