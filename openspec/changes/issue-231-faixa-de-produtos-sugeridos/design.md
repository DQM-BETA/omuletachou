# Design Técnico — ISSUE-231: Rastreio de cliques + faixa de produtos sugeridos (site público)

## 1. Visão geral

Duas capacidades novas, uma dependente da outra: (2) registrar cliques em produtos de forma
anônima (produto + timestamp) sem alterar o destino atual do clique; (1) usar essa contagem para
alimentar uma faixa/carrossel de "produtos sugeridos" na listagem pública, ordenada por mais
clicados dentro da categoria filtrada, com fallback para mais clicados em geral quando a listagem
principal (com os filtros atuais) não retorna produtos.

Contexto de volume (considerado nas decisões 1 e 2): catálogo de ~100-300 produtos ativos,
tráfego de afiliado baixo/médio. Isso descarta preocupações de escala tipo "milhões de cliques/dia"
e favorece soluções simples (escrita síncrona, sem fila) sobre soluções que só se justificam em
alto volume (jobs de agregação assíncrona, sharding, cache distribuído).

Esquema atual confirmado em `documentacoes/ISSUE-2-domain-efcore-schema/especificacao-tecnica.md`
(`ProductConfiguration`): `products.id` é `uuid`, `category` é `varchar(100)`, `status` é `int`
(enum convertido), `created_at` é `timestamptz`, `platform` é `int` (enum). Índices compostos
`(status, category, <sort> DESC)` já existem como padrão estabelecido na ISSUE-167 — a decisão 2
abaixo estende esse mesmo padrão em vez de inventar um novo.

## 2. Contrato de componentes globais (site público, Next.js)

Esta issue não cria nem altera layout raiz — adiciona um componente novo à página de listagem já
existente. Tabela de contrato para evitar ambiguidade de onde o Dev deve montar a faixa:

| Componente | Renderiza em | NÃO renderiza em |
|---|---|---|
| Layout (Header + Footer) | layout raiz do `website/` (inalterado por esta issue) | Página de listagem, `SuggestedProductsCarousel` |
| Página de listagem de produtos (`app/page.tsx` ou equivalente, conforme ISSUE-167) | grid principal + `FilterBar` (existentes) + `SuggestedProductsCarousel` (novo) | — |
| `SuggestedProductsCarousel` (novo) | Dentro da página de listagem, abaixo/acima do grid principal (posição exata a critério do LT/UX) | Não é um Provider nem componente global; não aparece em outras rotas |
| Registro de clique (`trackProductClick`, função utilitária, não Provider) | Chamado pelo card de produto (componente já existente), tanto no grid principal quanto dentro do carrossel | Não precisa de Context/Provider — é uma chamada `sendBeacon` stateless |

## 3. Componentes afetados (mapa de mudança, alto nível)

Não tenho acesso de leitura ao código-fonte (escopo do Arquiteto é `documentacoes/`, `openspec/` e
`CLAUDE.md`) — nomes exatos de arquivo/classe ficam para o LT confirmar no refinamento. Mapa por
camada, seguindo a arquitetura já usada (`AfiliadoBot.Domain/Infrastructure/Application/Api` +
`website/`, conforme ISSUE-167/ISSUE-2):

| Camada | Componente | Mudança |
|---|---|---|
| Domain | `Product` | `+ int ClickCount` (default 0) |
| Domain | `ProductClick` (nova entidade) | `Id (bigserial/long), ProductId (uuid, FK), ClickedAt (timestamptz)` |
| Infrastructure | `ProductConfiguration` | `+ click_count`; novos índices (Decisão 2) |
| Infrastructure | `ProductClickConfiguration` (nova) | mapeia `product_clicks`, índice em `product_id` e `clicked_at` |
| Infrastructure | Migration nova | `ALTER TABLE products ADD COLUMN click_count`; `CREATE TABLE product_clicks`; índices |
| Api | `PublicController` | `+ POST /api/public/products/{id}/click`; `+ GET /api/public/products/suggested` |
| Api | `PublicDealDto` (ou equivalente já usado na listagem) | reaproveitado sem alteração como shape de resposta do endpoint de sugeridos |
| Website | Card de produto (componente já existente, usado no grid e no carrossel) | `onClick` passa a chamar `trackProductClick(id)` (fire-and-forget) antes/junto da navegação existente, sem alterar `href`/destino |
| Website | `SuggestedProductsCarousel` (novo) | carrossel horizontal com setas, busca `GET /api/public/products/suggested`, renderiza cards reaproveitando o componente de card existente |
| Website | `lib/api.ts` (ou equivalente) | `+ fetchSuggestedProducts(categories, mainListingHasResults)`, `+ trackProductClick(id)` |

