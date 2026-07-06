---
issue: 6
titulo: feat: Processor Job (Midia + Fila de Publicacao)
rota: normal
etapa_atual: PM Fase 1 — aguardando spawn
repo: omuletachou
docs_path: repos/omuletachou/documentacoes/ISSUE-6-processor-job
openspec_path: repos/omuletachou/openspec/changes/ISSUE-6-processor-job
tech_stacks:
  - .NET 8
  - Hangfire
  - HttpClient
ultimo_agente: coordenador
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

### Pontos de ambiguidade a esclarecer com PM

**(a) Slug — duplicação ou re-geração?**
- A entidade `Product` já define `Slug` no momento da criação pelos collectors (Amazon, ML, Shopee).
- A Issue #6 descreve gerar slug **novamente** no ProcessorJob.
- **Questão:** é re-geração intencional (pode divergir do slug original), ou duplicação a evitar?

**(b) AffiliateLink MercadoLivre — preenchimento no ProcessorJob?**
- A Issue #5 (Gate 1) decidiu que o `AffiliateLink` do MercadoLivre fica `null` até aprovação do scoring e seria **preenchido pelo ProcessorJob** (Issue #6).
- O corpo da Issue #6 **não menciona** este preenchimento.
- **Questão:** o ProcessorJob é responsável por preenchê-lo? Se sim, é a partir de qual campo ou tabela?

**(c) Campo MediaLocalPath não existe na entidade Product**
- A Issue #6 usa `Product.MediaLocalPath` para armazenar o caminho local das mídias baixadas.
- A entidade `Product` **não possui este campo atualmente**.
- **Questão:** é necessária nova migration para adicionar `MediaLocalPath: string?` a `Product`?

## Histórico
- 2026-07-06 — Coordenador preparou Issue (estado.md, diretórios, label, card no board)

## Custo (ledger)
| # | Etapa | Agente | Modelo | Tokens | Tools | Tempo (s) |
|---|---|---|---|---|---|---|
| 1 | Preparação | coordenador | haiku | — | — | — |

---
*Aguardando spawn para PM Fase 1.*
