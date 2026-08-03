---
issue: 15
titulo: feat: Deploy Oracle Cloud + SSL + Dominio
etapa_atual: QA aprovado
rota: normal
repo: omuletachou
ultimo_agente: qa
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
code_review_homolog_pr: 127
qa_status: aprovado
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

## Fix — NEXT_PUBLIC_API_URL build-time (PR #127 code review) (2026-08-03)

Bug apontado pelo `/code-review` no PR #127 (comentário
https://github.com/DQM-BETA/omuletachou/pull/127#issuecomment-5166321017): `NEXT_PUBLIC_API_URL`
estava definida apenas em `environment:` (runtime) no serviço `website` do `docker-compose.yml`,
mas o Next.js embute variáveis `NEXT_PUBLIC_*` no bundle do browser em **build time**
(`npm run build`) — o `website/Dockerfile` não tinha `ARG`/`ENV` correspondente antes do build, e
o `docker-compose.yml` não passava `args:` no `website.build:`. Resultado: valor vazio/undefined no
bundle real, quebrando a subscription de push (`website/lib/push.ts`, Issue #14) — o mesmo bug que
o PR #127 deveria ter corrigido, só que silenciosamente.

**Fix aplicado** (branch `fix/127-nextpublic-api-url`, a partir de `desenv`):
- `docker-compose.yml`: adicionado `build.args: NEXT_PUBLIC_API_URL: ${API_PUBLIC_URL}` no serviço
  `website` (build-time). Mantida a mesma variável em `environment:` (runtime) por completude de
  inspeção via `docker inspect` — comentário no arquivo deixa explícito que o valor que importa
  para o bundle do browser é o de `args`, não o de `environment`.
- `website/Dockerfile`: adicionado `ARG NEXT_PUBLIC_API_URL` + `ENV NEXT_PUBLIC_API_URL=$NEXT_PUBLIC_API_URL`
  no estágio `build`, antes de `RUN npm run build`.
- Confirmado por leitura de `website/lib/push.ts` que o único consumo client-side é via
  `NEXT_PUBLIC_API_URL` (nenhum uso novo introduzido); `website/lib/api.ts` só menciona
  `NEXT_PUBLIC_*` em comentário (server-only, não é consumo real).
- Validação real com Docker: `.env` de teste com `API_PUBLIC_URL=https://api-teste.omuletachou.com.br`,
  `docker compose build website` executado com sucesso, e
  `grep -rl 'api-teste.omuletachou.com.br' /app/.next/static/` dentro do container **encontrou o
  valor embutido** em `chunks/app/layout-7e699a36f804729e.js` — confirma que o build-time embedding
  funciona de fato, não apenas por inspeção de configuração.
- `npm test` (website, Jest): **79/79 aprovados**, nenhuma regressão.
- Containers/imagens de teste (`docker compose down -v`, `docker image rm`) e `.env` de teste
  removidos ao final — nenhum resíduo no repo ou na máquina.

PR **#128 squash-merged em `desenv`** (commit `942c9d8`, branch remota `fix/127-nextpublic-api-url` deletada).

## Líder Técnico — Merge PR #128 e verificação de propagação para PR #127 (2026-08-03)

- `gh pr diff 128` revisado: `docker-compose.yml` (`build.args.NEXT_PUBLIC_API_URL`) e
  `website/Dockerfile` (`ARG`/`ENV` antes de `RUN npm run build`) conferem com o fix descrito.
  PR #128 confirmado `MERGEABLE`/`CLEAN`.
- PR #128 squash-merged em `desenv` (commit `942c9d8`), branch remota deletada, working copy
  local sincronizada (`git pull`).
- **PR #127 (desenv→homolog) já reflete o fix automaticamente** — confirmado via
  `gh pr diff 127 --name-only`: `docker-compose.yml` e `website/Dockerfile` aparecem na lista
  (GitHub recalcula o diff de PR aberto quando a branch head avança). Nenhum ajuste manual
  necessário.
- **Revalidação de boot real (item 6 do spawn) NÃO executada por este agente**: rodar
  `docker compose up -d --build` e inspecionar o bundle gerado é execução de código de
  aplicação, fora do escopo de ferramentas do Líder Técnico (`Bash` restrito a
  git/gh/movimentação de arquivos — nunca build/boot de app; ver CLAUDE.md → papel do LT). A
  evidência de validação real já existe e foi documentada pelo Dev na seção "Fix —
  NEXT_PUBLIC_API_URL build-time" acima (build da imagem + `grep` no bundle `.next/static/`
  confirmando o valor embutido + 79/79 testes). Recomenda-se que a segunda rodada de
  **Code Review** (que tem escopo de build/boot/testes) reexecute essa validação como parte da
  checagem do PR #127 já com o fix incorporado.

