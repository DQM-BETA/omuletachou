# Proposal — ISSUE-260: Busca textual inteligente (fonética/fuzzy) na tela de produtos do site público

## Objetivo
Adicionar um campo de busca textual à barra de filtros (`filter-bar`) da tela de listagem de produtos do site público (`website/`, Next.js), que interprete a intenção de busca do visitante de forma flexível: além de match exato, deve tolerar erros de digitação e variações de escrita (busca fonética/fuzzy), sempre trazendo resultados aproximados quando não há match exato — sem depender de chamada à IA por requisição (restrição de negócio definitiva, confirmada no Gate 1).

**Origem:** item 4 do pedido original da Issue #230, separado por decisão do Gerente por ter complexidade e decisão arquitetural distintas dos itens 1-3 (não deve travar aquela entrega).

## Usuários
- Visitante do site público (`omuletachou.com.br`) — usa a barra de filtros na tela de listagem de produtos; hoje não existe nenhum campo de busca textual (livre) na tela — este é um campo **novo**, que convive com os demais filtros (categoria, preço, plataforma etc., incluindo os da Issue #230).

## Casos de uso principais
1. O visitante digita um termo de busca no novo campo da `filter-bar` — a listagem de produtos é filtrada **em tempo real** (com debounce), sem precisar de botão/Enter, com resposta percebida como instantânea.
2. O visitante digita um termo que corresponde exatamente (ou por substring) a um título, categoria ou descrição de produto — os produtos com match no **título** aparecem primeiro no ranking de resultados, seguidos por matches em categoria e depois em descrição.
3. O visitante digita um termo com erro de digitação comum (troca de letra, letra faltando/sobrando, plural/singular, etc.) — a busca ainda retorna produtos relevantes, tratados como resultados aproximados/sugestivos, mesmo sem match exato.
4. O visitante digita um termo levemente diferente da grafia real de um produto (variação de escrita) — a busca tenta o maior número possível de correspondências aproximadas dentro da técnica de banco de dados escolhida pelo Arquiteto (meta qualitativa: cobrir o máximo de casos possível de erro de digitação/variação, sem garantia de 100% dos casos fonéticos extremos).
5. O visitante apaga o termo de busca (campo vazio) — a listagem volta ao estado padrão (sem filtro de busca textual aplicado), respeitando os demais filtros ativos.

## Casos de uso de exceção
- O termo buscado não encontra **nenhum** resultado, nem por aproximação (abaixo do menor threshold de similaridade aceitável) — a tela exibe um estado de **vazio genuíno**, com mensagem clara ao usuário (ex.: "nenhum produto encontrado para 'X'"), distinto do estado de "resultados aproximados".
- O termo buscado não encontra match exato, mas encontra resultados por aproximação/similaridade — a tela deve deixar claro ao usuário que os resultados são aproximados (ex.: mensagem "resultados aproximados para 'X'" ou equivalente), evitando a falsa impressão de match exato.
- Termo de busca muito curto (ex.: 1 caractere) — comportamento (não buscar ainda / buscar normalmente) fica a critério do refinamento técnico, desde que não gere erro nem resultado enganoso.
- Erro de rede/timeout na busca — segue o padrão de tratamento de erro já existente na aplicação para falha de carregamento de listagem, sem quebrar a tela.
- Termo de busca combinado com outros filtros ativos (preço, categoria, plataforma) — a busca textual deve compor com os demais filtros (AND lógico), não substituí-los.

## Regras de negócio (confirmadas no Gate 1)
1. Campo **novo** na `filter-bar` — não existe busca textual hoje para substituir; convive com os filtros já existentes (incluindo os revisados na Issue #230).
2. Escopo de campos buscados: **título, categoria e descrição** do produto — todos os campos textuais disponíveis.
3. Ranking prioriza match no **título**, depois categoria, depois descrição.
4. Busca dispara em **tempo real**, com debounce (valor exato de debounce e alvo de latência em ms ficam a critério do Arquiteto/LT — meta de negócio: "percebido como instantâneo pelo usuário", referência de alvo técnico: <300-500ms).
5. Comportamento **sugestivo obrigatório**: a busca nunca retorna lista vazia apenas por não haver match exato — deve tentar resultados aproximados via threshold de similaridade mais permissivo antes de declarar "sem resultados". O estado de "sem resultados" só ocorre quando nem a aproximação encontra nada (vazio genuíno).
6. Meta de qualidade da busca fuzzy/fonética é **qualitativa, não 100% garantida**: cobrir o máximo de casos possível de erro de digitação/variação de escrita, dentro da restrição técnica "sem IA".
7. **Restrição vinculante e definitiva:** proibido uso de chamada à IA (Claude) por requisição de busca — solução via técnica de banco de dados (ex.: `pg_trgm`/similaridade de trigramas, full-text search no Postgres, ou combinação). Esta restrição não será reaberta em nenhuma etapa seguinte (Arquiteto, LT, Dev).

## Integrações
- Nenhuma integração externa nova. Mudança contida no componente `filter-bar` do site público (`website/`, Next.js) e no endpoint de listagem/filtro de produtos já existente (API consumida pelo `website/`), que precisará de um novo parâmetro de busca textual e de uma consulta com suporte a similaridade no PostgreSQL (ex.: extensão `pg_trgm`, a confirmar pelo Arquiteto). Sem chamada à IA (Claude) por requisição.

## Restrições
- Rota `normal` — pipeline completo.
- Restrição de negócio definitiva: **sem IA (Claude) por requisição de busca** — solução via técnica de banco de dados.
- Performance: resposta percebida como rápida/instantânea pelo usuário em tempo real (alvo técnico definido pelo Arquiteto/LT).
- Escopo isolado à tela de listagem de produtos do site público (`website/`); sem impacto esperado em dashboard, salvo eventual necessidade de índice/migração no banco compartilhado (a confirmar no refinamento técnico).
- Sem exemplos concretos de casos fonéticos fornecidos pelo Gerente — a calibração de threshold/qualidade fica a critério do Arquiteto/LT, com meta de máxima abrangência possível dentro da restrição "sem IA".

## Definição de pronto
Ver `documentacoes/ISSUE-260-busca-textual-inteligente/criterios-aceite.md` para os critérios Given/When/Then completos. Resumo (confirmado no Gate 1):
- Campo de busca novo na `filter-bar`, buscando em título, categoria e descrição, com título priorizado no ranking.
- Busca em tempo real com debounce, resposta percebida como instantânea.
- Resultados aproximados/sugestivos exibidos quando não há match exato, com sinalização clara ao usuário (ex.: "resultados aproximados para X").
- Estado de vazio genuíno exibido apenas quando nem a aproximação encontra nada.
- Nenhuma chamada à IA por requisição de busca — solução via técnica de banco de dados.

## Ambiguidade arquitetural avaliada pelo PM
**Há ambiguidade arquitetural que justifica o Arquiteto.** A técnica exata de busca fuzzy/similaridade dentro da restrição "sem IA" não é óbvia e envolve decisões não-triviais: escolha entre `pg_trgm` (similaridade de trigramas), full-text search nativo do Postgres (`tsvector`/`tsquery`), ou uma combinação de ambos; estratégia de índice (ex.: GIN/GiST) para manter a performance em tempo real com o catálogo crescendo; definição do(s) threshold(s) de similaridade que equilibra abrangência (meta qualitativa do negócio) com relevância dos resultados; e como aplicar peso maior ao campo título no ranking (ex.: `ts_rank` com pesos por campo, ou score combinado por múltiplas colunas). Segue para o **Arquiteto**.
