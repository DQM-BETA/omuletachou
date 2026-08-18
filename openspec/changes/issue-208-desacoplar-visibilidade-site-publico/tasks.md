# Tasks — ISSUE-208: Desacoplar visibilidade do site público do requisito de rede social configurada

> Devs leem apenas este arquivo. Contexto técnico completo em `especificacao-tecnica.md` e
> `design.md` (mesma pasta / `../../../documentacoes/ISSUE-208-desacoplar-visibilidade-site-publico/`).

## T-01 (sub-issue backend) — `ProcessorJob`: publicar no site independe de rede social

**Stack:** stack:dotnet
**Arquivos:** `backend/src/AfiliadoBot.Application/Jobs/ProcessorJob.cs`,
`backend/src/AfiliadoBot.Domain/Entities/Product.cs`,
`backend/src/AfiliadoBot.Tests/Jobs/ProcessorJobTests.cs`,
`backend/src/AfiliadoBot.Tests/Public/PublicControllerTests.cs`

### O que fazer
1. Em `ProcessorJob.ExecuteAsync`, remover o branch `if (queuedCount == 0) MarkAsError(...) else
   MarkAsPublished()` (linhas ~90-102). `CreatePublicationQueueEntriesAsync` continua sendo
   chamada exatamente como hoje (mantém a fila social funcionando para redes qualificadas); logo
   em seguida, `product.MarkAsPublished()` passa a ser chamada **sempre** (incondicional), como
   único passo após `CreatePublicationQueueEntriesAsync`.
2. Adicionar `_logger.LogInformation` quando `queuedCount == 0` (retorno de
   `CreatePublicationQueueEntriesAsync`), registrando que o produto foi publicado no site sem
   nenhuma rede social qualificada (decisão de observabilidade — ver
   `especificacao-tecnica.md` §0.2).
3. Atualizar o comentário XML de `Product.MarkAsPublished()` deixando explícito que, a partir da
   Issue #208, `Published` é exclusivamente sobre visibilidade no site, independente de rede
   social (sem mudar assinatura nem efeito do método).
4. `CreatePublicationQueueEntriesAsync` **não muda** (mesma lógica de qualificação por rede,
   mesmo retorno `queuedCount`).

### Testes a reescrever/adicionar (`ProcessorJobTests.cs`)
- Reescrever `ExecuteAsync_MarcaError_QuandoNenhumaRedeQualificada` (linha ~439-459): produto
  aprovado + link válido + zero rede qualificada → `Status == Published` **e** nenhuma
  `PublicationQueue` criada. Renomear para refletir o novo comportamento (ex.:
  `ExecuteAsync_MarcaPublished_QuandoNenhumaRedeQualificada`). Comentar no PR que substitui
  intencionalmente o teste das Issues #133/#145.
- Reescrever `ExecuteAsync_MarcaError_QuandoRedeHabilitadaMasSemCredenciais` (linha ~461-478):
  mesmo ajuste — rede habilitada sem credenciais não qualifica, produto ainda assim vai para
  `Published`, sem `PublicationQueue` para essa rede.
- Adicionar teste: produto aprovado + link válido + rede qualificada → `Status == Published` **e**
  `PublicationQueue` criada para a rede (não-regressão explícita, se não já coberta por
  `ExecuteAsync_MarcaPublished_AoFinalizarComSucesso`, linha 423-437 — conferir se cobre as 3
  plataformas de origem, senão parametrizar/duplicar para Mercado Livre/Amazon/Shopee).
- Adicionar teste de não-retroatividade: produto `Published` sem nenhuma `PublicationQueue` +
  qualificar uma rede social nova (seed de `AppSettings`) + rodar `job.ExecuteAsync()` de novo →
  nenhuma nova `PublicationQueue` criada para aquele produto (ele não está mais `Queued`, a query
  do topo de `ExecuteAsync` não o pega — comportamento já emergente, só precisa de teste
  explícito).

### Testes a adicionar (`PublicControllerTests.cs`)
- Produto `Published` sem nenhuma `PublicationQueue` aparece em `GET /api/public/deals`.
- Produto `Published` com uma `PublicationQueue` em `Failed` continua aparecendo normalmente em
  `GET /api/public/deals` (falha de rede social não afeta o site).

