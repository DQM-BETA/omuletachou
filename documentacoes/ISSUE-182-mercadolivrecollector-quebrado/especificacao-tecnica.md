# Especificação Técnica — ISSUE-182: MercadoLivreCollector quebrado — reconstrução com Highlights API

Consolida `proposal.md` (PRD), `criterios-aceite.md` (27 cenários), `design.md` (decisões 1-4 do
Arquiteto + Seção 10, bloqueio confirmado ao vivo) e a **resolução do Gate 1.5** (decisão do
Gerente, registrada em `estado.md` → `blockers`) em plano técnico executável. Este documento
substitui, na prática, o fluxo descrito nas seções 1-9 do `design.md` (que assumia `GET /items`
acessível) pelo fluxo real confirmado — as decisões técnicas 1 e 3 do `design.md` continuam válidas
como estão; a decisão 2 (batching do multi-get) fica **obsoleta** (não há mais multi-get); a decisão
4 (validação do link de afiliado via API) é **substituída** pelo fluxo semi-manual desenhado aqui.

Todos os paths abaixo são relativos à raiz do repo `omuletachou`.

## 0. Resumo da mudança de forma (por que este documento diverge do `design.md` original)

| | `design.md` original (seções 1-9) | Resolução real (este documento) |
|---|---|---|
| Resolução de detalhes do produto | `GET /items?ids=...` (multi-get, 1 chamada por lote de até 10 IDs) | `GET /products/{catalog_product_id}` + `GET /products/{catalog_product_id}/items` (2 chamadas **por produto**, sem multi-get — endpoint `/items` retorna 403) |
| Batching (Decisão 2, `ChunkIds`/`Enumerable.Chunk`) | Lote de 10 IDs por categoria | **Obsoleto** — resolução é sequencial, 1 produto por vez, sem chunking |
| `SourceUrl`/permalink | Vinha pronto no campo `permalink` do `/items` | Campo `permalink` de `/products/{id}` vem sempre vazio → construído como `https://www.mercadolivre.com.br/p/{catalog_product_id}` (ver Seção 1) |
| Desconto (`OriginalPrice`/`DiscountPct`) | Vinha pronto (`original_price`/`discount`) do `/items` | Sem sinal de desconto disponível nos endpoints acessíveis → `OriginalPrice = SalePrice`, `DiscountPct = 0` (fallback documentado, Seção 2.3) |
| Link de afiliado (`EnsureAffiliateLinkAsync`) | `POST affiliate-tools/links` (endpoint já implementado) | Endpoint inalcançável (404) → fluxo semi-manual: produto fica `AwaitingAffiliateLink`, operador cola o link gerado na ferramenta oficial do ML, dashboard importa em lote (Seção 3) |
| Volume de chamadas HTTP/ciclo | ~16/dia (8 Highlights + 8 multi-get) | Até ~168/dia (8 Highlights + até 80×2 produto/items) — ver Seção 2.4, ainda trivial frente à cota de 18.000/hora da aplicação |

As Decisões 1 (`CategoryMap`, `design.md` §3) e 3 (sem rate limiter dedicado, `design.md` §5)
**não mudam** — seus valores/racional confirmados seguem válidos e são reaproveitados como estão.

## 1. Confirmação ao vivo do padrão de permalink — resultado

Conforme instruído no Gate 1.5 (`estado.md` → `blockers`), tentei confirmar ao vivo, via navegador
real, se `https://www.mercadolivre.com.br/p/{catalog_product_id}` resolve para a página pública do
produto (o LT não tem `curl`/API HTTP como ferramenta de investigação de aplicação em geral — só
`git`/`gh`/mover arquivos — mas o Gate 1.5 pediu explicitamente essa confirmação como pré-requisito
para fechar a especificação; usei o Playwright/Chromium já instalado no projeto (`website/`,
`@playwright/test` 1.62.1) como ferramenta de diagnóstico read-only, sem executar/alterar código de
aplicação, análogo ao que a sessão de LT anterior já havia feito via `curl` na Seção 10 do
`design.md`).

