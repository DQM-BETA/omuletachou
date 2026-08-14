# Design Técnico — ISSUE-167: Categorização unificada + remoção de distinção de plataforma

## 1. Visão geral

O PRD e o Gate 1 já fecharam toda a sequência de negócio (dicionário na coleta, IA só no
`ProcessorJob` pós-aprovação, teto de orçamento, ordenação padrão inalterada, `Platform` some do
DTO público). Este design resolve as 3 questões técnicas escaladas ao Arquiteto — contabilização
de custo/orçamento, índices compostos, convivência/substituição da rota de categoria — e mapeia os
componentes de código reais impactados (li o backend .NET e o frontend Next.js antes de decidir).

Achado relevante não previsto no PRD, que **condiciona a Decisão 1 e o item 4 abaixo**:
`AfiliadoBot.Infrastructure` referencia apenas `AfiliadoBot.Domain` — **não** referencia
`AfiliadoBot.Application` (ver `AfiliadoBot.Infrastructure.csproj`). É `Application` quem referencia
`Infrastructure` (`ProcessorJob`/`CollectorJob` chamam serviços de Infrastructure). Hoje
`CategoryDetector` mora em `AfiliadoBot.Application`, mas os 3 collectors (que precisam chamá-lo em
`CollectAsync`, por decisão do Gate 1) moram em `AfiliadoBot.Infrastructure.Integrations.Platforms`.
Chamar `Application.CategoryDetector` a partir de `Infrastructure` criaria referência circular —
**não compila**. Decisão obrigatória: mover `CategoryDetector` para `AfiliadoBot.Domain` (classe
estática, sem I/O, já é uma regra de negócio pura — encaixa como serviço de domínio). Ambos
`Infrastructure` e `Application` já referenciam `Domain`, então a dependência resolve sem ciclo.

## 2. Componentes afetados (mapa de mudança)

| Camada | Componente | Mudança |
|---|---|---|
| Domain | `CategoryDetector` (movido de Application) | Expandir dicionário p/ 9 categorias/~35 subcategorias; `Detect` passa a retornar `(Category, Subcategory)` |
| Domain | `Product` | + propriedade `Subcategory` (nullable); novo método `SetCategoryFromAiFallback` |
| Domain | `IAiService` | + `Task<CategoryClassification?> ClassifyCategoryAsync(Product, ct)` |
| Domain | `CategoryClassification` (novo DTO) | `record CategoryClassification(string Category, string? Subcategory)` |
| Infrastructure | `AmazonCollector`/`MercadoLivreCollector`/`ShopeeCollector` | trocam `category: DefaultCategory` por `CategoryDetector.Detect(title, description)` na construção do `Product` |
| Infrastructure | `IAnthropicClientWrapper`/`AnthropicClientWrapper` | `CompleteAsync` passa a retornar `ClaudeCompletionResult(Text, InputTokens, OutputTokens)` em vez de `string` (usa `MessageResponse.Usage` do Anthropic.SDK — ver Decisão 1) |
| Infrastructure | `ClaudeAiService` | + `ClassifyCategoryAsync`; `ScoreProductAsync`/`GenerateCaptionAsync` só trocam `response` por `response.Text` (mecânico, escopo inalterado — CA 3.4) |
| Infrastructure | `IClaudeBudgetService`/`ClaudeBudgetService` (novo) | gate + contabilização do orçamento mensal (Decisão 1) |
| Infrastructure | `ProductConfiguration` | + coluna `subcategory`; novos índices compostos (Decisão 2) |
| Infrastructure | `AppSettingConfiguration` | + seeds: `claude.monthly_budget_limit_brl`, `claude.monthly_usage`, `claude.price_input_usd_per_mtok`, `claude.price_output_usd_per_mtok`, `claude.usd_brl_rate` |
| Infrastructure | Migration nova | `ALTER TABLE products ADD COLUMN subcategory`, índices, seeds acima |
| Application | `ProcessorJob` | `EnsureCategory` (dicionário) sai; entra `EnsureCategoryFallbackAsync` (só chama IA se `Category == "Geral"`) |
| Api | `PublicController.GetDeals` | + query params `category`, `subcategory`, `minPrice`, `maxPrice`, `minDiscount`, `sort` |
| Api | `PublicController` | + `GET /api/public/categories` (árvore com contagem) |
| Api | `PublicController.GetByCategory` (`/deals/category/{categoria}`) | **removida** (Decisão 3) |
| Api | `PublicDealDto` | remove `Platform`; mantém demais campos, `+ Subcategory` |
| Website | `website/lib/api.ts` (`fetchByCategory`) | passa a chamar `/api/public/deals?category=` em vez da rota removida |
| Website | `website/lib/types.ts` (`Deal`) | remove `platform`; `+ subcategory` |
| Website | `Header.tsx` | remove os chips de plataforma (`PLATFORMS`, `activePlatform`) — é a UI de "distinção de plataforma" citada no título da issue |
| Website | `FilterBar` (novo componente) | dropdowns dependentes categoria→subcategoria, slider de preço, botões de desconto, seletor de ordenação |
| Website | `app/page.tsx` (Home) | remove filtro client-side por `platform`; passa a ler `category/subcategory/minPrice/maxPrice/minDiscount/sort` de `searchParams` e repassar para `fetchDeals` |

