# Estado — ISSUE-11: REST API (Dashboard + Endpoints Publicos)

## Campos principais
issue: 11
repo: omuletachou
titulo: feat: REST API (Dashboard + Endpoints Publicos)
rota: normal
etapa_atual: Arquiteto — revisão focada (JWT signing key/refresh, mascaramento de secrets, rate limit atrás de proxy reverso)
docs_path: repos/omuletachou/documentacoes/ISSUE-11-rest-api
openspec_path: repos/omuletachou/openspec/changes/issue-11-rest-api
openspec_change: repos/omuletachou/openspec/changes/issue-11-rest-api
ultimo_agente: pm-analista-negocios
status_comment_id: 4962193361
pr_feature: ~
pr_homologacao: ~
pr_release: ~
qa_status: ~
code_review_homolog_pr: ~
closedAt: ~

## Contexto
Stack: .NET 8, ASP.NET Core Web API, Controllers (ProductsController, QueueController, SettingsController, JobsController, ReportsController, PublicController, PushController)
Repo: DQM-BETA/omuletachou
Branch base: desenv
Dependências: Issues #2 (Domain + EFCore schema), #6 (ProcessorJob) — ambas em produção (main)

**Contexto técnico diferenciado (em relação a issues anteriores):**
Esta Issue implementa a **REST API que expõe dados para o Dashboard Angular (Issue #13, futura)** e para endpoints públicos (site Next.js Issue #12, futura, e PWA).

Diferente das Issues #7-#10 (integrações de rede social, aditivas ao publisher), a Issue #11 é a **infraestrutura de exposição de dados** (layer HTTP acima do domain/jobs já existentes) e introduz **autenticação/autorização pela primeira vez no sistema** (JWT, hash bcrypt, seed de usuário via env var), mascaramento de secrets e política de CORS explícita.

## PM Fase 1 — levantamento de requisitos
Concluído. Perguntas postadas na Issue #11 (comentário https://github.com/DQM-BETA/omuletachou/issues/11#issuecomment-4962241310), cobrindo os 7 eixos (auth, endpoints públicos, versionamento, paginação, mascaramento, CORS, escopo).

## Gate 1 — Gerente
Concluído em 2026-07-17. Respostas completas em https://github.com/DQM-BETA/omuletachou/issues/11#issuecomment-5003551503:
1. JWT, usuário único, `POST /api/auth/login`, expiração 24h, tabela `users` com hash bcrypt, seed via env var. Todos os controllers do dashboard com `[Authorize]`, exceto `/api/public/*` e `/api/auth/login`.
2. Campos públicos restritos (nunca `ExternalId`/`AiScore`/`AiReason`/`app_settings`). Rate limiting nativo .NET 8: 60 req/min/IP leitura, 10 req/min/IP escrita (push subscribe).
3. Sem versionamento de API por enquanto.
4. Paginação `page`/`pageSize` (default 20, máx 100), envelope `items`/`page`/`pageSize`/`totalItems`/`totalPages`.
5. Mascaramento obrigatório de `_key`/`_secret`/`_token`/`_password` em `GET /api/settings` (últimos 4 caracteres). `PUT` sempre sobrescreve, nunca lê valor completo de volta.
6. CORS com lista explícita de 5 origins, nunca `AllowAnyOrigin`, configurável por ambiente.
7. Fatiar em 5 sub-issues (Sub-A Autenticação; Sub-B Products+Queue; Sub-C Settings+Jobs; Sub-D Public+CORS+RateLimit; Sub-E Push+Reports) — issue-pai vira guarda-chuva.

## PM Fase 2 — PRD consolidado
Concluído em 2026-07-17.
- `proposal.md` escrito em `repos/omuletachou/openspec/changes/issue-11-rest-api/proposal.md` (objetivo, usuários, casos de uso/exceção, regras de negócio, fatiamento em 5 sub-issues, integrações, restrições, definição de pronto).
- `criterios-aceite.md` escrito em `repos/omuletachou/documentacoes/ISSUE-11-rest-api/criterios-aceite.md` — 46 CAs organizados por sub-issue (Sub-A a Sub-E) + 2 CAs transversais (testes de integração).
- Comentário de sumário do PRD postado na Issue #11: https://github.com/DQM-BETA/omuletachou/issues/11#issuecomment-5003577610

**Avaliação de ambiguidade arquitetural: SIM, escalar ao Arquiteto (escopo focado, não redesenho completo).**
Motivo: primeira introdução de autenticação/autorização no sistema. 3 pontos identificados fora do julgamento de negócio do PM, que devem pautar o `design.md`:
1. Estratégia de assinatura JWT (algoritmo, armazenamento da chave, aceitabilidade de não ter refresh token dado usuário único).
2. Suficiência do mascaramento de secrets (últimos 4 caracteres) — avaliar necessidade de camada adicional (ex.: auditoria de acesso a `GET /api/settings`).
3. Rate limiting nativo do .NET 8 atrás de proxy reverso (Oracle Cloud VM) — particionamento por IP precisa considerar `X-Forwarded-For`/`X-Real-IP` corretamente.

## Arquiteto (chamado)
Etapa atual. Escopo focado nos 3 pontos acima — não é revisão de todo o PRD (regras de negócio já fechadas com o Gerente). Deve produzir/completar `design.md` em `repos/omuletachou/openspec/changes/issue-11-rest-api/`.

## Líder Técnico — refinamento técnico
Etapa pendente. Aguarda conclusão do Arquiteto. Ao refinar, avaliar dependência de ordem entre Sub-A (autenticação) e as demais sub-issues (podem rodar em paralelo com stub de auth, ou há dependência sequencial real) — ver nota em `proposal.md`.

## Dev .NET
Etapa pendente. Espera refinamento técnico + criação das 5 sub-issues reais no GitHub.

## Sub-issues
sub_issues: []
desenv_tasks_merged: []

## Historico de etapas
| # | Etapa | Agente | Status |
|---|---|---|---|
| 1 | Preparacao | coordenador | concluido — Issue #11 preparada, estado.md criado, comentario 📍 Status adicionado (id 4962193361), Issue adicionada ao Project em Backlog, card movido para Em Desenvolvimento (kickoff) |
| 2 | PM Fase 1 | pm-analista-negocios | concluido — perguntas de levantamento postadas na Issue #11 (comentário 4962241310), comentario 📍 Status atualizado para Gate 1, aguardando resposta do Gerente |
| 3 | PM Fase 2 | pm-analista-negocios | concluido — Gate 1 respondido (comentário 5003551503), proposal.md + criterios-aceite.md escritos (46 CAs em 5 sub-issues), sumário do PRD postado (comentário 5003577610), comentario 📍 Status atualizado para Arquiteto, ambiguidade=sim (escopo focado: JWT signing/refresh, mascaramento de secrets, rate limit atrás de proxy) |

## Custo (ledger)
| # | Etapa | Agente | Modelo | Tokens | Tools | Tempo_s |
|---|---|---|---|---|---|---|
| 1 | Preparacao | coordenador | haiku | 61934 | 56 | 302s |
| 2 | PM Fase 1 | pm | sonnet | 30761 | 9 | 68s |
| 3 | PM Fase 2 | pm | sonnet | (preencher pela sessão principal com o `<usage>` do HANDOFF) | | |

**Consolidação (quiescência):** A preencher pela sessão principal após cada etapa.

---
_Última atualização: 2026-07-17 — mantido pelo PM (pm-analista-negocios)_
