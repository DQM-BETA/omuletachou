# Tasks — ISSUE-11: REST API (Dashboard + Endpoints Públicos)

## Ordem de dependência entre sub-issues

**Decisão: Sub-A é sequencial e bloqueante; Sub-B, Sub-C, Sub-D, Sub-E rodam em paralelo somente após Sub-A estar merged em `desenv`.**

Avaliada a alternativa de paralelizar tudo com um stub de `[Authorize]` (ex.: policy vazia/sempre-autoriza como placeholder, substituída depois pela real) e descartada pelos seguintes motivos:

1. **Primeira introdução de autenticação no sistema** — não há precedente de `[Authorize]`/JWT já rodando em nenhuma issue anterior (#2 a #10 são domínio/jobs/integrações, sem HTTP-facing auth). Um stub criado só para desbloquear paralelismo precisaria ser trocado pela implementação real de Sub-A em cada um dos outros 4 controllers, gerando retrabalho garantido (editar 4 arquivos de novo) em vez de retrabalho evitável.
2. **Risco de o stub vazar para produção**: se Sub-B/C/D/E forem revisadas e aprovadas (Code Review, QA) com um `[Authorize]` placeholder que sempre libera acesso, existe risco real de o merge para `homolog`/`main` acontecer antes da troca pelo `[Authorize]` real de Sub-A — abrindo os controllers do dashboard sem autenticação de fato em produção, exatamente na primeira vez que autenticação é modelada no sistema. O custo de um incidente de segurança aqui é desproporcional ao ganho de tempo do paralelismo.
3. **Superfície pequena de Sub-A**: Sub-A é a menor sub-issue em escopo (tabela `users`, seed, login, emissão/validação de JWT, `[Authorize]` global) — 10 CAs, sem UI, sem dependência de dado externo. É rápida de fechar sozinha, o que reduz o custo real de serializar (diferente de um cenário onde a sub-issue bloqueante fosse grande).
4. **Contrato de paginação compartilhado (`PagedResult<T>`, ver especificacao-tecnica.md §4) também gera uma dependência leve entre Sub-B e Sub-D** — rodando ambas só depois de Sub-A (mesmo branch `desenv` já atualizada), a primeira das duas a começar implementa o helper compartilhado; a outra reaproveita via rebase/merge de `desenv`, sem precisar de coordenação adicional em tempo real entre os dois Devs.

**Fluxo de branches resultante:**
```
desenv
  └─ feature/ISSUE-<SubA>-autenticacao   (Dev 1, sozinho)
       → merge → desenv (LT)
            ├─ feature/ISSUE-<SubB>-products-queue     (Dev, paralelo)
            ├─ feature/ISSUE-<SubC>-settings-jobs       (Dev, paralelo)
            ├─ feature/ISSUE-<SubD>-public-cors-ratelimit (Dev, paralelo)
            └─ feature/ISSUE-<SubE>-push-reports         (Dev, paralelo)
                 → merge → desenv (LT, um de cada vez — ver regra "merges sequenciais" do orquestrador)
                      → PR desenv→homolog (após as 5 sub-issues merged)
```

Sub-B/C/D/E podem ser spawnadas em paralelo (múltiplos Devs simultâneos) assim que o merge de Sub-A em `desenv` for confirmado pelo LT. Entre si, Sub-B/C/D/E não têm dependência de ordem — CAs independentes, controllers/tabelas distintos (exceto o contrato de paginação compartilhado, resolvido via merge de `desenv`, não via coordenação prévia).

---

## Sub-A — Autenticação
CAs: CA-A1 a CA-A10 (`criterios-aceite.md`)
Contexto técnico: `especificacao-tecnica.md` §1 (schema `users`), §2 (`Jwt__SigningKey`, `AddJwtBearer`), §3 (ordem de middlewares — Sub-A registra `UseAuthentication`/`UseAuthorization`, os demais itens da ordem completa ficam para Sub-D quando o CORS/RateLimiter entrarem)
Critério de "pronto para desbloquear as demais": PR feature→desenv merged, com `[Authorize]` de fato validando token em pelo menos um endpoint de smoke-test (pode ser um endpoint mínimo criado só para o teste de integração de Sub-A, descartável).

## Sub-B — ProductsController + QueueController
CAs: CA-B1 a CA-B11
Depende de: Sub-A merged em `desenv`.
Contexto técnico: `especificacao-tecnica.md` §4 (envelope de paginação — implementar `PagedResult<T>` se ainda não existir ao iniciar; checar `desenv` antes de duplicar).

## Sub-C — SettingsController + JobsController
CAs: CA-C1 a CA-C10
Depende de: Sub-A merged em `desenv`.
Contexto técnico: `especificacao-tecnica.md` §5 (formato exato do mascaramento — 16 asteriscos fixos + últimos 4 chars; `null`/"não configurado" para valor vazio, CA-C3). Incluir o log estruturado recomendado pelo Arquiteto (`design.md` §2.2) em `GET`/`PUT` de `/api/settings` — não bloqueante para a Definição de Pronto, mas trivial (1-2 linhas via `ILogger`), incluir se o esforço for baixo.

## Sub-D — PublicController + CORS + RateLimit
CAs: CA-D1 a CA-D12
Depende de: Sub-A merged em `desenv` (para a ordem completa de middlewares — CORS/RateLimiter entram no pipeline que já tem Authentication/Authorization de Sub-A).
Contexto técnico: `especificacao-tecnica.md` §3 (ordem completa do pipeline: ForwardedHeaders → Https → CORS → Authentication → Authorization → RateLimiter), §4 (paginação — reaproveitar `PagedResult<T>` se Sub-B já o criou); `design.md` §3 (ForwardedHeadersMiddleware, `KnownNetworks`, `ForwardLimit=1`).
Esta sub-issue é responsável por registrar `AddRateLimiter` com as policies nomeadas `"public-read"` (60 req/min) e `"public-write"` (10 req/min) usadas também por Sub-E.

## Sub-E — PushController + ReportsController
CAs: CA-E1 a CA-E6
Depende de: Sub-A merged em `desenv`; e de Sub-D para a policy `"public-write"` do RateLimiter estar registrada (se Sub-E terminar antes de Sub-D, o Dev registra a policy `"public-write"` localmente e resolve conflito trivial no merge — não é bloqueio duro, apenas atenção no merge).
Contexto técnico: `especificacao-tecnica.md` §6 (CA-E3: `DELETE /unsubscribe` retorna 204 idempotente, não 404, para endpoint não cadastrado — decisão do LT documentada com justificativa de segurança).

## Transversais (todas as sub-issues)
CA-T1, CA-T2: testes de integração com `WebApplicationFactory`, cobrindo 401 sem token + sucesso com token válido por controller protegido; sem chamadas externas reais (banco de teste, conforme padrão já definido no repo — testcontainers ou in-memory, ver `repos/omuletachou/CLAUDE.md`/padrão das Issues anteriores).
