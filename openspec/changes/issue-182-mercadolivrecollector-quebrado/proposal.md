# Proposal — ISSUE-182: MercadoLivreCollector quebrado — reconstruir coleta com Highlights API

## Objetivo
Restaurar a coleta de produtos do Mercado Livre, hoje 100% quebrada porque o endpoint usado pelo `MercadoLivreCollector` (`GET /sites/MLB/search?sort=best_seller`) passou a responder 403 para qualquer chamada (mudança de política da plataforma em 2026, confirmada ao vivo, não é problema de credencial). A coleta passa de um modelo "1 busca site-wide, top N geral" para um modelo "iterar uma lista curada de categorias, top 10 produtos em destaque por categoria via Highlights API", mantendo a mesma frequência (1x/dia) e o mesmo comportamento de upsert/scoring/publicação já existente. Adicionalmente, valida-se de ponta a ponta — com credenciais reais — que o link final gerado para os produtos do Mercado Livre é de fato um link de afiliado rastreável, não apenas uma chamada de API que responde 200.

## Usuários
- Sistema (`MercadoLivreCollector`, `CollectorJob`, `ProcessorJob`) — passa a iterar categorias em vez de fazer uma única busca site-wide.
- Operador/administrador (dashboard) — consome o resultado normalmente (produtos de Mercado Livre voltam a aparecer na fila), sem mudança de interface.
- Visitante do site público — volta a ver ofertas de Mercado Livre nas categorias cobertas (efeito indireto, sem mudança de contrato de API pública).
- Gerente (dono do link de afiliado) — precisa da garantia de que o link publicado carrega sua tag de afiliado, não o link original do produto.

## Casos de uso principais
1. `CollectorJob` dispara `MercadoLivreCollector.CollectAsync` na cadência atual (`schedule.collector_cron`, 1x/dia — sem mudança).
2. Para cada categoria da lista curada (ver Regras de negócio), o collector chama a Highlights API (`GET /highlights/MLB/category/{category_id}`) e obtém os IDs dos até 10 produtos mais destacados daquela categoria, ordenados por `position`.
3. Os IDs obtidos de todas as categorias do ciclo são resolvidos em detalhes completos (título, preço, imagem, link original) via o endpoint de multi-consulta `GET /items?ids=...` do Mercado Livre — respeitando o limite de IDs por chamada da API (o Arquiteto/LT confirma o valor exato e decide como agrupar os IDs em lotes, se necessário).
4. Cada produto resolvido é mapeado para a mesma estrutura de `Product` usada hoje pelos demais collectors (preço, desconto, imagem, `SourceUrl`, categoria interna já atribuída conforme a categoria do Mercado Livre de origem) e upsertado por `(Platform, ExternalId)`, reaproveitando o `UpdateFromCollector` já existente — sem lógica nova de deduplicação.
5. O produto segue o pipeline normal: scoring (`ScoreProductAsync`), fila de publicação, e no `ProcessorJob`, geração do `AffiliateLink` via `EnsureAffiliateLinkAsync` → `affiliate-tools/links` (endpoint já existente, não alterado por esta issue).
6. Validação de ponta a ponta (requisito crítico do Gerente): rodando localmente com credenciais reais de Mercado Livre, confirmar que o `AffiliateLink` final de ao menos um produto coletado e aprovado é um link de afiliado de fato (domínio/formato reconhecível como link de afiliado do Mercado Livre, contendo a tag do Gerente), e não o link original do produto (`permalink`) nem um link de afiliado "genérico"/sem tag.