> Nomes exatos confirmados pelo LT na `especificacao-tecnica.md` desta issue (controller/rota real,
> boundary client/server component do `website/`) — ver seção 12 abaixo para os ajustes feitos
> durante o refinamento técnico face ao código real.

## 4. Decisão técnica 1 — Persistência de cliques: tabela de eventos + contador desnormalizado

**Escolhida: as duas coisas, não uma ou outra.**

- `product_clicks` (tabela de eventos, append-only): `id`, `product_id` (FK), `clicked_at`. Guarda
  o histórico granular por clique.
- `products.click_count` (int, default 0): contador agregado, atualizado de forma **síncrona** no
  mesmo request que insere o evento (`UPDATE products SET click_count = click_count + 1 WHERE id = @id`).

Por que não só um dos dois:

- **Só contador (sem tabela de eventos)** foi descartado: perderia qualquer possibilidade de
  relatório futuro (cliques por dia, por período) sem custo nenhum de manter — o insert extra é uma
  linha, indexada por PK implícito (`bigserial`), em uma tabela sem foreign keys de saída, sem joins
  no caminho de escrita. Dado que a issue já pede pensar em "valor futuro" e o custo de manter é
  desprezível neste volume (100-300 produtos, tráfego baixo/médio — mesmo a alguns milhares de
  cliques/mês isso é irrelevante para o Postgres), não há razão para abrir mão do histórico.
- **Só tabela de eventos (sem contador, agregando on-the-fly com `COUNT`/`GROUP BY` a cada leitura)**
  foi descartada para o caminho de leitura: a listagem de produtos é a página de maior tráfego do
  site (mesma preocupação de performance já registrada na ISSUE-167). Fazer `JOIN product_clicks
  GROUP BY product_id` a cada carregamento de página (grid principal decide ordenação por
  `ai_score` hoje, mas a faixa de sugeridos precisa de "mais clicados" a cada render) adiciona uma
  agregação em tempo real desnecessária quando um simples `SELECT ... ORDER BY click_count DESC`
  com índice resolve em O(log n) sem groupby. Não há necessidade de introduzir um job assíncrono
  (Hangfire, já usado no projeto) para manter esse contador "quase em tempo real" — dado o volume,
  a atualização síncrona (um `UPDATE` de uma linha, por PK, indexado) custa microssegundos e evita
  a janela de staleness que um job periódico introduziria (produto que acabou de viralizar só
  apareceria como "mais clicado" no próximo ciclo do job). Rejeitado por adicionar complexidade
  operacional (mais um job Hangfire para monitorar/falhar) sem ganho real neste volume.
- Concorrência: `UPDATE products SET click_count = click_count + 1` é um incremento relativo
  atômico no Postgres (não é read-modify-write em código de aplicação) — não há necessidade de lock
  otimista/pessimista adicional mesmo com cliques concorrentes no mesmo produto.

Migration: `ALTER TABLE products ADD COLUMN click_count int NOT NULL DEFAULT 0;` e
`CREATE TABLE product_clicks (id bigserial PRIMARY KEY, product_id uuid NOT NULL REFERENCES
products(id) ON DELETE CASCADE, clicked_at timestamptz NOT NULL DEFAULT now());`.

## 5. Decisão técnica 2 — Agregação "mais clicados por categoria": índices, sem job

Como o contador já vive desnormalizado em `products.click_count` (Decisão 1), a agregação por
categoria não precisa de `GROUP BY` nem de job — é uma leitura direta com `ORDER BY`, no mesmo
padrão de índice composto já estabelecido na ISSUE-167 (`status` sempre lidera, coluna de
ordenação por último para o Postgres não precisar de `Sort` explícito). Desempate por "mais
recentes" (CA 1.7) entra como segunda chave de ordenação, já dentro do índice:

```sql
-- Ranking por categoria (faixa de sugeridos, filtro de categoria ativo)
CREATE INDEX IX_products_status_category_clickcount
    ON products (status, category, click_count DESC, created_at DESC);

-- Ranking geral (fallback "mais clicados" — sem filtro de categoria)
CREATE INDEX IX_products_status_clickcount
    ON products (status, click_count DESC, created_at DESC);
```

