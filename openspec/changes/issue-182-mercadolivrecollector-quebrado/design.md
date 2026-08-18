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

> **ATUALIZAÇÃO — LT confirmou ao vivo em 2026-08-17 e encontrou um BLOQUEIO CRÍTICO não previsto
> por este design original.** As confirmações das Decisões 1 e 3 fecharam normalmente (valores
> reais preenchidos abaixo). A confirmação da Decisão 2, porém, revelou que a estratégia inteira de
> resolução de detalhes (`GET /items?ids=...`) **não funciona com as credenciais atualmente
> configuradas** — não é uma questão de limite de lote, é bloqueio de acesso (HTTP 403). O mesmo
> vale para a Decisão 4: o endpoint `affiliate-tools/links`, já implementado em
> `ProcessorJob.EnsureAffiliateLinkAsync` e assumido como "existente e funcionando", responde HTTP
> 404 ("resource not found") para qualquer chamada, com qualquer payload. Ver **Seção 10** (nova) —
> este achado é bloqueante e foi escalado ao Gerente (Gate 1.5) antes de qualquer sub-issue ser
> criada. Não editei as seções 1-9 do racional original do Arquiteto (a estratégia de arquitetura
> permanece correta em abstrato — a limitação é de acesso da API, não de desenho); só preenchi os
> placeholders `[LT CONFIRMAR AO VIVO]` com os valores/achados reais.

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

**Ver Seção 10 — o passo `GET /items?ids=...` acima está bloqueado (HTTP 403) com as credenciais
atuais; o fluxo real, tal como confirmado ao vivo, tem um estágio intermediário adicional
(`/products/{id}` → `/products/{id}/items`) que resolve parcialmente os dados, mas não resolve o
`permalink`/`SourceUrl`.**

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

### 3.2 Como os valores foram confirmados — `GET /sites/MLB/categories`, executado ao vivo pelo LT

```
GET https://api.mercadolibre.com/sites/MLB/categories
Authorization: Bearer <mercadolivre.access_token de app_settings>
→ HTTP 200
```

Retornou a lista real das **32 categorias de topo do site brasileiro** (sem necessidade de
autenticação, mas testado com o token real). Para as categorias sem correspondência 1:1 óbvia por
nome (Climatização, Casa e Cozinha), aprofundei com:

```
GET https://api.mercadolibre.com/categories/{category_id}
→ HTTP 200, corpo com children_categories (subcategorias)
```

### 3.3 Critério de decisão aplicado para os casos N:1 previstos (nenhum se confirmou necessário)

O design original sinalizava "Climatização" e "Moda" como candidatos prováveis a tratamento especial
(subcategoria isolada / N:1). A inspeção real da árvore mudou a conclusão para os dois casos:

1. **Climatização**: **não é** categoria de topo do Mercado Livre — é a subcategoria
   `MLB252358` ("Ar e Ventilação"), filha de `MLB5726` ("Eletrodomésticos"), confirmado consultando
   `GET /categories/MLB5726`. **Isso é o cenário já previsto na seção 3.4 original do design**
   ("provável subcategoria de Eletrodomésticos, verificar via /categories/{id do pai}") — confirmado
   como verdadeiro. Testei `GET /highlights/MLB/category/MLB252358` diretamente: **a Highlights API
   aceita normalmente um ID de subcategoria, não só categoria de topo** (retornou 200 com 9 produtos
   ranqueados) — não exige nenhum tratamento especial no código, o mapeamento aponta direto para o
   ID da subcategoria como se fosse qualquer outra entrada do dicionário. Mapeamento: 1:1 (não N:1),
   usando o ID da subcategoria em vez do topo.
