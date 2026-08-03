---
issue: 15
titulo: feat: Deploy Oracle Cloud + SSL + Dominio
etapa_atual: Code Review
rota: normal
repo: omuletachou
ultimo_agente: lider-tecnico
status_comment_id: 5074045241
openspec_change: repos/omuletachou/openspec/changes/issue-15-deploy-oracle-ssl-dominio
tech_stacks: [infra]
repos: {}
repo_path: repos/omuletachou
docs_path: repos/omuletachou/documentacoes/ISSUE-15-deploy-oracle-ssl-dominio
openspec_path: repos/omuletachou/openspec/changes/issue-15-deploy-oracle-ssl-dominio
sub_issues: ["#125 (stack:infra, task_id:Sub-A)"]
desenv_tasks_merged: ["#125"]
sub_issues_frontend: {}
pr_homologacao: 127
pr_release: ~
code_review_homolog_pr: ~
qa_status: ~
figma_url: ~
blockers: nenhum
closedAt: ~
---

## Contexto
Deploy completo no Oracle Cloud Free Tier com HTTPS, domínio personalizado e todos os containers rodando.

## PM Fase 1 — Levantamento (2026-07-24)
Issue de infraestrutura/deploy de produção real (diferente das anteriores, que eram features de código). Levantamento postado na Issue #15 (comentário https://github.com/DQM-BETA/omuletachou/issues/15#issuecomment-5074076622) cobrindo:
1. Acesso ao servidor (VM já provisionada? SSH key/IP/usuário disponíveis ao pipeline, ou deploy fica sob operação manual do Gerente?)
2. Domínio e DNS (registro já existe? quem configura os registros A/CNAME?)
3. SSL (confirma Nginx Proxy Manager + Let's Encrypt automático)
4. Estratégia de deploy (compose por repo via git clone, ou compose consolidado? CI/CD automático ou manual?)
5. Segredos de produção (preenchimento manual pós-boot ou mecanismo de secrets?)
6. Firewall/portas (apenas 22/80/443, ou porta de admin adicional restrita?)
7. Rollback (estratégia esperada para o primeiro deploy?)
8. Confirmação de dependência com Issue #14 (PWA + Push liberados por este deploy)

Aguardando resposta do Gerente (Gate 1) antes de seguir para Fase 2 (PRD/proposal.md/critérios de aceite).

## PM Fase 2 — PRD e avaliação de ambiguidade (2026-07-30)
Gerente respondeu ao Gate 1 (comentário https://github.com/DQM-BETA/omuletachou/issues/15#issuecomment-5135745488): VM e domínio ainda não provisionados (pré-requisitos manuais do Gerente, primeira tarefa da issue); deploy manual sem SSH pelo pipeline; SSL confirmado via Nginx Proxy Manager + Let's Encrypt; compose consolidado na raiz do monorepo + `deploy.sh` manual (CI/CD fora de escopo); segredos de infra via `.env` na VM, integrações externas via dashboard (`app_settings`); firewall restrito a 22/80/443, containers só em rede Docker interna; rollback via `git checkout` + `docker compose up -d --build`, sem tocar `postgres_data`; confirma dependência com Issue #14 (PWA + Push exigem HTTPS real).

Implicação de escopo: pipeline não tem acesso SSH à VM real -> entregável é documentação + scripts + config versionada (compose, `deploy.sh`, runbook), validados via `docker compose` local — não deploy ao vivo. `proposal.md` e `criterios-aceite.md` adaptados para refletir validação de artefatos, não de boot real na VM.

PRD escrito em `openspec/changes/issue-15-deploy-oracle-ssl-dominio/proposal.md` e `documentacoes/ISSUE-15-deploy-oracle-ssl-dominio/criterios-aceite.md`.

**Avaliação de ambiguidade arquitetural: SIM.** Já existe um `docker-compose.yml` na raiz (issues anteriores) que expõe portas diretamente ao host (5432/5000/3000/4200), incompatível com o requisito de portas 22/80/443 apenas — precisa ser reestruturado. Três pontos exigem decisão de arquitetura antes do refinamento do LT: (a) como reestruturar o compose consolidado existente para rede interna + Nginx Proxy Manager sem quebrar os builds já existentes; (b) desenho da rede Docker/isolamento e exposição (ou não) da porta de admin do NPM; (c) esquema de nomeação de variáveis de ambiente entre os 4 serviços, incluindo a URL pública que hoje aponta para `localhost:5000` (incompatível com produção) — e se a estratégia de subdomínios da spec original ainda vale ou se muda para path routing sob um único domínio. Detalhes completos na seção "Nota de escalonamento" do `proposal.md`. Decisão: escalar para o **Arquiteto**.

## Arquiteto — Design técnico (2026-07-30)
Design completo em `openspec/changes/issue-15-deploy-oracle-ssl-dominio/design.md`. Resumo técnico postado na Issue #15 (comentário https://github.com/DQM-BETA/omuletachou/issues/15#issuecomment-5135809936).

Inspecionado o código real antes de decidir: `dashboard/nginx.conf` já faz `proxy_pass` para `http://api:8080/api/` (proxy interno já existente), `website/lib/api.ts` já usa `API_INTERNAL_URL` server-side (nunca exposto ao browser), `website/lib/push.ts` usa `NEXT_PUBLIC_API_URL` client-side (precisa de host público real), e `backend/.../Cors/CorsConfigurator.cs` + `appsettings.json` já hardcodeiam `dashboard.omuletachou.com.br`/`omuletachou.com.br`/`www.omuletachou.com.br` na allowlist de CORS desde a Issue #11 — subdomínios já eram o pressuposto implícito do código.

**Decisões**:
1. **Compose consolidado**: manter um único `docker-compose.yml`; remover `ports:` de `db`/`api`/`website`/`dashboard`; adicionar serviço `nginx-proxy-manager` (imagem `jc21/nginx-proxy-manager`) como único ponto com portas publicadas (80/443, +81 temporária). Nenhum Dockerfile muda. Adiciona healthcheck em `db`/`api` (API já expõe `/health`) + `depends_on: condition: service_healthy` para corrigir corrida de inicialização.
2. **Rede Docker**: rede única `omuletachou_net` (bridge custom) para todos os serviços + NPM — segmentação `frontend`/`backend` rejeitada por adicionar complexidade sem ganho real de segurança neste contexto (squad pequena/solo, nenhum serviço interno exposto de qualquer forma). Porta de admin do NPM (81) exposta só durante o setup inicial, fechada depois (procedimento documentado no runbook).
3. **Variáveis de ambiente**: infra existente (`DB_USER`, `JWT_SIGNING_KEY`, `SEED_USER_*`) mantida sem prefixo (sem colisão, evita diff em código estável); variáveis novas de URL pública com sufixo `_PUBLIC_URL` (`WEBSITE_PUBLIC_URL`, `DASHBOARD_PUBLIC_URL`, `API_PUBLIC_URL`). **Confirma subdomínios** (não path-routing) — path-routing exigiria reescrever `dashboard/nginx.conf`, `base href` do Angular e roteamento do Next.js, fora do escopo de infraestrutura. `NEXT_PUBLIC_API_URL` passa a apontar para `API_PUBLIC_URL`.

Diagrama textual da topologia de rede/portas, riscos e justificativas completas no `design.md`. Pronto para o refinamento técnico do Líder Técnico.

## Líder Técnico — Refinamento (2026-07-30)
Refinamento técnico concluído. Documentação escrita:
- `documentacoes/ISSUE-15-deploy-oracle-ssl-dominio/especificacao-tecnica.md` — estrutura exata do `docker-compose.yml` consolidado (com healthcheck db/api, rede `omuletachou_net`, serviço `nginx-proxy-manager`), `deploy.sh` (idempotente, `set -euo pipefail`, falha cedo sem `.env`), `.env.example` (novas `DOMAIN_ROOT`/`WEBSITE_PUBLIC_URL`/`DASHBOARD_PUBLIC_URL`/`API_PUBLIC_URL`), e passos de validação local (sem VM).
- `documentacoes/ISSUE-15-deploy-oracle-ssl-dominio/runbook-deploy.md` — runbook operacional completo (VM Oracle, DNS/registro.br, instalação Docker, configuração NPM + Let's Encrypt via UI, fechamento da porta 81 pós-setup, preenchimento de segredos, checklist de verificação, rollback) — já satisfaz o critério de aceite que referenciava este arquivo.
- `openspec/changes/issue-15-deploy-oracle-ssl-dominio/tasks.md` — critérios de aceite (Given/When/Then) da sub-issue + raciocínio de fatiamento.

**Task breakdown**: avaliado dividir por tipo de artefato (compose / script / env) e rejeitado — os 3 artefatos são pequenos, compartilham as mesmas variáveis e não têm fronteira de PR/teste independente (critérios de aceite exigem `docker compose up -d --build` com os três já consistentes; um PR isolado de `.env.example` não é testável sozinho). Mantida **uma única sub-issue coesa**: #125 "Sub-A: Artefatos de deploy — compose, script, .env" (label `infra`).

**UX/UI**: não acionado — issue de infraestrutura de deploy/proxy/DNS, sem componente de UI, tela ou fluxo visual novo nos 4 serviços.

**Escolha de Dev**: indicado **dev-dotnet**. Justificativa: a squad não tem um dev genérico de infra (devs são por stack de aplicação: angular/dotnet/nodejs/python/react-native); o `devops` da squad é só diagnóstico, não implementa (fronteira definida em CLAUDE.md). O trabalho é majoritariamente compose/bash na raiz do monorepo, mas a interseção mais direta é com o backend: o healthcheck do `api` depende do endpoint `/health` já existente e pode exigir ajuste mínimo no `Dockerfile` do backend (instalar `curl`, documentado na especificação técnica) — dev-dotnet é quem mais entende o comportamento de boot/health da API do qual o healthcheck do compose passa a depender.

Sumário técnico postado na Issue #15 (comentário https://github.com/DQM-BETA/omuletachou/issues/15#issuecomment-5135860550). Comentário 📍 Status atualizado para "Em Desenvolvimento".

## Dev Sub-A #125 — Artefatos de deploy (2026-07-30)

Implementado exatamente conforme `especificacao-tecnica.md`:
- `docker-compose.yml` reestruturado: removidas `ports:` de `db`/`api`/`website`/`dashboard`; todos
  os 4 serviços + novo serviço `nginx-proxy-manager` (imagem `jc21/nginx-proxy-manager:latest`) na
  rede interna `omuletachou_net` (bridge); NPM é o único serviço com portas publicadas (80/443/81,
  81 comentada como temporária no próprio arquivo). Healthcheck `pg_isready` em `db` e
  `curl -f http://localhost:8080/health` em `api`, com `depends_on: db: condition: service_healthy`
  no `api`. `website` recebe `NEXT_PUBLIC_API_URL: ${API_PUBLIC_URL}` no lugar do
  `http://localhost:5000` hardcoded.
- `backend/src/AfiliadoBot.Api/Dockerfile`: adicionado `apt-get install curl` no estágio final —
  exceção documentada na especificação técnica, necessária porque não foi possível confirmar ao
  vivo (`docker compose exec api curl --version`) se a imagem `mcr.microsoft.com/dotnet/aspnet:8.0`
  já traz `curl`, dado o ambiente local sem Docker funcional (ver limitação abaixo). Instalar
  explicitamente é a opção mais segura/idempotente independente da imagem base.
- `.env.example` atualizado com `DOMAIN_ROOT`, `WEBSITE_PUBLIC_URL`, `DASHBOARD_PUBLIC_URL`,
  `API_PUBLIC_URL`, mantendo as variáveis de infraestrutura existentes sem prefixo.
- `deploy.sh` criado na raiz conforme especificação (idempotente, `set -euo pipefail`, falha cedo
  sem `.env`, `git pull --ff-only` + `docker compose up -d --build`), marcado executável
  (`chmod +x` + `git update-index --chmod=+x`, confirmado `100755` no índice).
- `.gitignore` já listava `.env` — nenhuma mudança necessária (conferido).
- Nenhuma mudança em `website/lib/push.ts`: já usa `NEXT_PUBLIC_API_URL` (client-side) desde
  antes desta issue — o Arquiteto já havia inspecionado isso no `design.md`. `CorsConfigurator.cs`
  e `appsettings.json` já listam os subdomínios de produção corretos, sem mudança necessária.

**⚠️ Limitação relevante para Code Review/QA — Docker Desktop local indisponível.** O ambiente
local de execução deste Dev teve o Docker Desktop com um bug de infraestrutura da própria máquina
(arquivos AF_UNIX/reparse-point órfãos em `%LOCALAPPDATA%\Docker\run\` — `dockerInference`,
`dockerEthernetVfkit`, `userAnalyticsOtlpHttp.sock` — travados por um crash anterior do backend,
"file cannot be accessed by the system" mesmo após matar todos os processos Docker e `wsl
--shutdown`; provável necessidade de reboot do host para liberar os handles). Não foi possível
subir os containers (`docker compose up -d --build`) nem validar healthchecks/portas ao vivo.
**Validação alternativa realizada, sem daemon:**
- `docker compose config` — renderiza o compose completo (interpolação de variáveis do `.env`
  incluída) sem erros: confirma sintaxe válida, `NEXT_PUBLIC_API_URL` resolvido corretamente para
  `https://api.omuletachou.com.br`, healthchecks/`depends_on: condition` presentes, e que **apenas**
  `nginx-proxy-manager` tem `ports:` publicadas (80/443/81) — `api`/`db`/`website`/`dashboard` sem
  nenhuma porta mapeada ao host.
- Revisão manual linha a linha contra `especificacao-tecnica.md` §1 (compose já reproduzido acima).
- `dotnet test` (backend): **306/306 aprovados**, 0 falhas — nenhuma regressão pelas mudanças de
  infra (nenhum código de app alterado, exceto Dockerfile).
- `npm test` (website, Jest): **79/79 aprovados**.
- `npm test` (dashboard, Karma/Jasmine, ChromeHeadless): **105/105 aprovados**.
- Não foi possível: boot real dos 4 serviços + NPM, validar `curl` de fato instalado na imagem
  final da API, acessar a UI do NPM localmente, ou confirmar healthcheck `healthy` em runtime.
  **Recomenda-se que o Code Review/QA tente reproduzir a subida via `docker compose up -d --build`
  num ambiente com Docker funcional antes de aprovar** — esta sub-issue entrega os artefatos
  estaticamente corretos e testados o quanto foi possível localmente, mas o critério de aceite
  "os 4 serviços + NPM sobem saudáveis" não foi validado por boot real neste PR.

PR aberto: `feature/125-deploy-artifacts` → `desenv`.

## Líder Técnico — Merge Sub-A #125 e validação de boot real (2026-08-03)

Conflito trivial em `estado.md` resolvido (edições aditivas concorrentes: linha de custo do Dev
Sub-A #125 na branch do PR + linha de custo do DevOps já em `desenv`) — ambas mantidas, sem
duplicação (merge de `origin/desenv` na branch `feature/125-deploy-artifacts`, commit `9e197f9`).

**Validação de boot real** (Docker Desktop do Dev estava indisponível na implementação; ambiente
corrigido após reboot do host — confirmado `docker ps`/`docker info` funcionais):
- `docker compose up -d --build`: os 5 serviços (`db`, `api`, `website`, `dashboard`,
  `nginx-proxy-manager`) subiram sem erro (usando um remapeamento temporário de portas do host
  para `nginx-proxy-manager`, revertido antes do merge — porta 80/443 do host local já ocupada por
  outro processo do Windows, HTTP.sys; **não afeta a validação**, que é sobre publicação de portas
  no `docker-compose.yml`, não disponibilidade da porta específica na máquina do LT).
- `db` e `api` reportaram `healthy` (`docker compose ps`): `pg_isready` e
  `curl -f http://localhost:8080/health` passando.
- `docker compose ps` confirmou **nenhum serviço além do `nginx-proxy-manager`** com porta
  publicada ao host (`PublishedPort: 0` em `api`/`db`/`website`/`dashboard`).
- `docker compose exec api curl --version`: `curl 7.88.1` confirmado instalado na imagem final da API.
- UI de admin do Nginx Proxy Manager acessível (HTTP 200).
- `docker compose down -v` ao final; `docker-compose.yml`/`.env` revertidos ao estado commitado
  (nenhuma alteração permanente no repo ou na máquina).

PR #126 squash-merged em `desenv` (commit único, branch remota deletada). Sub-issue #125 fechada.
Como é a única sub-issue da Issue #15, PR de homologação criado: `desenv` → `homolog` (**PR #127**,
merge commit — não squash — na promoção conforme convenção da squad).

## Custo (ledger)

| # | Etapa | Agente | Modelo | Tokens | Tools | Tempo (s) | Data |
|---|---|---|---|---|---|---|---|
| 1 | Preparacao | Coordenador | haiku-4.5 | 24745 | 18 | 108s | 2026-07-24 |
| 2 | PM Fase 1 | pm | sonnet | 29560 | 9 | 68s | 2026-07-24 |
| 3 | PM Fase 2 | pm-analista-negocios | sonnet | 57585 | 34 | 311s | 2026-07-30 |
| 4 | Arquiteto | arquiteto | sonnet | 63814 | 38 | 216s | 2026-07-30 |
| 5 | Refinamento LT | lider-tecnico | sonnet | 74694 | 26 | 306s | 2026-07-30 |
| 6 | Dev Sub-A #125 (PR #126) | dev-dotnet | sonnet | 130908 | 107 | 1631s | 2026-07-30 |
| 7 | DevOps — diagnóstico Docker Desktop | devops | haiku-4.5 | 28592 | 13 | 133s | 2026-07-30 |
