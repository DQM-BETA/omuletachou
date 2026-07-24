---
issue: 15
titulo: feat: Deploy Oracle Cloud + SSL + Dominio
etapa_atual: Gate 1 (Gerente)
rota: normal
repo: omuletachou
ultimo_agente: pm-analista-negocios
status_comment_id: 5074045241
openspec_change: ~
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

## Custo (ledger)

| # | Etapa | Agente | Modelo | Tokens | Tools | Tempo (s) | Data |
|---|---|---|---|---|---|---|---|
| 1 | Preparacao | Coordenador | haiku-4.5 | 24745 | 18 | 108s | 2026-07-24 |
| 2 | PM Fase 1 | pm | sonnet | 29560 | 9 | 68s | 2026-07-24 |