### 1.1 O que foi testado

1. **`curl` direto** (com headers de navegador real: `User-Agent`, `Accept-Language`) em
   `https://www.mercadolivre.com.br/p/MLB16855791` (ID de catálogo real, público, de um produto
   conhecido) → `HTTP 302` para
   `https://www.mercadolivre.com.br/gz/account-verification?go=<url original>` — página de
   verificação anti-bot, mesma família de proteção já documentada na Seção 10 do `design.md` para o
   site público.
2. **Chromium real via Playwright** (`headless: true`, mesmo `User-Agent`/`locale: pt-BR`,
   motor de navegador completo, não `curl`) em dois IDs:
   - `MLB16855791` (ID de catálogo real e existente)
   - `MLB99999999999` (ID inexistente, inventado para servir de controle negativo)

   **Resultado: os dois IDs — o real e o inventado — resolveram para a mesma página de
   verificação anti-bot** (`HTTP 200`, `finalUrl` = `.../gz/account-verification?go=...`, título
   `"Mercado Libre"`), sem nenhuma diferenciação entre um produto que existe e um que não existe.

### 1.2 Conclusão — inconclusivo por ferramental, não por ambiguidade do padrão

Mesmo um motor de navegador completo (Chromium, não uma requisição HTTP simples) é barrado pelo
gate anti-bot do site público **antes de chegar à página do produto em si** — o desafio de
verificação intercepta a rota `/p/{id}` para qualquer cliente sem uma sessão de navegador humana
prévia (cookies + resolução de desafio JS), **independente do ID ser válido ou inválido**. Isso é
consistente com — e agrava — o mesmo tipo de bloqueio anti-bot já documentado na Seção 10 do
`design.md` (lá, para `affiliate-tools/links`; aqui, para o próprio permalink do produto). Não há
ferramenta de automação (nem headless completo) disponível para este agente que resolva esse
desafio — exigiria uma sessão de navegador humana real e logada, o mesmo tipo de limitação já
reconhecido para a geração do link de afiliado.

**Decisão: usar `https://www.mercadolivre.com.br/p/{catalog_product_id}` como `SourceUrl` mesmo
assim**, pelos seguintes motivos, cada um reduzindo o risco de estar errado a um nível aceitável:

1. É o padrão de URL de página de produto de catálogo **publicamente documentado e usado
   universalmente** pelo Mercado Livre (distinto de `/MLB-########-titulo-do-produto` usado para
   anúncios individuais/`item_id`) — o mesmo padrão foi verificado, batendo tudo (protocolo,
   domínio, estrutura `/p/{ID}`), só não foi possível confirmar via automação que o servidor
   efetivamente **resolve** aquele ID específico para a página certa (por causa do gate anti-bot, não
   por incerteza sobre o formato da URL).
2. **Falha visível e segura, não silenciosa**: essa mesma URL (`SourceUrl`) é exatamente o dado que
   o operador vai copiar e colar na ferramenta oficial de geração de link de afiliado do Mercado
   Livre (Seção 3). Se o padrão estiver de fato errado para algum produto, **o próprio operador
   humano vê isso na hora** — a ferramenta oficial do ML mostra erro ou não encontra o produto ao
   colar uma URL inválida — muito antes de qualquer publicação. Não há caminho de propagação
   silenciosa do erro até o site público (o produto fica preso em `AwaitingAffiliateLink` até
   alguém resolver).
3. Constitui-se, portanto, um checkpoint humano natural (o mesmo padrão de "verificação manual
   pelo operador" que a Seção 6 do `design.md` já previa para o link de afiliado, agora estendido
   para cobrir também o formato do link original).

**Não é necessário nem recomendado tentar resolver o bloqueio anti-bot com mais engenharia** (ex.:
replay de sessão de navegador, cookies persistidos) — mesmo risco/fragilidade já descartado pelo
Gerente na Seção 10/resolução do Gate 1.5 para o link de afiliado.