## 3. Decisão técnica 1 — Contabilização de custo Claude e orçamento mensal

### 3.1 A API do Anthropic.SDK já retorna uso de tokens
Confirmado inspecionando o pacote instalado (`Anthropic.SDK 5.10.0`, `Anthropic.SDK.Messaging.MessageResponse`):
a resposta expõe `Usage` (com `InputTokens`/`OutputTokens`, além de `CacheCreationInputTokens`/
`CacheReadInputTokens`). Hoje `AnthropicClientWrapper.CompleteAsync` descarta isso e devolve só o
texto (`response.Content...Text`). **Não é preciso estimar tokens por conta própria** — basta parar
de descartar o `Usage` da resposta real.

### 3.2 Onde calcular: só no ponto do fallback de categorização, não transversal a toda chamada
Avaliei as duas opções levantadas no PRD:
- **Transversal (dentro de `ClaudeAiService`/`AnthropicClientWrapper`, contando scoring + legenda + categorização)**: rejeitada. A regra de negócio do Gate 1 (proposal.md item 5 / CA 4.4) é explícita — "o teto rege exclusivamente o fallback de categorização", e scoring/legenda **nunca** são desativados por ele. Se o contador somasse todas as chamadas, o volume de scoring (roda em todo produto coletado, muito mais frequente que o fallback de categorização) esgotaria o teto de R$30 antes mesmo de qualquer categorização ser tentada — inverte a intenção do Gerente, que é proteger especificamente o gasto do fallback. Contar tudo também exigiria decidir se um estouro por scoring "vaza" para bloquear categorização — nenhuma cláusula do Gate 1 pede isso.
- **Escopado ao fallback de categorização (`ClassifyCategoryAsync`)**: escolhida. Bate literalmente com CA 4.2 ("uma chamada de fallback de categorização... o custo é somado") e mantém `ScoreProductAsync`/`GenerateCaptionAsync` sem qualquer nova dependência de orçamento (CA 3.4 continua satisfeito por construção).

Consequência prática: `IAnthropicClientWrapper.CompleteAsync` passa a devolver
`ClaudeCompletionResult(string Text, int InputTokens, int OutputTokens)` (dado bruto, sem custo/
BRL — isso é responsabilidade de quem consome). `ScoreProductAsync`/`GenerateCaptionAsync` ignoram
os tokens (só usam `.Text`, sem chamar o orçamento). Só `ClassifyCategoryAsync` (novo) usa os tokens
para debitar o orçamento.

### 3.3 Granularidade: por chamada, usando tokens reais (não estimativa fixa por prompt)
Estimativa fixa por tipo de prompt foi descartada — já temos os tokens reais na resposta, então usar
um valor fixo seria menos preciso sem ganhar simplicidade real (o cálculo é uma multiplicação
trivial). Fórmula:
```
custoUSD = (InputTokens / 1_000_000m) * precoInputUsdPorMTok
         + (OutputTokens / 1_000_000m) * precoOutputUsdPorMTok
custoBRL = custoUSD * taxaUsdBrl
```
`precoInputUsdPorMTok`, `precoOutputUsdPorMTok` e `taxaUsdBrl` ficam em `app_settings` (configuráveis
sem deploy — preço de modelo e câmbio mudam; o design não deve fixar isso em código). Não faço
conversão de câmbio em tempo real via API externa (nova integração, novo ponto de falha) para um
guard-rail de orçamento que já é soft (CA 4.3 só impede *novas* chamadas, não é contabilidade
fiscal) — uma taxa fixa configurável é suficiente e mais simples/confiável.

