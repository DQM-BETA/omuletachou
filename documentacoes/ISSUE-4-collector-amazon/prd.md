# PRD — Collector Amazon (PAAPI v5 + Scoring Automático)

> Atualizado na Fase 2 (PM) com as respostas do Gate 1 (Gerente, 2026-07-06). Ver Issue #4, comentários "Gate 1 — Perguntas para o Gerente" e "Gate 1 — Respostas do Gerente".

## Objetivo de negócio
Coletar automaticamente os produtos em oferta mais relevantes da Amazon (via Product Advertising API v5) para alimentar o catálogo de ofertas do omuletachou, aplicando triagem automática de qualidade via IA (Claude) antes de qualquer publicação.

## Problema que resolve
Hoje a curadoria de produtos é manual. O collector automatiza a descoberta de ofertas na Amazon, elimina duplicidade e usa IA para filtrar produtos de baixa qualidade/relevância antes de entrarem na fila de publicação.

## Usuários afetados
- Operação/curadoria do omuletachou (indiretamente: menos trabalho manual de garimpo de ofertas)
- Consumidores finais do site/canais de afiliado (recebem ofertas mais relevantes e sem duplicatas)

## Casos de uso principais
1. **Coleta manual sob demanda**: o método `AmazonCollector.CollectAsync(CancellationToken ct)` (implementação de `IPlatformCollector`) busca até `amazon.max_results` (padrão 20) produtos com a keyword "oferta do dia" na Amazon PAAPI v5, ordenados por relevância (`SortBy: Relevance`).
2. **Acionamento do ciclo de coleta**: via dashboard Hangfire (execução manual do job) ou endpoint `POST /api/jobs/collector/trigger`. Agendamento recorrente (cron) é **fora do escopo** desta Issue — tratado em issue futura de orquestração de jobs.
3. **Deduplicação/Upsert**: produto já existente (mesma combinação `Platform` + `ExternalId`) não gera novo registro; atualiza campos de preço/mídia (ver Regras de negócio).
4. **Scoring automático**: todo produto **novo** salvo passa por `IAiService.ScoreProductAsync`, resultando em `Status = Queued` (score >= `AiScoreThreshold`) ou `Rejected` (abaixo do threshold), via `Product.UpdateAiResult`.
5. **Geração de link de afiliado**: cada produto coletado recebe link no formato `https://www.amazon.com.br/dp/{ASIN}?tag={partner_tag}`.

## Casos de exceção
- **Credenciais ausentes/inválidas**: `amazon.access_key`, `amazon.secret_key` ou `amazon.partner_tag` em branco → `InvalidOperationException` com mensagem descritiva, lançada **antes de qualquer chamada HTTP** (fail-fast).
- **Marketplace inválido**: `amazon.marketplace` diferente de `www.amazon.com.br` → `InvalidOperationException` ("Marketplace não suportado nesta versão"), validado junto com as credenciais antes de qualquer chamada HTTP.
- **Rate limit (HTTP 429)**: retry com backoff exponencial, máximo 3 tentativas (2s, 4s, 8s de espera entre tentativas). Após a 3ª falha: loga `Warning` e **aborta o ciclo inteiro do collector** (não lança exception — produtos já salvos no ciclo permanecem persistidos/na fila).
- **PAAPI fora do ar / timeout / outros erros HTTP não-429**: não especificado explicitamente pelo Gerente; tratar como falha do ciclo (mesmo comportamento do retry esgotado: loga Warning, aborta o ciclo, não lança exception) — a ser confirmado/ajustado pelo LT/Dev se surgir ambiguidade de implementação.
- **Zero resultados retornados pela busca**: ciclo termina normalmente sem erros, sem produtos novos.
- **Produto já existente com preço desatualizado**: tratado via upsert (ver Regras de negócio, item Upsert de preço).

## Regras de negócio
- **Rate limiting**: 1 requisição/segundo para a PAAPI (`Task.Delay(1000)` entre requests).
- **Retry em 429**: 3 tentativas com backoff exponencial (2s, 4s, 8s). Falha final → loga Warning, aborta o ciclo (sem exception).
- **Fail-fast de configuração**: credenciais ausentes/inválidas OU marketplace != `www.amazon.com.br` → `InvalidOperationException` antes de qualquer chamada HTTP.
- **Deduplicação/Upsert por `(Platform, ExternalId)`**:
  - Produto **novo** (combinação inexistente): cria registro, chama `IAiService.ScoreProductAsync` para definir `AiScore`/`AiReason`/`Status`.
  - Produto **existente** (mesma combinação): atualiza `SalePrice`, `OriginalPrice`, `DiscountPct`, `MediaUrl` (equivalente a `ImageUrl`) e `UpdatedAt`. Preserva `Id`, `Status`, `AiScore`, `Slug`, `CollectedAt` (equivalente a `CreatedAt`) — **não** reexecuta o scoring de IA.
