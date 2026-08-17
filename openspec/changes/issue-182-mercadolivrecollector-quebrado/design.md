# Design Técnico — ISSUE-182: MercadoLivreCollector quebrado — reconstruir com Highlights API

## 0. Nota de escopo/ferramentas (leia antes do LT)

Este design resolve as 4 ambiguidades técnicas listadas em `proposal.md` §"Ambiguidade arquitetural
avaliada pelo PM". O Arquiteto, por papel, **não tem acesso a `Bash` além de `gh` (leitura/comentário
de Issue)** e **não lê código-fonte do repo** (só `documentacoes/`, `openspec/` e `CLAUDE.md`) — não
subo a stack Docker nem faço chamadas HTTP ao vivo contra a API do Mercado Livre, mesmo o
`proposal.md`/spawn desta issue sugerindo isso. Três das quatro decisões abaixo (categoria→ID,
limite do multi-get, rate limit) dependem de um valor que só existe fazendo a chamada real —
por isso cada uma vem em duas partes: **(a) a decisão de arquitetura/estratégia**, que não muda
independente do valor exato descoberto, e **(b) o valor/lista concretos**, marcados
`[LT CONFIRMAR AO VIVO]`, com o comando exato a rodar. O LT (que tem Bash completo e acesso à stack
Docker local) roda essas confirmações como primeiro passo do refinamento técnico, antes de escrever
`especificacao-tecnica.md`/`tasks.md` — é trabalho de poucos minutos (3-4 chamadas HTTP), não um novo
ciclo de design. Isso é consistente com o padrão já usado na Issue #167 (design.md lá também deixou
valores de preço/câmbio como "confirmar no momento do deploy" — aqui o motivo é ferramental, lá era
temporal, mas o padrão de handoff é o mesmo).

## 1. Visão geral

O fluxo antigo (`GET /sites/MLB/search?sort=best_seller`, 1 chamada site-wide) é substituído por um
fluxo de 3 estágios por ciclo diário:

```
[8 categorias internas, mapeadas 1x em código para IDs MLB####]
        │
        ▼
GET /highlights/MLB/category/{category_id}   (1 chamada por categoria — 8/dia)
        │  → até 10 IDs de produto por categoria, ordenados por `position`
        ▼
GET /items?ids=id1,id2,...                    (1 chamada por lote — ver Decisão 2)
        │  → título, preço, imagem, permalink por ID resolvido
        ▼
new Product(...) / UpdateFromCollector(...)   (inalterado, já existe)
```

Nenhuma das 4 decisões abaixo introduz componente novo de infraestrutura (sem novo serviço, sem
nova tabela, sem novo job) — tudo vive dentro do `MercadoLivreCollector` já existente, reescrevendo
seu método `CollectAsync` (hoje inteiro em torno da chamada quebrada a `/sites/MLB/search`) e
adicionando um mapa estático de categorias.

## 2. Componentes afetados (mapa de mudança)