### 3.4 Onde persistir o acumulado mensal
Uma única chave nova em `app_settings`, valor JSON (schema já usado pra outras chaves como texto
livre, sem precisar de tabela dedicada — volume e cardinalidade não justificam):
```
key = "claude.monthly_usage"
value = {"month": "2026-08", "spend_brl": 12.34}
```
Reset mensal **preguiçoso** (lazy), sem job/cron dedicado: tanto a leitura (`IsCategorizationBudgetAvailableAsync`)
quanto a escrita (`RecordUsageAsync`) comparam o `month` armazenado com o mês corrente (`yyyy-MM`,
UTC); se diferente, tratam o gasto como zero (leitura) ou reinicializam o JSON (escrita). Isso cobre
CA 4.5 sem precisar de um novo `RecurringJob` — menos peça móvel, menos risco.

`claude.monthly_budget_limit_brl` (chave já prevista no Gate 1) segue como está: valor simples
(string numérica), default `"30"`.

### 3.5 Race condition — múltiplos jobs em paralelo (Hangfire)
Risco real, não hipotético: `ProcessorJob` não tem `[DisableConcurrentExecution]` e existe um
endpoint de disparo manual (`JobsController.TriggerProcessor`) que pode rodar concorrente ao cron
agendado. Ler o JSON em C#, somar em memória e regravar (`AppSetting.UpdateValue` + `SaveChangesAsync`)
tem *lost update* clássico sob concorrência (dois workers leem o mesmo valor antes de qualquer um
gravar).

Decisão: o incremento roda como **um único `UPDATE` SQL atômico** (via
`ExecuteSqlInterpolatedAsync`, fora do change tracker do EF), que já embute a lógica de reset de mês
no mesmo statement:
```sql
UPDATE app_settings
SET value = CASE
        WHEN (value::jsonb->>'month') = @mesAtual
            THEN jsonb_set(value::jsonb, '{spend_brl}',
                 to_jsonb(((value::jsonb->>'spend_brl')::numeric + @deltaBrl)))::text
        ELSE jsonb_build_object('month', @mesAtual, 'spend_brl', @deltaBrl)::text
    END,
    updated_at = now()
WHERE key = 'claude.monthly_usage';
```
Um `UPDATE` de uma única linha é atômico no Postgres (lock de linha serializa concorrência
automaticamente) — não precisa de lock distribuído, `SELECT ... FOR UPDATE` explícito, nem tabela de
ledger. **Trade-off aceito**: a checagem de orçamento antes de decidir se chama a IA
(`IsCategorizationBudgetAvailableAsync`) é um `SELECT` simples, sem lock — existe uma janela
teórica entre "checar disponível" e "gravar uso" em que duas chamadas concorrentes poderiam ambas
passar do teto por uma fração de centavo. Não vale complexidade de lock distribuído para isso: o
padrão de execução do `ProcessorJob` é essencialmente sequencial (loop `for` sobre produtos, uma
chamada de IA por vez); o único cenário de concorrência real é disparo manual sobrepondo o cron, e o
pior caso é um estouro de poucos centavos em um teto de R$30 — irrelevante financeiramente. Se o
volume de disparos manuais concorrentes crescer a ponto de importar, a mitigação futura é
`[DisableConcurrentExecution]` no `ProcessorJob` (simples, mas fora do escopo desta issue).