- **Link de afiliado**: `https://www.amazon.com.br/dp/{ASIN}?tag={partner_tag}`.
- **Campos coletados da PAAPI**: título (`ItemInfo.Title`), preço de venda (`Offers.Listings.Price`), preço original (`Offers.Listings.SavingBasis`), imagem principal (`Images.Primary.Large`).
- **Keywords fixas**: `"oferta do dia"`, `SortBy: Relevance`, `ItemCount` configurável via `amazon.max_results` (padrão 20, interno ao `AmazonCollector`, não exposto na assinatura de `IPlatformCollector`).
- **Marketplace fixo BR** nesta fase: `amazon.marketplace` deve ser lido e validado como `www.amazon.com.br`.

## Integrações externas
- Amazon Product Advertising API v5 (`webservices.amazon.com.br/paapi5/searchitems`), autenticação AWS Signature V4 manual (sem SDK AWS).
- `IAiService` (Claude, já implementado na Issue #3) para scoring — chamado via injeção direta dentro do `AmazonCollector` (sem necessidade de orquestrador externo; decisão de negócio fechada, sem ambiguidade arquitetural).
- Persistência via EF Core (domínio definido na Issue #2).
- Hangfire: exposição de acionamento manual via dashboard + endpoint `POST /api/jobs/collector/trigger`. Agendamento recorrente é escopo de issue futura.

## Mudança na entidade `Product` (Domain)
- Adicionar campo `ExternalId` (`string`, não nulo) à entidade `Product` (`AfiliadoBot.Domain/Entities/Product.cs`) — identificador externo da plataforma (ASIN para Amazon; reutilizável para MercadoLivre/Shopee no futuro).
- Criar **índice único composto** `(Platform, ExternalId)` via Fluent API no `AppDbContext` (`AfiliadoBot.Infrastructure`), garantindo a regra de deduplicação no nível de banco.
- **Nova migration EF Core** necessária para adicionar a coluna `ExternalId` (NOT NULL) e o índice único composto à tabela `Products` existente — a cargo do LT/Dev no refinamento técnico. Atenção a dados pré-existentes na tabela (se houver produtos sem `ExternalId`, migration precisa de estratégia de backfill ou valor default transitório).
- Necessário adicionar/ajustar método na entidade (ex.: `UpdateFromCollector(...)` ou similar) para o upsert de preço/mídia preservando `Id`, `Status`, `AiScore`, `Slug`, `CreatedAt` — decisão de nome/assinatura do método fica com o LT/Dev.

## Restrições técnicas conhecidas
- PAAPI exige ao menos 1 venda de afiliado nos primeiros 180 dias, ou o acesso à API é revogado (risco operacional, não é decisão de escopo desta issue).
- Método público a implementar: `CollectAsync(CancellationToken ct)`, assinatura já definida em `IPlatformCollector` (`Domain/Interfaces/IPlatformCollector.cs`). Confirmado no Gate 1 — não há mais divergência de assinatura.
- Credenciais lidas de `app_settings`: `amazon.access_key`, `amazon.secret_key`, `amazon.partner_tag`, `amazon.marketplace`, `amazon.max_results` (novo, padrão 20).

## Definição de pronto
- `AmazonCollector : IPlatformCollector` implementado em `AfiliadoBot.Infrastructure/Integrations/Platforms/`, expondo `CollectAsync(CancellationToken ct)`.
- Autenticação AWS Signature V4 funcional sem SDK AWS.
- Campo `ExternalId` adicionado à entidade `Product` + migration criando a coluna e o índice único `(Platform, ExternalId)`.
- Deduplicação e upsert de preço/mídia funcionando conforme regras acima.
- Fail-fast (`InvalidOperationException`) para credenciais ausentes/inválidas e marketplace não suportado, antes de qualquer chamada HTTP.
- Retry com backoff exponencial (3x: 2s/4s/8s) em 429; após falha final, loga Warning e aborta o ciclo sem lançar exception.
- Todo produto novo persistido recebe score de IA (`IAiService.ScoreProductAsync`) e status resultante (`Queued`/`Rejected`); produtos já existentes não são re-scoreados.
- Rate limiting de 1 req/s respeitado entre requests à PAAPI.
- Endpoint `POST /api/jobs/collector/trigger` disponível para acionamento manual do ciclo (via Hangfire).
- Testes unitários com mock de `HttpMessageHandler` (sem chamadas reais à PAAPI) cobrindo os critérios de aceite (ver `criterios-aceite.md`).
- Credenciais e configuração lidas de `app_settings` (`amazon.access_key`, `amazon.secret_key`, `amazon.partner_tag`, `amazon.marketplace`, `amazon.max_results`).

## Pontos que seguem para o refinamento técnico (LT)
- Nome/assinatura exata do método de upsert na entidade `Product`.
- Estratégia de migration para backfill de `ExternalId` em produtos pré-existentes (se aplicável no ambiente atual).
- Tratamento específico de erros HTTP não-429 (timeout, 5xx, DNS) — se deve seguir o mesmo fluxo de "loga e aborta o ciclo" ou merece tratamento distinto.
- Estrutura de implementação da assinatura AWS Signature V4 manual (detalhe técnico, sem impacto em requisito de negócio).