Dois índices porque o predicado do fallback não fixa `category` (é `WHERE status = @status ORDER BY
click_count DESC`) — um índice composto `(status, category, click_count DESC)` não serve para
ordenar por `click_count` quando `category` não é filtrado (o Postgres não consegue pular a coluna
do meio e ainda usar o índice para ordenação). Mesmo raciocínio já documentado na
`especificacao-tecnica.md` da ISSUE-2/167 para as demais variantes de ordenação.

Múltiplas categorias filtradas ao mesmo tempo (ponto deixado em aberto no proposal.md, item 3 da
seção "Restrições"): `WHERE category = ANY(@categories)` — Postgres consegue usar o índice composto
acima com `ANY`/`IN` na segunda coluna (bitmap/index scan), aceitável dado o número baixo de
categorias e produtos no catálogo. A decisão de ranking é sobre a união dos produtos das categorias
ativas (não um ranking por categoria individual seguido de merge) — mais simples e suficiente,
já que o proposal.md deixou esse detalhe explicitamente a critério do refinamento técnico.

## 6. Decisão técnica 3 — Contrato do endpoint da faixa de sugeridos

**Fallback calculado no backend** (não no frontend). Motivo: a regra de fallback (CA 1.2) depende
do resultado da listagem principal com todos os filtros aplicados (categoria + preço, etc.), não só
da categoria — replicar essa lógica no frontend duplicaria regra de negócio em dois lugares e
arriscaria os dois desalinharem no futuro (ex.: mudar o mínimo de 4 exigiria trocar em dois
pontos). O frontend só informa **o que já sabe do seu próprio estado de filtro**; o backend decide
qual lista montar e devolve o resultado final (já pronto para renderizar, ou vazio se abaixo do
mínimo).

```
GET /api/public/products/suggested?categories={csv opcional}&hasResults={bool}
```

- `categories`: lista de categorias atualmente ativas no filtro da listagem principal (vazio/ausente
  se nenhum filtro de categoria estiver aplicado).
- `hasResults`: `true`/`false` — se a listagem principal, com **todos** os filtros atuais aplicados
  (categoria + preço + o que mais existir), retornou pelo menos 1 produto. É este campo, não
  `categories`, que decide o fallback (CA 1.2 fala em "filtro que não retorna nenhum produto",
  cenário que pode incluir faixa de preço combinada com categoria).

Lógica no backend:
```
se categories vazio OU hasResults == false:
    lista = fallback geral: WHERE status=Published ORDER BY click_count DESC, created_at DESC LIMIT 10
senão:
    lista = por categoria: WHERE status=Published AND category = ANY(categories)
            ORDER BY click_count DESC, created_at DESC LIMIT 10

se lista.Count < 4: retornar [] (frontend não renderiza a faixa — CA 1.5)
senão: retornar lista
```

Decisão explícita de um ponto não coberto literalmente pelos critérios de aceite: quando **não há
filtro de categoria ativo** (visitante navegando sem filtro, não é o mesmo caso de "filtro que
retorna 0 resultados"), trato como equivalente ao fallback geral — não existe uma categoria de
referência para especializar a sugestão. Registro aqui por ser uma lacuna do proposal.md
("comportamento exato... fica a critério do refinamento técnico").

Resposta: reaproveita o DTO já usado pela listagem pública (mesmo shape de card), sem novo
contrato de dados — menos superfície para o Dev manter.

CA 1.8 (endpoint indisponível não quebra a página): tratamento é do lado do frontend —
`SuggestedProductsCarousel` busca os dados em um `try/catch` isolado do restante da página; falha
ou erro apenas omite o carrossel, sem propagar para o grid principal.

## 7. Decisão técnica 4 — Endpoint/mecanismo de registro de clique

```
POST /api/public/products/{id}/click
```

- **Sem corpo de request** — o id do produto vai na URL, o endpoint não precisa parsear body. Isso
  é deliberado para casar com `navigator.sendBeacon(url)`, a API de browser desenhada
  especificamente para "disparar um sinal de tracking sem bloquear/atrasar uma navegação que já
  está acontecendo" (exatamente o requisito da CA 2.4 — não atrasar nem bloquear o destino do
  clique, que no caso de link de afiliado é navegação para fora do site). `sendBeacon` é
  fire-and-forget por design: o browser garante o envio mesmo que a página já esteja
  descarregando, sem o `await` que um `fetch` comum exigiria no meio do handler de clique.
  Fallback (browsers antigos sem `sendBeacon`): `fetch(url, { method: 'POST', keepalive: true })`,
  também sem esperar a resposta antes de deixar a navegação prosseguir.
