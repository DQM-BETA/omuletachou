# Critérios de aceite — ISSUE-12: Site Público Next.js (ISR + SEO)

Organizados por agrupamento sugerido (Sub-A/B/C — ver `proposal.md`). O LT usará esta numeração como base para o breakdown técnico final e criação das sub-issues reais no GitHub.

---

## Sub-A — Integração de dados + Home

**CA-A1 — `fetchDeals` consome a API pública paginada**
Given a API pública disponível em `http://api:8080/api/public/deals`
When `lib/api.ts` chama `fetchDeals(page, category?)` a partir de um Server Component
Then a função retorna a lista de ofertas paginada, sem expor a URL interna `http://api:8080` ao browser em nenhum momento (view-source não deve conter essa string).

**CA-A2 — Home renderiza grade de cards via ISR**
Given a rota `/` (Home)
When a página é construída/regenerada
Then usa ISR com `revalidate: 300` (não SSR puro, não client-side fetch para o conteúdo principal), e o HTML de resposta já contém a grade de cards renderizada sem depender de JavaScript no cliente.

**CA-A3 — Card de oferta exibe os elementos obrigatórios**
Given uma oferta retornada pela API com preço original, preço com desconto e imagem
When `DealCard.tsx` é renderizado
Then exibe: imagem do produto, título resumido, preço original riscado, preço com desconto, badge vermelho com `%OFF` e botão "Ver oferta".

**CA-A4 — CTA do card usa `rel=nofollow` e abre em nova aba**
Given o botão "Ver oferta" de um `DealCard`
When renderizado
Then o link de afiliado tem `target="_blank"` e `rel="nofollow"`.

**CA-A5 — Filtro por plataforma atualiza a grade sem reload**
Given a Home com filtros de plataforma (Amazon, MercadoLivre, Shopee)
When o visitante seleciona uma plataforma
Then a grade de cards é atualizada refletindo o filtro, sem recarregar a página inteira (comportamento client-side sobre dados já carregados ou nova navegação App Router sem full reload).

**CA-A6 — Paginação da Home**
Given mais ofertas do que cabem em uma página
When o visitante navega para a página seguinte
Then a grade exibe o próximo conjunto de ofertas, mantendo o filtro de plataforma/categoria ativo.

**CA-A7 — Imagem ausente/quebrada não quebra o layout**
Given uma oferta sem `MediaUrl` válido
When `DealCard.tsx` é renderizado
Then exibe um placeholder de imagem, sem erro de layout ou crash da página.

---

## Sub-B — Página de oferta + SEO de produto

**CA-B1 — `fetchDeal` consome o endpoint de detalhe**
Given a API pública disponível em `http://api:8080/api/public/deals/{slug}`
When `lib/api.ts` chama `fetchDeal(slug)`
Then retorna os dados completos da oferta correspondente ao slug.

**CA-B2 — Página de produto renderiza via ISR**
Given a rota `/oferta/{slug}`
When a página é construída/regenerada
Then usa ISR com `revalidate: 300`, e o HTML já vem com o conteúdo do produto renderizado (sem depender de JS no cliente).

**CA-B3 — Slug inexistente retorna 404**
Given um slug que não existe na API
When `/oferta/{slug-invalido}` é acessado
Then a página retorna 404 tratado via `notFound()` do Next.js (não erro 500, não crash).

**CA-B4 — `DealDetail` exibe os elementos obrigatórios**
Given uma oferta válida
When `DealDetail.tsx` é renderizado
Then exibe: mídia (imagem/vídeo) em destaque, preço grande, badge de desconto, botão CTA principal e seção "Mais ofertas" com 4 produtos relacionados.

**CA-B5 — `generateMetadata` dinâmico por produto**
Given uma oferta com título e descrição
When o Google/crawler rastreia `/oferta/{slug}`
Then o HTML retornado já contém `<title>` e `<meta name="description">` corretos para aquele produto específico, sem necessidade de execução de JavaScript.