### 3.6 Contrato resultante
```csharp
// Domain
public record CategoryClassification(string Category, string? Subcategory);

public interface IAiService
{
    Task<ProductScore> ScoreProductAsync(Product product, CancellationToken ct = default); // inalterado
    Task<string> GenerateCaptionAsync(Product product, SocialNetwork network, CancellationToken ct = default); // inalterado
    Task<CategoryClassification?> ClassifyCategoryAsync(Product product, CancellationToken ct = default); // novo
}

// Infrastructure
public record ClaudeCompletionResult(string Text, int InputTokens, int OutputTokens);

public interface IAnthropicClientWrapper
{
    Task<ClaudeCompletionResult> CompleteAsync(string systemPrompt, string userMessage, CancellationToken ct = default);
}

public interface IClaudeBudgetService
{
    Task<bool> IsCategorizationBudgetAvailableAsync(CancellationToken ct = default);
    Task RecordUsageAsync(int inputTokens, int outputTokens, CancellationToken ct = default); // só chamada após sucesso (CA 4.2)
}
```
`ClaudeAiService.ClassifyCategoryAsync`: se `!budgetService.IsCategorizationBudgetAvailableAsync()`
retorna `null` sem chamar a API (CA 4.3). Se a chamada falhar (exceção/timeout), retorna `null` sem
debitar orçamento (só chamadas bem-sucedidas contam — CA 4.2 diz "executada com sucesso"; erro não
bloqueia o `ProcessorJob`, mesma postura já usada em `GenerateCaptionAsync`/`ScoreProductAsync`).
`ProcessorJob.EnsureCategoryFallbackAsync` só invoca `ClassifyCategoryAsync` quando
`product.Category == "Geral"` (o filtro `Status == Queued` já é garantido pela query do topo do
`ExecuteAsync`, então CA 3.1-3.3 ficam cobertos sem checagem adicional de status).

## 4. Decisão técnica 2 — Índices compostos

### 4.1 Padrões de query a suportar
- Home sem filtro (mais frequente hoje, CA 6.1): `status=Published`, `ORDER BY ai_score DESC`.
- Categoria+subcategoria (CA 6.2), combinável com faixa de preço/desconto mínimo (CA 6.3) e 4
  ordenações alternativas (CA 6.4): `ai_score` (padrão), `sale_price` asc, `discount_pct` desc,
  `created_at` desc (mais recente).
- Árvore de categorias com contagem (CA 6.7): `GROUP BY category, subcategory` filtrado por
  `status=Published`.

### 4.2 Índices escolhidos
```sql
CREATE INDEX IX_products_status_aiscore
    ON products (status, ai_score DESC);

CREATE INDEX IX_products_status_category_subcategory_aiscore
    ON products (status, category, subcategory, ai_score DESC);

CREATE INDEX IX_products_status_category_subcategory_saleprice
    ON products (status, category, subcategory, sale_price);

CREATE INDEX IX_products_status_category_subcategory_discountpct
    ON products (status, category, subcategory, discount_pct DESC);

CREATE INDEX IX_products_status_category_subcategory_createdat
    ON products (status, category, subcategory, created_at DESC);
```
Justificativa da ordem: `status` sempre lidera (todo filtro público começa com
`Status == Published` — mesmo padrão do índice único hoje inexistente para esse filtro, que também
fecho aqui: `IX_products_status_aiscore` cobre o caso hoje sem índice nenhum). `category` antes de
`subcategory` porque subcategoria só faz sentido combinada com categoria (nunca filtrada sozinha —
não há esse cenário nos critérios de aceite) e porque igualdade-antes-de-igualdade é o uso canônico
de índice composto B-tree. A coluna de ordenação (`ai_score`/`sale_price`/`discount_pct`/`created_at`)
fica por último em cada variante — permite ao Postgres usar o índice tanto para filtrar quanto para
já entregar os resultados ordenados (evita `Sort` explícito no plano).

`minPrice`/`maxPrice`/`minDiscount` são predicados de *range*, não de igualdade — funcionam como
scan de intervalo na cauda do índice cujo sort bate com o parâmetro `sort` escolhido; quando o
filtro de range não corresponde ao índice do sort ativo (ex.: `sort=price_asc` combinado com
`minDiscount`), a query cai para o índice de `sale_price` + filtro heap do `discount_pct` — aceito
sem índice dedicado para cada combinação (ver 4.3).

### 4.3 Trade-offs considerados e descartados
- **Índice parcial (`WHERE status = 3`)**: reduziria tamanho/custo de manutenção do índice, mas o
  EF Core parametriza `p.Status == ProductStatus.Published` por padrão — índice parcial só é
  aproveitado pelo planner quando o literal do predicado bate exatamente com o valor da query, o
  que fica frágil/imprevisível com queries parametrizadas sem forçar `EnableConstantParameterization`
  em cada LINQ. Descartado por simplicidade/previsibilidade: o volume deste projeto (produtos
  curados de afiliado, não um catálogo de e-commerce genérico — dezenas/centenas por ciclo) não
  justifica essa otimização fina.
