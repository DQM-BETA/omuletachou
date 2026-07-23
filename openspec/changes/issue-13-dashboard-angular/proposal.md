# Proposal — ISSUE-13: Dashboard Angular (Todas as Páginas Admin)

## Objetivo
Evoluir o scaffold Angular 17 já criado na Issue #17 (`dashboard/`, estrutura de rotas, stubs de páginas, Dockerfile, `nginx.conf` com proxy `/api/`) para um dashboard administrativo funcional, com autenticação JWT, consumindo a API administrativa (Issue #11), cobrindo 7 telas: as 5 originais do escopo (`/products`, `/queue`, `/facebook-manual`, `/settings`, `/reports`) mais 2 adicionadas no Gate 1 (`/login`, `/jobs`).

## Usuários afetados
- Operador único/administrador do canal `omuletachou.com.br` — usuário interno, sem multiusuário nem papéis/permissões (mesmo modelo de usuário único da Issue #11).
- Acesso majoritariamente via desktop (decisão do Gate 1: desktop-only, sem investimento em responsividade mobile/tablet).

## Casos de uso principais
1. **Login** (`/login`, tela nova): operador informa email/senha, `POST /api/auth/login` retorna JWT (24h, sem refresh token); token armazenado em memória (`AuthService` singleton) + `sessionStorage` (nunca `localStorage`); redirecionamento automático para `/products` (ou última rota protegida) se já autenticado; tela sem menu lateral do dashboard.
2. **Sessão expirada / 401 global**: qualquer chamada autenticada que receba 401 (token expirado ou ausente) é interceptada por um `HttpInterceptor` Angular, que limpa o token armazenado e redireciona para `/login` exibindo mensagem "Sessão expirada, faça login novamente" — sem tentativa de renovação silenciosa (não há refresh token).
3. **Gestão de Produtos** (`/products`): tabela com plataforma (badge), thumbnail, título, preço, desconto, `ai_score` (badge verde ≥8 / amarelo ≥6 / vermelho <6), `ai_reason` (tooltip), status, data; filtros por plataforma/status/data; ações de aprovar/rejeitar por linha (`PATCH` de status).
4. **Fila de Publicações** (`/queue`): tabela/timeline por rede social; status visual (agendado/publicado/falhou/manual_pending); botão "Retry" em itens `failed` (`POST` retry); filtros por rede social/status.
5. **Posts Pendentes Facebook** (`/facebook-manual`): cards com preview de mídia e legenda; botão "Copiar legenda" (`navigator.clipboard`); botão "Marcar como publicado" (`PATCH` status).
6. **Configurações** (`/settings`): formulário agrupado por seção (Amazon, MercadoLivre, Shopee, Telegram, YouTube, Instagram, TikTok, Claude AI, Agendamentos, Redes habilitadas); campos sensíveis mascarados (ver Regras de negócio); toggle show/hide em campos de senha; botão "Salvar" por seção (`PUT /api/settings/{key}` por campo alterado).
7. **Jobs manual** (`/jobs`, tela nova): botões para disparar manualmente `CollectorJob` (geral e por plataforma), `ProcessorJob` e `PublisherJob` via `POST /api/jobs/*/trigger`; exibe resultado da última execução disparada.
8. **Relatórios** (`/reports`): cards de totais (publicado hoje/semana/mês); gráfico de barras de publicações por rede nos últimos 7 dias (Chart.js/ng2-charts); tabela de falhas recentes com botão Retry.

## Casos de exceção
1. **401 em qualquer chamada** (token expirado, ausente ou inválido): interceptor global limpa sessão e redireciona a `/login` com mensagem — tratado uma única vez no interceptor, não em cada componente.
2. **Acesso a rota protegida sem token**: guard de rota (`CanActivate`) redireciona a `/login` antes de qualquer chamada à API.
3. **Acesso a `/login` já autenticado**: redireciona automaticamente para a área logada, sem exibir o formulário de login novamente.
4. **`PUT /api/settings/{key}` de campo deixado em branco no formulário**: o front **não envia** a requisição PUT para aquele campo específico (ver Regras de negócio — decisão de não exigir mudança retroativa na API).
5. **Job disparado manualmente falha ou demora**: tela `/jobs` exibe o resultado (sucesso/erro) da última execução, sem travar a UI aguardando o job terminar de fato (jobs são assíncronos via Hangfire).
6. **Botão "Testar conexão" por integração**: **fora de escopo desta issue** — endpoint ainda não existe na API (ver Débitos/Follow-ups).
7. **`npm run build`**: deve completar sem erros de TypeScript.

## Regras de negócio
- **Autenticação**: JWT armazenado em memória (`AuthService` singleton, estado do Angular) combinado com `sessionStorage` para persistir entre reloads de página — nunca `localStorage` (decisão do Gate 1, reduz superfície de exposição a XSS residual). `HttpInterceptor` anexa o header `Authorization: Bearer <token>` em toda chamada autenticada e captura 401 globalmente (ver Casos de exceção #1). Guard de rota (`CanActivate`) protege todas as rotas exceto `/login`.
- **Sem refresh token**: quando o JWT expirar (24h), o operador precisa logar novamente 1x/dia — comportamento aceito explicitamente pelo Gerente no Gate 1, sem necessidade de renovação silenciosa.
- **Mascaramento de secrets em `/settings` — decisão consolidada após investigação do contrato real da API (Issue #11)**:
  - A API (`GET /api/settings`, CA-C1/CA-C2/CA-C3 da Issue #11) já mascara valores sensíveis no formato fixo `****************a1b2` (16 asteriscos + últimos 4 caracteres), nunca retornando o valor completo.
  - `PUT /api/settings/{key}` (CA-C4/CA-C5 da Issue #11) é um endpoint **por chave individual** (`{ "value": "novo-valor" }`), não um payload em lote com múltiplos campos. Isso significa que **não é necessário nenhum ajuste retroativo no contrato da API #11**: o comportamento "campo vazio não sobrescreve" é obtido inteiramente no front, simplesmente **não disparando a chamada PUT** para as chaves que o operador deixou em branco no formulário. Cada seção do formulário de Settings, ao ser salva, dispara um `PUT` por campo alterado (não vazio) — os campos deixados em branco simplesmente não geram chamada nenhuma, preservando o valor já persistido no backend sem qualquer mudança de contrato.
  - Portanto, **não vira CA formal de ajuste retroativo na API** (diferente do padrão dos fixes retroativos das Issues #8/#9) — a decisão do Gate 1 é inteiramente implementável como responsabilidade do front-end dado o contrato já existente.
  - UX do campo: input vazio com placeholder "Valor atual: ****a1b2 — digite para substituir" (usa o valor mascarado já retornado por `GET /api/settings`); se o operador digitar um valor novo, este substitui integralmente o anterior via `PUT` daquela chave.
- **Design**: sem Figma — UX/UI livre a partir dos critérios funcionais (mesmo padrão da Issue #12), priorizando um design system pronto (Angular Material ou PrimeNG) em vez de componentes customizados — ferramenta interna, não vitrine de marca.
- **Responsividade**: desktop-only (decisão do Gate 1) — sem breakpoints mobile/tablet nesta fase.
- **Testes**: definição de pronto não exige Playwright/e2e nesta issue — testes unitários Angular (Jasmine/Karma ou Jest) cobrindo os serviços (`AuthService`, `ProductsService`, `QueueService`, `SettingsService`, `ReportsService`) + validação manual das telas. E2E registrado como melhoria futura (ver Débitos/Follow-ups).

## Integrações externas
- API administrativa já entregue na Issue #11 (`main`): `POST /api/auth/login`, GET/PATCH `/api/products`, GET/POST `/api/queue`, GET/PUT `/api/settings`, GET `/api/reports`, `POST /api/jobs/*/trigger` — consumida via proxy nginx `/api/` → `http://api:8080/api/` (configurado na Issue #1), nunca expondo a URL interna ao browser.
- Nenhuma integração de rede externa nova nesta issue (sem chamada direta a serviços de terceiros a partir do dashboard).

## Restrições / prazo
- Sem prazo explícito informado na Issue.
- Base já existente: scaffold da Issue #17 (`dashboard/`, Angular 17, rotas stub, Dockerfile, `nginx.conf`) — esta issue evolui essa base, não a recria.
- Dependência: Issue #11 (API administrativa) já entregue em `main`.

## Débitos / follow-ups formais (fora de escopo nesta issue)
1. **Botão "Testar conexão" por integração (`/settings`)**: endpoint ainda não existe na API. Sugestão de contrato alinhada com o Gerente no Gate 1: `POST /api/settings/{integration}/test-connection` (ex.: `telegram`, `youtube`, `instagram`, `tiktok`), chamada mínima e não-destrutiva à API externa correspondente usando as credenciais salvas, retornando `{ success: bool, message: string }`. **Autorizado explicitamente pelo Gerente a entregar Settings sem esse botão nesta issue** — não travar a entrega do dashboard por essa dependência. Deve virar uma Issue de follow-up própria (novo endpoint na API + botão no front) quando priorizado.
2. **Testes e2e (Playwright)** cobrindo os fluxos principais (login, aprovar/rejeitar produto, retry na fila, salvar settings): registrado como melhoria futura, não exigido nesta issue (decisão do Gate 1 — custo desproporcional ao risco para ferramenta interna de um usuário).
3. **Responsividade mobile/tablet**: fora de escopo (decisão do Gate 1), pode ser revisitada caso o perfil de uso do operador mude.

## Avaliação de ambiguidade arquitetural (PM Fase 2)
Dois pontos foram avaliados com cuidado antes de decidir se escalar ao Arquiteto (ver detalhamento em `estado.md`):
1. **Mascaramento em `/settings` e possível ajuste retroativo na API #11**: investigado o contrato real (`especificacao-tecnica.md` e `criterios-aceite.md` da Issue #11, CA-C1 a CA-C6). Conclusão: **nenhum ajuste retroativo é necessário** — `PUT /api/settings/{key}` já opera por chave individual, então "não sobrescrever campo vazio" é inteiramente resolvido no front (não disparar a chamada para chaves em branco). Não gera CA de ajuste retroativo, diferente das Issues #8/#9.
2. **Estratégia de auth (JWT em memória + `sessionStorage` + `HttpInterceptor` de 401)**: é a primeira feature de sessão de usuário no browser do projeto, mas o padrão (interceptor + guard + storage de token) é textbook Angular, já com as decisões de risco (XSS residual, ausência de refresh token) resolvidas explicitamente pelo Gerente no Gate 1. Não há CSRF residual relevante (token vai em header `Authorization`, não em cookie enviado automaticamente pelo browser). Não configura decisão de arquitetura não-óbvia.
- **Conclusão**: **sem ambiguidade arquitetural que exija revisão do Arquiteto** — segue direto para o Líder Técnico (design.md resumido + task breakdown).

## Escopo — fatiamento em sub-issues (visão do PM; LT decide o breakdown técnico final)
Baseado na sugestão do Gerente no Gate 1 (não fechada):
- **Sub-A — Autenticação**: tela `/login`, `AuthService` (estado + `sessionStorage`), `HttpInterceptor` (anexa token + trata 401), guard de rota (`CanActivate`).
- **Sub-B — Products + Queue**: as duas telas de maior uso diário.
- **Sub-C — Settings + Jobs manual**: formulário de configurações com mascaramento e tela de disparo manual de jobs.
- **Sub-D — Facebook Manual + Reports**: cards de posts pendentes e relatórios/gráficos.

Dependência de ordem: Sub-A entrega `AuthService`/interceptor/guard, pré-requisito funcional para todas as demais telas protegidas — Sub-B/C/D dependem dela (mas podem iniciar em paralelo assim que o contrato do `AuthService` estiver definido, mesmo antes do login estar 100% pronto, similar ao padrão adotado na Issue #12).

## Definição de pronto
- 7 telas funcionais: `/login`, `/products`, `/queue`, `/facebook-manual`, `/settings`, `/jobs`, `/reports`.
- Autenticação completa: login, guard de rota, interceptor de 401 com redirecionamento e mensagem de sessão expirada.
- `/settings` com mascaramento correto (placeholder mascarado, PUT só para campos preenchidos) e sem o botão "Testar conexão" (registrado como follow-up).
- `npm run build` sem erros de TypeScript.
- Testes unitários Angular cobrindo os serviços principais + validação manual das telas.
- Nenhuma responsividade mobile/tablet exigida.
