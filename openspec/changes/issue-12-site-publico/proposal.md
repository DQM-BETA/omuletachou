# Proposal — ISSUE-12: Site Público Next.js (ISR + SEO)

## Objetivo
Evoluir o scaffold Next.js 14 já criado na Issue #18 (`website/`, App Router, TypeScript, Dockerfile, páginas stub) para um site público funcional que consome a API REST pública (Issue #11) e entrega as 3 páginas de catálogo (Home, produto, categoria) com ISR (`revalidate: 300s`) e o pacote de SEO completo exigido pelo Gerente no Gate 1 (meta tags dinâmicas, Open Graph, Schema.org `Product`, sitemap.xml dinâmico, robots.txt). PWA/push e deploy de produção ficam fora de escopo (Issues #14 e #15).

## Usuários afetados
- Visitantes do site público `omuletachou.com.br` (desktop/mobile) — navegam pelo catálogo de ofertas e acessam páginas de produto.
- Google/crawlers de busca e Facebook/WhatsApp (link preview) — consomem o HTML pré-renderizado (ISR) e as meta tags/Open Graph/JSON-LD para indexação e rich snippets.
- Indiretamente, o negócio de afiliados: o objetivo final do site é converter clique no link de afiliado (CTA), não oferecer uma experiência visual elaborada.

