# Design — ISSUE-208: Desacoplar visibilidade do site público do requisito de rede social configurada

## 1. Visão geral

A causa raiz do bug é que `ProcessorJob.ExecuteAsync` trata "publicar no site" e "publicar em
rede social" como uma decisão única: só marca `Product.Status = Published` (o único status que
`PublicController` considera "visível") quando `CreatePublicationQueueEntriesAsync` enfileira
pelo menos 1 rede social qualificada; caso contrário marca `Error`.

A decisão de design central desta issue é: **não introduzir nenhum campo novo em `Product` nem
nova tabela de tracking**. `ProductStatus.Published` já significa, sozinho, exatamente "visível
no site" — é o único critério que `PublicController` usa (`p.Status == ProductStatus.Published`).
O que falta não é um campo novo, é **parar de condicionar esse status ao resultado da fila
social**. E o rastreio "por destino social" que a issue pede já existe: é o próprio
`PublicationQueue`, que grava uma linha por `(ProductId, SocialNetwork)` com seu `PublicationStatus`
independente. A única lacuna é que hoje a *ausência* de linha (rede não qualificada) não é
distinguida de "não aplicável" em lugar nenhum da API/dashboard — isso é resolvido no nível de
leitura (agregação), não de escrita.

Resultado: mudança cirúrgica em `ProcessorJob` (remove o acoplamento) + endpoint/DTO do dashboard
que agrega `Product.Status` (site) + `PublicationQueue` (redes) para a tooltip. Zero migration de
schema. Zero mudança em `ProductStatus` enum. Zero mudança em `PublicController` além de nenhuma —
ele já filtra por `Published`, que passa a significar "aprovado + link válido" sem mais nada.

## 2. Decisões técnicas

### 2.1 Modelagem do domínio (pergunta 1 do PM) — Opção (a), sem novo campo

**Decisão:** manter `Product.Status` (enum `ProductStatus`) como está, com **`Published`
redefinido semanticamente** (não estruturalmente) para "visível no site público" — que já é,
hoje, o único uso real que `PublicController` faz dele. O "status por destino social"
(Cenário 3.1/3.2 dos critérios de aceite) é atendido pelo `PublicationQueue` existente:

- **Site**: `Product.Status == Published` → publicado; qualquer outro valor → não publicado no site.
- **Rede social X**: existe linha em `PublicationQueue` para `(ProductId, X)`?
  - Não existe → **"Não aplicável"** (rede não estava qualificada no momento do processamento,
    ou o produto não atende a um requisito específico da rede — ex.: falta vídeo para
    Youtube/Instagram). Nunca é tratado como erro (CA 3.2).
  - Existe → o `PublicationStatus` da linha mais recente para aquele `(ProductId, SocialNetwork)`
    dita o estado: `Scheduled`/`ManualPending` → pendente; `Published` → publicado;
    `Failed` → erro (dessa rede especificamente, sem afetar site nem outras redes — CA 2.3).

**Por que não (b) — campo/flag adicional no `Product` (ex. `IsPublishedOnSite`):** seria
redundante com o que `Published` já expressa hoje (nenhum outro código depende de `Published`
significar "site E rede social" — só o próprio `ProcessorJob`, que estamos corrigindo). Introduzir
um segundo campo booleano ao lado do enum criaria dois lugares para a mesma informação poderem
divergir (ex.: `Status != Published` mas `IsPublishedOnSite == true`), exigindo lógica de
sincronização sem ganho — o `PublicController` teria que passar a filtrar por dois critérios em
vez de um. Rejeitada por criar superfície de inconsistência sem necessidade.