## 2. `MercadoLivreCollector` — reconstrução

`backend/src/AfiliadoBot.Infrastructure/Integrations/Platforms/MercadoLivreCollector.cs`

### 2.1 Fluxo por ciclo (substitui inteiramente `SendWithRetryAsync`/`ParseItems`/`SearchUrl`)

```
foreach (categoriaInterna, mlCategoryIds) in CategoryMap:               // Decisão 1, design.md §3.4 — reaproveitada como está
    foreach mlCategoryId in mlCategoryIds:                              // hoje sempre 1 elemento; suporta N:1 futuro sem mudança de forma
        try:
            highlightIds = GET /highlights/MLB/category/{mlCategoryId}  // até 10 catalog_product_id, ordenados por `position`
        catch (falha de rede / HTTP não-2xx):
            log warning "categoria {categoriaInterna}/{mlCategoryId} falhou, pulando"; continue   // CA 5.1, isolamento por categoria

        delay 300ms                                                     // mesmo delay defensivo da Decisão 3 (design.md §5.2)

        foreach catalogProductId in highlightIds:
            product = await ResolveAndUpsertAsync(catalogProductId, categoriaInterna, ct)
            if product is not null: collected.Add(product)
```

`ResolveAndUpsertAsync` (novo método privado, substitui `UpsertProductAsync` + `ParseItems` juntos):

```
try:
    productResp = GET /products/{catalogProductId}          // -> name, pictures (permalink vem vazio, ignorado)
    delay 300ms
    itemsResp   = GET /products/{catalogProductId}/items     // -> lista de {item_id, price, seller_id, category_id, shipping}
    delay 300ms
catch (falha de rede / HTTP não-2xx / 404):
    log warning "produto {catalogProductId} nao resolvido, pulando"; return null   // CA 3.3/5.2, isolamento por produto (mais granular que o design.md original previa, mas satisfaz o mesmo princípio)

if productResp sem "name" OU itemsResp vazio:
    log warning; return null

item = itemsResp.OrderBy(i => i.price).First()    // Seção 2.2 — critério de escolha entre múltiplos vendedores
salePrice = item.price
originalPrice = salePrice                          // Seção 2.3 — sem sinal de desconto disponível
discountPct = 0
title = productResp.name
thumbnail = productResp.pictures?.FirstOrDefault()?.url
sourceUrl = $"https://www.mercadolivre.com.br/p/{catalogProductId}"   // Seção 1
externalId = catalogProductId                       // NÃO usar item_id — precisa ser estável entre ciclos (CA 4.2/4.3)

// dali em diante: exatamente o UpsertProductAsync já existente hoje (upsert por (Platform, ExternalId),
// CategoryDetector.Detect(title), IAiService.ScoreProductAsync para produto novo, UpdateFromCollector para existente)
```

### 2.2 Critério de escolha entre múltiplos itens/vendedores do mesmo `catalog_product_id`

`GET /products/{catalog_product_id}/items` retorna a lista de anúncios (de vendedores possivelmente
diferentes) que compõem aquele produto de catálogo — sem um campo `buy_box_winner` utilizável
(confirmado `null` na investigação da Seção 10 do `design.md`, em 4 produtos testados). **Decisão:
usar o item de **menor `price`** entre os retornados** — na ausência de um sinal explícito de "oferta
vencedora", o menor preço é o critério mais defensável e mais alinhado ao propósito do collector
(surfacear a melhor oferta disponível daquele produto). Documentar isso como constante nomeada, não
"mágica", no código (ex.: comentário explicando a ausência de `buy_box_winner`).

### 2.3 Ausência de sinal de desconto — fallback documentado

