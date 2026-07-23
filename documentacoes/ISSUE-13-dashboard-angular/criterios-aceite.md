# Critérios de aceite — ISSUE-13: Dashboard Angular (Todas as Páginas Admin)

Organizados por agrupamento sugerido (Sub-A/B/C/D — ver `proposal.md`). O LT usará esta numeração como base para o breakdown técnico final e criação das sub-issues reais no GitHub.

---

## Sub-A — Autenticação (Login, AuthService, HttpInterceptor, Guard)

**CA-A1 — Login com credenciais válidas**
Given um usuário com email/senha válidos cadastrados na API (Issue #11)
When o operador submete o formulário em `/login`
Then `POST /api/auth/login` é chamado, o JWT retornado é armazenado (memória do `AuthService` + `sessionStorage`), e o operador é redirecionado para a área logada (`/products` ou última rota protegida).

**CA-A2 — Login com credenciais inválidas**
Given credenciais incorretas
When o operador submete o formulário em `/login`
Then a API retorna 401, a tela exibe mensagem de erro ("Email ou senha inválidos"), sem armazenar token nenhum.

**CA-A3 — Armazenamento do token (não localStorage)**
Given um login bem-sucedido
When o token é persistido
Then é armazenado em `sessionStorage` (nunca `localStorage`), acessível apenas nas abas da mesma sessão do navegador.

**CA-A4 — Guard de rota bloqueia acesso sem token**
Given um usuário não autenticado (sem token válido)
When tenta acessar qualquer rota protegida (`/products`, `/queue`, `/settings`, `/jobs`, `/facebook-manual`, `/reports`)
Then o `CanActivate` guard redireciona para `/login` antes de qualquer chamada à API.

**CA-A5 — Acesso a `/login` já autenticado redireciona**
Given um usuário já autenticado (token válido em sessão)
When acessa `/login` diretamente pela URL
Then é redirecionado automaticamente para a área logada, sem exibir o formulário novamente.

**CA-A6 — Interceptor anexa o token em toda chamada autenticada**
Given um token válido armazenado
When qualquer chamada HTTP a um endpoint protegido da API é disparada
Then o `HttpInterceptor` anexa o header `Authorization: Bearer <token>` automaticamente, sem necessidade de cada serviço fazer isso manualmente.

**CA-A7 — Interceptor trata 401 globalmente**
Given uma chamada autenticada que retorna 401 (token expirado ou inválido)
When a resposta 401 chega ao `HttpInterceptor`
Then o token armazenado é limpo (memória + `sessionStorage`), o operador é redirecionado para `/login`, e uma mensagem "Sessão expirada, faça login novamente" é exibida.

**CA-A8 — Tela de login sem menu lateral**
Given a rota `/login`
When renderizada
Then não exibe o menu lateral/layout do dashboard (layout próprio, dedicado só ao formulário de login).

---

## Sub-B — Products + Queue

**CA-B1 — Tabela de produtos exibe os campos obrigatórios**
Given uma lista de produtos retornada por `GET /api/products`
When `/products` é renderizada
Then a tabela exibe: plataforma (badge colorido), thumbnail, título, preço, desconto, `ai_score` (badge), `ai_reason` (tooltip), status, data.

**CA-B2 — Badge de `ai_score` usa as cores corretas**
Given um produto com `ai_score = 9`
When listado em `/products`
Then o badge é verde (regra: verde ≥8, amarelo ≥6, vermelho <6) exibindo o valor numérico.

**CA-B3 — Filtros de produtos**
Given a tela `/products`
When o operador aplica filtro de plataforma, status ou data de coleta
Then a tabela reflete apenas os produtos que atendem ao(s) filtro(s) selecionado(s).

**CA-B4 — Aprovar produto**
Given um produto listado com ação de aprovar disponível
When o operador clica no ícone de aprovar (check verde)
Then `PATCH /api/products/{id}` é chamado alterando o status para aprovado, e a tabela reflete a mudança sem reload completo da página.

**CA-B5 — Rejeitar produto**
Given um produto listado com ação de rejeitar disponível
When o operador clica no ícone de rejeitar (x vermelho)
Then `PATCH /api/products/{id}` é chamado alterando o status para rejeitado, e a tabela reflete a mudança.

**CA-B6 — Fila exibe status visual por cor**
Given itens na fila com status `agendado`, `publicado`, `falhou` e `manual_pending`
When `/queue` é renderizada
Then cada status usa a cor definida (cinza/verde/vermelho/laranja respectivamente).

**CA-B7 — Retry em item com falha**
Given um item da fila com status `failed`
When o operador clica em "Retry"
Then `POST` de retry é chamado e o item muda de status para `Scheduled` (agendado) refletido na tela.

**CA-B8 — Filtros da fila**
Given a tela `/queue`
When o operador aplica filtro de rede social ou status
Then a tabela/timeline reflete apenas os itens que atendem ao filtro.

---

## Sub-C — Settings + Jobs manual

**CA-C1 — Campo sensível carrega mascarado, nunca com valor real**
Given uma chave sensível já configurada (ex.: `telegram.bot_token`) retornada mascarada por `GET /api/settings` (formato `****************a1b2`)
When o formulário de `/settings` carrega
Then o campo correspondente aparece **vazio**, com placeholder "Valor atual: ****a1b2 — digite para substituir" (nunca pré-preenchido com o valor mascarado nem com o valor real).

**CA-C2 — Campo deixado em branco não é enviado no PUT**
Given um campo sensível deixado em branco pelo operador (não editado)
When a seção do formulário é salva
Then **nenhuma chamada `PUT /api/settings/{key}` é disparada para essa chave** — o valor já persistido no backend permanece intacto (sem necessidade de alteração no contrato da API #11, já que o PUT é por chave individual).

**CA-C3 — Campo preenchido substitui o valor integralmente**
Given um campo sensível em que o operador digita um novo valor
When a seção é salva
Then `PUT /api/settings/{key}` é chamado com `{ "value": "<novo-valor>" }` para aquela chave, substituindo o valor anterior integralmente.

**CA-C4 — Toggle show/hide em campos de senha**
Given um campo de senha/API key no formulário
When o operador clica no ícone de toggle show/hide
Then o campo alterna entre `type="password"` e `type="text"`.

**CA-C5 — Botão "Salvar" por seção**
Given o formulário agrupado por seção (Amazon, MercadoLivre, Shopee, Telegram, YouTube, Instagram, TikTok, Claude AI, Agendamentos, Redes habilitadas)
When o operador clica em "Salvar" de uma seção específica
Then apenas os campos alterados daquela seção são enviados via `PUT`, sem afetar as demais seções.

**CA-C6 — Sem botão "Testar conexão" nesta issue**
Given a tela `/settings` entregue nesta issue
When revisada
Then não contém o botão "Testar conexão" por integração — funcionalidade registrada como follow-up formal (ver `proposal.md` — Débitos), já que o endpoint correspondente não existe na API ainda.

**CA-C7 — Tela de Jobs manual dispara jobs via API**
Given a tela `/jobs`
When o operador clica em um dos botões (`CollectorJob` geral/por plataforma, `ProcessorJob`, `PublisherJob`)
Then o respectivo `POST /api/jobs/*/trigger` é chamado e a API confirma o enfileiramento (200/202).

**CA-C8 — Resultado da última execução exibido**
Given um job disparado manualmente pela tela `/jobs`
When a execução é concluída (ou a chamada de disparo retorna)
Then a tela exibe o resultado (sucesso/erro) da última execução disparada, sem travar a UI aguardando o job terminar de fato.

---

## Sub-D — Facebook Manual + Reports

**CA-D1 — Cards de posts pendentes exibem preview de mídia e legenda**
Given posts pendentes retornados pela API
When `/facebook-manual` é renderizada
Then cada card exibe preview da mídia (imagem/vídeo) e o texto completo da legenda.

**CA-D2 — Copiar legenda para a área de transferência**
Given um card de post pendente
When o operador clica em "Copiar legenda"
Then o texto da legenda é copiado para a área de transferência via `navigator.clipboard` (validável por leitura do clipboard em teste).

**CA-D3 — Marcar post como publicado**
Given um card de post pendente
When o operador clica em "Marcar como publicado"
Then `PATCH` de status é chamado, e o card correspondente deixa de aparecer na lista de pendentes (ou reflete o novo status).

**CA-D4 — Cards de totais em Reports**
Given dados de `GET /api/reports`
When `/reports` é renderizada
Then exibe cards com totais de publicações hoje, na semana e no mês.

**CA-D5 — Gráfico de publicações por rede (últimos 7 dias)**
Given dados históricos de publicação por rede social
When `/reports` é renderizada
Then um gráfico de barras (Chart.js/ng2-charts) exibe a distribuição de publicações por rede nos últimos 7 dias.

**CA-D6 — Tabela de falhas recentes com Retry**
Given publicações que falharam recentemente
When `/reports` é renderizada
Then a tabela de falhas exibe a mensagem de erro de cada item e um botão "Retry" que aciona o mesmo fluxo de retry da fila (Sub-B).

---

## Transversal (todas as sub-issues)

**CA-T1 — Build sem erros de TypeScript**
Given o código completo das 4 sub-issues integrado
When `npm run build` é executado
Then o build completa sem erros de TypeScript.

**CA-T2 — Testes unitários dos serviços principais**
Given os serviços `AuthService`, `ProductsService`, `QueueService`, `SettingsService`, `ReportsService`
When a suíte de testes unitários (Jasmine/Karma ou Jest) é executada
Then cobre os fluxos principais de cada serviço (chamadas HTTP, tratamento de erro, armazenamento de token no caso do `AuthService`), sem exigência de testes e2e/Playwright nesta issue.

**CA-T3 — Nenhuma responsividade mobile/tablet exigida**
Given o escopo desta issue (decisão do Gate 1: desktop-only)
When o código é revisado
Then não há exigência de breakpoints mobile/tablet — layout otimizado para desktop.

**CA-T4 — Nenhum ajuste retroativo no contrato da API #11**
Given o comportamento de mascaramento em `/settings` (CA-C1/CA-C2/CA-C3 desta issue)
When o código é revisado
Then nenhuma alteração é feita no contrato de `PUT /api/settings/{key}` da Issue #11 — a lógica de "não sobrescrever campo vazio" é resolvida inteiramente no front, não disparando a chamada para chaves em branco.
