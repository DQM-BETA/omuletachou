# Especificação Técnica — ISSUE-208: Desacoplar visibilidade do site público do requisito de rede social configurada

> Refinamento do LT sobre `design.md` (Arquiteto). Confirmações ao vivo contra o código real
> registradas na seção 0. Sem novo campo/tabela — decisão já fechada pelo Arquiteto (design.md §2.1).

## 0. Confirmações ao vivo (LT, contra o repo real)

1. **Nomes/casing** — todos conferem com o código real, sem divergência do design:
   - `AfiliadoBot.Application.Jobs.ProcessorJob.ExecuteAsync` — branch a remover está nas linhas
     90-102 (`backend/src/AfiliadoBot.Application/Jobs/ProcessorJob.cs`).
   - `AfiliadoBot.Domain.Entities.Product.MarkAsPublished()` — linha 106-110, sem parâmetros,
     `Status = ProductStatus.Published`.
   - `AfiliadoBot.Domain.Entities.PublicationQueue` — `ProductId`, `SocialNetwork` (enum),
     `Status` (`PublicationStatus`), `CreatedAt` — confere exatamente com design.md §2.1/§2.3.
   - `SocialNetwork` enum (`backend/src/AfiliadoBot.Domain/Enums/SocialNetwork.cs`): `Telegram,
     Youtube, Instagram, TikTok, Facebook` — 5 valores, confere.
   - `PublicationStatus` enum: `Scheduled, Published, Failed, ManualPending` — confere com o
     mapeamento do design (§2.3: Scheduled/ManualPending→Pending, Published→Published,
     Failed→Failed).
   - `AfiliadoBot.Api.Products.ProductDtos.ProductListItemDto` (`backend/src/AfiliadoBot.Api/Products/ProductDtos.cs`,
     linhas 10-23) — último campo hoje é `SourceUrl` (Issue #184, "campo aditivo ao final").
     `Destinations` deve seguir o mesmo padrão: novo último campo do record, aditivo.
   - `ProductsController.GetProducts` (`backend/src/AfiliadoBot.Api/Controllers/ProductsController.cs`,
     linhas 32-75) — hoje projeta `ProductListItemDto` direto no `.Select()` do `IQueryable`
     paginado via `ToPagedResultAsync`. Confirma a necessidade da mudança em 2 etapas descrita no
     design.md §2.3 (não dá para agregar `PublicationQueue` dentro do `.Select()` de um
     `IQueryable` sem N+1 ou `GroupJoin` complexo — mais simples paginar `Product` primeiro,
     depois buscar `PublicationQueue` da página em uma query separada).
   - **Serialização JSON**: `backend/src/AfiliadoBot.Api/Program.cs` não tem
     `AddJsonOptions`/`PropertyNamingPolicy` customizado → usa o default do ASP.NET Core
     (`JsonNamingPolicy.CamelCase`). Confirma design.md §2.3: `Destinations` → JSON `destinations`
     sem precisar de `[JsonPropertyName]` (só `ai_score`/`ai_reason`/`ai_caption` têm override
     explícito para snake_case, todo o resto do DTO já é camelCase por padrão). O mesmo vale para
     as propriedades de `PublicationDestinationDto` (`Destination`→`destination`,
     `Status`→`status`), sem precisar de atributo.
   - `dashboard/src/app/core/services/products.service.ts` (`ProductListItem`) — confirma o campo
     aditivo `destinations?: { destination: string; status: string }[]` no mesmo padrão de
     `sourceUrl?: string | null` já existente.
   - `dashboard/src/app/pages/products/products.component.html` — coluna `status` (linhas 75-88)
     já usa `[matTooltip]` + `[matTooltipDisabled]` (mesmo padrão da coluna `aiScore`, linhas
     60-73). Confirma design.md §2.4: reaproveitar o padrão existente, sem template rico — decisão
     do LT abaixo (§4).

2. **Log quando `queuedCount == 0`** (decisão de observabilidade do LT): **sim, adicionar**
   `_logger.LogInformation` explícito em `ExecuteAsync` (não dentro de
   `CreatePublicationQueueEntriesAsync`, que já loga por rede pulada) quando
   `queuedCount == 0` após a chamada, algo como *"produto {ProductId} publicado no site sem
   nenhuma rede social qualificada"*. Justificativa: sem esse log, a leitura de
   `CreatePublicationQueueEntriesAsync` sozinha não deixa óbvio, num grep de log de produção, que
   o caminho "zero redes" é esperado e não um bug — importante justamente porque este é o cenário
   que causou a confusão original (Issues #182/#199/#204). Não é `LogWarning` (não é uma condição
   anômala, é o comportamento correto pós-fix).

3. **Reset de dados (proposal, Cenário 5.1)**: confirmado contra `deploy.sh` e
   `documentacoes/ISSUE-15-deploy-oracle-ssl-dominio/runbook-deploy.md` — **não existe nenhuma
   rotina de reset/truncate no processo de deploy atual** (nem em `deploy.sh`, nem no runbook).
   O deploy é `git pull --ff-only` + `docker compose up -d --build`, sem qualquer passo de
   limpeza de dados; o runbook inclusive instrui explicitamente a **nunca** rodar
   `docker compose down -v` (preservar `postgres_data`). Portanto, o reset mencionado no
   proposal é uma **ação manual pontual que o Gerente vai executar por conta própria** (ex.:
   `TRUNCATE products, publication_queues, publication_logs RESTART IDENTITY CASCADE` via
   `psql`), fora do escopo desta issue de código — **nada a implementar aqui**. Registrado em
   `tasks.md` como item de checklist de deploy (não de código), conforme já antecipado pelo
   design.md §2.6.

## 1. Contratos de API

### 1.1 `GET /api/products` (dashboard, autenticado) — mudança aditiva

`ProductListItemDto` ganha o campo `Destinations` (JSON `destinations`), último campo do record:

```
GET /api/products?status=Published
200 OK
{
  "items": [
    {
      "id": "...", "title": "...", ..., "sourceUrl": "...",
      "destinations": [
        { "destination": "Site", "status": "Published" },
        { "destination": "Telegram", "status": "Published" },
        { "destination": "Youtube", "status": "NotApplicable" },
        { "destination": "Instagram", "status": "NotApplicable" },
        { "destination": "TikTok", "status": "NotApplicable" },
        { "destination": "Facebook", "status": "Pending" }
      ]
    }
  ],
  ...
}
```

Regras de montagem (design.md §2.3, confirmadas):
- `"Site"` só aparece na lista quando `product.Status == ProductStatus.Published` (com
  `status: "Published"`); caso contrário a entrada `"Site"` é **omitida** (não é "NotApplicable" —
  simplesmente não existe até o produto estar `Published`).
- Uma entrada para cada valor de `SocialNetwork` (Telegram, Youtube, Instagram, TikTok, Facebook),
  sempre presente, com status:
  - sem linha em `PublicationQueue` para `(ProductId, SocialNetwork)` → `"NotApplicable"`.
  - linha mais recente (`OrderByDescending(CreatedAt)` — mesmo critério de
    `ProductsController.GetProduct`/`facebookCaption`) com `PublicationStatus.Scheduled` ou
    `ManualPending` → `"Pending"`.
  - `PublicationStatus.Published` → `"Published"`.
  - `PublicationStatus.Failed` → `"Failed"`.
- Agregação em 1 query adicional por página (`_db.PublicationQueues.Where(q =>
  productIds.Contains(q.ProductId))`, agrupada em memória) — nunca N+1.

Sem mudança de query string, paginação, filtros ou ordenação. `PublicationDestinationDto` é
`internal`/`public record` novo em `AfiliadoBot.Api/Products/ProductDtos.cs`:

```csharp
public record PublicationDestinationDto(string Destination, string Status);
```

### 1.2 `GET /api/public/deals` (site público) — sem mudança de código

`PublicController.GetDeals`/`GetBySlug` continuam com `WHERE Status = Published`, inalterado. O
efeito da issue é inteiramente no que `Published` passa a significar (via `ProcessorJob`), não no
controller.

## 2. Domínio — `ProcessorJob`/`Product`

`ProcessorJob.ExecuteAsync` (linhas 90-102 hoje):

- Remove o `if (queuedCount == 0) { product.MarkAsError(...) } else { product.MarkAsPublished(); }`.
- Substitui por: chama `CreatePublicationQueueEntriesAsync` (mantém assinatura e retorno
  `queuedCount`, usado só para o log da confirmação 2 acima), depois `product.MarkAsPublished()`
  **incondicional** — único guard restante antes desse ponto é `linkOk` (já existe, linha 83-88,
  inalterado).
- `Product.MarkAsPublished()` não muda assinatura/efeito — só o comentário XML do método passa a
  deixar explícito que, a partir da Issue #208, `Published` é exclusivamente sobre visibilidade
  no site, independente de rede social.

`CreatePublicationQueueEntriesAsync` (linhas 224-269): **sem mudança de lógica** — continua
criando 0..N entradas de `PublicationQueue` por rede qualificada, exatamente como hoje. Só deixa
de ser usada para ramificar `Published`/`Error`.

## 3. Frontend — tooltip do dashboard

`products.service.ts` (`ProductListItem`): novo campo aditivo opcional:

```ts
destinations?: { destination: string; status: string }[];
```

`products.component.html`, coluna `status` (linhas 75-88): o `matTooltip` da badge de status passa
a ser condicional — quando `product.status === 'Published'` e `product.destinations` presente,
mostra a lista de destinos; senão mantém o comportamento atual (tooltip de `ai_reason` quando
`status === 'Error'`).

**Decisão do LT sobre formato de exibição** (design.md §2.4 delegava ao LT): **texto simples via
`matTooltip` (string)**, no mesmo padrão já usado pelas colunas `aiScore`/`status` hoje (nenhuma
delas usa template rico). Formato sugerido: `"Site: Publicado · Telegram: Publicado · Instagram:
Não aplicável · TikTok: Não aplicável · Facebook: Pendente"` — um método no componente
(`buildDestinationsTooltip(destinations)`) monta a string a partir do array, traduzindo os status
(`Published`→"Publicado", `Pending`→"Pendente", `Failed`→"Erro", `NotApplicable`→"Não aplicável").
Não há Issue de UI disparada para esta mudança (extensão pontual de tela existente, não tela
nova) — não escalado para UX/UI.

## 4. Padrões obrigatórios

- Nenhuma migration EF Core (sem mudança de schema).
- `feature/ISSUE-208-SUB-*` a partir de `desenv`, merge squash para `desenv` (padrão do repo).
- Toda sub-issue backend roda `dotnet test` (cobertura mínima já praticada no projeto) antes do
  PR; frontend roda `ng test` (Karma/Jasmine, conforme `products.component.spec.ts` existente).
- Teste de `ProcessorJobTests` que hoje espera `MarkAsError` no cenário "zero rede qualificada"
  (`ExecuteAsync_MarcaError_QuandoNenhumaRedeQualificada`, linha 439-459 de
  `backend/src/AfiliadoBot.Tests/Jobs/ProcessorJobTests.cs`) e o teste de rede habilitada sem
  credenciais (`ExecuteAsync_MarcaError_QuandoRedeHabilitadaMasSemCredenciais`, linha 461-478)
  **devem ser reescritos** (não apenas deletados) para expressar o novo comportamento esperado
  (`Status == Published`, `PublicationQueue` vazia) — rastreável no PR às Issues #133/#145,
  superadas pela #208 (risco já identificado no design.md §6).

## 5. Casos de teste (mapeamento para tasks.md, ver seção correspondente)

Ver design.md §5 — mapeamento completo por Given/When/Then já feito pelo Arquiteto; `tasks.md`
distribui cada item por sub-issue.