## Casos de uso de exceção
- Uma categoria específica falha na chamada da Highlights API (erro/timeout/rate limit) → o ciclo pula essa categoria (log de erro) e segue normalmente com as demais — mesmo padrão de isolamento de falha já usado entre plataformas (Amazon/ML/Shopee independentes entre si).
- O multi-get (`/items?ids=...`) falha para um lote de IDs → o lote afetado é pulado (log de erro), os produtos daquele lote não são coletados neste ciclo; demais lotes/categorias seguem normalmente.
- Um produto retornado pelo Highlights não é resolvido no multi-get (ex.: item removido entre as duas chamadas) → o produto é ignorado silenciosamente (log de aviso), sem interromper o restante do lote.
- Produto já existente (mesmo `ExternalId`) volta a aparecer em destaque em um ciclo seguinte → comportamento de upsert já existente (`UpdateFromCollector`) atualiza preço/mídia, sem lógica nova.
- O mesmo produto aparece destacado em mais de uma categoria no mesmo ciclo → não é necessária deduplicação especial; o upsert por `(Platform, ExternalId)` já absorve o caso (segunda ocorrência no mesmo ciclo apenas atualiza o registro já upsertado na primeira).
- Falha na geração do `AffiliateLink` (`EnsureAffiliateLinkAsync`) → comportamento já existente do `ProcessorJob` para essa falha é mantido, fora de escopo desta issue (não é uma regra nova).

## Regras de negócio (confirmadas no Gate 1)
1. **Lista curada de categorias, não a árvore completa do Mercado Livre.** Ponto de partida: as 8 categorias mapeáveis da taxonomia unificada já usada na Issue #167 — Eletrodomésticos, Climatização, Ferramentas, Eletrônicos, Casa e Cozinha, Beleza, Moda, Brinquedos ("Geral" não se aplica, por não ter correspondência natural no Mercado Livre). O mapeamento de cada uma dessas 8 categorias para o(s) ID(s) de categoria real(is) do Mercado Livre (formato `MLB####`, obtidos via `GET /sites/MLB/categories`) é decisão técnica, feita pelo Arquiteto/LT — não é uma decisão de negócio em aberto.
2. **Volume por categoria: top 10** produtos em destaque, conforme o ranking (`position`) retornado pela Highlights API — sem paginação além disso.
3. **Frequência: 1x/dia, sem mudança.** Mantém `schedule.collector_cron` como está hoje; o aumento no número de chamadas de API por ciclo (N categorias × Highlights + multi-get em lotes) não justifica, por ora, reduzir a frequência — cabe ao Arquiteto avaliar rate limit dentro de um único ciclo diário (não entre ciclos).
4. **Isolamento de falha por categoria.** Categoria que falhar é pulada; o ciclo segue com as demais categorias — mesmo padrão já usado entre plataformas (Amazon/ML/Shopee independentes).
5. **Sem lógica nova de deduplicação/upsert.** O comportamento existente de `UpdateFromCollector` (upsert por `(Platform, ExternalId)`) é suficiente e não muda.
6. **Validação end-to-end do link de afiliado é requisito crítico e bloqueante da Definição de Pronto desta issue** (não apenas "nice to have"). Não basta a chamada a `affiliate-tools/links` responder HTTP 200 — é preciso confirmar, com dados reais, que o link retornado carrega a tag/identificação de afiliado do Gerente e é distinto do link original do produto. Esse endpoint (`EnsureAffiliateLinkAsync`) já existe e não é alterado por esta issue; o que muda é que agora existe uma verificação explícita e documentada de que ele funciona de ponta a ponta para produtos vindos do novo fluxo de coleta.

## Integrações
- API pública do Mercado Livre (`api.mercadolibre.com`), sem mudança de credenciais/escopo OAuth (já configuradas e funcionando em `app_settings`):
  - `GET /sites/MLB/categories` — obtenção dos IDs de categoria reais (uso pontual/cacheável, não a cada ciclo necessariamente — decisão técnica).
  - `GET /highlights/MLB/category/{category_id}` — substitui `GET /sites/MLB/search?sort=best_seller` como fonte de produtos em destaque, agora por categoria.
  - `GET /items?ids=...` — multi-get para resolver detalhes completos dos produtos a partir dos IDs do Highlights (endpoint ainda não testado ao vivo pela investigação da issue; testar limite de IDs por chamada e formato de resposta faz parte do refinamento técnico).
  - `POST/GET affiliate-tools/links` (via `EnsureAffiliateLinkAsync`) — sem mudança de contrato, mas ganha um critério de validação explícito nesta issue.