Os campos observados em `/products/{catalog_product_id}/items` (Seção 10.1 do `design.md`:
`item_id`, `price`, `seller_id`, `category_id`, `shipping`) não incluem preço original/lista nem
percentual de desconto. **Antes de codar o parsing, o Dev deve inspecionar o payload real** (rodando
localmente contra a API, mesma prática já usada no projeto — ver `ParseItems` atual, escrito contra
resposta real amostrada) **à procura de um campo equivalente também em `GET /products/{id}`** (não
testado a fundo na investigação da Seção 10, que só confirmou `name`/`pictures`/`permalink`/
`buy_box_winner`). **Se nenhum campo de preço original/desconto existir em nenhuma das duas
respostas**, usar o fallback: `OriginalPrice = SalePrice`, `DiscountPct = 0` — mesmo padrão de
fallback defensivo já usado em `ParseItems` hoje (`finalOriginalPrice = originalPrice ?? salePrice`),
só que aqui o valor "ausente" é a regra, não a exceção. Isso não viola nenhum CA (Cenário 4.1 exige
o campo *preenchido*, não exige que ele reflita um desconto real quando a API não expõe o dado) — é
uma limitação de dado da fonte, documentada, não um bug.

### 2.4 Volume de chamadas por ciclo — atualização da Decisão 3 (`design.md` §5)

Volume revisado: até 8 chamadas de Highlights + até 80 × 2 chamadas de `/products/{id}`+`/items`
(pior caso, 8 categorias × 10 produtos, todos resolvidos) = **até ~168 chamadas HTTP/ciclo**, ainda
1x/dia. Isso é ~10x a estimativa original do `design.md` (~16/dia), mas continua **trivial** frente à
cota de `max_requests_per_hour: 18000` da aplicação (Seção 10.1 do `design.md`) — a decisão da
Seção 5.2 do `design.md` (delay defensivo de 300ms entre chamadas, sem rate limiter dedicado, tratar
HTTP 429 como falha comum já coberta pelo isolamento por categoria/produto) **permanece válida sem
alteração**. Efeito colateral aceito: com 168 chamadas × 300ms, o ciclo completo leva ~50s de delay
puro — irrelevante para um job diário em background (Hangfire).

### 2.5 O que é removido / fica obsoleto

- `SearchUrl`, `SendWithRetryAsync`, `ParseItems`, `RetryDelaysMs`, `MercadoLivreItem` (record) —
  todo o fluxo antigo de busca site-wide.
- `ChunkIds`/`Enumerable.Chunk` (batching do multi-get, `design.md` §4.3) — não existe mais
  multi-get a ser chunkado.
- **Mantido sem alteração**: `LoadSettingsAsync`, `ValidateCredentials`, `EnsureValidTokenAsync`,
  `RequestNewTokenAsync`, `PersistTokenAsync`, `UpsertSettingAsync` (autenticação OAuth2
  `client_credentials` já funciona — confirmado, não é o componente bloqueado), `GenerateSlug`,
  `CategoryDetector.Detect`, upsert por `(Platform, ExternalId)`.
- `CategoryMap` (novo membro estático, `design.md` §3.4) — copiar exatamente a tabela já validada
  ao vivo (8 entradas, todas 1:1).

### 2.6 Critérios de aceite mapeados (revisão do que `criterios-aceite.md` originalmente previa)

CA 1.1/1.2 (categorias) — inalterados, `design.md` §3 já satisfaz. CA 2.1/2.2 (Highlights) —
inalterados. CA 3.1 (título/preço/imagem/link) — satisfeito pela composição `/products/{id}` +
`/products/{id}/items` + `SourceUrl` construído (Seção 1), não mais por um único multi-get. CA 3.2
(respeito a limite de IDs por chamada) — **não se aplica mais** (não há mais lote de IDs numa única
chamada; cada produto é resolvido individualmente) — considerar satisfeito por vacuidade, documentar
no relatório de QA que o cenário mudou de forma. CA 3.3 (item não resolvido é pulado) — satisfeito,
agora à granularidade de produto individual (Seção 2.1). CA 4.1-4.3 (mapeamento/upsert/dedupe) —
inalterados. CA 5.1-5.3 (isolamento de falha) — satisfeitos, isolamento por categoria (Highlights) e
por produto (resolução de detalhes). CA 6.1 (frequência) — inalterado. CA 7.1-7.3 (link de
afiliado) — **substituídos** pela Seção 3 abaixo (fluxo semi-manual). CA 8.1-8.4 (sem regressão) —
inalterados, nada nesta seção toca scoring/categorização/fila/Amazon/Shopee.