**Por que não (c) — nova entidade `ProductPublication { Destination, Status, PublishedAt }`
genérica cobrindo site + redes:** seria a modelagem "mais correta" a longo prazo (site e cada
rede como linhas homogêneas de uma mesma tabela de destinos), mas é a mudança de maior escopo:
exige migration, exige reescrever `PublicController` (que hoje faz um `WHERE Status = Published`
simples e rápido, apoiado nos 5 índices compostos da Issue #167/#168) para um `JOIN`/`EXISTS`
contra a nova tabela, e exige popular uma linha "Site" por produto em todo lugar onde hoje só se
seta `Status`. Sem retroatividade nenhum desses índices muda de utilidade — a query pública mais
quente do sistema (`GET /api/public/deals`) ficaria mais cara sem necessidade. Rejeitada por não
ser a menor mudança que atende o requisito; fica registrada como possível evolução futura se um
dia o "site" precisar de estados intermediários (hoje não precisa — é binário: apareceu ou não).

**Trade-off aceito:** o rastreio de "site" (via `Product.Status`) e o de "redes sociais" (via
`PublicationQueue`) continuam vivendo em modelos de dados fisicamente diferentes — não há uma
única tabela "destinos". Isso é aceitável porque (1) já é a realidade atual do sistema — não é
uma mudança introduzida por esta issue, apenas deixa de estar acoplado por trás de uma única
condição de bloqueio; (2) a agregação para o dashboard (tooltip) é feita em tempo de leitura,
sem duplicar dado nem exigir sincronização entre os dois modelos.

### 2.2 `ProcessorJob.ExecuteAsync` (pergunta 2 do PM)

**Decisão:** separar as duas decisões no loop. O branch atual:

```csharp
var queuedCount = await CreatePublicationQueueEntriesAsync(product, settingsMap, slots[i], ct);

if (queuedCount == 0)
{
    product.MarkAsError("Nenhuma rede social habilitada com credenciais validas para publicar este produto.");
}
else
{
    product.MarkAsPublished();
}
```

passa a ser:

```csharp
await CreatePublicationQueueEntriesAsync(product, settingsMap, slots[i], ct);

// Issue #208: publicar no site depende apenas de aprovacao pela IA (Status == Queued,
// garantido pela query do topo) + link de afiliado valido (garantido por EnsureAffiliateLinkAsync
// ja ter retornado true neste ponto — linkOk). A fila social (acima) e uma decisao independente:
// zero rede qualificada apenas significa zero linhas em PublicationQueue, nao bloqueia o site
// nem e mais um erro (supera o item A4 das Issues #133/#145 — a causa do bug relatado nas
// Issues #182/#199/#204).
product.MarkAsPublished();
```

O branch `MarkAsError("Nenhuma rede social habilitada...")` **desaparece por completo** — não
vira "sucesso silencioso via warning" nem outro status; simplesmente deixa de existir, porque
"zero rede qualificada" não é mais uma condição de erro do produto (CA 3.2, CA 7.1). O
`_logger.LogWarning`/`LogInformation` já existentes dentro de `CreatePublicationQueueEntriesAsync`
(por rede pulada) continuam como estão — são log operacional, não status de domínio, e seguem
úteis para diagnosticar por que uma rede específica não entrou na fila.

`CreatePublicationQueueEntriesAsync` mantém a mesma assinatura e o mesmo `return queuedCount`
(usado só para log/telemetria opcional agora, não mais para ramificar `Published` vs `Error`) —
**[LT CONFIRMAR AO VIVO]** se vale adicionar um `_logger.LogInformation` explícito quando
`queuedCount == 0` registrando "produto publicado no site sem nenhuma rede social qualificada"
(observabilidade, não obrigatório para os critérios de aceite).

`Product.MarkAsPublished()` (em `Product.cs`) não muda de assinatura nem de efeito (`Status =
ProductStatus.Published`) — só o comentário XML acima do método deve ser atualizado para deixar
explícito que, a partir da Issue #208, `Published` é exclusivamente sobre visibilidade no site,
independente de qualquer rede social.

### 2.3 Tooltip do dashboard (pergunta 3 do PM)

**Decisão:** endpoint existente `GET /api/products` (dashboard, `ProductsController.GetProducts`)
ganha um campo aditivo agregado no `ProductListItemDto` — sem endpoint novo, sem quebrar
consumidores existentes (mesmo padrão já usado para `SourceUrl`, adicionado "ao final" na Issue
#184).

Novo tipo (`AfiliadoBot.Api/Products/ProductDtos.cs`):

```csharp
public record PublicationDestinationDto(string Destination, string Status);
// Destination: "Site", "Telegram", "Youtube", "Instagram", "TikTok", "Facebook"
// Status: "Published" | "Pending" | "Failed" | "NotApplicable"
```

`ProductListItemDto` ganha `IReadOnlyList<PublicationDestinationDto> Destinations` como último
campo (aditivo). Exemplo de nome de propriedade e casing exato do JSON —
**[LT CONFIRMAR AO VIVO]** (camelCase `destinations` seguindo o padrão já usado pelos demais
campos do DTO, exceto os explicitamente `ai_score`/`ai_reason`).

Construção do DTO em `ProductsController.GetProducts` — hoje o `.Select(...)` projeta
`ProductListItemDto` direto na query paginada (`IQueryable` → `ToPagedResultAsync`). Como
`Destinations` depende de agregar `PublicationQueue` por produto (múltiplas linhas), a montagem
passa a ser em duas etapas, no mesmo padrão de "paginar entidade, depois montar DTO" que
`PublicController.ToDtoPagedResultAsync` já usa:

1. Pagina `Product` (sem mudar filtros/ordenação atuais).
2. Busca, numa única query, todas as `PublicationQueue` cujo `ProductId` esteja na página atual
   (`_db.PublicationQueues.Where(q => productIds.Contains(q.ProductId))`), agrupa em memória por
   `(ProductId, SocialNetwork)` pegando a linha de `CreatedAt` mais recente por par — mesmo
   critério de "mais recente" já usado em `ProductsController.GetProduct` para o `facebookCaption`
   (`OrderByDescending(q => q.CreatedAt)`).
3. Para cada produto da página, monta `Destinations`:
   - Uma entrada `"Site"` com `Status = Published` se `product.Status == ProductStatus.Published`,
     senão **omitida** (CA 4.2 fala de destinos "onde foi publicado" + "não aplicáveis ou
     pendentes" — o Site em si só faz sentido como linha da tooltip quando o status consolidado é
     "Published"; ver §2.4 sobre quando a tooltip é exibida).
   - Uma entrada por cada valor do enum `SocialNetwork` (Telegram, Youtube, Instagram, TikTok,
     Facebook — os 5 já suportados hoje; novo publisher futuro só precisa existir no enum para
     aparecer automaticamente, sem tocar nesta lógica de agregação):
     - sem linha correspondente em `PublicationQueue` → `Status = "NotApplicable"`;
     - `PublicationStatus.Scheduled` ou `ManualPending` → `"Pending"`;
     - `PublicationStatus.Published` → `"Published"`;
     - `PublicationStatus.Failed` → `"Failed"`.

Isso evita N+1 (uma query de `PublicationQueue` por página, não por produto) e evita expor a
entidade `PublicationQueue` diretamente (mantém o padrão de DTO dedicado já usado no resto da API).

**Custo de leitura**: pageSize default do dashboard é pequeno (20, conforme
`products.component.ts`); o pior caso realista (`pageSize=200`, usado por
`listAwaitingAffiliateLink`) ainda é uma única query com `IN (200 guids)`, aceitável. Sem índice
novo necessário — `PublicationQueue.ProductId` já é FK indexada implicitamente pelo EF/Postgres.

### 2.4 Status consolidado "Published" no dashboard

Nenhuma mudança de backend é necessária para o rótulo consolidado em si — `ProductListItemDto.Status`
já expõe `product.Status.ToString()`, e como `Published` agora só depende de site (independente de
rede social), o rótulo "Published" já é, por construção, o "publicado em pelo menos um destino"
que a CA 4.1 pede (o site sempre conta como destino quando `Published`). **Não é necessário
calcular nem persistir um status consolidado separado** — decisão que resolve a segunda parte da
pergunta 2 do PM (tempo de leitura vs. persistido/cacheado): **tempo de leitura, reaproveitando o
campo que já existe**, sem cache, porque a fonte (`Product.Status`) já é a fonte de verdade e a
query de listagem já a lê sem custo adicional.

No frontend (`products.component.html`), a coluna `status` já renderiza `product.status` — ganha
apenas o novo `matTooltip` alimentado por `product.destinations` quando `product.status ===
'Published'` (reaproveitando o padrão de `matTooltipDisabled` já usado nas colunas `aiScore` e
`status` para `Error`). Formato de exibição da lista dentro do tooltip (texto simples
"Site: Publicado · Telegram: Pendente · Instagram: Não aplicável...", ou um template
Angular com ícones por status) — **[LT CONFIRMAR AO VIVO / UX-UI]**: como não há Issue de UI
disparada nesta mudança (é uma extensão pontual de uma tela já existente, não uma tela nova), a
recomendação é texto simples via `matTooltip` (string), consistente com o padrão já usado nas
colunas `aiScore`/`status` hoje (que já usam `matTooltip` com string simples, não template rico) —
se o LT julgar que o resultado visual não é aceitável como string simples, escalar para UX/UI.

### 2.5 Sem retroatividade (pergunta 5 do PM / restrição do proposal)

Nenhum mecanismo novo é necessário. A regra "produtos antigos não são reenfileirados quando uma
rede nova é qualificada" já é garantida estruturalmente pelo fluxo atual, sem nenhuma mudança
nesta issue: `CreatePublicationQueueEntriesAsync` só roda **dentro do loop de
`ExecuteAsync`**, que só processa produtos com `Status == Queued` no momento da execução. Um
produto que já foi marcado `Published` nunca volta a `Queued` (não há nenhuma transição de volta
de `Published` para `Queued` em `Product.cs`), logo nunca reentra no loop do `ProcessorJob` e
nunca é reavaliado contra o mapa de redes qualificadas do momento — isso já é, por construção,
"produtos processados antes da qualificação da rede não são retroativamente reenfileirados"
(CA 6.1). Novos produtos (ou os que passam por `ResolveAffiliateLink`, que os devolve a `Queued`
explicitamente) são avaliados com o `settingsMap` carregado naquele instante, cobrindo CA 6.2 sem
nenhum código adicional. Esta seção existe apenas para deixar explícito ao LT que **não há tarefa
de implementação aqui** — é uma propriedade que já emerge do design em 2.1/2.2, e deve ser
validada por teste (cenário: produto `Published` sem rede social + configurar rede social depois
+ rodar `ProcessorJob` de novo não deve gerar `PublicationQueue` para aquele produto, porque ele
não está mais em `Queued`).

### 2.6 Reset de dados (Cenário 5.1 do proposal — fora de escopo de código)

O proposal confirma que os 111 produtos em `Error` e os demais dados atuais serão
"apagados/resetados como parte do processo de deploy". Isso **não é uma migration EF Core** (não
há mudança de schema nesta issue) — é uma ação operacional (ex.: `TRUNCATE products,
publication_queues, publication_logs RESTART IDENTITY CASCADE` via `psql`, ou um script de
deploy). **[LT CONFIRMAR AO VIVO]** se isso já existe como rotina no processo de deploy do
projeto (`docker-compose`/CI) ou se precisa ser documentado como passo manual do Gerente no
runbook de deploy desta issue — está fora do escopo desta mudança de código (nenhuma classe/
migration para implementar), mas deve constar em `tasks.md` como item de checklist de deploy, não
de código.

## 3. Fluxo de dados (resumo)

```
ProcessorJob.ExecuteAsync (produto Queued)
  ├─ EnsureAffiliateLinkAsync
  │    └─ falha (sem SourceUrl / ML sem link) → Error / AwaitingAffiliateLink → continue (inalterado)
  ├─ CreatePublicationQueueEntriesAsync (settingsMap do momento)
  │    └─ por rede: qualificada? cria PublicationQueue(Scheduled|ManualPending) : nao cria nada
  └─ product.MarkAsPublished()  ← incondicional a partir daqui (mudanca desta issue)

GET /api/public/deals (PublicController)
  └─ WHERE Status = Published   ← inalterado, mas agora reflete so aprovacao+link (nao mais rede social)

GET /api/products (ProductsController, dashboard)
  ├─ pagina Product → ProductListItemDto (campos atuais, inalterados)
  └─ + Destinations: Site (do Product.Status) + cada SocialNetwork (da PublicationQueue mais recente,
       ou "NotApplicable" se nao existe linha)
```

## 4. Componentes afetados

| Componente | Mudança | Escopo |
|---|---|---|
| `AfiliadoBot.Application.Jobs.ProcessorJob` | Remove branch `queuedCount == 0 → MarkAsError`; `MarkAsPublished()` incondicional após link válido | Backend |
| `AfiliadoBot.Domain.Entities.Product` | Atualiza comentário XML de `MarkAsPublished()` (sem mudança de assinatura/efeito) | Backend |
| `AfiliadoBot.Api.Products.ProductDtos` (`ProductListItemDto`) | Novo campo aditivo `Destinations` (`PublicationDestinationDto[]`) | Backend |
| `AfiliadoBot.Api.Controllers.ProductsController.GetProducts` | Query em 2 etapas: pagina `Product`, agrega `PublicationQueue` da página, monta `Destinations` | Backend |
| `AfiliadoBot.Api.Controllers.PublicController` | **Nenhuma mudança de código** — `WHERE Status = Published` já é o comportamento correto pós-fix | Backend |
| `dashboard/.../products.service.ts` (`ProductListItem`) | Novo campo `destinations?: { destination: string; status: string }[]` | Frontend |
| `dashboard/.../products.component.html` | `matTooltip` da coluna `status` passa a mostrar `destinations` quando `status === 'Published'` | Frontend |
| Testes (`ProcessorJobTests`, `ProductsControllerTests`, `PublicControllerTests`, `products.component.spec.ts`) | Cobrir os cenários dos critérios de aceite (ver §5) | Backend/Frontend |
| Deploy/runbook | Reset de dados (§2.6) — checklist, não código | Operacional |

**Nenhuma migration EF Core é necessária** para esta issue (nenhuma coluna/tabela nova).

## 5. Casos de teste a cobrir (mapeamento para os critérios de aceite)

- `ProcessorJobTests`: produto aprovado + link válido + **zero** rede qualificada → `Status ==
  Published` e **nenhuma** `PublicationQueue` criada (CA 1.1, CA 2.2, CA 3.2 — substitui o teste
  atual que espera `MarkAsError` nesse cenário, que deve ser removido/reescrito).
- `ProcessorJobTests`: produto aprovado + link válido + rede qualificada → `Status == Published`
  **e** `PublicationQueue` criada para a rede (CA 1.2, CA 2.1 — não-regressão).
  Idem para as 3 plataformas de origem (CA 1.4).
- `ProcessorJobTests`: produto sem link de afiliado válido → não marca `Published` (CA 1.3,
  comportamento já existente, garantir que não regrediu).
- `PublicControllerTests`: produto `Published` sem nenhuma rede aparece em `GET
  /api/public/deals` (CA 1.1); produto `Published` com rede em `Failed` no `PublicationQueue`
  continua aparecendo (CA 2.3).
- `ProductsControllerTests`: `GET /api/products` retorna `Destinations` com `"NotApplicable"`
  para rede sem linha em `PublicationQueue`, e não como erro (CA 3.2); `"Site"` presente quando
  `Published` (CA 4.2).
- `products.component.spec.ts`: tooltip da coluna status exibe destinos quando `status ===
  'Published'`; não quebra para os demais status (CA 4.3).
- Teste dedicado de não-retroatividade (§2.5): produto `Published` sem rede + nova rede
  qualificada depois + `ProcessorJob.ExecuteAsync` rodado de novo → nenhuma nova
  `PublicationQueue` para esse produto (CA 6.1); produto novo processado após a qualificação
  considera a rede normalmente (CA 6.2).

## 6. Riscos e mitigação

| Risco | Mitigação |
|---|---|
| Teste existente de `ProcessorJobTests` que cobre o branch `MarkAsError("Nenhuma rede social...")` (Issues #133/#145) vai falhar após a mudança — é esperado, não uma regressão | LT/Dev deve localizar e reescrever esse teste como parte da task, não apenas deletá-lo silenciosamente; documentar no PR que ele foi intencionalmente substituído (rastreável às Issues #133/#145 → superadas pela #208) |
| Aumento de 1 query por página em `GET /api/products` (agregação de `PublicationQueue`) | Volume de dados é pequeno (dashboard interno, paginação já limita a página); sem índice novo necessário; medir apenas se o LT perceber degradação real, não preventivamente |
| Confusão futura entre "Published = site" (`Product.Status`) e "Published" como valor de `PublicationStatus` (fila social) — mesmo nome, semânticas diferentes, em dois enums diferentes | Já é a realidade atual do código (dois enums já compartilham o literal `Published` hoje); nomear explicitamente nos comentários de código (já presente em `Product.MarkAsPublished()` após esta mudança) evita ambiguidade — não introduzido por esta issue |
| `NetworkSettings` (array estático em `ProcessorJob`) e o enum `SocialNetwork` podem divergir de quais redes existem — se um publisher novo for adicionado ao enum sem entrar em `NetworkSettings`, a tooltip mostraria sempre "NotApplicable" para ele mesmo com credenciais configuradas | Fora do escopo desta issue (já é uma invariante implícita do sistema hoje); mencionar como nota para quem adicionar um publisher novo no futuro |
| Reset de dados (§2.6) executado incorretamente ou esquecido no deploy | Não é responsabilidade desta mudança de código; deixar claro no `tasks.md` como item de checklist de deploy separado das tasks de implementação, para o Gerente/LT não perderem de vista |

## 7. Dependências

- Nenhuma dependência externa nova.
- Depende do `PublicationQueue` e `SocialNetwork`/`PublicationStatus` existentes (Issue #6/#7/#8/#9/#10) — reaproveitados, não alterados.
- Depende de `ProductStatus.Published` continuar existindo com o mesmo valor de enum (não há renomeação) — evita quebrar dados/serialização já em uso pelo frontend (`ProductStatus` no `products.service.ts` já lista `'Published'` como um dos valores possíveis, inalterado).

## 8. Fora de escopo (confirmado no proposal)

- Migração/reprocessamento retroativo de produtos antigos (Cenário 5.1/5.2, restrição do Gerente).
- Qualquer nova condição de bloqueio de site além de aprovação IA + link de afiliado válido (CA 7.1).
- Mudança de comportamento dos publishers por rede social (Telegram/Instagram/Facebook/TikTok/Youtube) — inalterados.
