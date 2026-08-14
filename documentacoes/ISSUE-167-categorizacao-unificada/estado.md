---
issue: 167
titulo: feat: Categorização unificada de produtos + remoção de distinção de plataforma no site
etapa_atual: Aguardando Aprovação — Gate 1
ultimo_agente: pm-analista-negocios
rota: backlog
openspec_change: ~
tech_stacks:
  - dotnet
  - nodejs
repos:
  omuletachou: repos/omuletachou
repo_path: repos/omuletachou
docs_path: repos/omuletachou/documentacoes/ISSUE-167-categorizacao-unificada
openspec_path: repos/omuletachou/openspec/changes/ISSUE-167-categorizacao-unificada
sub_issues: []
desenv_tasks_merged: []
sub_issues_frontend: {}
pr_homologacao: ~
pr_release: ~
code_review_homolog_pr: ~
qa_status: ~
figma_url: ~
blockers: nenhum
status_comment_id: ~
createdAt: 2026-08-14
---

## Resumo
Demanda `backlog`: categorização unificada de produtos (Category + Subcategory) e remoção da distinção de plataforma (Amazon/MercadoLivre/Shopee) no site público. Documentação completa (proposta técnica inicial, componentes impactados) entregue na Issue. PM Fase 1 concluída: validação técnica da proposta contra o código real (CategoryDetector, ClaudeAiService.ScoreProductAsync, ProcessorJob, PublicController/PublicDealDto, Product.Category) postada como comentário na Issue, junto com perguntas de Gate 1 ao Gerente. Aguardando respostas.

## Validação técnica (PM Fase 1) — achados principais
- `CategoryDetector` confirma as 6 categorias (5 por dicionário + "Geral" fallback); estrutura simples de estender.
- **Conflito de sequência**: `ScoreProductAsync` é chamado nos collectors (Amazon/MercadoLivre/Shopee) no momento da coleta, com `Category` sempre = "Geral" (hardcoded nos collectors). O dicionário (`CategoryDetector`) só roda depois, em `ProcessorJob.EnsureCategory`, e só para produtos já aprovados (Status=Queued). Combinar o fallback de IA na mesma chamada de `ScoreProductAsync` (como proposto) exige mover a checagem de dicionário para antes/durante a coleta — decisão para o Arquiteto/LT.
- `ProcessorJob`: ordem atual é `EnsureSlug` → `EnsureCategory` (slug antes de categoria) — proposta descreve o inverso; reordenar é trivial mas precisa ser explícito na spec técnica.
- `PublicDealDto` expõe `Platform` hoje (remoção = mudança de contrato real). Filtro de categoria já existe mas via segmento de rota (`/category/{categoria}`), não querystring — endpoint novo/reformulado é decisão técnica. Ordenação hoje é fixa por `AiScore`.
- **Achado relevante**: `DealCard.tsx`/`DealDetail.tsx` não exibem badge de plataforma hoje (campo `platform` existe no tipo/DTO mas não é renderizado em nenhum componente encontrado) — "remoção do badge" é, na prática, só remoção do campo do contrato de dados, não mudança visual.
- `Product.Category` confirmado como `string` livre (não enum) — proposta de `Subcategory` como novo campo `string` segue o mesmo padrão, sem migração de enum.

## Perguntas de Gate 1 (postadas na Issue, aguardando Gerente)
1. Taxonomia da tabela é definitiva ou exemplo? Quantas categorias/subcategorias no total (v1)?
2. Recategorização retroativa de produtos já publicados, ou só produtos novos?
3. Teto de gasto com chamadas extras à API do Claude (fallback de categorização), dado o achado do conflito de sequência acima?
4. Ordenação padrão do site continua por AiScore (com novos filtros como opção), ou muda?
5. Remoção de `Platform` do contrato público é por higiene/privacidade de dados (já que não há exibição visual hoje), ou havia exibição não localizada?

## Próximos passos
- **Gate 1 (Gerente)**: responder às perguntas acima na Issue #167
- PM Fase 2: atualizar proposta com respostas, criar openspec change, escrever proposal.md + criterios-aceite.md
- Arquiteto: resolver o conflito de sequência (dicionário vs. scoring) e demais trade-offs arquiteturais
- UX/UI: mockups de barra de filtros, novo layout de resultado
- LT: criação de sub-issues e tasks.md (após refinamento completo) — fora do escopo da rota `backlog` por ora

## Custo (ledger)
| # | Etapa | Agente | Modelo | Tokens | Tools | Tempo (s) |
|---|---|---|---|---|---|---|
| 1 | Backlog | Coordenador | Haiku | 2850 | 5 | 8 |
