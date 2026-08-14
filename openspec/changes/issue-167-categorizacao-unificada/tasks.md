# Tasks — ISSUE-167: Categorização unificada de produtos + remoção de distinção de plataforma

Ordem de dependência: **Sub-A primeiro** (schema + `CategoryDetector` + collectors). **Sub-B e Sub-C
podem rodar em paralelo entre si** assim que a Sub-A estiver mergeada em `desenv` (ambas dependem só
do schema/coluna `Subcategory`, não uma da outra). **Sub-D** (frontend) pode ser codada em paralelo
com mocks contra o contrato descrito na especificação técnica, mas **não pode ser mergeada/deployada
em produção antes da Sub-C** (design.md §5.2 — remoção da rota antiga `GetByCategory` quebra
`/categoria/[categoria]` se o frontend não tiver migrado ainda). Release `homolog→main` espera as 4
prontas juntas.

Referência completa (contratos exatos, trechos de código, paths, offsets de linha):
`documentacoes/ISSUE-167-categorizacao-unificada/especificacao-tecnica.md` (seções indicadas em cada
sub-tarefa abaixo). Critérios de aceite completos (Given/When/Then):
`documentacoes/ISSUE-167-categorizacao-unificada/criterios-aceite.md`.

## Sub-A (#168) — backend-schema-collectors, bloqueante — `stack:dotnet`

### Critérios de aceite
CA 1.1, 1.2 (migration aditiva), CA 2.1, 2.2, 2.3 (dicionário expandido + 3 collectors).

### O que fazer
1. Migration EF Core `AddSubcategoryAndCategorizationBudget`: coluna `Subcategory` (nullable,
   VARCHAR 100) em `Product`, 5 índices compostos, seeds novos em `app_settings` (orçamento Claude) —
   especificação técnica seção 1.
2. Mover `CategoryDetector` de `AfiliadoBot.Application` para `AfiliadoBot.Domain.Services` (resolve
   dependência circular achada pelo Arquiteto) — seção 2, com a tabela de todos os arquivos afetados.
3. Expandir o dicionário de 6 para 9 categorias / ~35 subcategorias, mudar assinatura de `Detect` para
   `(string Category, string? Subcategory)` — seção 3. Curadoria das keywords é do Dev.
4. Integrar `CategoryDetector.Detect` nos 3 collectors (`AmazonCollector`, `MercadoLivreCollector`,
   `ShopeeCollector`), setando `Category`/`Subcategory` já na criação do `Product` — seção 4. Não
   tocar `UpdateFromCollector` (sem recategorização retroativa).
5. Ajustar `CategoryDetectorTests.cs` (nova assinatura + casos por categoria/subcategoria — CA 2.3) e
   testes dos 3 collectors.

### Contexto técnico
- Especificação técnica seções 1-4 (migration completa, mapeamento de arquivos do `CategoryDetector`,
  estrutura de dados do dicionário, mudança nos 3 collectors).
- Base para as Sub-B e Sub-C (ambas dependem do schema/coluna já existir e do `CategoryDetector`
  já estar em `Domain`). **Fazer merge desta sub-issue em `desenv` antes de iniciar Sub-B/Sub-C.**
- Repo: `repos/omuletachou`. Stack: ASP.NET Core 8 / EF Core 8 / PostgreSQL 16.

## Sub-B (#169) — backend-ia-orcamento, depende da Sub-A — `stack:dotnet`

### Critérios de aceite
CA 3.1, 3.2, 3.3, 3.4 (fallback IA no `ProcessorJob`), CA 4.1, 4.2, 4.3, 4.4, 4.5 (orçamento mensal).

### O que fazer
1. Remover `EnsureCategory` (dicionário) do `ProcessorJob` — essa camada já roda nos collectors
   (Sub-A). Adicionar `EnsureCategoryFallbackAsync` (assíncrono, chama IA só quando
   `Category == "Geral"`), reordenar o loop para rodar o fallback **antes** de `EnsureSlug` — seção 5.
2. Criar `IClaudeBudgetService`/`ClaudeBudgetService` (Infrastructure): leitura de disponibilidade de
   orçamento (`IsCategorizationBudgetAvailableAsync`) e `UPDATE` atômico de uso
   (`RecordUsageAsync`, via `ExecuteSqlInterpolatedAsync`, fora do change tracker) — seção 6.
3. `IAnthropicClientWrapper`/`ClaudeAiService`: `CompleteAsync` passa a retornar
   `ClaudeCompletionResult(Text, InputTokens, OutputTokens)`; novo `ClassifyCategoryAsync` (checa
   orçamento → chama Claude → registra uso só em caso de sucesso) — seção 7. `ScoreProductAsync`/
   `GenerateCaptionAsync` só trocam de `response` para `response.Text`, sem ganhar lógica de
   orçamento (CA 3.4, 4.4).
4. Testes: `ProcessorJobTests` (fallback condicionado a `Status==Queued` + `Category=="Geral"` +
   orçamento disponível, reordenação), testes de `ClaudeBudgetService` (reset mensal lazy, `UPDATE`
   atômico), testes de `ClassifyCategoryAsync` (orçamento estourado retorna `null` sem debitar).

### Contexto técnico
- Especificação técnica seções 5-7 (trechos de código completos do `ProcessorJob`,
  `IClaudeBudgetService`, `UPDATE` SQL atômico com `CASE`, contrato `ClaudeCompletionResult`).
- Depende da Sub-A apenas para a coluna `Subcategory` existir (usada em
  `SetCategoryFromAiFallback`) — pode iniciar assim que a Sub-A estiver em `desenv`.