- **Síncrono no backend, sem fila/Hangfire.** A escrita é trivial (Decisão 1: um insert + um update,
  ambos por chave indexada) — não há trabalho pesado a descarregar para um job. Introduzir
  Hangfire aqui adicionaria uma camada de indireção (enfileirar, processar, monitorar falha do job)
  sem necessidade real neste volume; o próprio corpo da requisição HTTP responde rápido o
  suficiente para não precisar de fila. Resposta `202 Accepted` (nem o frontend espera por ela, dado
  o `sendBeacon`).
- Sem autenticação (endpoint público, evento anônimo por definição). Sem deduplicação (CA explícito:
  não é requisito de negócio impedir múltiplas contagens do mesmo visitante). Rate limiting
  básico por IP, se necessário, fica a critério do LT/DevOps no nível de proxy reverso (Nginx) — não
  é modelagem de aplicação e não contradiz o requisito de anonimato (não guarda o IP, só limita
  taxa).

## 8. Fluxo de dados (resumo)

**Clique em card (grid ou carrossel):** usuário clica → handler do card dispara
`navigator.sendBeacon('/api/public/products/{id}/click')` (não aguarda) → navegação para o destino
atual do link prossegue imediatamente, inalterada → backend recebe o POST, insere em
`product_clicks`, incrementa `products.click_count` (mesma transação) → responde `202` (ignorado
pelo client).

**Carregamento da faixa de sugeridos:** página de listagem renderiza → `SuggestedProductsCarousel`
chama `GET /api/public/products/suggested?categories=...&hasResults=...` com o estado atual do
filtro → backend decide categoria vs. fallback (Decisão 3), consulta via os índices da Decisão 2,
aplica corte mínimo de 4 → frontend renderiza carrossel com setas (ou nada, se lista vazia/erro).

## 9. Investigação `discount_pct` (Amazon/Shopee) — CONCLUÍDA (sessão principal, 2026-08-21)

O Arquiteto não tinha acesso de leitura ao código-fonte (`backend/src/`) para executar esta
investigação (escopo restrito a `documentacoes/`, `openspec/`, `CLAUDE.md`) — deixou registrada a
query e o critério de decisão na seção 9 original deste documento (ver histórico do commit). A
sessão principal executou a investigação por **inspeção do código-fonte dos 3 collectors**
(o banco local só tem produtos coletados do Mercado Livre — sem dado empírico de Amazon/Shopee
para rodar a query SQL proposta pelo Arquiteto com resultado significativo; a leitura de código é
conclusiva de qualquer forma, pois mostra a origem do dado, não depende de amostra):

- `AmazonCollector.cs` (linhas ~253-274): calcula `discountPct` **real**, a partir do campo
  `SavingBasis` retornado pela Amazon PA-API — `(1 - salePrice/originalPrice) * 100`. Dado genuíno
  sempre que a Amazon fornece esse campo (não é hardcoded).
- `ShopeeCollector.cs` (linhas ~119-259): a query GraphQL já solicita o campo `discount` diretamente
  da API da Shopee (`productOfferV2 { ... discount ... }`), usado tal como recebido — também dado
  real, não calculado nem hardcoded no lado do coletor.
- `MercadoLivreCollector.cs` (linha ~339): **único** collector com `discountPct` hardcoded em `0` —
  limitação conhecida e já documentada da API pública do Mercado Livre (Issue #182/#192, que já
  isentou o Mercado Livre do critério de scoring por desconto por esse motivo).

