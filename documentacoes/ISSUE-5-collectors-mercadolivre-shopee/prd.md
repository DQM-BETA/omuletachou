# PRD — Issue #5: Collectors MercadoLivre e Shopee

## Objetivo
Implementar dois novos coletores de produtos (`MercadoLivreCollector` e `ShopeeCollector`), seguindo o padrão `IPlatformCollector` (`CollectAsync(CancellationToken ct)`) já estabelecido pelo `AmazonCollector` (Issue #4), para ampliar as fontes de produtos elegíveis a publicação como afiliado.

## Usuários afetados
- Administrador do sistema (dashboard Angular): configura credenciais das plataformas em `app_settings` e acompanha produtos coletados.
- Sistema automatizado (Hangfire Jobs): executa os coletores periodicamente, sem intervenção humana.
- Indiretamente: seguidores do canal/site que recebem as publicações geradas a partir dos produtos coletados.

## Casos de uso principais

1. **Coleta MercadoLivre** (`MercadoLivreCollector.CollectAsync`):
   - Verifica cache de token OAuth2 em `app_settings` (`mercadolivre.access_token` + `mercadolivre.token_expires_at`), com margem de 5 minutos antes da expiração.
   - Se ausente/expirado: solicita novo token via `POST /oauth/token` (`client_credentials`) e persiste ambos os campos antes de prosseguir.
   - Busca produtos mais vendidos via `GET /sites/MLB/search?sort=best_seller&limit=20`.
   - Faz upsert por `(Platform, ExternalId)`.
   - **Não gera link de afiliado na coleta** — `AffiliateLink` fica `null` até aprovação pelo scoring (ver Regras de Negócio).
   - Aciona `IAiService.ScoreProductAsync` apenas para produtos novos.

2. **Coleta Shopee** (`ShopeeCollector.CollectAsync`):
   - Autentica via HMAC-SHA256 no header `Authorization: SHA256 {signature}` usando `shopee.app_id` + `shopee.secret`.
   - Consulta produtos via GraphQL `getProducts(sortType: 2, limit: 20)`.
   - Mapeia mídia: usa vídeo quando disponível (`MediaType = "video"`); fallback para imagem (`MediaType = "image"`) quando não há vídeo; produto sem nenhuma mídia é salvo com `MediaUrl`/`MediaType` nulos.
   - Link de afiliado (`offerLink`) da Shopee **já vem pronto na resposta da API** — pode ser atribuído diretamente ao `AffiliateLink` na criação (diferente do ML, que depende de aprovação do scoring). *(nota: caso a Shopee não deva ter link imediato por paridade de regra de negócio, LT deve confirmar; ver "Pontos em aberto")*.
   - Faz upsert por `(Platform, ExternalId)`.
   - Aciona `IAiService.ScoreProductAsync` apenas para produtos novos.

3. **Produto já existente**: em ambas as plataformas, produto já coletado anteriormente (mesmo `ExternalId`) tem preço/mídia atualizados via `Product.UpdateFromCollector` sem repetir o scoring de IA.

4. **Geração de link de afiliado ML pós-scoring** (fora do escopo de implementação desta Issue, mas contrato de dados afetado): o `ProcessorJob` (Issue #6, ainda não implementado) chama `POST /affiliate-tools/links` do ML somente para produtos aprovados (`Status == Queued`), preenchendo `AffiliateLink` antes da criação de entradas na `PublicationQueue`.

## Casos de exceção
- **Credenciais ausentes**: `InvalidOperationException` fail-fast antes de qualquer chamada HTTP, identificando a chave exata ausente (ex.: `mercadolivre.access_token`, `shopee.app_id`, `shopee.secret`, `shopee.affiliate_id`).
- **Falha de rede/resposta não-sucesso**: ciclo abortado sem exception (log de warning), mesmo padrão do `AmazonCollector` — retorna coleção vazia/parcial.
- **Rate limit (429)**:
  - MercadoLivre: delay de 150ms entre chamadas (10 req/s); retry com backoff exponencial 3x (2s/4s/8s); aborta sem exception se persistir.
  - Shopee: delay de 1s entre chamadas; mesma estratégia de backoff 3x (2s/4s/8s).
- **Falha de uma plataforma não afeta a outra**: o job orquestrador (`CollectorJob` — ver "Pontos em aberto") itera os collectors em sequência, captura exceção por collector individualmente, loga Error e prossegue ao próximo. Produtos coletados com sucesso seguem normalmente para o `ProcessorJob`.
- **Produto Shopee sem nenhuma mídia**: salvo mesmo assim, com `MediaUrl`/`MediaType` nulos (não descartado).

## Regras de negócio
- Upsert por `(Platform, ExternalId)` — já suportado pela entidade `Product` desde a Issue #4.
- Scoring de IA (`IAiService.ScoreProductAsync`) ocorre apenas na criação do produto (não em updates).
- `ProductStatus` definido por `Product.UpdateAiResult` conforme threshold `AiScoreThreshold`.
- **`AffiliateLink` torna-se nullable na entidade `Product`** (mudança de contrato — ver "Mudança na entidade Product" abaixo). Para o ML, o link só é preenchido pelo `ProcessorJob` após aprovação do scoring. A Amazon (Issue #4) continua preenchendo o link de forma síncrona na criação — comportamento inalterado, campo apenas deixa de ser obrigatório na assinatura do construtor.
- **Novos campos `MediaUrl` (string?) e `MediaType` (string?, valores "video"|"image")** substituem o uso implícito de `ImageUrl` para os novos collectors. `ImageUrl` permanece existente na entidade (usado pela Amazon); os novos campos são adicionais, não um replace.

## Mudança na entidade `Product`
- Construtor: parâmetro `affiliateLink` passa a aceitar `null`/vazio (remover o `ArgumentNullException` fail-fast atual em `Product.cs:53-54`, ou tornar a validação condicional/removida). Propriedade `AffiliateLink` passa a ser `string?`.
- Novos campos: `MediaUrl` (`string?`) e `MediaType` (`string?`).
- `UpdateFromCollector` deve aceitar e atualizar `MediaUrl`/`MediaType` além dos parâmetros já existentes (mantendo compatibilidade com `ImageUrl` para Amazon).
- **Migration nova necessária**: alterar coluna `AffiliateLink` para nullable no schema; adicionar colunas `MediaUrl` (nullable) e `MediaType` (nullable) na tabela `Products`.
- Nenhuma mudança de comportamento para o `AmazonCollector` (Issue #4): continua preenchendo `AffiliateLink` de forma síncrona na criação, exercitando o mesmo caminho de código com o parâmetro agora opcional.

## Pontos em aberto / decisões do LT
- **CollectorJob orquestrador** (Hangfire, que itera os collectors em sequência com captura de exceção isolada): mencionado nas respostas do Gate 1 mas **não existe ainda no código** (Issue #4 só criou endpoint manual de trigger por collector). PM avalia que a criação deste job de orquestração está fora do escopo estrito desta Issue #5 (que trata dos collectors ML/Shopee em si) — **LT deve decidir se cria o `CollectorJob` como parte do task breakdown desta Issue (é o consumidor natural dos 3 collectors agora existentes: Amazon, ML, Shopee) ou abre uma sub-issue/issue separada de orquestração**. Recomendação do PM: como o job só orquestra collectors já existentes e o comportamento (log + prossegue) já está definido no Gate 1, é razoável incluir no escopo desta Issue se o LT concordar que não há risco de regressão.
- **Link de afiliado da Shopee**: a API retorna `offerLink` pronto na resposta do produto (diferente do ML, que exige uma chamada extra pós-aprovação). PM assume que a Shopee pode preencher `AffiliateLink` diretamente na coleta (sem custo de chamada extra), mas como o Gate 1 não foi perguntado especificamente sobre a Shopee, **LT deve confirmar esta interpretação ou tratar Shopee com o mesmo adiamento do ML por consistência de regra de negócio** (only-scored-products-get-links).

## Integrações externas
- MercadoLivre API (`api.mercadolibre.com`) — OAuth2 client_credentials, REST.
- Shopee Affiliate API (`open-api.affiliate.shopee.com.br`) — HMAC-SHA256, GraphQL.

## Restrições / prazo
- Dependência: Issues #2 (Domain/EF Core/Schema), #3 (ClaudeAiService), #4 (padrão AmazonCollector).
- Sem prazo explícito informado na Issue.

## Definição de pronto
- `MercadoLivreCollector` e `ShopeeCollector` implementados como `IPlatformCollector`, seguindo `CollectAsync(CancellationToken ct)`.
- Entidade `Product` atualizada: `AffiliateLink` nullable, novos campos `MediaUrl`/`MediaType`; migration criada e aplicada.
- Cache de token OAuth2 do ML funcional (`mercadolivre.access_token` + `mercadolivre.token_expires_at`, margem de 5 min).
- Rate limiting e retry com backoff implementados para ambas as plataformas (150ms/10 req/s ML; 1s Shopee; backoff 2s/4s/8s em 429).
- Upsert por `(Platform, ExternalId)` funcional para ambas as plataformas.
- Scoring de IA acionado na criação de novos produtos (não em updates).
- Falha de um collector não impede o outro (comportamento validado por teste, independente de o `CollectorJob` orquestrador estar nesta Issue ou não).
- Fail-fast (`InvalidOperationException`) com mensagem identificando a chave exata ausente, para ambos os collectors.
- Testes unitários com mock HTTP cobrindo: sucesso, credenciais ausentes, falha de rede/resposta não-sucesso, 429 com retry, refresh de token ML, produto Shopee sem mídia — sem chamadas reais às APIs.
- `dotnet test` passando.