### Critérios de aceite (Given/When/Then)
- **CA 1.1**: nenhuma rede social configurada + produto aprovado com link válido processado →
  aparece em `GET /api/public/deals`.
- **CA 1.2**: rede(s) qualificada(s) + produto aprovado com link válido → aparece em
  `GET /api/public/deals` independentemente do resultado da publicação social.
- **CA 1.3**: produto sem link de afiliado válido → **não** aparece (comportamento já existente,
  não deve regredir — sem mudança de código aqui, só confirmar via teste que não quebrou).
- **CA 1.4**: mesmo comportamento (CA 1.1) para Mercado Livre, Amazon e Shopee.
- **CA 2.1**: rede qualificada → entra em `PublicationQueue` normalmente, sem regressão.
- **CA 2.2**: nenhuma rede qualificada → publicado no site, mas sem nenhuma entrada em
  `PublicationQueue`.
- **CA 2.3**: falha em uma rede não afeta o site nem as demais redes.
- **CA 3.2**: ausência de rede qualificada não é registrada como erro em lugar nenhum (nem
  `Product.Status`, nem `PublicationQueue`).
- **CA 6.1 / CA 6.2**: sem retroatividade (produto antigo não reenfileirado; produto
  novo/atualizado considera a rede recém-qualificada normalmente).
- **CA 7.1**: nenhuma condição de bloqueio de site além de aprovação + link válido.

---

## T-02 (sub-issue backend) — API do dashboard: campo `Destinations` agregado

**Stack:** stack:dotnet
**Depende de:** nada de T-01 em termos de compilação (mudança isolada em outro arquivo), mas
conceitualmente assume que T-01 já desacoplou `Published`. Pode ser feita em paralelo a T-01;
merge de T-01 primeiro é recomendado para evitar confusão de leitura no PR, mas não é bloqueante
técnico.
**Arquivos:** `backend/src/AfiliadoBot.Api/Products/ProductDtos.cs`,
`backend/src/AfiliadoBot.Api/Controllers/ProductsController.cs`,
`backend/src/AfiliadoBot.Tests/Products/ProductsControllerTests.cs`

### O que fazer
1. Adicionar em `ProductDtos.cs`:
   ```csharp
   public record PublicationDestinationDto(string Destination, string Status);
   ```
