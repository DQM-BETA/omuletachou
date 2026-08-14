---
issue: 154
titulo: "bug: Site público (website) sem nenhum estilo CSS implementado — apenas HTML puro"
etapa_atual: Backlog
ultimo_agente: coordenador
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

## Próximas Etapas

1. PM Fase 1: requisitos visuais
2. Arquiteto: decisão técnica (CSS solution)
3. UX/UI: spec a partir do design system
4. Dev(s): implementação
5. Code Review + QA: validação visual (novo checkpoint)

## Ledger de Custo

| # | Etapa | Agente | Modelo | Tokens | Tools | Tempo (s) |
|---|-------|--------|--------|--------|-------|-----------|
| 1 | Preparação | Coordenador | Haiku | — | — | — |

---

_Mantido pelo Coordenador. Última atualização: 2026-08-14._
