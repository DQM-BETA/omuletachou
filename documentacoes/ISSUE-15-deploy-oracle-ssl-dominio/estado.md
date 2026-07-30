---
issue: 15
titulo: feat: Deploy Oracle Cloud + SSL + Dominio
etapa_atual: Refinamento Técnico
rota: normal
repo: omuletachou
ultimo_agente: arquiteto
status_comment_id: 5074045241
openspec_change: repos/omuletachou/openspec/changes/issue-15-deploy-oracle-ssl-dominio
tech_stacks: []
repos: {}
repo_path: repos/omuletachou
docs_path: repos/omuletachou/documentacoes/ISSUE-15-deploy-oracle-ssl-dominio
openspec_path: repos/omuletachou/openspec/changes/issue-15-deploy-oracle-ssl-dominio
sub_issues: []
desenv_tasks_merged: []
sub_issues_frontend: {}
pr_homologacao: ~
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

## Custo (ledger)

| # | Etapa | Agente | Modelo | Tokens | Tools | Tempo (s) | Data |
|---|---|---|---|---|---|---|---|
| 1 | Preparacao | Coordenador | haiku-4.5 | 24745 | 18 | 108s | 2026-07-24 |
| 2 | PM Fase 1 | pm | sonnet | 29560 | 9 | 68s | 2026-07-24 |
| 3 | PM Fase 2 | pm-analista-negocios | sonnet | 57585 | 34 | 311s | 2026-07-30 |
| 4 | Arquiteto | arquiteto | sonnet | (a preencher pelo orquestrador) | - | - | 2026-07-30 |
