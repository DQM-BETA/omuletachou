---
issue: 133
titulo: "chore: Hardening e débito técnico — auditoria completa 2026-08-03"
etapa_atual: Em Desenvolvimento
ultimo_agente: lt
status_comment_id: 5178622317
openspec_change: ~
tech_stacks:
  - dotnet
  - angular
  - infra
repos:
  omuletachou: repos/omuletachou
repo_path: repos/omuletachou
docs_path: repos/omuletachou/documentacoes/ISSUE-133-hardening-debito-tecnico
openspec_path: ~
sub_issues:
  - "#145 (stack:dotnet, task_id:Sub-A)"
  - "#146 (infra, task_id:Sub-B)"
  - "#147 (stack:angular, task_id:Sub-C)"
desenv_tasks_merged: []
sub_issues_frontend: {}
pr_homologacao: ~
pr_release: ~
code_review_homolog_pr: ~
qa_status: ~
figma_url: ~
blockers: nenhum
createdAt: "2026-08-04"
closedAt: ~
---

## Descrição
Consolidação de achados não-bloqueantes da auditoria completa de código (Code Review) + teste
funcional (QA) pedida pelo Gerente em 2026-08-03. Achados categorizados por tema:

- **Segurança**: DELETE sem rate-limiting, senha com comparação não tempo-constante, SSRF, header forwarding
- **Dependências vulneráveis**: Angular, next-pwa, Newtonsoft.Json com vulnerabilidades High
- **Infraestrutura**: .gitignore bloqueando .dockerignore, deploy sem healthcheck, imagens sem pin de versão
- **Qualidade de código**: Código morto (Class1.cs, testes boilerplate)
- **Lacuna funcional**: ProcessorJob com falsa sensação de "publicado", Facebook credentials não seedadas

## Triagem (LT, 2026-08-04) — issue puramente técnica, sem PM/Arquiteto

O Gerente autorizou ("resolva") — triagem feita diretamente pelo LT, dado que não há ambiguidade
de negócio nem de arquitetura em nenhum destes itens (fixes técnicos objetivos).

### Fazer agora (baixo risco, mecânico, alto valor) → Sub-A/B/C

| Item | Por quê agora |
|---|---|
| Rate-limit em `unsubscribe` | Policy `PublicWritePolicy` já existe e já é usada em `subscribe`/`vapid-public-key` — só falta o atributo. Zero risco, 1 linha. |
| `HangfireAuthFilter` timing-safe + lockout | Vulnerabilidade real (timing attack) e endpoint administrativo sensível (`/hangfire` expõe todos os jobs). Fix mecânico (`CryptographicOperations.FixedTimeEquals` + contador em memória), sem mudança de contrato externo. |
| SSRF allowlist em `LocalMediaStorage` | Defesa em profundidade barata (checagem de IP antes do download). Risco de regressão baixo — só rejeita ranges que nunca deveriam ser mídia legítima de produto. |
| `dashboard/nginx.conf` X-Forwarded-* | 3 linhas de config, sem risco — hoje "funciona por acidente" via NPM, tornar explícito remove fragilidade do rate-limiting por IP real. |
| `.gitignore`/`.dockerignore` | Puramente aditivo (remove 1 linha do gitignore, cria 3 arquivos novos). Sem risco de regressão. |
| `deploy.sh` healthcheck | Script de operação, não código de produção — falha segura (para de reportar sucesso falso). Baixo risco, alto valor operacional (1º deploy real ainda vai acontecer). |
| Pin de versão `postgres`/NPM | Mecânico (troca de tag), reduz risco de drift silencioso em `docker compose pull` futuro. |
| `Class1.cs` mortos | Código nunca referenciado desde o scaffold inicial — remoção sem risco. |
| `app.component.spec.ts` boilerplate | Teste placeholder sem valor de regressão real — remoção/substituição sem risco. |
| `ProcessorJob.MarkAsPublished()` incondicional | Bug real de domínio (produto marcado "Published" sem nada enfileirado, distorce Reports). Fix contido: reaproveita `ProductStatus.Error` já existente, sem introduzir novo status nem migração de schema. |
| Seed `facebook.access_token`/`facebook.page_id` | Mesmo padrão já usado 2x (Instagram/YouTube) — sem essa seed, a lacuna funcional acima (A4) não pode nem ser testada fim-a-fim para Facebook (rede nunca teria credenciais para qualificar). Migration mecânica, ids 49/50 confirmados livres. |
| `Newtonsoft.Json` transitivo | Investigado: vem só do `Hangfire.Core` (não há referência direta em nenhum `.csproj`). Fix é 1 `PackageReference` direto pinado em 13.0.3 — mecânico, baixo risco, dentro da mesma major. Por isso migrou de "fora de escopo" (proposto pelo Gerente) para "fazer agora" (critério do próprio brief: "se for fix simples, mova para fazer agora"). |

### Fora de escopo desta rodada (documentar por quê)

| Item | Por quê fica de fora |
|---|---|
| Upgrade Angular `17.3.0` → 18/19 | Breaking change real (major bump, migração de APIs, possível rework de templates/testes do dashboard inteiro). Esforço desproporcional a uma rodada de hardening — precisa de sprint dedicado com regressão completa do dashboard. As 10 vulnerabilidades High são de XSS em libs internas do Angular CLI/build, não expostas diretamente por input de usuário não sanitizado conhecido — risco real mitigado, urgência menor que o esforço. |
| Upgrade/substituição `next-pwa` | Cadeia transitiva (`serialize-javascript`) é RCE, mas **só em build-time** (nunca roda em produção com input de usuário) — risco real baixo apesar da severidade "High" do scanner. Trocar a lib de PWA do Next.js é mudança arquitetural (avaliar alternativas como `@ducanh2912/next-pwa` ou Workbox direto), não um bump de versão simples. Precisa de avaliação própria antes de decidir a lib substituta. |
| Backup automatizado do volume `postgres_data` | Decisão operacional/infra que depende da VM real de produção (Oracle Cloud ARM) ainda não provisionada (Issue #15, backlog). Definir estratégia (cron + `pg_dump` + storage externo, snapshot de volume, etc.) sem o ambiente real na frente é prematuro — mais adequado quando a #15 for executada na prática. |

### Paralelismo
As 3 sub-issues (#145 backend .NET, #146 infra, #147 frontend/dashboard) não têm dependência
funcional entre si — tocam arquivos/repos-lógicos disjuntos (backend/, docker-compose.yml +
deploy.sh + .gitignore, dashboard/). Podem ser desenvolvidas em paralelo por devs distintos.

## Custo (ledger)

| # | Etapa | Agente | Modelo | Tokens | Ferramentas | Tempo (s) |
|---|-------|--------|--------|--------|-------------|-----------|
| 1 | Preparação (Issue + estado.md) | Coordenador | Haiku | 25067 | 14 | 82s |
| 2 | Refinamento (triagem + sub-issues + especificacao-tecnica.md) | Líder Técnico | Sonnet | 75154 | 38 | 303s |

**Total acumulado:** — tokens · — min proc. (merge pendente)

---
_Criado: 2026-08-04 — Coordenador_
_Atualizado: 2026-08-04 — Líder Técnico (triagem + task breakdown)_
