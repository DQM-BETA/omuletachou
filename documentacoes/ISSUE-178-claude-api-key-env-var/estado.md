---
issue: 178
titulo: "bug: Claude__ApiKey nunca chega no container da API (docker-compose.yml/.env.example/runbook incompletos)"
etapa_atual: QA (merge desenv->homolog aprovado, PR #180 mergeado)
rota: rapido
ultimo_agente: code_review
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
code_review_homolog_pr: 180
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
PR homologação: https://github.com/DQM-BETA/omuletachou/pull/180 (desenv→homolog, MERGEADO via merge commit `dc67d03e40f27287ee7b56d65435ff069713bc58`).

## Code Review — PR #180

**APROVADO.** Validação executada ao vivo (2ª camada, não só leitura). 1ª camada (`/code-review` leve, sem achados) já revisada previamente: https://github.com/DQM-BETA/omuletachou/pull/180#issuecomment-5317099636.

- `git fetch && git checkout desenv && git pull origin desenv` — branch limpa, sem divergência.
- Diff do PR (`gh pr diff 180`) conferido: escopo exatamente como descrito — `.env.example` e `docker-compose.yml` (serviço `api`) ganharam `Claude__ApiKey`/`Claude__Model` mapeados de `CLAUDE_API_KEY`/`CLAUDE_MODEL`, e `runbook-deploy.md` §4/§8 corrigido (código morto de `app_settings` documentado, chave não é lida do dashboard). Nenhuma alteração de código de aplicação (`Program.cs`/`AnthropicClientWrapper.cs` intocados), conforme escopo do fix.
- `dotnet test` (backend, `AfiliadoBot.slnx`): **414/414 passando**, 0 falhas — sem regressão.
- Docker real: `docker compose up -d --build db api` com `.env` de teste (`CLAUDE_API_KEY=sk-ant-test-cr-validacao`, `DB_PASSWORD`/`JWT_SIGNING_KEY` fake gerados para o teste). Primeira tentativa falhou por volume `postgres_data` órfão de uma validação anterior com senha diferente (`password authentication failed` — ambiente local, não bug do PR); resolvido com `docker compose down -v` + subida limpa. Após isso: `afiliado_db` e `afiliado_api` **healthy**; `docker exec afiliado_api env | grep Claude` confirmou `Claude__ApiKey=sk-ant-test-cr-validacao` e `Claude__Model=` (vazio, cai no default hardcoded) presentes no container — a variável chega corretamente com o valor exato definido no `.env`. Logs do container sem exceção (boot limpo, Hangfire e Kestrel subiram normalmente); healthcheck interno reportou `{"status":"healthy"}` HTTP 200 em 3 ciclos consecutivos.
- Checklist de veto: compila e sobe (OK, evidenciado acima); integração real (fix é config-only, sem lógica nova a testar via integração — o teste de veto aqui é o boot real do container com a var chegando, que foi feito); conformidade com spec (bug descrito na Issue #178 resolvido — variável chega no container); sem teste-lixo (não aplicável, fix não adiciona testes de código); **sem segredo commitado** — `.env.example` só tem chaves vazias, `.env` de teste usado (`sk-ant-test-cr-validacao`) nunca foi commitado (`.gitignore` cobre `.env`, confirmado) e foi removido ao final; `.first()`/`.nth()`/`.last()` — não aplicável (sem specs E2E no diff).
- Ambiente Docker completamente removido ao final (`docker compose down -v`, `.env` de teste apagado). Imagem `omuletachou-api:latest` pré-existente (criada 2026-08-14, todas as camadas cacheadas nesta build — não é artefato desta validação) deixada intacta.
- **Merge realizado**: `desenv` → `homolog` via merge commit `dc67d03e40f27287ee7b56d65435ff069713bc58` (PR #180), conforme CLAUDE.md (nunca squash entre branches de longa vida).

## Próximos passos
1. Sessão principal spawna QA para validação independente em `homolog`.
2. Se QA aprovar: Líder Técnico cria PR `homolog→main` → Gate 2 (Gerente).

## Custo (ledger)

| # | Etapa | Agente | Modelo | Tokens | Ferramentas | Tempo (s) |
|---|-------|--------|--------|--------|-------------|-----------|
| 1 | Coordenador (preparação) | Coordenador | Haiku | 30212 | 34 | 227s |
| 2 | Dev (fix env vars docker-compose/.env.example/runbook, PR #179) | Dev .NET | Sonnet | 56481 | 37 | 284s |
| 3 | LT (merge PR #179 + PR homologação #180) | Líder Técnico | Sonnet | 37240 | 11 | 82s |
| 4 | Code Review (validação ao vivo PR #180, merge desenv->homolog) | Code Review | Sonnet | 71787 | 31 | 299s |