## 3. Fluxo semi-manual de link de afiliado

### 3.1 Visão geral

```
MercadoLivreCollector (automático, 1x/dia)
        │
        ▼
ProcessorJob.EnsureAffiliateLinkAsync
        │  produto ML sem AffiliateLink → Status = AwaitingAffiliateLink (NOVO), sem chamada HTTP
        ▼
Dashboard "Links de Afiliado — Mercado Livre" (NOVA tela)
        │  lista produtos AwaitingAffiliateLink com SourceUrl (copiável)
        │  operador copia a lista, cola na ferramenta oficial do ML
        │  ("Gerador de produtos recomendados", mercadolivre.com.br/afiliados/linkbuilder)
        │  operador copia os links gerados de volta, cola na tela
        ▼
POST /api/products/affiliate-links/import (NOVO endpoint)
        │  pareamento por linha/produto explícito (client-side, ver 3.3 — não por ordem implícita no servidor)
        ▼
Product.ResolveAffiliateLink(link) → AffiliateLink preenchido + Status = Queued (NOVO método)
        │
        ▼
Próxima execução do ProcessorJob (agendada ou disparada manualmente em
"Jobs" → "Processor", endpoint já existente `POST /api/jobs/processor/trigger`)
publica normalmente — fluxo já existente, sem mudança
```

### 3.2 Domínio — `Product` (`backend/src/AfiliadoBot.Domain/Entities/Product.cs`)

Dois métodos novos:

```csharp
/// <summary>
/// Marca o produto ML como aguardando importacao manual do link de afiliado (Gate 1.5, Issue
/// #182 — o endpoint affiliate-tools/links nao esta acessivel; fluxo passa a ser semi-manual).
/// </summary>
public void MarkAsAwaitingAffiliateLink()
{
    Status = ProductStatus.AwaitingAffiliateLink;
    UpdatedAt = DateTime.UtcNow;
}

/// <summary>
/// Preenche o AffiliateLink importado manualmente pelo operador (Issue #182) e devolve o
/// produto ao fluxo normal do ProcessorJob (Status = Queued, reprocessado na proxima execucao).
/// </summary>
public void ResolveAffiliateLink(string link)
{
    if (string.IsNullOrWhiteSpace(link))
        throw new ArgumentException("Link nao pode ser nulo ou vazio.", nameof(link));

    AffiliateLink = link;
    Status = ProductStatus.Queued;
    UpdatedAt = DateTime.UtcNow;
}
```

`SetAffiliateLink` (já existente) permanece como está, usado por outros fluxos/testes que não
passam pelo status; não remover.

### 3.3 `ProductStatus` (`backend/src/AfiliadoBot.Domain/Enums/ProductStatus.cs`)

Adicionar **ao final** do enum (preserva os valores `int` já persistidos — sem migration, mesmo
padrão já usado no projeto para conversões de enum, `ProductConfiguration.cs` linha 138,
`HasConversion<int>()`):

```csharp
public enum ProductStatus
{
    Pending,
    Queued,
    Published,
    Rejected,
    Processing,
    Error,
    AwaitingAffiliateLink   // NOVO — Issue #182
}
```

### 3.4 `ProcessorJob.EnsureAffiliateLinkAsync` (`backend/src/AfiliadoBot.Application/Jobs/ProcessorJob.cs`)

Remove a constante `AffiliateLinkUrl` e toda a chamada HTTP (linhas 209-264 do arquivo atual).
Substitui por:

