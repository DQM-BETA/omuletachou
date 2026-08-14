---
issue: 154
titulo: "bug: Site público (website) sem nenhum estilo CSS implementado — apenas HTML puro"
etapa_atual: Aguardando Aprovação — Gate 1
ultimo_agente: pm-analista-negocios
rota: normal
openspec_change: ~
tech_stacks: [nodejs]
repos:
  omuletachou: main
repo_path: repos/omuletachou
docs_path: repos/omuletachou/documentacoes/ISSUE-154-site-sem-css
openspec_path: repos/omuletachou/openspec/changes/ISSUE-154-site-sem-css
status_comment_id: "5293952020"
sub_issues: []
desenv_tasks_merged: []
sub_issues_frontend: {}
pr_homologacao: ~
pr_release: ~
code_review_homolog_pr: ~
qa_status: ~
figma_url: https://www.figma.com/design/yi6YkNAy9HfHus2oiPi3G7/Diego-Mulet-s-team-library
blockers: nenhum
createdAt: "2026-08-14T00:00:00Z"
---

## Resumo

Site público (Next.js) renderiza como HTML puro sem estilo CSS — classes BEM estruturadas mas sem implementação visual. Crítico para UX.

## Contexto

- Confirmado visualmente: site rodando localmente via Docker mostra apenas texto corrido, sem layout, grid ou cards.
- Arquivos CSS vazios (boilerplate não customizado).
- Classes BEM bem estruturadas nos componentes (DealCard, Header) e páginas, mas sem regras CSS correspondentes.
- Pipeline anterior não tinha validação visual em navegador (foco em API e Jest).
- Causa raiz do gap de QA registrada em `.claude/melhorias/2026-08-14-qa-gate-visual-nunca-disparou-website-dashboard.md`: Gate Visual do QA depende do script `test:visual` no `package.json`, inexistente em `website`/`dashboard` desde o scaffold — Gate sempre resolveu N/A.

## PM Fase 1 (levantamento)

Bug técnico claro sobre feature já especificada (Issues #12/#94/#95/#96/#117) — sem requisitos de negócio novos. Levantamento restrito a decisões do Gerente, postado como comentário na Issue #154:
1. Identidade visual (referência de marca existente vs. criar do zero a partir do Figma)
2. Prioridade de telas (Home, categoria, deal-detail — todas nesta rodada?)
3. Escopo de configuração de Playwright/`test:visual` (nesta issue ou issue técnica separada, dado que `dashboard` também não tem)
4. Responsividade / mobile-first / breakpoints prioritários
5. Relação com PWA existente (Issue #117) — manifest/ícones precisam alinhar com o CSS novo?

Aguardando respostas do Gerente para Fase 2 (PRD + critérios de aceite).

## Próximas Etapas

1. PM Fase 1: requisitos visuais — **feito, aguardando Gate 1**
2. PM Fase 2: PRD + critérios de aceite (após respostas do Gerente)
3. Arquiteto: decisão técnica (CSS solution) — se houver ambiguidade arquitetural
4. UX/UI: spec a partir do design system
5. Dev(s): implementação
6. Code Review + QA: validação visual (novo checkpoint)

## Ledger de Custo

| # | Etapa | Agente | Modelo | Tokens | Tools | Tempo (s) |
|---|-------|--------|--------|--------|-------|-----------|
| 1 | Preparação | Coordenador | Haiku | — | — | — |

---

_Mantido pelo PM. Última atualização: 2026-08-14._