- **Um índice único cobrindo todas as combinações filtro×sort**: impossível com B-tree simples
  quando há múltiplas ordenações alternativas independentes — cada sort exige sua própria cauda de
  índice. Optei por replicar o prefixo `(status, category, subcategory)` em 4 índices (um por sort)
  em vez de tentar 1 índice "genérico" que obrigaria `Sort` explícito no plano para 3 das 4
  ordenações.
- **Cobrir toda combinação filtro×sort (ex.: `minDiscount`+`sort=price_asc` com prefixo
  categoria)**: explosão combinatória (4 sorts × 2 filtros de range × com/sem categoria) sem ganho
  proporcional dado o volume atual — os índices acima cobrem os padrões *combinados* citados no
  enunciado (categoria+subcategoria+faixa de preço, para as 4 ordenações), casos residuais caem em
  scan filtrado sobre um índice parcialmente útil, aceitável nesta escala.

### 4.4 Migration
`Subcategory` nullable, `VARCHAR(100)` (mesmo padrão de `Category`, mas nullable — CA 1.1/1.2):
```sql
ALTER TABLE products ADD COLUMN subcategory VARCHAR(100) NULL;
```
Sem backfill (Gate 1, regra 3) — produtos existentes ficam com `subcategory = NULL`.

## 5. Decisão técnica 3 — Rota `/api/public/deals/category/{categoria}`

### 5.1 O `website` consome a rota antiga hoje
Confirmado lendo o código: `website/lib/api.ts::fetchByCategory` monta
`GET /api/public/deals/category/{categoria}` e é chamada por
`website/app/categoria/[categoria]/page.tsx` — uma rota Next.js real, indexável, com
`generateMetadata` (SEO). Não é um resquício morto: **se a rota do backend for removida sem migrar
o consumo, a página `/categoria/[categoria]` do site quebra em produção.**

### 5.2 Decisão: remover a rota antiga do backend, preservar a URL do site, migrar o consumo
- **Backend**: remove `PublicController.GetByCategory` (`/deals/category/{categoria}`). A lógica
  dele é subconjunto estrito da nova `GetDeals` (mesmo filtro de status, mesmo `OrderByDescending
  (AiScore)`, mesmo DTO) — mantê-la como um segundo caminho de código faz duas rotas fazerem a
  mesma coisa de formas divergentes ao longo do tempo (uma delas inevitavelmente fica desatualizada
  quando o filtro combinável evoluir). Depreciar-e-manter (ex.: 301 ou redirect HTTP) foi descartado
  porque não há consumidor externo de terceiros documentado — o único cliente conhecido é o próprio
  `website`, que este design já migra.
- **Frontend**: `website/app/categoria/[categoria]/page.tsx` **mantém a URL/rota Next.js
  inalterada** (`/categoria/{categoria}`, preserva SEO/links já indexados). Só troca a
  implementação de `fetchByCategory` para chamar `GET /api/public/deals?category={categoria}`
  (mesma paginação, mesmo contrato de resposta `PagedResult<Deal>`). Consumo migrado, não a URL
  pública do site — os dois são independentes.
- **Consequência para o LT/Dev**: a task de "novos endpoints" precisa incluir explicitamente
  "migrar `fetchByCategory` para a querystring nova + remover a rota antiga do backend" como parte
  da mesma sub-issue (não pode ser feito em ordens separadas sem quebrar o site: se o backend
  remover a rota antes do frontend migrar, `/categoria/[categoria]` quebra em produção — a ordem
  seguro é: (1) subir `GetDeals` com os novos filtros, (2) migrar `fetchByCategory`, (3) só então
  remover `GetByCategory`, idealmente no mesmo PR/deploy para não deixar uma janela quebrada).

## 6. Contrato de componentes globais (frontend)

O projeto **não centraliza `Header` no root layout** hoje — cada página (`app/page.tsx`,
`app/categoria/[categoria]/page.tsx`, `app/oferta/[slug]/page.tsx`) renderiza seu próprio
`<Header />`. Esta issue **preserva esse padrão existente** (não é escopo migrar para layout
global) — só simplifica o conteúdo do `Header` (remove os chips de plataforma) e adiciona o novo
`FilterBar` como componente de página (não global), evitando que o Dev duplique `Header`/`FilterBar`
dentro do root layout por engano.