- Nenhuma integração externa nova além das já mapeadas na investigação técnica da própria Issue #182.

## Restrições
- Sem alteração de schema/migration prevista — a estrutura de `Product` já suporta os dados vindos do novo fluxo (mesmos campos usados pelos demais collectors).
- `AmazonCollector`/`ShopeeCollector` fora de escopo, não devem ser tocados.
- `EnsureAffiliateLinkAsync`/`affiliate-tools/links` (geração do link de afiliado) não deve ser alterado — o requisito crítico é de **validação**, não de mudança de implementação, a menos que a validação revele um defeito real (nesse caso, o achado deve ser tratado como um novo problema, reportado antes de qualquer alteração fora do escopo original).
- Nenhuma credencial deve ser commitada; testes ao vivo com credenciais reais rodam apenas em ambiente local, conforme já orientado na Issue.
- Limite de IDs por chamada do `/items?ids=...` e política de rate limit entre as N chamadas de Highlights + M chamadas de multi-get por ciclo são decisões técnicas não-óbvias, sem resposta de negócio única — ver seção de ambiguidade abaixo.

## Definição de pronto
Ver `documentacoes/ISSUE-182-mercadolivrecollector-quebrado/criterios-aceite.md` para os critérios Given/When/Then completos. Resumo:
- `MercadoLivreCollector` reconstruído sobre Highlights API + multi-get, cobrindo as 8 categorias mapeadas, top 10 produtos por categoria.
- Mapeamento categoria interna → ID(s) de categoria real(is) do Mercado Livre documentado e testado.
- Isolamento de falha por categoria (log + continue), sem abortar o ciclo inteiro.
- Upsert existente (`UpdateFromCollector`) reaproveitado sem lógica nova.
- Frequência inalterada (`schedule.collector_cron`, 1x/dia).
- Validação end-to-end, com credenciais reais em ambiente local, de que o `AffiliateLink` final de um produto coletado por este novo fluxo é um link de afiliado rastreável (tag do Gerente), distinto do link original do produto — documentada e reproduzível.
- Sem regressão no restante do pipeline: scoring, categorização (Issue #167), fila de publicação e collectors de Amazon/Shopee continuam funcionando como hoje.

## Ambiguidade arquitetural avaliada pelo PM
**Existe ambiguidade real que exige o Arquiteto**, apesar de as decisões de negócio (categorias, volume, frequência, fallback, dedupe) já estarem fechadas pelo Gerente no Gate 1. Restam decisões técnicas não-óbvias, de integração externa, sem resposta única de negócio:
1. **Mapeamento das 8 categorias internas para ID(s) reais de categoria do Mercado Livre** (`MLB####`) — algumas categorias internas (ex. "Casa e Cozinha") podem corresponder a mais de uma categoria de topo do Mercado Livre, exigindo agregação de múltiplos `category_id` por categoria interna; decisão de qual(is) ID(s) usar por categoria não é óbvia sem inspecionar a árvore real (`/sites/MLB/categories`).
2. **Limite de IDs por chamada do `/items?ids=...`** (multi-get) — endpoint ainda não testado ao vivo pela investigação técnica da issue; é preciso confirmar o limite documentado/real e decidir a estratégia de lotes (batching) caso o total de IDs por ciclo (até 8 categorias × 10 = 80 IDs) exceda o limite de uma única chamada.
3. **Rate limit dentro de um mesmo ciclo diário** — o novo fluxo faz bem mais chamadas por ciclo (N Highlights + M multi-get) do que a busca única anterior; é preciso decidir se há necessidade de espaçamento/retry entre chamadas dentro do mesmo ciclo para não estourar limites da API do Mercado Livre.
4. **Onde e como cachear/obter a árvore de categorias** (`/sites/MLB/categories`) — buscar a cada ciclo, cachear em `app_settings`/config estática, ou hardcode dos IDs mapeados após validação manual — decisão técnica de custo/atualização vs. simplicidade.

Essas são decisões de arquitetura/integração externa, não de negócio — encaminhado ao Arquiteto antes do refinamento técnico do LT.
