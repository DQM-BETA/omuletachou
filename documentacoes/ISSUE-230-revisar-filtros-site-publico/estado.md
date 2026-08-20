# Estado — Issue #230

## Identificação
- issue: 230
- titulo: revisar filtros da tela de produtos do site público (desconto, preço, busca inteligente)
- repo: DQM-BETA/omuletachou
- repo_path: repos/omuletachou
- docs_path: repos/omuletachou/documentacoes/ISSUE-230-revisar-filtros-site-publico/
- openspec_change: (a definir na Fase 2)

## Pipeline
- rota: backlog
- etapa_atual: PM Fase 1 — aguardando Gate 1
- ultimo_agente: pm-analista-negocios
- status_comment_id: (gerenciado pelo Coordenador — não criado ainda por este agente)

## Resumo da demanda
4 itens sobre a barra de filtros (`filter-bar`) do site público (`website/`):
1. Remover filtro de desconto mínimo (10%+/30%+/50%+).
2. Corrigir bug no filtro de preço (slider) — causa raiz desconhecida, precisa reprodução ao vivo.
3. Permitir digitar preço mínimo/máximo (campos numéricos, além do slider).
4. Busca textual "inteligente" com correspondência fonética/fuzzy — mais complexo, decisão arquitetural pendente (IA por requisição vs. `pg_trgm`/full-text no Postgres).

## Perguntas abertas ao Gerente (postadas na Issue em 2026-08-20)
1. Escopo: manter os 4 itens em #230 com sub-issues, ou separar item 4 em issue própria? (recomendação do PM: separar)
2. Item 4: busca via IA (custo por requisição, mais flexível) ou técnica de banco (`pg_trgm`/full-text, sem custo de IA, mais rígida)? Ou Arquiteto avalia e recomenda?
3. Item 2: alguma pista de quando/como o bug do slider acontece? (não bloqueante)
4. Confirmar definição de pronto por item (proposta enviada na issue).
5. Rota do pipeline: manter `backlog`, mudar para `normal`, ou rotas diferentes por issue caso item 4 seja separado?

## Notas
- Nenhum código ou arquitetura decidido ainda — aguardando Gate 1.
- openspec change ainda não criado (será feito na Fase 2, após respostas do Gerente).

## Custo (ledger)
| # | Etapa | Agente | Modelo | Tokens | Tools | Tempo (s) |
|---|---|---|---|---|---|---|
| 1 | PM Fase 1 | pm-analista-negocios | Sonnet | (preencher pela sessão principal via usage do HANDOFF) | - | - |