```csharp
private async Task<bool> EnsureAffiliateLinkAsync(Product product, CancellationToken ct)
{
    if (product.Platform != Platform.MercadoLivre || !string.IsNullOrWhiteSpace(product.AffiliateLink))
        return true;

    if (string.IsNullOrWhiteSpace(product.SourceUrl))
    {
        _logger.LogWarning(
            "ProcessorJob: SourceUrl ausente para o produto {ProductId}. Nao e possivel colocar em espera de link de afiliado ML.",
            product.Id);
        product.MarkAsError("SourceUrl ausente — nao e possivel gerar link de afiliado ML");
        return false;
    }

    // Gate 1.5 (Issue #182): affiliate-tools/links nao existe/nao e acessivel. Fluxo semi-manual —
    // produto aguarda importacao manual do link via dashboard (ver especificacao-tecnica.md §3).
    product.MarkAsAwaitingAffiliateLink();
    _logger.LogInformation(
        "ProcessorJob: produto {ProductId} aguardando importacao manual de link de afiliado ML.",
        product.Id);
    return false;
}
```

O restante de `ExecuteAsync` **não muda** — o `if (!linkOk) { save; continue; }` já existente
(linhas 88-93) já cobre o novo caso sem alteração de estrutura. `using System.Net`,
`System.Net.Http.Headers`, `System.Text`, `System.Text.Json` no topo do arquivo ficam órfãos após a
remoção da chamada HTTP — remover os que não forem mais usados por nenhum outro método do arquivo
(checar antes de remover, `DownloadMediaAsync`/outros podem não precisar deles de qualquer forma).

### 3.5 API — novo endpoint de importação

`backend/src/AfiliadoBot.Api/Controllers/ProductsController.cs` — novo método:

```csharp
/// <summary>
/// Issue #182: importa em lote os links de afiliado gerados manualmente pelo operador na
/// ferramenta oficial do Mercado Livre. Pareamento produto/link e feito EXPLICITAMENTE por
/// ProductId no corpo da requisicao (montado pelo dashboard, que ja tem o ProductId de cada
/// linha exibida) — nao por ordem/posicao inferida no servidor, para nao quebrar se a lista de
/// AwaitingAffiliateLink mudar entre a exportacao e a importacao (produto novo entrando em espera
/// no meio do processo, por exemplo). Nunca falha o lote inteiro por um item invalido (mesmo
/// principio de isolamento de falha ja usado no resto do projeto) — cada item e validado e
/// reportado individualmente no resultado.
/// </summary>
[HttpPost("affiliate-links/import")]
public async Task<ActionResult<ImportAffiliateLinksResult>> ImportAffiliateLinks(
    [FromBody] ImportAffiliateLinksRequest request,
    CancellationToken ct)
{
    var skipped = new List<AffiliateLinkImportSkip>();
    var imported = 0;

    foreach (var item in request.Items)
    {
        if (string.IsNullOrWhiteSpace(item.AffiliateLink))
        {
            skipped.Add(new AffiliateLinkImportSkip(item.ProductId, "Link vazio"));
            continue;
        }

        var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == item.ProductId, ct);
        if (product is null)
        {
            skipped.Add(new AffiliateLinkImportSkip(item.ProductId, "Produto nao encontrado"));
            continue;
        }

        if (product.Status != ProductStatus.AwaitingAffiliateLink)
        {
            skipped.Add(new AffiliateLinkImportSkip(
                item.ProductId,
                $"Status atual e {product.Status}, esperado AwaitingAffiliateLink"));
            continue;
        }

        product.ResolveAffiliateLink(item.AffiliateLink.Trim());
        imported++;
    }

    await _db.SaveChangesAsync(ct);

    return Ok(new ImportAffiliateLinksResult(imported, skipped));
}
```

DTOs novos em `backend/src/AfiliadoBot.Api/Products/ProductDtos.cs`:

