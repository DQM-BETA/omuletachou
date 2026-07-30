---
issue: 15
titulo: feat: Deploy Oracle Cloud + SSL + Dominio
etapa_atual: Refinamento Técnico
rota: normal
repo: omuletachou
ultimo_agente: pm-analista-negocios
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

## Custo (ledger)

| # | Etapa | Agente | Modelo | Tokens | Tools | Tempo (s) | Data |
|---|---|---|---|---|---|---|---|
| 1 | Preparacao | Coordenador | haiku-4.5 | 24745 | 18 | 108s | 2026-07-24 |
| 2 | PM Fase 1 | pm | sonnet | 29560 | 9 | 68s | 2026-07-24 |
