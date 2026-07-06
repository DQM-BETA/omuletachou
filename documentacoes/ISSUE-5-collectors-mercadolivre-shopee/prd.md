# PRD — Issue #5: Collectors MercadoLivre e Shopee

## Objetivo
Implementar dois novos coletores de produtos (`MercadoLivreCollector` e `ShopeeCollector`), seguindo o padrão `IPlatformCollector` já estabelecido pelo `AmazonCollector` (Issue #4), para ampliar as fontes de produtos elegíveis a publicação como afiliado.

## Usuários afetados
- Administrador do sistema (dashboard Angular): configura credenciais das plataformas em `app_settings` e acompanha produtos coletados.
- Sistema automatizado (Hangfire Jobs): executa os coletores periodicamente, sem intervenção humana.
- Indiretamente: seguidores do canal/site que recebem as publicações geradas a partir dos produtos coletados.

## Casos de uso principais
1. **Coleta MercadoLivre**: o job periódico autentica via OAuth2 (`client_credentials`), busca os produtos mais vendidos (`best_seller`) via `GET /sites/MLB/search`, gera o link de afiliado via `POST /affiliate-tools/links`, faz upsert por `(Platform, ExternalId)` e aciona `IAiService.ScoreProductAsync`.
2. **Coleta Shopee**: o job periódico autentica via HMAC-SHA256, consulta produtos via GraphQL (`getProducts`), aplica fallback de mídia (imagem quando não há vídeo), faz upsert e aciona o scoring de IA.
3. **Produto já existente**: em ambas as plataformas, produto já coletado anteriormente (mesmo `ExternalId`) tem preço/mídia atualizados sem repetir o scoring de IA (mesmo padrão do `Product.UpdateFromCollector`).

## Casos de exceção
- Credenciais ausentes ou inválidas (`InvalidOperationException` fail-fast, mesmo padrão do AmazonCollector) — **a confirmar no Gate 1**.
- Falha de rede ou resposta HTTP não-sucesso de uma das plataformas — ciclo abortado sem exception (log de warning), mesmo padrão do AmazonCollector.
- Rate limit (429) — **a confirmar se ML/Shopee possuem limite documentado; se sim, aplicar mesmo padrão de retry com backoff da Amazon.**
- Produto sem imagem/vídeo em nenhum formato — **a confirmar comportamento (descartar item ou manter sem mídia).**

## Regras de negócio conhecidas
- Upsert por `(Platform, ExternalId)` — já suportado pela entidade `Product` desde a Issue #4.
- Scoring de IA (`IAiService.ScoreProductAsync`) ocorre apenas na criação do produto (não em updates), mesmo padrão do AmazonCollector.
- `ProductStatus` definido por `Product.UpdateAiResult` conforme threshold `AiScoreThreshold`.

## Pontos em aberto (ver Gate 1)
- Assinatura do método do collector (`CollectAsync` vs. `FetchBestSellersAsync` citado na Issue).
- Suporte a `media_type`/`media_url` — a entidade `Product` atual só possui `ImageUrl`; Issue sugere suporte a vídeo (Shopee).
- Estratégia de token do OAuth2 do MercadoLivre (cache/refresh vs. novo token a cada execução).
- Momento da geração do link de afiliado do ML (todos os produtos coletados vs. só aprovados pelo scoring).
- Rate limiting de ML/Shopee.
- Independência de execução entre os dois collectors no mesmo Job.
- Fail-fast de credenciais ausentes (mesmo padrão da Amazon).

## Integrações externas
- MercadoLivre API (`api.mercadolibre.com`) — OAuth2 client_credentials, REST.
- Shopee Affiliate API (`open-api.affiliate.shopee.com.br`) — HMAC-SHA256, GraphQL.

## Restrições / prazo
- Dependência: Issues #2 (Domain/EF Core/Schema), #3 (ClaudeAiService), #4 (padrão AmazonCollector).
- Sem prazo explícito informado na Issue.

## Definição de pronto (proposta, sujeita a confirmação)
- `MercadoLivreCollector` e `ShopeeCollector` implementados como `IPlatformCollector`, seguindo o padrão de `CollectAsync(CancellationToken ct)`.
- Upsert por `(Platform, ExternalId)` funcional para ambas as plataformas.
- Scoring de IA acionado na criação de novos produtos.
- Testes unitários com mock HTTP cobrindo sucesso, credenciais ausentes e falha de rede/resposta não-sucesso, sem chamadas reais às APIs.
- `dotnet test` passando.