| Componente | Renderiza em | NÃO renderiza em |
|---|---|---|
| `RootLayout` (html/body, manifest, theme-color) | `app/layout.tsx` | Screens individuais |
| `PushSubscriptionManager` | `app/layout.tsx` (global, 1x) | Screens individuais |
| `Header` (marca/logo — chips de plataforma removidos nesta issue) | Cada screen que já o usa hoje: `app/page.tsx`, `app/categoria/[categoria]/page.tsx`, `app/oferta/[slug]/page.tsx` (padrão pré-existente, não alterado) | `app/layout.tsx` |
| `FilterBar` (novo — dropdowns categoria/subcategoria, slider de preço, botões de desconto, seletor de ordenação) | `app/page.tsx` (Home) — único escopo pedido pelos critérios de aceite (7.1-7.5) | `app/layout.tsx`; `app/categoria/[categoria]/page.tsx` (fora de escopo — página já filtra por categoria via rota); `app/oferta/[slug]/page.tsx` (detalhe não tem filtros) |
| `DealCard`/`DealDetail` | Sem mudança de posição; só param de receber/exibir `platform` (se algum dia recebiam — grep confirmou que não renderizam badge de plataforma hoje, CA 7.4 já estava satisfeito) | — |

## 7. Dependências

- Anthropic.SDK 5.10.0 (já instalado) — `MessageResponse.Usage` é o único ponto novo de uso da SDK,
  nenhuma dependência nova.
- Nenhuma integração externa nova (câmbio USD/BRL é config estática, não API — ver 3.3).
- Migration única cobre: coluna `subcategory`, 5 índices (seção 4.2), seeds novos de `app_settings`
  (`claude.monthly_budget_limit_brl` default `"30"`, `claude.monthly_usage` default
  `{"month":"","spend_brl":0}`, `claude.price_input_usd_per_mtok`, `claude.price_output_usd_per_mtok`,
  `claude.usd_brl_rate` — valores default de preço/câmbio precisam ser confirmados pelo
  Gerente/DevOps com a tabela de preços vigente da Anthropic no momento do deploy; este design só
  garante que sejam configuráveis sem redeploy, não fixa o valor correto hoje).
- Mover `CategoryDetector` de `Application` para `Domain` (seção 1) — mecânico, mas obrigatório
  antes de qualquer collector poder chamá-lo; sem isso a sub-issue dos collectors não compila.

## 8. Riscos

- **Cobertura do dicionário v1**: 9 categorias/~35 subcategorias cobrindo produtos reais de 3
  marketplaces é trabalho de curadoria (não só código) — risco de volume alto em "Geral" no
  lançamento se as palavras-chave não cobrirem bem o catálogo real. Mitigação: CA 2.3 já exige teste
  por categoria/subcategoria; validação com amostra real fica com o Dev/QA, fora do escopo deste
  design.
- **Preço/câmbio desatualizados**: se `claude.price_*`/`claude.usd_brl_rate` não forem atualizados
  quando a Anthropic mudar preços, o teto de R$30 fica impreciso (para mais ou para menos) — soft
  guard, não afeta scoring/legenda, risco financeiro limitado ao próprio teto configurado.
- **Migração da rota antiga em produção (Decisão 3)**: se o deploy do backend (remoção de
  `/deals/category/{categoria}`) acontecer antes do deploy do frontend migrado, `/categoria/*`
  quebra temporariamente. Mitigado exigindo que a sub-issue trate os dois lados juntos (seção 5.2).
- **Concorrência do `ProcessorJob`** (seção 3.5): aceito como risco financeiro desprezível, não
  como risco de corrupção de dado (o `UPDATE` atômico elimina lost-update; só a checagem prévia tem
  janela).

## 9. Fora de escopo deste design (fica para o LT / Dev)

- Conteúdo exato do dicionário expandido (as ~35 subcategorias e suas palavras-chave) — curadoria de
  dado, não decisão de arquitetura.
- Layout visual do `FilterBar` (Figma/UX-UI, se aplicável antes dos devs, conforme máquina de
  estados da rota normal — esta issue é rota `backlog`, então isso fica para quando a issue for
  retomada).
- Nome exato dos parâmetros de `sort` (`price_asc`/`discount_desc`/`recent`/etc.) — só a existência
  e o comportamento (CA 6.4/6.5) são contrato; nomenclatura fina é refinamento do LT.
