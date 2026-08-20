# Estado — Issue #230

## Identificação
- issue: 230
- titulo: revisar filtros da tela de produtos do site público (desconto, preço) — item 4 (busca inteligente) separado para Issue #260
- repo: DQM-BETA/omuletachou
- repo_path: repos/omuletachou
- docs_path: repos/omuletachou/documentacoes/ISSUE-230-revisar-filtros-site-publico/
- openspec_change: repos/omuletachou/openspec/changes/issue-230-revisar-filtros-site-publico/

## Pipeline
- rota: normal
- etapa_atual: Refinamento Técnico
- ultimo_agente: pm-analista-negocios
- status_comment_id: (gerenciado pelo Coordenador — não criado ainda por este agente)

## Resumo da demanda
Escopo restrito aos itens 1-3 do pedido original (item 4 — busca inteligente — virou Issue #260, separada):
1. Remover filtro de desconto mínimo (10%+/30%+/50%+) da barra de filtros.
2. Corrigir bug no filtro de preço (slider) — pista do Gerente: arrastar rápido leva a uma página de erro sem mensagem clara (provável exceção não tratada no client). Causa raiz a investigar/documentar no refinamento técnico.
3. Permitir digitar preço mínimo/máximo (campos numéricos sincronizados com o slider), com validação de min > max e valores negativos.

## Gate 1 — respondido pelo Gerente (2026-08-20)
1. Escopo confirmado: separar item 4 (Issue #260). #230 fica só com itens 1-3.
2. Item 4: já refletido na Issue #260 (decisão: sem chamada à IA por requisição — abordagem via banco).
3. Pista do bug do slider: arrastar rápido → página de erro sem mensagem.
4. Definições de pronto confirmadas conforme propostas (ver proposal.md).
5. Rota: `normal`.

## Ambiguidade arquitetural
Avaliada como **inexistente**. Os 3 itens são mudanças de UI/bugfix pontuais no componente `filter-bar` já existente do `website/` (Next.js) — sem decisão de stack, integração externa nova ou trade-off de infraestrutura. Segue direto para o **Líder Técnico**.

## Documentos produzidos na Fase 2
- `openspec/changes/issue-230-revisar-filtros-site-publico/proposal.md`
- `documentacoes/ISSUE-230-revisar-filtros-site-publico/criterios-aceite.md` (Given/When/Then por item, incluindo o caso obrigatório "arrastar rápido → página de erro" no item 2)

## Notas
- Diretório duplicado encontrado em `documentacoes/ISSUE-230-revisar-filtros-site-público/` (com acento, criado em 2026-08-19) — parece artefato órfão de uma execução anterior; não foi tocado por este agente (fora do escopo de limpeza do PM). Sinalizar ao LT/DevOps para avaliar remoção.
- openspec change criado via `npx @fission-ai/openspec new change` — nome exigido em kebab-case minúsculo (`issue-230-...`, não `ISSUE-230-...`); path real difere do padrão usado em docs_path (que mantém `ISSUE-230` maiúsculo por convenção da squad).

## Custo (ledger)
| # | Etapa | Agente | Modelo | Tokens | Tools | Tempo (s) |
|---|---|---|---|---|---|---|
| 1 | PM Fase 1 | pm-analista-negocios | Sonnet | (preencher pela sessão principal via usage do HANDOFF) | - | - |
| 2 | PM Fase 2 | pm-analista-negocios | Sonnet | (preencher pela sessão principal via usage do HANDOFF) | - | - |