**CA-B6 — Open Graph completo na página de produto**
Given uma oferta com imagem, título e descrição
When a página `/oferta/{slug}` é servida
Then o `<head>` contém `og:title`, `og:description`, `og:image` (apontando para a imagem real do produto) e `og:url` (canonical da página).

**CA-B7 — Preview correto ao compartilhar no WhatsApp/Facebook**
Given `og:image` configurado com a imagem do produto
When o link `/oferta/{slug}` é compartilhado no WhatsApp ou Facebook
Then o preview exibe a imagem, título e descrição do produto (validação via debugger de Open Graph ou inspeção do HTML gerado).

**CA-B8 — Fallback de `og:image` quando imagem do produto está ausente**
Given uma oferta sem `MediaUrl` válido
When `generateMetadata` monta o Open Graph
Then usa uma imagem padrão do site como fallback (nunca deixa `og:image` vazio ou quebrado).

**CA-B9 — Schema.org JSON-LD `Product`/`Offer`**
Given uma oferta com nome, preço, imagem e disponibilidade
When `/oferta/{slug}` é servida
Then o HTML contém um bloco `<script type="application/ld+json">` com Schema.org tipo `Product`, incluindo `Offer` aninhado (preço, moeda, disponibilidade).

---

## Sub-C — Página de categoria + sitemap/robots

**CA-C1 — `fetchByCategory` consome o endpoint de categoria**
Given a API pública disponível em `http://api:8080/api/public/deals/category/{categoria}`
When `lib/api.ts` chama `fetchByCategory(categoria)`
Then retorna as ofertas daquela categoria, paginadas.

**CA-C2 — Página de categoria renderiza via ISR**
Given a rota `/categoria/{categoria}`
When a página é construída/regenerada
Then usa ISR com `revalidate: 300`, reaproveitando a mesma estrutura de grade da Home (cards, paginação).

**CA-C3 — Meta tags específicas de categoria**
Given a categoria "Eletrônicos"
When `/categoria/eletronicos` é servida
Then o `<title>` segue o padrão `Eletrônicos | O Mulet Achou` (nome da categoria + sufixo do site).

**CA-C4 — Categoria sem ofertas exibe estado vazio (não 404)**
Given uma categoria válida sem nenhuma oferta ativa no momento
When `/categoria/{categoria}` é acessada
Then a página renderiza normalmente com uma mensagem de "Nenhuma oferta encontrada nesta categoria" (categoria não é tratada como 404).

**CA-C5 — `sitemap.xml` dinâmico lista ofertas ativas**
Given o conjunto de ofertas ativas retornado pela API pública
When `/sitemap.xml` é acessado
Then o XML gerado via `app/sitemap.ts` lista a URL de cada oferta ativa (`/oferta/{slug}`) com `lastModified`, além das páginas Home e de categorias.

**CA-C6 — `robots.txt` permite indexação geral**
Given a rota `/robots.txt`
When acessada por um crawler
Then retorna um `robots.txt` estático com `Allow: /` (sem bloqueio geral) e referência ao `sitemap.xml`.

---

## Transversal (todas as sub-issues)

**CA-T1 — Build sem erros de TypeScript**
Given o código completo das 3 sub-issues integrado
When `npm run build` é executado
Then o build completa sem erros de TypeScript.

**CA-T2 — Resiliência do ISR à indisponibilidade da API**
Given a API pública momentaneamente indisponível durante uma janela de regeneração do ISR
When um visitante acessa qualquer uma das 3 páginas
Then o Next.js continua servindo a última versão estática válida em cache, sem erro 500 para o visitante.

**CA-T3 — Nenhum código de PWA/push introduzido**
Given o escopo desta issue
When o código é revisado
Then não há `manifest.json`, service worker ou lógica de subscription de push adicionados (escopo exclusivo da Issue #14).

**CA-T4 — Nenhuma configuração de deploy de produção alterada**
Given o `docker-compose.yml` e variáveis de ambiente já existentes (Issue #18)
When o código desta issue é revisado
Then nenhuma variável de produção (`NEXT_PUBLIC_API_URL` real, domínio, SSL) é introduzida ou alterada — escopo exclusivo da Issue #15.
