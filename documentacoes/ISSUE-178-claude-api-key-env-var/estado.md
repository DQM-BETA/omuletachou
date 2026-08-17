---
issue: 178
titulo: "bug: Claude__ApiKey nunca chega no container da API (docker-compose.yml/.env.example/runbook incompletos)"
etapa_atual: Code Review (PR desenv->homolog aberto)
rota: rapido
ultimo_agente: lt
openspec_change: ~
tech_stacks:
  - dotnet
repos:
  omuletachou: repos/omuletachou
repo_path: repos/omuletachou
docs_path: repos/omuletachou/documentacoes/ISSUE-178-claude-api-key-env-var
openspec_path: ~
sub_issues: []
desenv_tasks_merged: []
pr_feature: 179
pr_homologacao: 180
pr_release: ~
code_review_homolog_pr: ~
qa_status: ~
figma_url: ~
blockers: nenhum
status_comment_id: 5317053323
createdAt: "2026-08-17"
---

## Contexto

Bug de infra: variáveis de ambiente `Claude__ApiKey` e `Claude__Model` nunca chegam no container da API.

## Achado relacionado

`claude.api_key`/`claude.model` seedados em `app_settings` (ids 20/21) são código morto — nenhum lugar do sistema lê. Documentado no runbook-deploy.md (§8) e aqui; NÃO removido (fora de escopo deste fix) — registrar para triagem futura (ex.: melhoria em `.claude/melhorias/` ou nova issue de limpeza).

## O que foi feito (Dev, rota rapido)

Branch `fix/ISSUE-178-claude-api-key-env` (base `desenv`), worktree isolado, removido ao final.

- `docker-compose.yml` (serviço `api`): adicionadas `Claude__ApiKey: "${CLAUDE_API_KEY:-}"` e `Claude__Model: "${CLAUDE_MODEL:-}"` (opcional; vazio usa o default hardcoded `claude-haiku-4-5-20251001` em `AnthropicClientWrapper.cs`, confirmado lendo o código antes do fix).
- `.env.example`: adicionadas `CLAUDE_API_KEY=` e `CLAUDE_MODEL=` com comentários (formato `sk-ant-...`, obrigatória para scoring/legenda/categorização por IA, opcional o model).
- `documentacoes/ISSUE-15-deploy-oracle-ssl-dominio/runbook-deploy.md` §4: `CLAUDE_API_KEY` adicionada à lista de segredos a preencher no `.env` durante o deploy. §8 corrigido: removida menção a "Claude API" na lista de integrações configuráveis via dashboard Settings (era enganosa — a chave nunca foi lida de `app_settings`, só da env var) e adicionado parágrafo explicando a exceção + o achado do código morto.
- Código de `Program.cs`/`AnthropicClientWrapper.cs` NÃO alterado (fix é 100% de configuração/infra, conforme escopo).

**Validação:**
- `docker compose up -d --build db api` com `.env` de teste (`CLAUDE_API_KEY=sk-ant-test-fake-para-validar-wiring`) → `docker exec afiliado_api env | grep Claude` confirmou `Claude__ApiKey=sk-ant-test-fake-para-validar-wiring` e `Claude__Model=` presentes no container; `afiliado_api` subiu `healthy` (boot do DI sem exceção).
- `dotnet test` (backend): 414/414 passando, sem regressão (fix não toca lógica de código, apenas config).
- Ambiente Docker de teste limpo ao final (`docker compose down -v` + imagem local removida; `.env` de teste não commitado).

PR: https://github.com/DQM-BETA/omuletachou/pull/179 (feature→desenv, mergeado via squash pelo Líder Técnico).
PR homologação: https://github.com/DQM-BETA/omuletachou/pull/180 (desenv→homolog, aberto — aguardando Code Review + QA).

## Custo (ledger)

| # | Etapa | Agente | Modelo | Tokens | Ferramentas | Tempo (s) |
|---|-------|--------|--------|--------|-------------|-----------|
| 1 | Coordenador (preparação) | Coordenador | Haiku | 30212 | 34 | 227s |
| 2 | Dev (fix env vars docker-compose/.env.example/runbook, PR #179) | Dev .NET | Sonnet | 56481 | 37 | 284s |
| 3 | LT (merge PR #179 + PR homologação #180) | Líder Técnico | Sonnet | 37240 | 11 | 82s |