## Code Review — PR #127 (validação final) (2026-08-03)

Segunda camada (execução real) sobre o PR #127 (`desenv`→`homolog`), após o fix do PR #128
(commit `942c9d8`) já incorporado e reverificado pelo plugin `/code-review`
(https://github.com/DQM-BETA/omuletachou/pull/127#issuecomment-5166443181 — sem novos
problemas).

**Achados do plugin `/code-review` incorporados ao veredito:**
- 1ª rodada: achou o bug real do `NEXT_PUBLIC_API_URL` (env de runtime em vez de build-arg) —
  corrigido no PR #128.
- 2ª rodada (reverificação): confirmou o fix correto, nenhum novo problema.

**Boot real completo (a partir de `desenv`, head do PR #127):**
- `.env` de teste criado na raiz (baseado no `.env.example`, valores fake
  `*-teste.omuletachou.com.br`) — nunca commitado (confirmado `git check-ignore -v .env` →
  ignorado por `.gitignore:13`).
- `docker compose up -d --build`: as 5 imagens buildam sem erro (api, website, dashboard,
  db, nginx-proxy-manager).
- Ajuste local apenas de execução (não commitado, revertido ao final): portas 80/443/81 do
  host Windows já ocupadas por outro processo (`netstat` → PID 4/System) — portas do
  `nginx-proxy-manager` remapeadas temporariamente para 18080/18443/18081 só para rodar o
  teste local; `docker-compose.yml` restaurado ao original via backup (`diff` limpo após).
  Não é um problema do PR — é conflito de porta local do ambiente Windows do Code Review, os
  binds `80:80`/`443:443`/`81:81` do compose estão corretos para a VM Oracle (Linux).

**`docker compose ps` (evidência):**
```
afiliado_api         Up (healthy)   8080/tcp
afiliado_dashboard   Up             80/tcp
afiliado_db          Up (healthy)   5432/tcp
afiliado_npm         Up             0.0.0.0:18080->80/tcp, 0.0.0.0:18081->81/tcp, 0.0.0.0:18443->443/tcp
afiliado_website     Up             3000/tcp
```
Confirmado: nenhum serviço além do `nginx-proxy-manager` publica porta ao host (api/db/website/
dashboard só aparecem com porta interna `container/tcp`, sem bind `0.0.0.0:`).

**Validação específica do fix (item mais crítico):** `docker compose exec website sh -c
'grep -orl "api-teste" /app/.next/static'` → encontrou
`/app/.next/static/chunks/app/layout-7e699a36f804729e.js` contendo
`api-teste.omuletachou.com.br`. Confirma que `API_PUBLIC_URL` de teste foi embutido no bundle
JS do browser em build-time (não apenas env var de runtime) — o bug original do plugin
`/code-review` está corrigido e comprovado empiricamente, não só por leitura de código.

**`curl` na imagem da API:** `docker compose exec api curl --version` → `curl 7.88.1`
instalado corretamente (healthcheck do compose depende disso).

**Nginx Proxy Manager UI:** `curl -s -o /dev/null -w "%{http_code}" http://localhost:18081/`
→ `HTTP 200`. Sobe corretamente (porta 81 real, remapeada só para o teste local).

**Suítes de teste (todas passando, acima do mínimo exigido):**
- Backend: `dotnet test` → 306/306 aprovados (mínimo 306 ✓).
- Website: `npm test` → 79/79 aprovados, 14 suites (mínimo 79 ✓).
- Dashboard: `npm test -- --watch=false --browsers=ChromeHeadless` → 105/105 aprovados
  (mínimo 105 ✓).

**Checklist de veto:**
- Compila e sobe: ✓ (build + boot completos, 5/5 serviços, db/api healthy).
- Integração real: ✓ (boot real via Docker Compose, não mock — é o critério de aceite desta
  issue de infra, já que não há VM Oracle provisionada para SSH real).
- Conformidade com spec: ✓ (`criterios-aceite.md`/`design.md` — compose consolidado,
  `deploy.sh`, `.env.example`, runbook, apenas NPM publica porta, healthchecks db/api,
  `curl` na imagem API, build-arg do Next.js).
- Sem teste-lixo, sem segredo commitado (`.env` de teste nunca versionado, confirmado
  `git status` limpo antes/depois), cobertura ≥ 80% mantida nos 3 projetos.
- `.first()`/`.nth()`/`.last()` em specs E2E: busca em `*.spec.ts`/`*.e2e.ts` não
  encontrou nenhuma ocorrência — não aplicável a este PR (sem specs E2E estruturais alterados).

**Limpeza pós-validação:** `docker compose down -v` (containers + volumes removidos),
`docker-compose.yml` restaurado ao original (backup + restore, `git diff` limpo), `.env` de
teste removido. Working tree limpo, sem artefatos deixados para trás.

**Veredito: APROVADO.** Todos os itens do checklist de veto comprovados por execução real
(não "parece ok"). Merge `desenv`→`homolog` autorizado.

## QA — homolog (2026-08-03)

Validação independente a partir de `homolog` (commit `9a28436`, PR #127 já mergeado com o fix
do PR #128 incorporado). Repo sincronizado via `git fetch origin && git checkout homolog &&
git pull origin homolog` antes de qualquer teste; commit confirmado presente em `git log`.

**Suítes automatizadas (re-executadas a partir de `homolog`, sem regressão):**
- Backend: `dotnet test` → **306/306 aprovados**, 0 falhas.
- Website: `npm test` → **79/79 aprovados**, 14 suites.
- Dashboard: `npm test -- --watch=false --browsers=ChromeHeadless` → **105/105 aprovados**.

Números idênticos aos reportados pelo Code Review — confirma que nada regrediu entre a
validação do Code Review e esta.

**Validação integrada (boot real, independente do Code Review) — `docker compose up -d --build`
a partir de `homolog`:**
- `.env` de teste criado a partir do `.env.example` (valores fake, domínio
  `api-qateste.omuletachou.com.br`) — nunca commitado (`git check-ignore -v .env` confirma
  ignorado por `.gitignore:13`).
- Ajuste local **apenas de execução** (não commitado): portas `80`/`443`/`81` do host Windows
  já ocupadas por `HTTP.sys` (PID 4/System) — `nginx-proxy-manager` remapeado temporariamente
  para `18080`/`18443`/`18081` só para rodar o teste local. `docker-compose.yml` restaurado ao
  original via backup + `git diff` limpo ao final. Não afeta a validação: os binds `80:80`/
  `443:443`/`81:81` do compose commitado estão corretos para a VM Oracle (Linux).
- `docker compose ps` (após ~15s): **5/5 serviços `Up`**, `afiliado_db` e `afiliado_api`
  `(healthy)`. Nenhum serviço além de `nginx-proxy-manager` publica porta ao host — confirmado
  também via `docker compose ps --format json` (`PublishedPort: 0` em `api`/`db`/`website`/
  `dashboard`; só `nginx-proxy-manager` com `PublishedPort` != 0 em 80/443/81).
- `docker compose exec api curl --version` → `curl 7.88.1` presente na imagem final da API
  (pré-requisito do healthcheck).
- `docker compose exec api curl -f http://localhost:8080/health` → `200`,
  `{"status":"healthy",...}`.
- **Fix `NEXT_PUBLIC_API_URL` (build-time) reconfirmado de forma independente**:
  `docker compose exec website sh -c "grep -orl 'api-qateste' /app/.next/static"` encontrou o
  valor embutido em `chunks/app/layout-e75c14b71b4cb7cf.js` — o bug original do `/code-review`
  (env de runtime em vez de build-arg) segue corrigido, comprovado por execução, não só leitura.
- `curl -s -o /dev/null -w "%{http_code}" http://localhost:18081/` → `200` (UI de admin do NPM
  acessível). `curl` na raiz (`18080`) → `200` (página padrão do NPM).
- Limpeza: `docker compose down -v` (containers + volumes removidos), `docker-compose.yml`
  restaurado ao original (`git diff` limpo), `.env` de teste removido, imagens de teste
  (`omuletachou-api`/`omuletachou-website`/`omuletachou-dashboard`) removidas. `git status`
  limpo ao final (exceto `.worktrees/` pré-existente, não relacionado a esta validação).

**Revisão do `runbook-deploy.md` como documento** (critério de aceite "Runbook"):
Cobre, sem lacunas identificadas, todos os itens exigidos: provisionamento da VM (shape
`VM.Standard.A1.Flex`, Ubuntu 22.04, Security List 22/80/443 permanentes + 81 temporária —
§1), instalação de Docker/Compose (§2), registro de domínio + DNS com os 4 registros A e
verificação via `dig` antes de prosseguir para SSL (§3), clone + configuração de segredos de
infra via `.env` (§4), primeiro `deploy.sh` (§5), configuração do Nginx Proxy Manager com
Let's Encrypt via UI — 3 proxy hosts, tabela clara de domain/forward/porta (§6), fechamento da
porta 81 pós-setup com procedimento de reabertura pontual documentado (§7), preenchimento de
segredos de integrações externas via dashboard/`app_settings` (§8), checklist de verificação
pós-deploy cobrindo containers `Up`, SSL válido, `/health` 200, dashboard carrega, `/hangfire`
com senha (§9), e rollback via `git checkout` do compose + `docker compose up -d --build`, sem
tocar `postgres_data` (§10). Passo a passo executável por alguém sem conhecimento prévio do
projeto — nenhuma lacuna óbvia encontrada.

**Critérios de aceite — tabela de veredito:**

| Critério | Evidência | Status |
|---|---|---|
| Compose consolidado — 5 serviços sobem saudáveis | `docker compose ps`: 5/5 `Up`, db/api `healthy` | ✅ |
| Nenhum serviço de app publica porta ao host | `PublishedPort: 0` em api/db/website/dashboard | ✅ |
| Só `nginx-proxy-manager` publica porta (80/443) | Único com `PublishedPort` != 0 (80/443/81) | ✅ |
| `.env.example` completo, nomenclatura consistente | Inspeção: DB_USER/PASSWORD/JWT/domínio presentes | ✅ |
| `deploy.sh` idempotente, sem credencial hardcoded | Inspeção de código: `set -euo pipefail`, lê `.env`, `git pull --ff-only` + `up -d --build` | ✅ |
| SSL/Let's Encrypt automático (documentado) | Runbook §6 — não testável sem domínio real, conforme nota geral dos critérios | ✅ (documental) |
| Rede interna — api/dashboard/website/db só via Docker | Confirmado nas portas internas do compose e nos testes acima | ✅ |
| Segredos de integrações via dashboard (`app_settings`) | Runbook §8, mesmo padrão da Issue #11 | ✅ |
| `.env` no `.gitignore` | `.gitignore:13` + `git check-ignore -v` confirmado | ✅ |
| Runbook completo (VM, DNS, Docker, NPM+SSL, segredos, checklist, rollback) | Revisão de documento, sem lacunas | ✅ |
| Rollback sem afetar `postgres_data` | Runbook §10 — comando não usa `-v`, explícito | ✅ |
| Firewall — só 22/80/443 permanentes | Runbook §1.5, consistente com compose | ✅ |
| Hangfire — só via HTTPS + senha, sem porta extra | Runbook §9 checklist, sem proxy host adicional | ✅ (documental) |

**Suítes automatizadas**: 306/79/105 aprovados, sem regressão (idêntico ao Code Review).

**Veredito: APROVADO.** Todos os critérios de aceite validados — os itens artefatuais por
execução real independente (boot completo, isolamento de portas, fix confirmado no bundle,
`curl`/health/UI do NPM), os itens de VM real (SSL ao vivo, Security List físico) por revisão
documental do runbook, conforme a nota geral dos critérios de aceite desta issue de infra.
Nenhum bloqueio. PR `homolog`→`main` autorizado.

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
| 8 | Merge #125 (PR #126) + PR homologação #127 | lt | sonnet | 71808 | 54 | 593s | 2026-07-30 |
| 9 | Fix NEXT_PUBLIC_API_URL (code review PR #127) | dev-nodejs | sonnet | 60788 | 36 | 414s | 2026-08-03 |
| 10 | Merge PR #128 + verificação propagação PR #127 | lt | sonnet | 51005 | 21 | 178s | 2026-08-03 |
| 11 | Code Review — validação final PR #127 (build+boot+testes) | code-review | sonnet | 81301 | 45 | 602s | 2026-08-03 |
| 12 | QA — homolog (aprovado) | qa | sonnet | 69397 | 30 | 323s | 2026-08-03 |
