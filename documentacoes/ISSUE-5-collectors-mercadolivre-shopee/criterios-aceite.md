# Critérios de Aceite — Issue #5: Collectors MercadoLivre e Shopee

Formato Given/When/Then. Método de entrada em todos os cenários: `CollectAsync(CancellationToken ct)`.

## MercadoLivreCollector

### Autenticação OAuth2 / cache de token
1. Given não há `mercadolivre.access_token`/`mercadolivre.token_expires_at` em `app_settings`, When `CollectAsync` é chamado, Then um novo token é obtido via `POST /oauth/token` (client_credentials) e ambos os campos são salvos em `app_settings` antes de qualquer busca de produtos.
2. Given `mercadolivre.token_expires_at` está no futuro com mais de 5 minutos de folga, When `CollectAsync` é chamado, Then nenhuma chamada a `/oauth/token` é feita; o token em cache é reutilizado.
3. Given `mercadolivre.token_expires_at` está no passado ou a menos de 5 minutos do agora, When `CollectAsync` é chamado, Then um novo token é solicitado e os campos em `app_settings` são atualizados antes da busca de produtos.

### Coleta e upsert
4. Given credenciais válidas e token cacheado, When `CollectAsync` é chamado, Then produtos retornados por `GET /sites/MLB/search?sort=best_seller&limit=20` são mapeados e persistidos via upsert por `(Platform.MercadoLivre, ExternalId)`.
5. Given um produto ML já existente no banco (mesmo `ExternalId`), When `CollectAsync` roda novamente com preço/mídia atualizados na API, Then `Product.UpdateFromCollector` é chamado e o scoring de IA NÃO é reexecutado.
6. Given um produto ML novo, When ele é persistido, Then `AffiliateLink` é salvo como `null` (não preenchido na coleta) e `IAiService.ScoreProductAsync` é acionado.

### Rate limiting e retry
7. Given múltiplas chamadas sequenciais à API do ML, When `CollectAsync` executa, Then há um delay de pelo menos 150ms entre chamadas HTTP.
8. Given a API do ML responde 429, When `CollectAsync` está em execução, Then o collector aguarda 2s, tenta novamente; se 429 de novo, aguarda 4s, tenta; se 429 de novo, aguarda 8s, tenta; se ainda 429, aborta o ciclo sem lançar exception (loga warning).

### Falhas
9. Given `mercadolivre.access_token`/client_id/client_secret ausentes ou vazios em `app_settings`, When `CollectAsync` é chamado, Then lança `InvalidOperationException` com mensagem identificando exatamente qual chave está ausente, antes de qualquer chamada HTTP.
10. Given a API do ML retorna erro de rede ou status não-sucesso (não-429), When `CollectAsync` é chamado, Then o ciclo é abortado sem exception, com log de warning, retornando coleção vazia/parcial.

## ShopeeCollector

### Coleta e upsert
11. Given credenciais HMAC válidas (`shopee.app_id`, `shopee.secret`, `shopee.affiliate_id`), When `CollectAsync` é chamado, Then a query GraphQL `getProducts(sortType: 2, limit: 20)` é executada com header `Authorization: SHA256 {signature}` calculado corretamente.
12. Given produtos retornados pela API Shopee, When `CollectAsync` processa cada item, Then o upsert ocorre por `(Platform.Shopee, ExternalId)` usando `productId` como chave externa.

### Mídia
13. Given um produto Shopee com vídeo disponível, When coletado, Then `MediaType = "video"` e `MediaUrl` aponta para a URL do vídeo.
14. Given um produto Shopee sem vídeo mas com imagem, When coletado, Then `MediaType = "image"` e `MediaUrl` aponta para `productImage`.
15. Given um produto Shopee sem vídeo e sem imagem, When coletado, Then o produto é salvo mesmo assim, com `MediaUrl = null` e `MediaType = null` (não é descartado).

### Rate limiting e retry
16. Given múltiplas chamadas sequenciais à API da Shopee, When `CollectAsync` executa, Then há um delay de pelo menos 1s entre chamadas HTTP.
17. Given a API da Shopee responde 429, When `CollectAsync` está em execução, Then aplica o mesmo backoff 3x (2s/4s/8s) e aborta sem exception se persistir.

### Falhas
18. Given `shopee.app_id`, `shopee.secret` ou `shopee.affiliate_id` ausentes ou vazios, When `CollectAsync` é chamado, Then lança `InvalidOperationException` identificando exatamente qual chave está ausente, antes de qualquer chamada HTTP.
19. Given a API da Shopee retorna erro de rede ou status não-sucesso (não-429), When `CollectAsync` é chamado, Then o ciclo é abortado sem exception, com log de warning.

## Independência entre collectors
20. Given o `MercadoLivreCollector` lança exceção (ex.: credenciais ausentes) durante a execução orquestrada, When o `ShopeeCollector` roda em seguida (mesmo ciclo/job), Then o `ShopeeCollector` executa normalmente e completa sua coleta, sem ser impedido pela falha do ML (e vice-versa).

## Entidade Product (mudança de contrato)
21. Given o construtor de `Product` é chamado com `affiliateLink = null` ou vazio, When a instância é criada, Then nenhuma exception é lançada (validação de obrigatoriedade removida) e `AffiliateLink` fica `null`.
22. Given o `AmazonCollector` (Issue #4) continua preenchendo `affiliateLink` de forma síncrona na criação, When seus testes existentes rodam após a mudança de contrato, Then nenhum teste do `AmazonCollector` quebra (regressão zero).
23. Given a migration de banco aplicada, When a tabela `Products` é inspecionada, Then a coluna `AffiliateLink` aceita `NULL` e as colunas `MediaUrl`/`MediaType` existem como nullable.

## Testes (transversal)
24. Given `dotnet test` é executado, When os testes de `MercadoLivreCollector` e `ShopeeCollector` rodam, Then nenhuma chamada HTTP real é feita (mocks) e todos os testes passam.
