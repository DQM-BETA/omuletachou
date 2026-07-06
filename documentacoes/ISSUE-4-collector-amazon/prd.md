# PRD — Collector Amazon (PAAPI v5 + Scoring Automático)

## Objetivo de negócio
Coletar automaticamente os produtos em oferta mais relevantes da Amazon (via Product Advertising API v5) para alimentar o catálogo de ofertas do omuletachou, aplicando triagem automática de qualidade via IA (Claude) antes de qualquer publicação.

## Problema que resolve
Hoje a curadoria de produtos é manual. O collector automatiza a descoberta de ofertas na Amazon, elimina duplicidade e usa IA para filtrar produtos de baixa qualidade/relevância antes de entrarem na fila de publicação.

## Usuários afetados
- Operação/curadoria do omuletachou (indiretamente: menos trabalho manual de garimpo de ofertas)
- Consumidores finais do site/canais de afiliado (recebem ofertas mais relevantes e sem duplicatas)

## Casos de uso principais
1. **Coleta periódica**: o sistema busca até 20 produtos com a keyword "oferta do dia" na Amazon PAAPI v5, ordenados por relevância.
2. **Deduplicação**: produto já existente (mesma plataforma + identificador externo) não gera novo registro.
3. **Scoring automático**: todo produto novo salvo passa por `IAiService.ScoreProductAsync`, resultando em `Status = Pending` (aguardando fila) ou `Rejected` (reprovado pela IA).
4. **Geração de link de afiliado**: cada produto coletado recebe link com `partner_tag` da conta de afiliado.

## Casos de exceção
- Credenciais Amazon ausentes/inválidas em `app_settings`.
- PAAPI retorna erro de rate limit (429) ou erro de autenticação (401/403).
- PAAPI fora do ar / timeout.
- Produto já existente com preço desatualizado.
- Zero resultados retornados pela busca.

## Regras de negócio (conhecidas, já claras na Issue)
- Rate limiting: 1 requisição/segundo para a PAAPI (`Task.Delay(1000)` entre requests).
- Upsert: produto com mesma plataforma + identificador externo (ASIN) não é duplicado.
- Após persistir produto novo: chamar `IAiService.ScoreProductAsync`, que já define `Status` (Pending/Rejected) internamente via `Product.UpdateAiResult` (ver `Product.cs`, threshold `AiScoreThreshold = 6`).
- Link de afiliado: `https://www.amazon.com.br/dp/{ASIN}?tag={partner_tag}`.
- Campos coletados da PAAPI: título, preço de venda, preço original (`SavingBasis`), imagem principal.
- Keywords fixas: "oferta do dia", `SortBy: Relevance`, `ItemCount: 20`.

## Integrações externas
- Amazon Product Advertising API v5 (`webservices.amazon.com.br/paapi5/searchitems`), autenticação AWS Signature V4 manual (sem SDK AWS).
- `IAiService` (Claude, já implementado na Issue #3) para scoring.
- Persistência via EF Core (domínio definido na Issue #2).

## Restrições técnicas conhecidas
- PAAPI exige ao menos 1 venda de afiliado nos primeiros 180 dias, ou o acesso à API é revogado (risco operacional, não é decisão de escopo desta issue).
- `IPlatformCollector` já existe no domínio (`Domain/Interfaces/IPlatformCollector.cs`), com assinatura `Task<IEnumerable<Product>> CollectAsync(CancellationToken ct)` — **diferente** da assinatura citada nos critérios de aceite da Issue (`FetchBestSellersAsync(20)`). Ver Gate 1, pergunta 7.
- Entidade `Product` (Domain/Entities/Product.cs) **não possui campo `ExternalId`/ASIN** nem `Platform+ExternalId` como chave de deduplicação explícita — ver Gate 1, pergunta 1.

## Pontos em aberto (ver perguntas de Gate 1 na Issue)
Ver comentário "Gate 1 — Perguntas para o Gerente" na Issue #4. Bloqueiam o refinamento técnico (Fase 2) até resposta:
1. Estratégia de deduplicação (campo ASIN/ExternalId ainda não existe no domínio).
2. Comportamento em erro 429 da PAAPI (retry/backoff).
3. Comportamento quando credenciais Amazon não configuradas (fail-fast vs log e skip).
4. Agendamento do job (Hangfire) — frequência de execução.
5. Marketplace fixo BR ou configurável.
6. Upsert de preço para produtos já existentes.
7. Alinhamento de assinatura do método público do collector (`CollectAsync` vs `FetchBestSellersAsync`).

## Definição de pronto
- `AmazonCollector : IPlatformCollector` implementado em `AfiliadoBot.Infrastructure/Integrations/Platforms/`.
- Autenticação AWS Signature V4 funcional sem SDK AWS.
- Deduplicação e upsert funcionando conforme decisão do Gate 1.
- Todo produto novo persistido recebe score de IA e status resultante (Pending/Rejected).
- Rate limiting de 1 req/s respeitado.
- Testes unitários com mock de `HttpMessageHandler` (sem chamadas reais à PAAPI) cobrindo os critérios de aceite da Issue.
- Credenciais lidas de `app_settings` (`amazon.access_key`, `amazon.secret_key`, `amazon.partner_tag`, `amazon.marketplace`).
