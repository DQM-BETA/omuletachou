---
issue: 163
titulo: "chore: Substituir placeholder-deal.png (1x1px) por placeholder visual real"
etapa_atual: Dev concluído — PR feature→desenv aberto
rota: rapido
ultimo_agente: dev-nodejs
openspec_change: ~
tech_stacks:
  - nodejs
repos:
  omuletachou: repos/omuletachou
repo_path: repos/omuletachou
docs_path: repos/omuletachou/documentacoes/ISSUE-163-placeholder-deal
openspec_path: ~
sub_issues: []
desenv_tasks_merged: []
sub_issues_frontend: {}
pr_homologacao: ~
pr_release: ~
code_review_homolog_pr: ~
qa_status: ~
figma_url: ~
blockers: nenhum
createdAt: "2026-08-14"
status_comment_id: ~
---

## Síntese
Achado incidental: placeholder-deal.png é 1x1px (68 bytes) desde Issue #94. Renderiza como bloco preto sólido quando não há media_url. Substituir por placeholder visual real (SVG ou PNG conforme avaliação do Dev).

## Implementação (Dev)
Solução escolhida: SVG (markup, sem ferramentas de raster; leve, nítido em qualquer resolução).
- `website/public/placeholder-deal.svg`: novo — ícone "imagem indisponível" (moldura + foto), cores da paleta neutra do design system (`documentacoes/ISSUE-154-site-sem-css/ux-ui-spec.md`: `--color-neutral-100` #f5f3f6 fundo, `--color-neutral-200` #e4e0e6 borda/ícone, `--color-neutral-400` #a29aa8 traço), sem cor de marca (estado vazio ≠ CTA).
- `website/public/placeholder-deal.png`: removido (stub órfão de 1x1px).
- `website/lib/format.ts`: `resolveDealImageUrl` retorna `/placeholder-deal.svg`.
- `website/components/DealCard.test.tsx` (CA-A7): assertiva atualizada para `.svg`.
- Gate de testes: grep confirmou `DealCard.test.tsx` e `format.ts` como únicas referências ao asset no código — nenhum outro teste ficou desalinhado.

### Validação
- `npm test`: 80/80 passando (14 suites).
- `npm run build`: sucesso.
- Docker: stack `db+api+website` subida em worktree isolado (container_names/portas dedicados via override local, para não colidir com stack pré-existente `afiliado_*` do repo principal — parada e restaurada ao estado original ao final). Produto inserido via SQL direto (`media_url`/`media_local_path` NULL); API confirmou `mediaUrl: null`; Home e `/oferta/{slug}` servem `<img src="/placeholder-deal.svg">` (200, `image/svg+xml`); renderização confirmada como ícone visual (não bloco preto). `docker compose down -v` ao final — ambiente limpo, sem containers/volumes/networks residuais do teste.
- Worktree removido; `repo_path` em `desenv`.

PR: https://github.com/DQM-BETA/omuletachou/pull/164 (`chore/ISSUE-163-placeholder-real` → `desenv`)

## Próximos passos
- Líder Técnico: merge feature→desenv

## Custo (ledger)
| # | Etapa | Agente | Modelo | Tokens | Tools | Tempo(s) |
|---|---|---|---|---|---|---|
| 1 | Preparação (Issue + estado.md) | Coordenador | Haiku | 18991 | 6 | 45s |
| 2 | Dev (SVG placeholder, PR #164) | Dev Node.js | Sonnet | 79709 | 64 | 1221s |