| Camada | Componente | Mudança |
|---|---|---|
| Infrastructure | `MercadoLivreCollector.CollectAsync` | Reescrito: itera 8 categorias → Highlights → multi-get em lotes → `new Product`/`UpdateFromCollector` (inalterado). Chamada quebrada a `/sites/MLB/search` removida. |
| Infrastructure | `MercadoLivreCollector` (novo membro estático) | `private static readonly Dictionary<string, string[]> CategoryMap` — categoria interna → IDs `MLB####` (Decisão 1). Não é uma classe nova nem um serviço injetado — só uma constante estática no próprio collector, mesmo padrão de `DefaultCategory`/dicionários estáticos já usado no projeto (ver `CategoryDetector`, Issue #167). |
| Infrastructure | `MercadoLivreCollector` (novo membro privado) | `ChunkIds(IEnumerable<string> ids, int batchSize)` — utilitário de batching do multi-get (Decisão 2), sem dependência externa nova (`Enumerable.Chunk` do .NET 6+ já resolve isso nativamente, ver Decisão 2.3). |
| Infrastructure | `IMercadoLivreApiClient` (ou equivalente já existente, usado por `MercadoLivreCollector` para chamar a API) | +2 métodos: `GetHighlightsAsync(string categoryId, ct)` e `GetItemsAsync(IEnumerable<string> ids, ct)`. Método antigo de busca (`SearchAsync`/equivalente, usado pelo endpoint quebrado) é removido ou marcado obsoleto — decisão mecânica do LT/Dev, sem impacto de design. |
| Application | `ProcessorJob` / `EnsureAffiliateLinkAsync` | **Não muda** (Restrições do PRD) — só ganha um roteiro de validação manual/documentada (Decisão 4), não uma alteração de código. |
| — | `AmazonCollector`/`ShopeeCollector` | Não tocados (fora de escopo, confirmado no PRD). |

Não há mudança de schema/migration (o PRD já descarta isso) nem mudança de contrato de API pública —
issue inteiramente de infraestrutura/integração interna, sem UI (a tabela de contrato de componentes
globais do processo do Arquiteto não se aplica aqui).

## 3. Decisão técnica 1 — Mapeamento das 8 categorias internas → ID(s) reais do Mercado Livre

### 3.1 Estratégia: mapa estático hardcoded no código, não busca dinâmica a cada ciclo

Três opções foram avaliadas (mesma pergunta que a ambiguidade #4 do PRD, "onde cachear a árvore de
categorias"):

- **Buscar `/sites/MLB/categories` a cada ciclo e resolver o mapeamento em runtime**: rejeitada. A
  taxonomia de categorias de topo do Mercado Livre é essencialmente estática (muda em uma escala de
  anos, não de dias) — pagar uma chamada de API extra por ciclo (todo dia, para sempre) para resolver
  algo que não muda é desperdício puro, e ainda introduz uma nova superfície de falha em runtime
  (se `/sites/MLB/categories` cair ou mudar formato, o ciclo inteiro de Mercado Livre trava antes
  mesmo de tentar Highlights, pior do que o cenário de isolamento por categoria já decidido no Gate 1).
- **Cachear a árvore em `app_settings` (padrão já usado no projeto, ver `claude.monthly_usage` na
  Issue #167) e atualizar sob demanda**: rejeitada por complexidade desproporcional ao problema — 8
  categorias, mapeamento estável, não justifica um mecanismo de cache com invalidação/refresh quando
  o mapa cabe em uma constante de código.
- **Mapa estático hardcoded no `MercadoLivreCollector`, obtido uma única vez (validação manual do
  LT, ver 3.2) e versionado como código**: escolhida. Mesmo padrão já validado no projeto para
  `CategoryDetector` (Issue #167, dicionário de keywords hardcoded) — a categoria muda por decisão
  humana revisada em PR, não em runtime. Se o Mercado Livre reestruturar sua árvore de categorias no
  futuro (raro, mas já aconteceu — é o mesmo tipo de mudança de política que quebrou o `/sites/MLB/search`
  original desta issue), o sintoma é Highlights retornando vazio/erro para um `category_id`
  descontinuado — cai automaticamente no isolamento de falha por categoria já decidido (Gate 1 regra 4,
  CA 5.1), sem quebrar as demais 7 categorias. Reduz para "1 categoria some, log de erro visível" em
  vez de "o mapeamento inteiro trava".

### 3.2 Como preencher os valores — passo a passo para o LT `[LT CONFIRMAR AO VIVO]`

```
GET https://api.mercadolibre.com/sites/MLB/categories
```
(sem autenticação — já confirmado funcionando na investigação da Issue). Retorna uma lista plana
`[{id: "MLB####", name: "..."}]` das ~30 categorias de topo do site brasileiro. Para as categorias
internas cuja correspondência não for óbvia por nome, aprofundar com:
```
GET https://api.mercadolibre.com/categories/{category_id}
```
que devolve `path_from_root`/`children_categories` (subcategorias) — usar para decidir se a
categoria interna deve mapear para 1 categoria de topo ou para uma combinação de subcategorias de
categorias de topo diferentes (caso N:1 previsto no PRD/CA 1.2).

### 3.3 Critério de decisão para os casos N:1 (categoria interna sem correspondência 1:1 óbvia)

Regra de desambiguação, para o LT aplicar de forma consistente (documentando a justificativa por
categoria, conforme exige CA 1.2 — não decidir silenciosamente):
1. Se existe uma categoria de topo do ML cujo nome é sinônimo direto da categoria interna → mapeamento
   1:1, usar o `id` dela.
2. Se a categoria interna é mais ampla que qualquer categoria de topo isolada do ML (ex.: "Casa e
   Cozinha" internamente pode cobrir o que o ML separa em categorias de topo distintas, como
   utensílios domésticos vs. móveis/decoração) → mapeamento N:1, `string[]` com os IDs de todas as
   categorias de topo do ML que compõem a categoria interna. O collector, ao coletar essa categoria
   interna, chama Highlights **uma vez por ID do array** (não muda o formato da chamada, só itera o
   array) e agrega os resultados antes de aplicar o corte de top 10 (ordenando o conjunto agregado
   por `position` e cortando em 10 — não 10 por sub-ID, para não estourar o volume combinado acordado
   no Gate 1).
3. Categoria interna sem nenhuma correspondência razoável no ML → não deveria ocorrer (as 8 categorias
   foram escolhidas por já serem as mapeáveis da Issue #167 — "Geral" ficou de fora justamente por não
   ter correspondência); se acontecer na prática, tratar como achado a reportar na Issue antes de
   inventar um mapeamento forçado.

### 3.4 Tabela de mapeamento — placeholder estrutural (valores reais a preencher pelo LT)

```csharp
// MercadoLivreCollector.cs
private static readonly Dictionary<string, string[]> CategoryMap = new()
{
    ["Eletrodomésticos"] = new[] { "MLB####" }, // [LT CONFIRMAR AO VIVO]
    ["Climatização"]     = new[] { "MLB####" }, // [LT CONFIRMAR AO VIVO] — provável subcategoria de Eletrodomésticos no ML, não categoria de topo própria; verificar via /categories/{id do pai}
    ["Ferramentas"]      = new[] { "MLB####" }, // [LT CONFIRMAR AO VIVO]
    ["Eletrônicos"]      = new[] { "MLB####" }, // [LT CONFIRMAR AO VIVO]
    ["Casa e Cozinha"]   = new[] { "MLB####" }, // [LT CONFIRMAR AO VIVO] — candidato a N:1, ver 3.3
    ["Beleza"]           = new[] { "MLB####" }, // [LT CONFIRMAR AO VIVO]
    ["Moda"]             = new[] { "MLB####" }, // [LT CONFIRMAR AO VIVO] — candidato a N:1 (roupas + calçados + acessórios podem ser categorias de topo separadas no ML)
    ["Brinquedos"]       = new[] { "MLB####" }, // [LT CONFIRMAR AO VIVO]
};
```

**Importante — por que não deixo IDs "prováveis" aqui**: eu (Arquiteto) tenho conhecimento geral de
que o Mercado Livre historicamente organiza categorias como "Eletrodomésticos", "Ferramentas",
"Beleza e Cuidado Pessoal" etc. como categorias de topo, mas **não tenho evidência ao vivo desta
sessão** de que os IDs específicos ainda são válidos hoje (a própria issue documenta que a API do ML
mudou política em 2026 — nada garante que a árvore de categorias não também mudou desde o último
conhecimento consolidado). Colocar um ID "chutado" aqui seria exatamente o tipo de suposição não
verificada que este design deve evitar (o pedido explícito da tarefa é "evidências reais, não
suposições") — por isso o placeholder `MLB####` + o passo a passo de 3.2/3.3, não um valor
adivinhado. Dois casos (Climatização, Moda) já vêm sinalizados como candidatos prováveis a
tratamento especial (subcategoria vs. N:1) para o LT não perder tempo redescobrindo isso do zero.

### 3.5 Critério de aceite mapeado
Satisfaz CA 1.1 (todas as 8 com ao menos 1 ID, documentado em código) e CA 1.2 (decisão N:1
documentada com justificativa — seção 3.3 é a justificativa a citar/expandir por categoria quando o
LT preencher os valores reais).

## 4. Decisão técnica 2 — Limite/batching do multi-get (`GET /items?ids=...`)

### 4.1 Estratégia: alinhar o tamanho do lote à fronteira de categoria, não ao limite técnico bruto

Duas estratégias de batching foram avaliadas para os até 80 IDs/ciclo (8 categorias × top 10):

- **Empacotar por limite técnico máximo** (ex.: se o limite real for 20, agrupar todos os 80 IDs em
  lotes de 20 cruzando categorias livremente): minimiza o número de chamadas HTTP (4 em vez de 8),
  mas cada lote passa a conter IDs de categorias diferentes — se um lote falhar (Cenário 5.2 dos
  critérios de aceite: "falha em um lote de multi-get não aborta o ciclo"), a falha agora corta
  pedaços de produtos de 2+ categorias de forma não previsível, exigindo lógica extra para
  reconciliar "quais produtos de quais categorias vieram/faltaram" após o lote.
- **Alinhar o lote à fronteira de categoria (1 lote = 1 categoria = até 10 IDs)**: escolhida. Cada
  chamada de multi-get resolve exatamente os IDs de uma única categoria (nunca mistura). Isso:
  1. Mantém o isolamento de falha simples e correto por construção — se o multi-get de uma categoria
     falhar, só aquela categoria perde produtos neste ciclo (Cenário 5.2), sem precisar rastrear "qual
     subconjunto de qual categoria" dentro de um lote misto.
  2. É seguro **independente do limite real confirmado pelo LT**, desde que esse limite seja ≥ 10 (ver
     4.2) — top 10 por categoria (regra de negócio fechada no Gate 1) já é o teto superior de IDs por
     chamada nesta estratégia.
  3. Custa mais chamadas HTTP no caso N:1 do limite ser bem maior que 10 (ex.: se o limite real for 20,
     esta estratégia faz 8 chamadas de multi-get/dia em vez de 4) — aceito: a diferença é irrelevante
     de custo/tempo para um cron de 1x/dia (ver Decisão 3, volume total de chamadas é baixíssimo de
     qualquer forma) e a simplicidade de isolamento de falha vale mais do que economizar 4 chamadas
     HTTP por dia.

### 4.2 O que fazer se o limite real confirmado for **menor que 10** — `[LT CONFIRMAR AO VIVO]`

O PRD já registra que o multi-get nunca foi testado ao vivo pela investigação da issue. Conhecimento
público consolidado sobre a API do Mercado Livre (não verificado nesta sessão, mesma ressalva da
seção 3.4) sugere um limite histórico de até 20 IDs por chamada em `/items?ids=` — mas dado que o
endpoint antigo desta mesma issue quebrou justamente por uma mudança de política não documentada
antecipadamente, **não assumo esse número como fato**. Procedimento de confirmação, para o LT rodar
como primeiro passo do refinamento (reaproveita os mesmos IDs já obtidos ao validar a Decisão 1):

```
1. Pegar ~15-20 IDs reais de produto de uma única categoria populosa via Highlights
   (GET /highlights/MLB/category/{um id já mapeado, ex. Eletrônicos}).
2. Chamar GET /items?ids=id1,id2,...,id20 (todos de uma vez) e observar:
   - 200 com 20 objetos no array de resposta → limite ≥ 20, confirma que batch=10 (nossa estratégia) está seguro.
   - 400/erro mencionando limite de IDs → reduzir a quantidade (tentar 15, depois 10) até achar o teto exato.
3. Documentar o resultado em especificacao-tecnica.md (valor exato + payload de erro, se houver).
```

Se o limite real confirmado for **< 10** (cenário não esperado, mas coberto): a estratégia de "1 lote
= 1 categoria" deixa de ser suficiente sozinha — o LT subdivide cada categoria em sub-lotes de tamanho
igual ao limite real (ex.: limite 8 → 2 sub-lotes de 8+2 para uma categoria com 10 resultados),
mantendo a mesma regra de isolamento de falha por sub-lote (Cenário 5.2 já cobre "lote", não exige que
lote == categoria inteira). O código (`ChunkIds`, seção 2) já é escrito parametrizado por
`batchSize` justamente para não exigir reescrita se este cenário se confirmar — só mudar a constante.

### 4.3 Implementação do chunking
.NET 6+ já tem `Enumerable.Chunk(int size)` nativo (o projeto está em .NET 8 — `CLAUDE.md`), sem
dependência nova:
```csharp
foreach (var batch in categoryProductIds.Chunk(BatchSize)) // BatchSize = 10, constante nomeada
{
    var items = await _apiClient.GetItemsAsync(batch, ct);
    // ... mapear para Product, log+continue em caso de exceção do batch (Cenário 5.2)
}
```

### 4.4 Critério de aceite mapeado
CA 3.1 (resolução em um ou mais lotes), CA 3.2 (lotes respeitam o limite real — por construção, dado
que o tamanho de lote nunca excede 10 e 10 é o próprio teto de negócio, não deveria ser rejeitado por
excesso de IDs salvo o cenário 4.2), CA 3.3 (item não resolvido é ignorado — comportamento do
mapeamento pós-resposta, não muda com a estratégia de lote), CA 5.2 (isolamento de falha por lote,
try/catch por chamada de `GetItemsAsync`, mesmo padrão dos demais collectors).

## 5. Decisão técnica 3 — Rate limit / throttling dentro do ciclo diário

### 5.1 Volume real de chamadas por ciclo é baixo — não justifica limitador dedicado

Com as Decisões 1 e 2 fechadas, o volume máximo de chamadas externas por ciclo diário é:
- 8 chamadas de Highlights (uma por categoria; casos N:1 da seção 3.3 somam mais 1 chamada por ID
  extra do array, ainda assim um número pequeno de dígitos, não dezenas) +
- até 8 chamadas de multi-get (uma por categoria, seção 4.1) +
- (uso pontual, não recorrente) `/sites/MLB/categories`, chamado **zero vezes em produção** — só uma
  vez manualmente pelo LT na validação da Decisão 1 (seção 3.1), nunca pelo `CollectorJob`.

Total: ~16-20 chamadas HTTP, **uma vez por dia**. Isso é ordens de grandeza abaixo de qualquer limite
público conhecido de APIs REST de e-commerce (tipicamente centenas a milhares de requisições por
hora/minuto) — não há cenário plausível de um cron 1x/dia com ~20 chamadas estourar rate limit,
mesmo sem qualquer throttling. Construir um limitador de taxa (token bucket, semáforo, backoff
exponencial dedicado) para este volume seria engenharia desproporcional ao risco real — mesmo
raciocínio de "não vale complexidade" já usado no design da Issue #167 (seção de concorrência do
orçamento Claude).

### 5.2 Decisão: sem limitador dedicado; delay defensivo simples + reaproveitar o isolamento de falha já decidido

- Um `await Task.Delay(300)` (ou similar, valor exato não crítico) entre chamadas HTTP consecutivas
  ao domínio `api.mercadolibre.com` dentro do loop de categorias — não é uma resposta a um limite
  medido (não há evidência de que seja necessário no volume calculado acima), é uma prática defensiva
  barata contra heurísticas anti-burst que algumas APIs aplicam independente de limite documentado
  (custo: no máximo ~16 × 300 ms ≈ 5s adicionais no job, irrelevante para um cron diário).
- Se qualquer chamada (Highlights ou multi-get) retornar HTTP 429: tratar exatamente como qualquer
  outra falha de categoria/lote já decidida (Gate 1 regra 4, CA 5.1/5.2) — log + pular, sem retry
  especial. Não é necessária uma política de retry-com-backoff dedicada para 429 especificamente: o
  negócio já aceitou que uma categoria/lote que falhar por qualquer motivo (incluindo rate limit) é
  simplesmente pulado naquele ciclo, e roda de novo no dia seguinte — 429 não é uma classe de erro
  que precisa de tratamento diferenciado dado esse contrato já fechado.

### 5.3 Confirmação ao vivo (baixa prioridade, não bloqueante) `[LT CONFIRMAR AO VIVO — opcional]`
Durante as chamadas já necessárias para confirmar as Decisões 1 e 2, o LT pode inspecionar os headers
de resposta (`curl -i` ou equivalente) por `X-RateLimit-*`/`Retry-After` e registrar o que encontrar
em `especificacao-tecnica.md`, só para documentação — não é um pré-requisito para implementar, dado
que a decisão de arquitetura (5.1/5.2) já é válida independente do que esses headers disserem, por
causa do volume baixo.

### 5.4 Critério de aceite mapeado
Não há cenário Given/When/Then dedicado a rate limit nos critérios de aceite (a ambiguidade do PRD
pedia avaliação, não um comportamento testável específico) — a decisão acima é a resposta à
ambiguidade #3 do PRD.

## 6. Decisão técnica 4 — Validação end-to-end do link de afiliado (desenho do teste, requisito crítico)

Esta é uma validação **manual/ao vivo**, não uma linha de código nova (`EnsureAffiliateLinkAsync`
não muda, por restrição do PRD) — o que este design entrega é o roteiro objetivo e reproduzível que
o Dev/QA executa depois que o novo `MercadoLivreCollector` estiver rodando, satisfazendo CA 7.1-7.3.

### 6.1 Roteiro de validação
1. Rodar o novo `MercadoLivreCollector.CollectAsync` uma vez em ambiente local (com credenciais reais
   já configuradas) até pelo menos um produto de Mercado Livre chegar a `Status == Queued`
   (passar pelo `ScoreProductAsync`, mesmo pipeline de sempre, sem lógica nova para ML).
2. Anotar, para esse produto, o valor de `SourceUrl` (o `permalink` original vindo do multi-get,
   seção 4) **antes** do `ProcessorJob` rodar — é o baseline de comparação.
3. Rodar o `ProcessorJob` (job existente, sem mudança) até `EnsureAffiliateLinkAsync` gerar o
   `AffiliateLink` do mesmo produto.
4. Aplicar o checklist objetivo abaixo sobre o `AffiliateLink` resultante — **todos** os itens devem
   passar para o critério ser considerado satisfeito (CA 7.2 é explícito: HTTP 200 sozinho não basta):

| # | Verificação | Como checar | Resultado esperado |
|---|---|---|---|
| 1 | `AffiliateLink` é diferente de `SourceUrl`/`permalink` | comparação de string direta | `AffiliateLink != SourceUrl` |
| 2 | Domínio reconhecível do Mercado Livre ou do seu mecanismo de afiliados | inspecionar o host da URL | host contém `mercadolivre.com`/`mercadolibre.com` (formatos conhecidos do programa de afiliados do ML incluem links curtos `.../sec/{code}` e links longos com parâmetros `matt_word`/`matt_tool` — o formato exato só se confirma inspecionando a resposta real de `affiliate-tools/links`, já que este design não altera nem teve acesso de leitura a esse endpoint) |
| 3 | Presença de identificador de conta/tag, não só um link "genérico" | inspecionar path/querystring do link (ex. parâmetro tipo `matt_word=`, `matt_tool=`, ou um código opaco de `/sec/`) | ao menos um identificador presente — se o link vier "limpo" sem nenhum parâmetro/código de afiliado reconhecível, é reprovação (CA 7.2) |
| 4 | O identificador é estável/da conta do Gerente, não aleatório por chamada | gerar `AffiliateLink` para **dois produtos diferentes** no mesmo teste e comparar o identificador do item 3 entre os dois | mesmo identificador de conta nos dois links (valor da tag idêntico); só o código do produto/path muda |
| 5 (opcional, reforço) | O link de afiliado de fato redireciona para o produto correto | seguir o redirect do `AffiliateLink` (ex. `curl -IL`) | resolve (eventualmente) para uma URL cujo produto corresponde ao `permalink` original do item 2 |

5. Resultado (todos os 5 itens, valores mascarando o identificador de conta se for sensível para
   registro em arquivo versionado) documentado em um novo arquivo,
   `{docs_path}/validacao-link-afiliado.md`, produzido por quem executa o teste (Dev/QA) — não faz
   parte deste `design.md` porque depende de dados que só existem depois que o coletor novo já está
   implementado e rodando (ordem de dependência: Dev implementa → só então este roteiro é executável).
6. Se qualquer item do checklist falhar → não é um bug desta issue a corrigir silenciosamente (PRD,
   Restrições + CA 7.3): documentar o achado (novo arquivo em `.claude/melhorias/` ou nova Issue) e
   reportar antes de qualquer alteração em `EnsureAffiliateLinkAsync`.

### 6.2 Por que não executei este teste eu mesmo agora
A tarefa pediu para testar "se possível, ao vivo, agora mesmo" — não é possível dentro do escopo de
ferramentas do Arquiteto (sem Bash além de `gh`, sem acesso a código-fonte/stack Docker) **e** o
teste real só é executável depois que o `MercadoLivreCollector` novo existir e coletar ao menos um
produto de Mercado Livre (pré-requisito de dados que este design, por definição, ainda não criou) —
mesmo com acesso irrestrito a ferramentas, a Decisão 4 não poderia ser validada com dados reais nesta
etapa do pipeline. O roteiro acima é o entregável correto desta etapa (design do teste, não sua
execução) — execução cabe ao Dev, na sub-issue de implementação, e ao QA, na validação final.

### 6.3 Critério de aceite mapeado
CA 7.1 (link diferente do permalink + tag reconhecível — itens 1-3 do checklist), CA 7.2 (não se
satisfaz com 200 — o checklist inteiro é sobre conteúdo, não status HTTP), CA 7.3 (achado de defeito
tratado como problema separado, não corrigido silenciosamente).

## 7. Dependências

- Nenhuma dependência nova de pacote — `.NET 8`/`Enumerable.Chunk` já nativo (seção 4.3).
- Nenhuma mudança de schema/migration (confirmado no PRD, este design não introduz nenhuma).
- Depende de 3 confirmações ao vivo, todas de baixo custo (poucas chamadas HTTP), a cargo do LT no
  início do refinamento técnico: valores reais do `CategoryMap` (seção 3.4), limite real do multi-get
  (seção 4.2) e, opcionalmente, headers de rate limit (seção 5.3) — nenhuma delas exige uma nova
  rodada de design se o valor confirmado divergir da expectativa (batch size e delay são constantes
  nomeadas, parametrizadas, não hardcoded em múltiplos lugares).
- Depende de `EnsureAffiliateLinkAsync`/`affiliate-tools/links` continuarem funcionando como hoje
  (fora de escopo desta issue, só consumidos — seção 6).

## 8. Riscos

- **Mapeamento de categorias (seção 3) desatualiza silenciosamente**: se o Mercado Livre reestruturar
  sua árvore de categorias no futuro, os IDs hardcoded podem passar a apontar para categorias
  descontinuadas/renomeadas. Mitigação: o próprio padrão de isolamento de falha por categoria (Gate 1
  regra 4) já limita o dano a "essa categoria específica some da coleta, log de erro visível" — não é
  uma falha silenciosa, aparece nos logs de erro do `CollectorJob` e pode ser corrigida atualizando
  a constante, sem exigir nova migration/deploy de schema.
- **Limite do multi-get confirmado menor que 10** (seção 4.2, cenário não esperado mas coberto): exige
  subdividir lotes dentro de uma mesma categoria — código já preparado (`ChunkIds` parametrizado), só
  risco de esquecimento se o LT não rodar a confirmação antes de implementar; por isso a seção 4.2 é
  marcada como passo obrigatório do início do refinamento, não algo a descobrir só em produção.
- **Formato real do link de afiliado (`affiliate-tools/links`) diferente do assumido na seção 6.1**:
  o design não teve acesso de leitura ao código de `EnsureAffiliateLinkAsync`, então o checklist da
  seção 6 é genérico (baseado em formatos publicamente conhecidos de programas de afiliados do
  Mercado Livre) — se a resposta real tiver um formato distinto, o checklist ainda se aplica
  conceitualmente (itens 1-4 não dependem de um formato específico), só a coluna "Como checar" pode
  precisar de ajuste fino pelo Dev/QA ao executar.
- **Categorias N:1 (Casa e Cozinha, Moda — seção 3.3) aumentam levemente o número de chamadas de
  Highlights** (mais de 1 ID por categoria interna) — sem impacto de rate limit dado o volume total
  calculado na seção 5.1, mas aumenta a complexidade de agregação (cortar top 10 do conjunto
  combinado, não por sub-ID) — documentado explicitamente para o LT não simplificar isso incorretamente.

## 9. Fora de escopo deste design (para o LT/Dev)

- Valores reais do `CategoryMap` (seção 3.4) — `[LT CONFIRMAR AO VIVO]`, passo a passo na seção 3.2.
- Valor real do limite do multi-get e eventual ajuste de `BatchSize` (seção 4.2) — `[LT CONFIRMAR AO VIVO]`.
- Headers de rate limit, se existirem (seção 5.3) — opcional, não bloqueante.
- Execução do roteiro de validação do link de afiliado (seção 6.1) — depende do coletor novo já
  existir; cabe ao Dev (primeira execução, ao implementar) e ao QA (confirmação final).
- Nome exato dos métodos novos no client HTTP de Mercado Livre (`GetHighlightsAsync`/`GetItemsAsync`
  na seção 2) — refinamento de nomenclatura do LT, não decisão de arquitetura.