```csharp
public record AffiliateLinkImportItem(Guid ProductId, string AffiliateLink);
public record ImportAffiliateLinksRequest(List<AffiliateLinkImportItem> Items);
public record AffiliateLinkImportSkip(Guid ProductId, string Reason);
public record ImportAffiliateLinksResult(int Imported, List<AffiliateLinkImportSkip> Skipped);
```

**Listagem dos produtos pendentes: nenhum endpoint novo necessário.** `GET /api/products` já
suporta `?status=` com qualquer valor do enum via `Enum.TryParse<ProductStatus>` (ver
`ProductsController.GetProducts`, linhas 42-47 do arquivo atual) — `AwaitingAffiliateLink` funciona
automaticamente assim que o valor existir no enum. Uso pelo dashboard:
`GET /api/products?status=AwaitingAffiliateLink&pageSize=200`.

**Um campo precisa ser adicionado a `ProductListItemDto`** (mesmo arquivo `ProductDtos.cs`) — hoje
não expõe `SourceUrl`, necessário para o operador copiar a URL do produto:

```csharp
public record ProductListItemDto(
    Guid Id,
    string Title,
    decimal SalePrice,
    decimal OriginalPrice,
    decimal DiscountPct,
    string Status,
    string Platform,
    string Slug,
    string Category,
    [property: JsonPropertyName("ai_score")] int? AiScore,
    [property: JsonPropertyName("ai_reason")] string? AiReason,
    DateTime CreatedAt,
    string? SourceUrl);   // NOVO — Issue #182, campo aditivo ao final (nao quebra consumidores existentes)
```

E em `ProductsController.GetProducts`, adicionar `p.SourceUrl` ao final da projeção `Select(...)`
existente (linha ~70).

### 3.6 Dashboard — nova tela "Links de Afiliado — Mercado Livre"

Precedente direto no próprio projeto: `dashboard/src/app/pages/facebook-manual/` — mesmo padrão de
"fila de itens pendentes de uma ação manual do operador, com botão de confirmação e feedback via
snackbar" (Issue #13/#106), Angular standalone component + Angular Material.

**Rota**: `/mercadolivre-links` (`dashboard/src/app/app.routes.ts`, lazy-loaded, mesmo padrão de
`facebook-manual`). Item de navegação em `dashboard/src/app/core/shell/shell.component.ts` (array
`navItems`), ex.: `{ label: 'Links ML', path: '/mercadolivre-links', icon: 'link' }`.

**Componente**: `dashboard/src/app/pages/mercadolivre-links/mercadolivre-links.component.ts`.

**Contrato funcional (schema mínimo — layout/composição visual fica com UX/UI)**:

1. Ao carregar: `GET /api/products?status=AwaitingAffiliateLink&pageSize=200`, ordenado por
   `CreatedAt` (o que a API já faz por padrão) → lista de `{ id, title, sourceUrl, ... }`.
2. Se lista vazia: estado "nada pendente" (mesmo padrão do `facebook-manual` quando `posts.length
   === 0`).
3. Se não vazia:
   - Exibir a lista (título + `sourceUrl` de cada produto), com um botão "Copiar todas as URLs"
     que junta `sourceUrl` de todos os itens em texto, um por linha (`navigator.clipboard.writeText`,
     mesmo padrão já usado em `copyCaption` do `facebook-manual`), para o operador colar de uma vez
     na ferramenta do Mercado Livre.
   - Um `<textarea>` único onde o operador cola de volta os links gerados (um link por linha, **na
     mesma ordem em que os produtos foram exibidos/copiados** — a ferramenta do ML preserva ordem
     input→output 1:1, confirmado no Gate 1.5).
   - Botão "Importar": ao clicar, faz o split do textarea por linha
     (`text.split('\n').map(l => l.trim()).filter(l => l.length > 0)`), **pareia por índice com o
     array de produtos já carregado no componente** (não confia em nenhuma ordenação implícita do
     servidor — o pareamento é local, na mesma sessão de carregamento), monta o body
     `{ items: [{ productId, affiliateLink }, ...] }` e chama
     `POST /api/products/affiliate-links/import`.
   - Se o número de linhas coladas for diferente do número de produtos exibidos: **bloquear o envio
     e avisar o operador** (mismatch de contagem é o sinal mais barato de um erro de pareamento —
     evita enviar um lote errado silenciosamente).
   - Após resposta: snackbar com o resumo (`Imported` produtos avançaram, `Skipped.length`
     pulados — se houver `Skipped`, listar os motivos), recarregar a lista (os importados somem,
     pois deixam de ser `AwaitingAffiliateLink`).
   - Nota textual no componente (comunicação, não é validação de negócio): lembrete de que, após
     importar, o operador pode disparar `Jobs` → `Processor` (já existente,
     `POST /api/jobs/processor/trigger`, ver `jobs.component.ts`) para publicar imediatamente, ou
     aguardar a próxima execução agendada.

