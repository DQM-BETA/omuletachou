---
issue: 6
titulo: feat: Processor Job (Midia e Fila de Publicacao)
rota: normal
etapa_atual: PM Fase 1 — aguardando resposta Gate 1 (Gerente)
repo: omuletachou
docs_path: repos/omuletachou/documentacoes/ISSUE-6-processor-job
openspec_path: repos/omuletachou/openspec/changes/ISSUE-6-processor-job
tech_stacks:
  - .NET 8
  - Hangfire
  - HttpClient
ultimo_agente: pm
sub_issues: []
desenv_tasks_merged: []
sub_issues_frontend: {}
pr_homologacao: ~
pr_release: ~
code_review_homolog_pr: ~
qa_status: ~
figma_url: ~
blockers: nenhum
---

## Contexto

Issue #6 implementa o **Processor Job** — subsistema de processamento assíncrono de mídia e fila de publicação. É **dependente direto das Issues #2, #3, #4 e #5** (funciona com qualquer collector já implementado).

### Pontos de ambiguidade a esclarecer com o Gerente (Gate 1)

**(0) Status de entrada do ProcessorJob — NOVO, encontrado pelo PM**
- A Issue pede buscar produtos `Status = Pending`, mas `Product.UpdateAiResult` (já implementado) seta `Status = Queued` quando aprovado no scoring e `Rejected` quando reprovado — não há caminho para um produto aprovado permanecer `Pending`.
- **Questão:** o ProcessorJob deveria buscar `Status = Queued`? Precisa de um novo status terminal (ex. `Published`/`Ready`) para não colidir com o "Queued" já setado por `UpdateAiResult`?

**(a) Slug — duplicação ou re-geração?**
- A entidade `Product` já define `Slug` no momento da criação pelos collectors (Amazon, ML, Shopee).
- A Issue #6 descreve gerar slug **novamente** no ProcessorJob.
- **Questão:** é re-geração intencional (pode divergir do slug original), ou duplicação a evitar?

**(b) AffiliateLink MercadoLivre — preenchimento no ProcessorJob?**
- A Issue #5 (Gate 1) decidiu que o `AffiliateLink` do MercadoLivre fica `null` até aprovação do scoring e seria **preenchido pelo ProcessorJob** (Issue #6), usando `Product.SetAffiliateLink(string link)` (já implementado).
- O corpo da Issue #6 **não menciona** este preenchimento.
- **Questão:** o ProcessorJob é responsável por preenchê-lo? Se sim, a partir de qual campo/API (chamada real a `POST /affiliate-tools/links` do ML)?

**(c) Campo MediaLocalPath não existe na entidade Product**
- A Issue #6 usa `Product.MediaLocalPath` para armazenar o caminho local das mídias baixadas.
- A entidade `Product` **não possui este campo atualmente**.
- **Questão:** é necessária nova migration para adicionar `MediaLocalPath: string?` a `Product`?

**(d) Detecção de Category** — collectors já setam `"Geral"` hardcoded; fonte da detecção real não especificada (título/palavras-chave/IA).

**(e) Distribuição de ScheduledAt** — mecânica de distribuição entre horários do cron do publisher não especificada.

**(f) Produtos Rejected** — confirmar se ficam parados definitivamente ou há retry/limpeza futura.

**(g) Falha no download de mídia** — pular produto (retry no próximo ciclo) vs. processar sem mídia local.

**(h) Redes habilitadas sem credenciais** — criar item de fila mesmo assim (falhará no Publisher) vs. pular a rede.

## Histórico
- 2026-07-06 — Coordenador preparou Issue (estado.md, diretórios, label, card no board)
- 2026-07-06 — PM Fase 1: PRD inicial (`prd.md`) escrito; 9 perguntas de Gate 1 postadas na Issue #6 (comentário https://github.com/DQM-BETA/omuletachou/issues/6#issuecomment-4896543914)

## Custo (ledger)
| # | Etapa | Agente | Modelo | Tokens | Tools | Tempo (s) |
|---|---|---|---|---|---|---|
| 1 | Preparação | coordenador | haiku | — | — | — |
| 2 | PM Fase 1 | pm | sonnet | — | — | — |

---
*Aguardando resposta do Gerente às perguntas do Gate 1 na Issue #6.*