**Decisão (critério do Arquiteto aplicado ao achado real):** a coluna `discount_pct` **NÃO deve ser
removida** do schema de `Product` — é dado real e em uso corrente para 2 das 3 plataformas
integradas (Amazon, Shopee). Só o Mercado Livre não popula o campo com dado genuíno, e essa
limitação já tem tratamento próprio (isenção de scoring, Issue #182/#192), não uma remoção de
coluna. **Não há ação de schema decorrente desta investigação** — item 4 da issue original fica
resolvido como "manter, sem mudança necessária". O refinamento técnico (`especificacao-tecnica.md`
desta issue) **não inclui** nenhuma tarefa de remoção/alteração de `discount_pct` no task breakdown.

## 10. Riscos e trade-offs

- **Escrita dupla por clique (insert + update) em vez de uma só operação**: aceito — ambas indexadas
  por PK, custo desprezível no volume esperado; troca simplicidade de leitura (Decisão 1) por uma
  escrita ligeiramente mais cara, decisão correta quando a página de leitura (listagem) é
  visitada ordens de magnitude mais vezes que um único clique é registrado.
- **`sendBeacon` sem confirmação de entrega para o usuário**: aceito por design — CA 2.4 pede
  exatamente isso (falha não deve ser percebida pelo usuário nem bloquear navegação).
- **Sem deduplicação de cliques repetidos**: aceito, requisito de negócio explícito (proposal.md,
  "Casos de uso de exceção — Item 2").
- **Índices novos em `products` (mais 2 índices)**: tabela pequena (100-300 linhas) — custo de
  manutenção de índice em escrita é irrelevante; benefício de leitura é o argumento central da
  Decisão 2.
- **Investigação de `discount_pct`**: concluída (seção 9) sem impacto de schema nesta issue.

## 11. Dependências para o Líder Técnico

- Confirmar nomes exatos de arquivo/classe (fora do escopo de leitura do Arquiteto) e mapear o
  breakdown de sub-issues (ex.: uma para backend — migration + endpoints —, outra para frontend —
  card + carrossel).
- ~~Executar a query da seção 9~~ — concluído pela sessão principal (seção 9 acima), sem ação de
  schema decorrente.
- Confirmar o DTO de produto já usado pela listagem pública para reaproveitar como resposta do
  endpoint de sugeridos (Decisão 3), evitando um novo contrato de dados.
- Definir posição exata do carrossel na página (acima/abaixo do grid) — decisão de UX/UI, não
  arquitetural.

## 12. Ajustes do refinamento técnico (LT) face ao código real

O Arquiteto não tinha acesso de leitura ao `backend/src/` nem ao `website/` — as decisões acima
seguem válidas na essência; os pontos abaixo são os ajustes de nomes/arquitetura concretos, feitos
pelo LT após ler o código real, e detalhados em
`documentacoes/ISSUE-231-faixa-de-produtos-sugeridos/especificacao-tecnica.md`:

1. **Novo controller `PublicProductsController`** (`api/public/products`), em vez de sobrecarregar
   `PublicController` (`api/public/deals`) com rotas absolutas (`~/`). Motivo: os 2 endpoints novos
   giram em torno do recurso "product" (por id), não "deal" (por slug/filtro) — controller próprio é
   mais direto que 2 `[HttpGet("~/api/public/products/...")]` dentro de `PublicController`.
2. **`DealCard.tsx` é hoje um Server Component** (sem `onClick`, `<a href>` puro) — a Arquitetura
   pressupôs implicitamente que bastaria "adicionar onClick". Decisão do LT: extrair a tag `<a>` do
   CTA para um novo Client Component pequeno (`DealCardLink` ou equivalente, só o `<a>` + handler),
   mantendo `DealCard` como Server Component — minimiza a superfície de `'use client'` (padrão já
   usado no projeto, ex. `PushSubscriptionManager.tsx` é o único Client Component hoje).
3. **`trackProductClick` é client-side** — segue o padrão já estabelecido em `lib/push.ts`
   (`'use client'`, usa `NEXT_PUBLIC_API_URL`, nunca `API_INTERNAL_URL` que é server-only). Não deve
   ir em `lib/api.ts` (server-only, documentado explicitamente no topo do arquivo) — vai em um novo
   arquivo `lib/tracking.ts` (`'use client'`), mesmo padrão de `lib/push.ts`.
4. **`SuggestedProductsCarousel` busca do lado do cliente** (Client Component, fetch em
   `useEffect`/lib próprio usando `NEXT_PUBLIC_API_URL`), não como parte do `Promise.all` do
   `app/page.tsx` (Server Component). Motivo: isola naturalmente a falha (try/catch só afeta o
   próprio componente, CA 1.8) sem precisar trocar `Promise.all` por `Promise.allSettled` na página
   inteira, e mantém o padrão já usado para funcionalidades client-driven do site (`push.ts`).
5. **`categories` (plural, CSV) na prática recebe no máximo 1 valor hoje** — `app/page.tsx` só
   suporta filtro de categoria única (`searchParams.category`, string), não múltipla seleção. O
   contrato do endpoint aceita CSV (múltiplos) para não travar uma eventual extensão futura da UI,
   mas o `SuggestedProductsCarousel` só precisa enviar 0 ou 1 categoria por enquanto.