**Serviço Angular**: `dashboard/src/app/core/services/products.service.ts` (já existe, ver uso em
`facebook-manual.component.ts` via `ProductsService.getById`) — adicionar métodos
`listAwaitingAffiliateLink()` e `importAffiliateLinks(items)`, mesmo padrão HTTP client já usado
pelos demais métodos do serviço.

### 3.7 Critérios de aceite mapeados — substituem CA 7.1-7.3 de `criterios-aceite.md`

`criterios-aceite.md` §7 (CA 7.1-7.3) assumia que `affiliate-tools/links` gera o link
automaticamente e definia validação como "inspecionar o link retornado pela chamada HTTP". Esse
endpoint não é alcançável (Gate 1.5) — a validação de que o link final é de fato um link de afiliado
rastreável passa a ser **inerente ao processo**: o link não é gerado por código nenhum desta issue,
é colado pelo operador diretamente da saída da ferramenta oficial do Mercado Livre (não há
"resposta HTTP 200 sem tag" possível, porque não há chamada HTTP gerando o link — CA 7.2 fica
resolvido por construção). Critérios revisados, cobertos por esta especificação:
- Produto ML sem `AffiliateLink` fica em `AwaitingAffiliateLink` (não em `Error`, não silenciosamente
  descartado) — testável via unit test de `ProcessorJob`.
- Endpoint de importação nunca sobrescreve `AffiliateLink` de um produto que não está
  `AwaitingAffiliateLink` (guarda explícita, Seção 3.5) — testável via unit test do controller.
- Produto importado com sucesso volta a `Queued` e é reprocessado/publicado pelo `ProcessorJob` já
  existente, sem lógica nova de publicação — testável via teste de integração (import → trigger
  processor → produto `Published`).
- QA (fase posterior do pipeline) deve rodar esse fluxo ponta a ponta ao menos uma vez em ambiente
  local, colando manualmente um link de exemplo (não precisa ser um link real da ferramenta do ML
  para validar o encadeamento técnico — a autenticidade do link real é responsabilidade do
  operador/Gerente na operação real, fora do escopo de teste automatizado desta issue).

## 4. Dependências e riscos (atualização da Seção 7/8 do `design.md`)

- Nenhuma dependência nova de pacote.
- **Sem migration** — `ProductStatus` é `int` convertido (`HasConversion<int>()`), novo valor
  adicionado ao final do enum não quebra dados existentes.
- Risco herdado do `design.md` (mapeamento de categorias desatualiza silenciosamente) — mitigado
  como já estava, sem mudança.
- **Risco novo, aceito**: o padrão de `SourceUrl` (`/p/{catalog_product_id}`) não pôde ser 100%
  confirmado por automação (Seção 1) — mitigado pelo checkpoint humano natural no fluxo semi-manual
  (Seção 3.1). Se, na operação real, a ferramenta do ML não reconhecer a URL para algum produto, o
  achado deve ser documentado (`.claude/melhorias/` ou nova Issue), não corrigido às pressas dentro
  desta issue — mesmo princípio já usado no CA 7.3 original.