- Confirmar preço/câmbio vigentes do modelo `claude-haiku-4-5-20251001` antes do deploy (seção 1.3,
  seeds `claude.price_input_usd_per_mtok`/`claude.price_output_usd_per_mtok`/`claude.usd_brl_rate` —
  valores placeholder na especificação, "soft guard" não bloqueante).
- Repo: `repos/omuletachou`. Stack: ASP.NET Core 8 / Anthropic.SDK.

## Sub-C (#170) — backend-api-filtros, depende da Sub-A — `stack:dotnet`

### Critérios de aceite
CA 5.1, 5.2, 5.3 (remoção de `Platform` do DTO público), CA 6.1-6.7 (filtros + endpoint de árvore de
categorias).

### O que fazer
1. `PublicDealDto`: remover `Platform`, adicionar `Subcategory` — seção 8. DTO interno/dashboard
   (`ProductDtos.cs`) não é tocado.
2. `PublicController.GetDeals`: expandir com `[FromQuery]` de `category`, `subcategory`, `minPrice`,
   `maxPrice`, `minDiscount`, `sort` — todos opcionais/combináveis, sem parâmetro reconhecido cai no
   default (ordenação por `AiScore`) — seção 8, trecho de código completo.
3. Novo endpoint `GET /api/public/categories` retornando árvore `Category > [Subcategory]` só com
   produtos `Published`, com contagem — seção 8, novos DTOs `CategoryTreeDto`/`SubcategoryCountDto`.
4. Remover `GetByCategory` (`/api/public/deals/category/{categoria}`) — **só nesta sub-issue, não
   fazer deploy isolado em produção antes de a Sub-D estar pronta** (ordem de deploy obrigatória:
   `GetDeals` novo sobe → frontend migra → só então remove a rota antiga).
5. Testes de `PublicController` cobrindo os 7 cenários de CA 6 (filtros combináveis, valor não
   reconhecido → 200 vazio, ordenação default inalterada, árvore de categorias só com produtos
   ativos).

### Contexto técnico
- Especificação técnica seção 8 (trechos de código completos do `GetDeals`/`GetCategories`).
- Depende da Sub-A para a coluna `Subcategory` e os índices (usados pelos filtros/ordenação).
- Pode rodar em paralelo com a Sub-B (não há dependência entre elas).
- Repo: `repos/omuletachou`. Stack: ASP.NET Core 8 / EF Core 8.

## Sub-D (#171) — frontend-filtros, depende da Sub-C para o contrato final — `stack:nodejs`

### Critérios de aceite
CA 7.1, 7.2, 7.3, 7.4, 7.5.

### O que fazer
1. `website/lib/api.ts`: migrar `fetchByCategory` para reusar `fetchDeals` (querystring), `fetchDeals`
   ganha parâmetro `filters` (`category`, `subcategory`, `minPrice`, `maxPrice`, `minDiscount`,
   `sort`), novo `fetchCategories()` — seção 9.1. Ajustar todos os call sites e testes
   (`app/categoria/[categoria]/page.tsx`, `app/page.tsx`, `.test.tsx` correspondentes).
2. `website/lib/types.ts`: remover `Deal.platform`, adicionar `Deal.subcategory?`, novo tipo
   `CategoryTree` — seção 9.2.
3. `website/components/Header.tsx`: remover os chips de plataforma (`PLATFORMS`/`activePlatform`) —
   é aqui que a distinção de plataforma aparece hoje, não em badge de card. Ajustar
   `Header.test.tsx` — seção 9.3.
4. Novo componente `FilterBar` (usar layout/mockup do UX/UI): dropdowns dependentes
   categoria→subcategoria, slider de faixa de preço, botões de desconto mínimo (10%+/30%+/50%+),
   seletor de ordenação. Renderiza **só em `app/page.tsx` (Home)**. Estado dos filtros via
   `useSearchParams`/`router.push`. Estado vazio "nenhuma oferta encontrada" (CA 7.5) — seção 9.4.
5. **Aguardar o mockup do UX/UI antes de implementar o `FilterBar`** (layout/interação/responsivo do
   slider). Pode iniciar a migração de `api.ts`/`types.ts`/`Header.tsx` (itens 1-3) em paralelo com o
   UX/UI, já que não dependem de mockup.
6. Pode ser codada contra a especificação do endpoint (seção 8) com mocks antes de a Sub-C ser
   mergeada, mas o merge final para `desenv`/deploy espera a Sub-C estar pronta (contrato real de
   `GET /api/public/deals` e `GET /api/public/categories`).

### Contexto técnico
- Especificação técnica seção 9 (trechos de código completos de `api.ts`, assinatura de `fetchDeals`,
  tipos, achado do `Header.tsx`, escopo do `FilterBar`).
- UX/UI: mockup do `FilterBar` recomendado antes desta sub-issue (avaliação do LT em
  `especificacao-tecnica.md`, seção "Avaliação de necessidade de UX/UI").
- Repo: `repos/omuletachou`. Stack: Next.js 14+ (`website/`).

## Decisão UX/UI
**Sim, aciona o UX/UI**, antes da Sub-D. O `FilterBar` é UI nova real (dropdowns dependentes, slider
de faixa de preço, grupo de botões de desconto mínimo, seletor de ordenação), não ajuste de CSS em
componente existente — ver avaliação completa em
`documentacoes/ISSUE-167-categorizacao-unificada/especificacao-tecnica.md`, seção "Avaliação de
necessidade de UX/UI". As Sub-A/Sub-B/Sub-C (backend) não dependem do UX/UI e podem começar em
paralelo.