## Casos de uso principais
1. Visitante acessa `/` (Home) → recebe grade de cards de ofertas (imagem, título, preço riscado, preço atual, badge de desconto, CTA) renderizada via ISR (HTML gerado a cada 300s no máximo), com filtros por plataforma (Amazon/MercadoLivre/Shopee) e categoria, e paginação.
2. Visitante acessa `/oferta/{slug}` → página de produto com mídia em destaque, preço grande, badge de desconto, CTA principal (link de afiliado, `target=_blank`, `rel=nofollow`) e seção "Mais ofertas" (4 relacionados); `generateMetadata` preenche title/description/canonical/Open Graph dinamicamente a partir do produto; JSON-LD `Product`/`Offer` embutido no HTML.
3. Visitante acessa `/categoria/{categoria}` → mesma estrutura de grade da Home, filtrada por categoria, com meta tags específicas (ex. `Eletrônicos | O Mulet Achou`).
4. Google/crawler acessa `/sitemap.xml` (gerado dinamicamente via `app/sitemap.ts`, listando todas as ofertas ativas) e `/robots.txt` (estático, permitindo indexação geral) para descoberta e indexação do catálogo.
5. Usuário do WhatsApp/Facebook compartilha um link `/oferta/{slug}` → preview exibe imagem, título e descrição do produto via Open Graph.
6. Site consome a API pública (Issue #11) em 3 pontos: `GET /api/public/deals` (Home/categoria, paginado), `GET /api/public/deals/{slug}` (produto), `GET /api/public/deals/category/{categoria}` (categoria) — sempre a partir do servidor (Server Components), via URL interna Docker (`http://api:8080`), nunca expondo essa URL ao browser.

## Casos de exceção
1. **Slug inexistente** (`/oferta/{slug}` não encontrado na API): página retorna 404 (Next.js `notFound()`), sem quebrar o build nem vazar erro de infraestrutura.
2. **Categoria sem ofertas**: página de categoria renderiza normalmente com estado vazio ("Nenhuma oferta encontrada nesta categoria"), não 404 (categoria é uma dimensão de filtro, não uma entidade obrigatória).
3. **API pública indisponível/timeout no momento da regeneração ISR**: Next.js mantém servindo a última versão estática válida em cache (comportamento padrão do ISR); página não deve quebrar para o visitante. LT/Dev definem estratégia de log/observabilidade do erro no build.
4. **Imagem de produto ausente/quebrada**: `DealCard`/`DealDetail` exibem placeholder, sem quebrar layout nem afetar `og:image` (usar imagem padrão do site como fallback de Open Graph).
5. **`npm run build`**: deve completar sem erros de TypeScript (gate de qualidade explícito na Issue original).

## Regras de negócio
- **Renderização**: as 3 páginas (Home, produto, categoria) usam ISR com `revalidate: 300` (5 minutos) — decisão do Gerente no Gate 1. Nenhuma delas usa SSR puro (sem cache) nem client-side rendering para o conteúdo principal.
- **Fetch de dados**: sempre no lado do servidor (Server Components / `fetch` com `next: { revalidate: 300 }`), usando a URL interna Docker `http://api:8080` — a URL da API pública nunca é exposta ao browser (evita acoplar o cliente à topologia de rede interna e evita CORS desnecessário no cliente).
- **SEO obrigatório nesta issue** (não pode ser adiado — é a razão de escolher Next.js sobre Angular para o site público):
  1. `generateMetadata` dinâmico por página (title/description) usando dados reais do produto/categoria.
  2. Open Graph completo (`og:title`, `og:description`, `og:image`, `og:url`) nas páginas de produto e categoria.
  3. Schema.org JSON-LD tipo `Product` (com `Offer` aninhado: preço, disponibilidade) na página de oferta.
  4. `sitemap.xml` dinâmico via `app/sitemap.ts`, listando todas as ofertas ativas (URL + `lastModified`).
  5. `robots.txt` estático permitindo indexação geral (`Allow: /`, referência ao sitemap).
- **CTA de afiliado**: todo link de afiliado abre em nova aba (`target="_blank"`) com `rel="nofollow"` (evita repassar "link juice" de SEO e sinaliza corretamente aos crawlers que é um link comercial/patrocinado).
- **PWA/Push fora de escopo**: nenhum manifest.json, service worker ou lógica de subscription deve ser adicionado nesta issue — pertence integralmente à Issue #14.
- **Deploy de produção fora de escopo**: variáveis de ambiente de produção (`NEXT_PUBLIC_API_URL` apontando para a API pública real), domínio e SSL são tratados na Issue #15; aqui o serviço `website` roda sobre o container já existente no `docker-compose.yml` (porta 3000), testável em homolog.
- **Design**: sem Figma — UX/UI da squad define o layout a partir dos critérios funcionais (cards de oferta, página de produto) já descritos, priorizando simplicidade e performance de carregamento sobre design elaborado.

## Integrações externas
- API pública já entregue na Issue #11: `GET /api/public/deals`, `GET /api/public/deals/{slug}`, `GET /api/public/deals/category/{categoria}` — consumida via rede interna Docker (`http://api:8080`).
- CORS já configurado na Issue #11 para `omuletachou.com.br`, `www.omuletachou.com.br` e `localhost:3000` (relevante apenas se algum fetch futuro ocorrer client-side; o fluxo principal desta issue é 100% server-side, não dependendo de CORS).
- Nenhuma integração de rede externa nova (sem serviço de terceiros, sem chamada a Google/Facebook APIs — Open Graph/JSON-LD são apenas metadados HTML, não chamadas de API).

## Restrições / prazo
- Sem prazo explícito informado na Issue.
- Base já existente: scaffold da Issue #18 (`website/`, App Router, TypeScript, Dockerfile, páginas stub, serviço `website` no `docker-compose.yml`) — esta issue evolui essa base, não a recria.
- Dependência: Issue #11 (API pública) já entregue em `main`.
- Sem ambiguidade arquitetural relevante identificada pelo PM (ver `estado.md` — avaliação Fase 2): estratégia de renderização (ISR/300s), escopo de SEO e integração já foram decididos pelo Gerente no Gate 1; padrões de fetch em Server Components e fallback de ISR em caso de API fora do ar são práticas padrão do Next.js App Router, não decisões de arquitetura que exijam revisão do Arquiteto.

## Escopo — fatiamento em sub-issues (visão do PM; LT decide o breakdown técnico final)
Agrupamento natural sugerido, evitando dependências circulares (integração de dados é pré-requisito comum):
- **Sub-A — Integração de dados + Home**: `lib/api.ts` (fetchDeals/fetchDeal/fetchByCategory), `DealCard.tsx`, `Header.tsx`, página Home com ISR, filtros por plataforma/categoria e paginação.
- **Sub-B — Página de oferta + SEO de produto**: `DealDetail.tsx`, `oferta/[slug]/page.tsx` com ISR, `generateMetadata` dinâmico, Open Graph completo, Schema.org JSON-LD `Product`/`Offer`, tratamento de slug inexistente (404).
- **Sub-C — Página de categoria + sitemap/robots**: `categoria/[categoria]/page.tsx` com ISR e meta tags de categoria, `app/sitemap.ts` dinâmico, `robots.txt` estático.

Dependência de ordem: Sub-A entrega `lib/api.ts` e os componentes de card compartilhados — Sub-B e Sub-C dependem dela (mas podem iniciar em paralelo assim que a interface de `lib/api.ts` estiver definida, mesmo antes da Home estar 100% pronta). LT avalia se paraleliza com stub/contrato acordado antecipadamente.

## Definição de pronto
- As 3 páginas (Home, `/oferta/[slug]`, `/categoria/[categoria]`) renderizando dados reais da API pública via ISR (`revalidate: 300`).
- `npm run build` sem erros de TypeScript.
- Os 5 itens de SEO obrigatório implementados e verificáveis: `generateMetadata` dinâmico, Open Graph completo, JSON-LD `Product` na página de oferta, `/sitemap.xml` dinâmico listando ofertas ativas, `/robots.txt` estático.
- `curl`/view-source da página de produto mostra HTML já renderizado com title/description/OG corretos, sem depender de JavaScript no cliente.
- Slug inexistente retorna 404 tratado (não erro 500 nem crash).
- Cards seguem os critérios visuais definidos (imagem, título, preço riscado, preço atual, badge de desconto, CTA com `rel=nofollow`).
- Nenhum código de PWA/push ou configuração de deploy de produção introduzido nesta issue.
