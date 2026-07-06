# Critérios de Aceite — Collector Amazon (ISSUE-4)

> Reescritos na Fase 2 (PM) em termos de `CollectAsync(CancellationToken ct)` (interface `IPlatformCollector` já existente), conforme respostas do Gate 1 na Issue #4.

## 1. Coleta com credenciais válidas

**Given** as configurações `amazon.access_key`, `amazon.secret_key`, `amazon.partner_tag` e `amazon.marketplace` (= `www.amazon.com.br`) estão corretamente preenchidas em `app_settings`
**When** `AmazonCollector.CollectAsync(ct)` é chamado
**Then** o método retorna uma coleção com pelo menos 1 produto, cada um com `SalePrice > 0`, `AffiliateLink` preenchido no formato `https://www.amazon.com.br/dp/{ASIN}?tag={partner_tag}` e `ExternalId` (ASIN) preenchido.

## 2. Fail-fast por credenciais ausentes/inválidas

**Given** `amazon.access_key`, `amazon.secret_key` ou `amazon.partner_tag` está em branco ou não configurado
**When** `CollectAsync(ct)` é chamado
**Then** uma `InvalidOperationException` com mensagem descritiva é lançada **antes de qualquer chamada HTTP** à PAAPI.

## 3. Fail-fast por marketplace não suportado

**Given** `amazon.marketplace` está configurado com valor diferente de `www.amazon.com.br`
**When** `CollectAsync(ct)` é chamado
**Then** uma `InvalidOperationException` ("Marketplace não suportado nesta versão") é lançada antes de qualquer chamada HTTP.

## 4. Scoring automático de produto novo

**Given** um produto retornado pela PAAPI cuja combinação `(Platform, ExternalId)` ainda não existe no banco
**When** o produto é persistido durante `CollectAsync(ct)`
**Then** `IAiService.ScoreProductAsync` é chamado para esse produto, e o produto salvo tem `AiScore` preenchido e `Status` igual a `Queued` (score >= `Product.AiScoreThreshold`) ou `Rejected` (score abaixo do threshold).

## 5. Deduplicação — produto já existente não duplica

**Given** um produto já existente no banco com a mesma combinação `(Platform, ExternalId)` de um item retornado pela PAAPI
**When** `CollectAsync(ct)` é executado novamente
**Then** nenhum novo registro é criado para essa combinação — o total de produtos com aquele `(Platform, ExternalId)` no banco permanece 1.

## 6. Upsert de preço/mídia em produto existente

**Given** um produto já existente no banco (mesmo `Platform` + `ExternalId`) cujo preço ou imagem mudou na Amazon
**When** `CollectAsync(ct)` coleta esse mesmo produto novamente
**Then** os campos `SalePrice`, `OriginalPrice`, `DiscountPct`, `MediaUrl`/`ImageUrl` e `UpdatedAt` são atualizados no registro existente, e os campos `Id`, `Status`, `AiScore`, `Slug` e `CollectedAt`/`CreatedAt` permanecem inalterados (produto **não** é re-scoreado pela IA).

## 7. Retry com backoff em rate limit (HTTP 429)

**Given** a PAAPI responde HTTP 429 em uma chamada durante `CollectAsync(ct)`
**When** o collector processa essa resposta
**Then** ele tenta novamente até 3 vezes, com espera de 2s, 4s e 8s entre as tentativas, antes de desistir daquela chamada.

## 8. Falha final de rate limit aborta o ciclo sem exception

**Given** as 3 tentativas de retry em uma chamada com 429 se esgotam sem sucesso
**When** a última tentativa falha
**Then** o collector loga um `Warning` descritivo e aborta o ciclo inteiro de coleta **sem lançar exception** — produtos já persistidos anteriormente no mesmo ciclo permanecem salvos/na fila.

## 9. Rate limiting entre requisições

**Given** múltiplas requisições subsequentes à PAAPI durante um mesmo ciclo de `CollectAsync(ct)`
**When** o collector realiza cada nova chamada
**Then** há um intervalo de pelo menos 1 segundo (`Task.Delay(1000)`) entre requisições consecutivas.

## 10. Zero resultados

**Given** a busca por "oferta do dia" na PAAPI retorna zero itens
**When** `CollectAsync(ct)` é executado
**Then** o método retorna uma coleção vazia, sem lançar exceção e sem erros logados como falha.

## 11. Acionamento manual do ciclo de coleta

**Given** o sistema está em execução com o job Hangfire registrado
**When** um operador aciona a coleta via dashboard Hangfire ou via `POST /api/jobs/collector/trigger`
**Then** o `CollectorJob` correspondente é executado, invocando `AmazonCollector.CollectAsync(ct)` uma vez (sem agendamento recorrente automático nesta Issue).

## 12. Testes com mock HTTP

**Given** a suíte de testes unitários do `AmazonCollector`
**When** `dotnet test` é executado
**Then** todos os testes passam utilizando mock de `HttpMessageHandler`, sem qualquer chamada real à API da Amazon.