2. `ProductListItemDto` ganha `IReadOnlyList<PublicationDestinationDto> Destinations` como último
   campo do record (aditivo, mesmo padrão do `SourceUrl` da Issue #184).
3. Em `ProductsController.GetProducts`, mudar a montagem para 2 etapas (não dá para agregar
   `PublicationQueue` dentro do `.Select()` do `IQueryable` paginado sem N+1):
   - Paginar `Product` primeiro (query/filtros/ordenação **inalterados**).
   - Buscar, numa única query, todas as `PublicationQueue` cujo `ProductId` esteja nos IDs da
     página atual (`_db.PublicationQueues.Where(q => productIds.Contains(q.ProductId))`).
   - Agrupar em memória por `(ProductId, SocialNetwork)`, pegando a linha de `CreatedAt` mais
     recente por par (mesmo critério já usado em `ProductsController.GetProduct` para o
     `facebookCaption`, linha 85-90).
   - Montar `Destinations` por produto: `"Site"` presente só quando `Status == Published`; uma
     entrada por valor de `SocialNetwork` (5 hoje) com o mapeamento de status descrito em
     `especificacao-tecnica.md` §1.1.

### Testes a adicionar (`ProductsControllerTests.cs`)
- `GET /api/products` retorna `destinations` com `"NotApplicable"` para rede sem linha em
  `PublicationQueue` (não como erro).
- `destinations` inclui `"Site"` com `status: "Published"` quando o produto está `Published`; e
  **omite** `"Site"` quando não está.
- `destinations` reflete corretamente `Pending`/`Published`/`Failed` por rede quando há linha em
  `PublicationQueue` (uma linha por status, cobrindo os 3 casos).
- Quando há múltiplas linhas de `PublicationQueue` para o mesmo `(ProductId, SocialNetwork)`,
  usa a mais recente por `CreatedAt`.
- Não regride paginação/filtros/ordenação existentes de `GET /api/products`.

### Critérios de aceite (Given/When/Then)
- **CA 3.1**: possível determinar status de site separado de cada rede via API.
- **CA 3.2**: ausência de rede qualificada aparece como `"NotApplicable"`, não erro.
- **CA 4.2**: destinos "onde foi publicado" e os não aplicáveis/pendentes aparecem visíveis no
  payload consumido pela tooltip (não omitidos silenciosamente, exceto "Site" quando não
  `Published`, conforme especificação).

---

## T-03 (sub-issue frontend) — Dashboard: tooltip de destinos na coluna Status

**Stack:** stack:angular
**Depende de:** T-02 (contrato `destinations` no payload de `GET /api/products`) — pode
desenvolver em paralelo usando o contrato já especificado em `especificacao-tecnica.md` §1.1/§3,
mas o merge para `desenv` deve vir depois de T-02 (ou junto, testes de integração real dependem do
campo existir na API).
**Arquivos:** `dashboard/src/app/core/services/products.service.ts`,
`dashboard/src/app/pages/products/products.component.ts`,
`dashboard/src/app/pages/products/products.component.html`,
`dashboard/src/app/pages/products/products.component.spec.ts`

### O que fazer
1. Em `products.service.ts`, adicionar ao `ProductListItem`:
   ```ts
   destinations?: { destination: string; status: string }[];
   ```
2. Em `products.component.ts`, adicionar um método `buildDestinationsTooltip(destinations)` que
   monta uma string simples a partir do array (ex.: `"Site: Publicado · Telegram: Publicado ·
   Instagram: Não aplicável · TikTok: Não aplicável · Facebook: Pendente"`), traduzindo os valores
   de status: `Published`→"Publicado", `Pending`→"Pendente", `Failed`→"Erro",
   `NotApplicable`→"Não aplicável".
3. Em `products.component.html`, na coluna `status` (linhas 75-88), o `matTooltip` passa a
   priorizar `buildDestinationsTooltip(product.destinations)` quando `product.status ===
   'Published'` e `product.destinations` estiver presente; mantém o comportamento atual (tooltip
   de `ai_reason`) para `status === 'Error'`; sem tooltip nos demais casos (mesmo padrão de
   `matTooltipDisabled` já usado).
4. Decisão de formato já tomada pelo LT (texto simples, sem template rico) — ver
   `especificacao-tecnica.md` §3. Não escalar para UX/UI.

### Testes a adicionar (`products.component.spec.ts`)
- Produto com `status: 'Published'` e `destinations` preenchido → tooltip da badge de status
  reflete a lista de destinos (verificar via `MatTooltip`, mesmo padrão já usado no spec atual
  para `aiScore`).
- Produto com `status` diferente de `'Published'` (ex.: `'Pending'`, `'Error'`) → tooltip de
  destinos não interfere no comportamento existente (não quebra o teste de `ai_reason` para
  `Error`).
- Produto `Published` sem `destinations` (campo ausente, ex.: payload antigo/mock incompleto) →
  não quebra o componente (fallback gracioso, sem erro no console).

### Critérios de aceite (Given/When/Then)
- **CA 4.1**: status consolidado "Published" na listagem principal (já é o rótulo de
  `product.status`, sem mudança de backend — validar que a coluna continua mostrando isso).
- **CA 4.2**: tooltip detalha os destinos efetivos, incluindo não aplicáveis/pendentes.
- **CA 4.3**: produto não publicado em nenhum destino mostra o status real do ciclo de vida
  (Pending/Queued/Processing/Rejected/Error/AwaitingAffiliateLink) — sem mudança de comportamento
  aqui, só não regredir.

---

## Checklist de deploy (fora do escopo de código desta issue)

- [ ] **Reset de dados pós-deploy** (proposal, Cenário 5.1): confirmado que não existe rotina
  automática de reset no `deploy.sh`/runbook atual (ver `especificacao-tecnica.md` §0.3) — é uma
  ação manual que o **Gerente** deve executar por conta própria após o merge em `main` (ex.:
  `TRUNCATE products, publication_queues, publication_logs RESTART IDENTITY CASCADE` via `psql`
  na VM). Não é migration EF Core, não há classe/código para implementar. Nenhuma sub-issue de
  código cobre este item — sinalizar ao Gerente no Gate 2.