2. **Casa e Cozinha**: candidato a N:1 no design original ("pode cobrir o que o ML separa em
   categorias de topo distintas"). Na prática, o Mercado Livre tem uma única categoria de topo
   `MLB1574` ("Casa, Móveis e Decoração") cuja subcategoria `MLB1618` ("Cozinha") já cobre
   utensílios/eletros de cozinha — não há uma categoria de topo separada e concorrente para
   "cozinha" fora dessa árvore. Mapeamento: 1:1, usando o ID de topo `MLB1574` (cobre a categoria
   inteira, incluindo a subárvore de Cozinha — não é necessário apontar para a subcategoria
   especificamente, dado que o corte de top 10 já é sobre o conjunto ranqueado da categoria de topo).
3. **Moda**: candidato a N:1 no design original ("roupas + calçados + acessórios podem ser
   categorias de topo separadas"). Na prática, o Mercado Livre já agrega os três num único item de
   topo: `MLB1430` ("Calçados, Roupas e Bolsas"). Mapeamento: 1:1.
4. **Demais 5 categorias** (Eletrodomésticos, Ferramentas, Eletrônicos, Beleza, Brinquedos): todas
   têm correspondência de nome direta e inequívoca com uma categoria de topo do ML (regra 1 da seção
   3.3 original) — mapeamento 1:1 trivial.

**Conclusão: nenhuma das 8 categorias precisou de agregação N:1 (array com mais de 1 ID).** Todas
mapeiam para exatamente 1 ID real do Mercado Livre (de topo ou subcategoria, conforme o caso). A
regra de desambiguação N:1 da seção 3.3 original do Arquiteto continua válida como processo — só não
foi necessária neste conjunto específico de 8 categorias, com a árvore real de hoje.

### 3.4 Tabela de mapeamento — valores reais confirmados (2026-08-17)

```csharp
// MercadoLivreCollector.cs
private static readonly Dictionary<string, string[]> CategoryMap = new()
{
    ["Eletrodomésticos"] = new[] { "MLB5726" },   // "Eletrodomésticos" — categoria de topo, 1:1
    ["Climatização"]     = new[] { "MLB252358" }, // "Ar e Ventilação" — subcategoria de Eletrodomésticos (MLB5726); Highlights aceita ID de subcategoria normalmente
    ["Ferramentas"]      = new[] { "MLB263532" }, // "Ferramentas" — categoria de topo, 1:1
    ["Eletrônicos"]      = new[] { "MLB1000" },   // "Eletrônicos, Áudio e Vídeo" — categoria de topo, 1:1
    ["Casa e Cozinha"]   = new[] { "MLB1574" },   // "Casa, Móveis e Decoração" — categoria de topo (cobre subárvore "Cozinha", MLB1618); sem N:1 necessário
    ["Beleza"]           = new[] { "MLB1246" },   // "Beleza e Cuidado Pessoal" — categoria de topo, 1:1
    ["Moda"]             = new[] { "MLB1430" },   // "Calçados, Roupas e Bolsas" — já agrega os 3, categoria de topo, 1:1
    ["Brinquedos"]       = new[] { "MLB1132" },   // "Brinquedos e Hobbies" — categoria de topo, 1:1
};
```

### 3.5 Critério de aceite mapeado
Satisfaz CA 1.1 (todas as 8 com ao menos 1 ID, documentado em código, confirmado via
`GET /sites/MLB/categories` real) e CA 1.2 (decisão N:1 documentada com justificativa — seção 3.3
documenta, para cada uma das 8, por que o mapeamento final é 1:1 e não N:1, incluindo os 2 casos que
o design original sinalizava como prováveis candidatos a tratamento especial).

## 4. Decisão técnica 2 — Limite/batching do multi-get (`GET /items?ids=...`)

### 4.1 Estratégia: alinhar o tamanho do lote à fronteira de categoria, não ao limite técnico bruto

(Racional original do Arquiteto, mantido — ver seção 10 para o achado que invalida a premissa de que
este endpoint é alcançável com as credenciais atuais.)

Duas estratégias de batching foram avaliadas para os até 80 IDs/ciclo (8 categorias × top 10):

- **Empacotar por limite técnico máximo** (ex.: se o limite real for 20, agrupar todos os 80 IDs em
  lotes de 20 cruzando categorias livremente): minimiza o número de chamadas HTTP (4 em vez de 8),
  mas cada lote passa a conter IDs de categorias diferentes — se um lote falhar (Cenário 5.2 dos
  critérios de aceite: "falha em um lote de multi-get não aborta o ciclo"), a falha agora corta
  pedaços de produtos de 2+ categorias de forma não previsível, exigindo lógica extra para
  reconciliar "quais produtos de quais categorias vieram/faltaram" após o lote.
- **Alinhar o lote à fronteira de categoria (1 lote = 1 categoria = até 10 IDs)**: escolhida. Cada
  chamada de multi-get resolve exatamente os IDs de uma única categoria (nunca mistura). Isso:
  1. Mantém o isolamento de falha simples e correto por construção.
  2. É seguro **independente do limite real confirmado pelo LT**, desde que esse limite seja ≥ 10.
  3. Custa mais chamadas HTTP no caso do limite real ser bem maior que 10 — aceito, irrelevante para
     um cron de 1x/dia.

### 4.2 Valor real confirmado pelo LT — `GET /items?ids=...` aceita ≥ 18 IDs num único lote

Procedimento executado: peguei os 18 IDs retornados pelo Highlights de "Eletrodomésticos" (`MLB5726`)
e chamei `GET /items?ids=id1,...,id18` (todos de uma vez, 18 > 10, acima do batch planejado de 10):

```
HTTP 200 — envelope aceito com os 18 IDs em uma única chamada, sem nenhum erro de "excesso de IDs".
```

**O envelope de lote (quantidade de IDs por chamada) funciona normalmente e aceita bem mais que 10 —
confirma que `BatchSize = 10` (a estratégia de 1 lote = 1 categoria) é segura do ponto de vista de
limite técnico bruto.** Isso valida a Decisão 2 na sua forma original.

**Porém — achado não previsto pelo design original**: o *conteúdo* de cada item dentro desse
envelope de 200 retornou bloqueado (não é erro de limite, é erro de acesso). Ver Seção 10 para o
detalhe completo — a decisão de batching em si (seção 4.1, `BatchSize = 10`) permanece correta;
o que está bloqueado é a leitura do corpo de cada item individual, independente de como os IDs são
agrupados.

### 4.3 Implementação do chunking
.NET 8 já tem `Enumerable.Chunk(int size)` nativo, sem dependência nova:
```csharp
foreach (var batch in categoryProductIds.Chunk(BatchSize)) // BatchSize = 10, constante nomeada
{
    var items = await _apiClient.GetItemsAsync(batch, ct);
    // ... mapear para Product, log+continue em caso de exceção do batch (Cenário 5.2)
}
```

### 4.4 Critério de aceite mapeado
CA 3.2 (lotes respeitam o limite real — confirmado, `BatchSize = 10` é seguro, testado até 18 num
único envelope) permanece satisfeito na parte de *batching*. CA 3.1 e CA 3.3 (resolução efetiva de
cada ID em título/preço/imagem/permalink) **ficam bloqueadas** pelo achado da Seção 10 — não é mais
possível confirmá-las com as credenciais atuais, independente da estratégia de lote.

## 5. Decisão técnica 3 — Rate limit / throttling dentro do ciclo diário

### 5.1 Volume real de chamadas por ciclo é baixo — não justifica limitador dedicado

Com as Decisões 1 e 2 fechadas, o volume máximo de chamadas externas por ciclo diário é:
- 8 chamadas de Highlights (uma por categoria — sem casos N:1, confirmado na seção 3.4) +
- até 8 chamadas de multi-get (uma por categoria, seção 4.1) +
- (uso pontual, não recorrente) `/sites/MLB/categories`, chamado **zero vezes em produção** — só uma
  vez manualmente pelo LT na validação da Decisão 1, nunca pelo `CollectorJob`.

Total: ~16 chamadas HTTP, **uma vez por dia** (menor até que a estimativa original de 16-20, já que
nenhuma categoria precisou de N:1). Ordens de grandeza abaixo de qualquer limite público conhecido de
APIs REST de e-commerce — não há cenário plausível de estourar rate limit, mesmo sem throttling.

### 5.2 Decisão: sem limitador dedicado; delay defensivo simples + reaproveitar o isolamento de falha já decidido

- Um `await Task.Delay(300)` entre chamadas HTTP consecutivas ao domínio `api.mercadolibre.com`
  dentro do loop de categorias — prática defensiva barata, não resposta a um limite medido.
- Se qualquer chamada retornar HTTP 429: tratar como qualquer outra falha de categoria/lote já
  decidida (Gate 1 regra 4, CA 5.1/5.2) — log + pular, sem retry especial.

### 5.3 Confirmação ao vivo dos headers de rate limit — `[LT CONFIRMAR AO VIVO]` RESOLVIDO

Inspecionei os headers completos (`curl -D -`) de `GET /sites/MLB/categories`,
`GET /highlights/MLB/category/{id}` (2 categorias testadas) e `GET /items?ids=...`:

**Nenhum header `X-RateLimit-*`, `Retry-After` ou equivalente foi encontrado em nenhuma das
respostas** (headers presentes: `content-type`, `x-request-id`, `x-api-server-segment`,
`strict-transport-security`, `x-frame-options`, `x-xss-protection`, `access-control-*`, headers de
CloudFront). A API do Mercado Livre não expõe rate limit via header nestas rotas — confirma que não
há como implementar throttling adaptativo baseado em header mesmo se quiséssemos; a decisão 5.2
(delay fixo + tratar 429 como falha comum) é a única abordagem viável, não apenas a mais simples.

Achado adicional relevante: `GET /applications/{client_id}` (consultado durante a investigação da
Seção 10) expõe `"max_requests_per_hour":18000` para a aplicação registrada — não é um rate limit por
endpoint, é o teto de cota da aplicação como um todo. Nosso volume (~16/dia) está muitas ordens de
grandeza abaixo disso.

### 5.4 Critério de aceite mapeado
Não há cenário Given/When/Then dedicado a rate limit — decisão acima confirmada com evidência real
(headers inspecionados, nenhum limite documentado neles).

## 6. Decisão técnica 4 — Validação end-to-end do link de afiliado (desenho do teste, requisito crítico)

**Esta decisão não pôde ser validada — ver Seção 10.** O roteiro abaixo é o desenho original do
Arquiteto, mantido como referência para quando o bloqueio da Seção 10 for resolvido; a execução
real (passo 6.1) não foi possível porque `affiliate-tools/links` retorna HTTP 404 (endpoint
inalcançável) para as credenciais atuais, independente do produto/URL enviado — não é uma questão de
não haver um produto de teste disponível (não havia nenhum produto de Mercado Livre com
`Status == Queued` na base local, coleta está zerada desde que o collector quebrou — mas mesmo
inserindo um produto de teste manualmente, o endpoint em si já rejeita a chamada antes de chegar a
avaliar o conteúdo/URL, então o teste do checklist de 5 itens é inconclusivo por bloqueio de acesso,
não por falta de dado).

### 6.1 Roteiro de validação (desenho original — não executável até a Seção 10 ser resolvida)
1. Rodar o novo `MercadoLivreCollector.CollectAsync` uma vez em ambiente local até pelo menos um
   produto de Mercado Livre chegar a `Status == Queued`.
2. Anotar o `SourceUrl` desse produto antes do `ProcessorJob` rodar (baseline de comparação).
3. Rodar o `ProcessorJob` até `EnsureAffiliateLinkAsync` gerar o `AffiliateLink`.
4. Aplicar o checklist objetivo de 5 itens (tabela original abaixo) sobre o `AffiliateLink` resultante.

| # | Verificação | Como checar | Resultado esperado |
|---|---|---|---|
| 1 | `AffiliateLink` é diferente de `SourceUrl`/`permalink` | comparação de string direta | `AffiliateLink != SourceUrl` |
| 2 | Domínio reconhecível do Mercado Livre ou do seu mecanismo de afiliados | inspecionar o host da URL | host contém `mercadolivre.com`/`mercadolibre.com` |
| 3 | Presença de identificador de conta/tag, não só um link "genérico" | inspecionar path/querystring do link | ao menos um identificador presente |
| 4 | O identificador é estável/da conta do Gerente, não aleatório por chamada | gerar `AffiliateLink` para dois produtos diferentes e comparar | mesmo identificador de conta nos dois links |
| 5 (opcional) | O link de afiliado de fato redireciona para o produto correto | seguir o redirect (`curl -IL`) | resolve para o produto correto |

5. Resultado documentado em `{docs_path}/validacao-link-afiliado.md`.
6. Se qualquer item falhar → achado documentado (`.claude/melhorias/` ou nova Issue), sem correção
   silenciosa de `EnsureAffiliateLinkAsync` fora do escopo original.

### 6.2 O que foi de fato executado pelo LT (2026-08-17)

Testei diretamente `POST https://api.mercadolibre.com/affiliate-tools/links` com o payload exato
usado pelo código (`{"url": "<permalink>"}`), com token válido, e variações (payload alternativo,
método GET, paths alternativos `affiliate-tools`, `affiliate-program/links`). **Todas as variações
retornaram HTTP 404** com o corpo padrão de rota inexistente do gateway do Mercado Livre
(`"error":"resource not found"`, mensagem genérica de "recurso não encontrado, consulte
developers.mercadolibre.com"). Isso é o mesmo tipo de resposta que a API retorna para qualquer rota
que simplesmente não existe/não está habilitada para a aplicação — diferente do 403 `access_denied`
observado no bloqueio dos itens (Seção 10), que é uma negativa de acesso a um recurso que existe.
Ou seja: **não é um problema de payload ou de produto de teste — o próprio endpoint não está
acessível para esta aplicação**, o que impede a execução do roteiro de validação (passo 3 em diante)
independente de qualquer outra decisão desta issue.

### 6.3 Critério de aceite mapeado
CA 7.1, 7.2 **não puderam ser confirmados** (bloqueados, ver Seção 10). CA 7.3 ("achado de defeito é
tratado como problema separado, não corrigido silenciosamente dentro desta issue") é exatamente o
procedimento que este LT está seguindo agora: reportando o achado e escalando ao Gerente (Gate 1.5)
em vez de tentar contornar/adivinhar um novo endpoint/payload por conta própria.

## 7. Dependências

- Nenhuma dependência nova de pacote — `.NET 8`/`Enumerable.Chunk` já nativo (seção 4.3).
- Nenhuma mudança de schema/migration.
- **Nova dependência crítica identificada pela Seção 10**: a viabilidade completa desta issue (tanto
  a reconstrução do collector quanto a validação do link de afiliado) depende de resolver o acesso
  da aplicação Mercado Livre (`client_id` atual) a `/items` e a `affiliate-tools/links` — depende de
  uma ação fora do escopo de ferramentas de qualquer agente da squad (portal de desenvolvedores do
  Mercado Livre, do lado do Gerente/dono da conta).

## 8. Riscos

- **Mapeamento de categorias (seção 3) desatualiza silenciosamente**: mitigado pelo isolamento de
  falha por categoria já decidido no Gate 1.
- ~~Limite do multi-get confirmado menor que 10~~: **descartado** — confirmado ≥ 18, acima do
  necessário.
- **Ver Seção 10 para o risco que se materializou**: formato/acesso ao link de afiliado e aos dados
  de item divergiram fundamentalmente do assumido, ao ponto de bloquear a validação, não apenas de
  exigir ajuste fino.

## 9. Fora de escopo deste design (para o LT/Dev)

- ~~Valores reais do `CategoryMap`~~ — **confirmado, seção 3.4**.
- ~~Valor real do limite do multi-get~~ — **confirmado, seção 4.2 (≥18)**.
- ~~Headers de rate limit~~ — **confirmado, seção 5.3 (nenhum header de rate limit exposto)**.
- Execução do roteiro de validação do link de afiliado (seção 6.1) — **bloqueada, ver Seção 10**,
  não é mais "depende do coletor existir", é "depende do endpoint ficar acessível".
- Nome exato dos métodos novos no client HTTP de Mercado Livre — refinamento de nomenclatura,
  suspenso até a Seção 10 ser resolvida (não faz sentido nomear métodos para um fluxo que pode mudar
  de forma).

## 10. BLOQUEIO CRÍTICO confirmado ao vivo pelo LT (2026-08-17) — requer decisão do Gerente

### 10.1 O que foi testado e o resultado

| Chamada | Resultado | O que significa |
|---|---|---|
| `GET /sites/MLB/categories` | HTTP 200 | OK — usado na Decisão 1 |
| `GET /categories/{id}` (2x, para investigar subcategorias) | HTTP 200 | OK — usado na Decisão 1 |
| `GET /highlights/MLB/category/{id}` (2 categorias testadas: Eletrodomésticos, Climatização) | HTTP 200 | OK — retorna lista de IDs ranqueados por `position`. **Os IDs retornados são `catalog_product_id` (produto agregado de catálogo), não `item_id` (anúncio individual de um vendedor)** — achado não previsto pelo design original |
| `GET /items?ids=<18 catalog_product_ids do Highlights>` | HTTP 200 (envelope) com **cada item individual = `code:404`** | Os IDs do Highlights não são reconhecidos por `/items` — confirma que são IDs de outro tipo de recurso (catálogo, não item) |
| `GET /products/{catalog_product_id}` | HTTP 200 | Funciona — retorna `name` (título), `pictures` (imagens), mas **`permalink` sempre vazio (`""`)** e `buy_box_winner` sempre `null`, testado em 4 produtos diferentes de 2 categorias |
| `GET /products/{catalog_product_id}/items` | HTTP 200 | Funciona — retorna a lista de anúncios (`item_id`, `price`, `seller_id`, `category_id`, `shipping`) que compõem aquele produto de catálogo. **Não inclui título, imagem nem permalink** |
| `GET /items/{item_id}` (usando `item_id` real, obtido do endpoint acima — não mais o `catalog_product_id` do Highlights) | **HTTP 403** `{"error":"access_denied","message":"Access to the requested resource is forbidden"}` — testado em 4 `item_id`s diferentes, com e sem `official_store_id` associado | **Bloqueio de acesso, não erro de payload/ID.** Sem esse endpoint não há como obter `permalink` de nenhum anúncio individual |
| `GET /items?ids={item_id}` (multi-get, mesmo `item_id` real) | HTTP 200 (envelope) com item individual `code:403` | Mesmo bloqueio, confirmado também via a rota de lote |
| `POST /affiliate-tools/links` com `{"url": "<qualquer URL>"}` (payload idêntico ao já implementado em `ProcessorJob.EnsureAffiliateLinkAsync`) | **HTTP 404** `{"error":"resource not found", ...}` — testado com 2 URLs de exemplo diferentes | Rota não reconhecida pelo gateway da API para esta aplicação (diferente do 403 acima — este é "rota não existe/habilitada", não "acesso negado a um recurso que existe") |
| `GET /affiliate-tools/links`, `GET /affiliate-tools`, `GET /affiliate-program/links` (variações de path/método, só para descartar erro de digitação) | HTTP 404 em todas | Reforça que não é erro de payload — a família de rotas não está habilitada/reconhecida |
| Headers de resposta de todas as chamadas acima | Nenhum `X-RateLimit-*`/`Retry-After` em nenhuma | Ver Decisão 3/seção 5.3 |
| `GET /applications/{client_id}` (introspecção da app registrada) | HTTP 200 — `"sandbox_mode": true`, `"certification_status": "not_certified"`, `"allow_flow": ["client_credentials"]`, `scopes` sem nenhum escopo de afiliados | **Hipótese de causa raiz**, ver 10.2 |

### 10.2 Hipótese de causa raiz (não confirmável sem acesso ao painel de desenvolvedores do Mercado Livre)

A aplicação registrada (`client_id` em `app_settings.mercadolivre.client_id`) está com
`sandbox_mode: true` e `certification_status: "not_certified"`, habilitada apenas para o fluxo OAuth2
`client_credentials` (aplicação, sem usuário) — não há `authorization_code` habilitado, e não existe
`mercadolivre.refresh_token` populado em `app_settings` (o campo existe no schema — usado por outras
plataformas do projeto — mas está vazio para Mercado Livre). Os `scopes` da aplicação listam permissões
genéricas de marketplace/admin, sem nada que pareça um escopo de programa de afiliados.

Isso é consistente com os dois bloqueios observados:
- **`/items` (leitura de anúncio individual) exige mais do que um token de aplicação sem certificação**
  — o mesmo tipo de restrição de política de 2026 que já havia quebrado `/sites/MLB/search` (premissa
  original desta issue) parece se aplicar também à leitura de detalhes de item avulso, não só à
  busca. Times legítimos de comparação de preço/afiliados historicamente liam `/items/{id}` sem
  qualquer autenticação — o fato de agora exigir 403 mesmo autenticado (app token) sugere que o nível
  de acesso necessário subiu para "aplicação certificada" e/ou "token de usuário autorizado"
  (`authorization_code`), não apenas "aplicação com client_id/secret válidos".
- **`affiliate-tools/links` como rota pública de API pode nunca ter existido nesse formato** — o
  Programa de Afiliados do Mercado Livre historicamente opera por um painel web dedicado
  (geração de link manual/via dashboard do afiliado), não necessariamente por uma rota REST pública
  documentada sob `api.mercadolibre.com`. A implementação atual de `EnsureAffiliateLinkAsync`
  (Issue #6) parece ter sido escrita sem validação end-to-end contra a API real — o que é exatamente
  o gap que a regra de negócio 6 do Gate 1 desta issue (CA 7) foi criada para expor.

**Nenhuma dessas duas hipóteses pode ser confirmada ou corrigida por um agente da squad** — ambas
dependem de ações no painel de desenvolvedores do Mercado Livre (certificar a aplicação, habilitar
`authorization_code` e completar o consentimento OAuth via navegador, e/ou confirmar com a
documentação oficial/suporte do Mercado Livre qual é a rota real do programa de afiliados, se
diferente de `affiliate-tools/links`) — todas exigem acesso humano à conta/portal do Mercado Livre do
Gerente, fora do escopo de `Bash`/`gh` de qualquer agente.

### 10.3 Opções levantadas para o Gerente decidir (Gate 1.5)

1. **Certificar a aplicação no painel de desenvolvedores do Mercado Livre** e/ou trocar o fluxo OAuth
   para `authorization_code` (exige o Gerente logar como o vendedor/afiliado e autorizar a aplicação
   via navegador — não automatizável por um agente) — se isso destravar `/items` e/ou revelar a rota
   real do programa de afiliados, o design original (seções 1-9) permanece válido como está, só
   trocando o mecanismo de obtenção do token.
2. **Buscar na documentação/suporte oficial do Mercado Livre qual é a rota real e atual do programa
   de afiliados** (pode ter mudado de nome/formato desde que `EnsureAffiliateLinkAsync` foi
   implementado na Issue #6) — se existir uma rota diferente, ajustar só essa chamada, sem impacto
   nas Decisões 1-3 (categorias, Highlights, batching já validados e funcionando).
3. **Descope parcial**: reconstruir o `MercadoLivreCollector` usando os dados parcialmente
   disponíveis sem autenticação elevada (`/products/{id}` para título/imagem, `/products/{id}/items`
   para preço), aceitando que `SourceUrl`/permalink e a geração do `AffiliateLink` ficam
   temporariamente indisponíveis/pendentes de uma issue separada — produtos de Mercado Livre voltam
   a ser coletados e pontuados, mas não publicados até o link de afiliado ser resolvido à parte
   (mantém a régua de "não corrigir/inventar link sem tag" do CA 7.2/7.3, não polui a fila de
   publicação com links quebrados).
4. **Pausar esta issue** até a situação da conta/aplicação Mercado Livre ser resolvida pelo Gerente,
   sem nenhuma sub-issue de implementação criada ainda (evita retrabalho: qualquer especificação
   técnica escrita agora teria que ser refeita dependendo de qual das opções acima for escolhida).

O LT não tem mandato para escolher entre essas opções (são decisões de produto/acesso a conta externa,
não uma ambiguidade técnica de arquitetura) — reportado ao Gerente via comentário na Issue #182,
aguardando resposta antes de prosseguir com `especificacao-tecnica.md`/`tasks.md`/sub-issues.
