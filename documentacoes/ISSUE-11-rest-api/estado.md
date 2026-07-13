# Estado — ISSUE-11: REST API (Dashboard + Endpoints Publicos)

## Campos principais
issue: 11
repo: omuletachou
titulo: feat: REST API (Dashboard + Endpoints Publicos)
rota: normal
etapa_atual: Gate 1 — aguardando resposta do Gerente (perguntas de levantamento postadas)
docs_path: repos/omuletachou/documentacoes/ISSUE-11-rest-api
openspec_path: repos/omuletachou/openspec/changes/issue-11-rest-api
openspec_change: ~
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

Diferente das Issues #7-#10 (integrações de rede social, aditivas ao publisher), a Issue #11 é a **infraestrutura de exposição de dados** (layer HTTP acima do domain/jobs já existentes). Envolve **decisões arquiteturais reais: autenticação/autorização do Dashboard, versionamento de API, paginação padrão, CORS para domínios públicos, mascaramento de valores sensíveis em responses.**

Estas decisões **NÃO são triviais** — impactam como todos os clientes (Dashboard, Next.js, PWA) consumirão dados. PM/Arquiteto devem avaliar se há ambiguidade que justifique escalar ao Arquiteto após o PM Fase 2.

**Padrão de evolução:** Endpoints `/api/products`, `/api/queue`, `/api/settings`, `/api/jobs/trigger`, `/api/reports/summary` (protegidos, Dashboard apenas), e `/api/public/deals` + `/api/public/push` (sem autenticação, site/PWA públicos). CORS restrito a `https://omuletachou.com.br` e localhost em dev.

## PM Fase 1 — levantamento de requisitos
Concluído. Perguntas postadas na Issue #11 (comentário https://github.com/DQM-BETA/omuletachou/issues/11#issuecomment-4962241310), cobrindo:
1. Autenticação/autorização do Dashboard (login próprio vs. provedor externo; usuário único vs. múltiplos papéis)
2. Endpoints públicos — confirmação de acesso irrestrito + necessidade de rate limiting
3. Versionamento de API (`/api/v1/...` desde já ou prematuro)
4. Paginação (tamanho padrão e máximo)
5. Mascaramento de credenciais em Settings (nunca retornar secret puro)
6. CORS (domínios exatos: produção, dev, homolog, subdomínios)
7. Escopo da issue (todos os controllers nesta issue única ou fatiar em sub-issues/issues futuras)

Aguardando resposta do Gerente (Gate 1) para avançar à Fase 2.

## PM Fase 2
Etapa pendente. Consolidar PRD, critérios de aceite, validação de ambiguidade. Depende das respostas do Gate 1.

## Arquiteto (se chamado)
Etapa condicional. Escalonada apenas se PM Fase 2 identificar ambiguidade arquitetural.

## Líder Técnico — refinamento técnico
Etapa pendente. Espera Gate 1 (respostas do Gerente) + PM Fase 2.

## Dev .NET
Etapa pendente. Espera refinamento técnico + definição de sub-issues.

## Sub-issues
sub_issues: []
desenv_tasks_merged: []

## Historico de etapas
| # | Etapa | Agente | Status |
|---|---|---|---|
| 1 | Preparacao | coordenador | concluido — Issue #11 preparada, estado.md criado, comentario 📍 Status adicionado (id 4962193361), Issue adicionada ao Project em Backlog, card movido para Em Desenvolvimento (kickoff) |
| 2 | PM Fase 1 | pm-analista-negocios | concluido — perguntas de levantamento postadas na Issue #11 (comentário 4962241310), comentario 📍 Status atualizado para Gate 1, aguardando resposta do Gerente |

## Custo (ledger)
| # | Etapa | Agente | Modelo | Tokens | Tools | Tempo_s |
|---|---|---|---|---|---|---|
| 1 | Preparacao | coordenador | haiku | 61934 | 56 | 302s |
| 2 | PM Fase 1 | pm | sonnet | 30761 | 9 | 68s |

**Consolidação (quiescência):** A preencher pela sessão principal após cada etapa. Nenhuma invocação anterior — isto é a Issue #11, primeira vez no pipeline.

---
_Última atualização: 2026-07-13 — mantido pelo PM (pm-analista-negocios)_
