# Critérios de Aceite — ISSUE-228: Relatório de produtos com filtros na tela Reports

## 1. Relatório de produtos publicados — exibição padrão (sem filtros)

**Cenário 1.1 — Acesso à tela Reports sem filtro aplicado**
- Given o operador acessa a tela `Reports` do dashboard
- And existem produtos com `Status = Published`
- When a tela carrega
- Then o novo relatório de produtos publicados é exibido, com cards de resumo agregados (no mínimo: total de produtos publicados no site, quebra por Plataforma, quebra por Categoria) refletindo todos os produtos `Published`
- And uma tabela/gráfico detalhado é exibido abaixo dos cards, refletindo o mesmo universo (todos os produtos `Published`)

**Cenário 1.2 — Cards e tabela/gráfico existentes preservados**
- Given o operador acessa a tela `Reports`
- When a tela carrega
- Then os cards "Hoje/Semana/Mês" e o gráfico "Publicações por rede (últimos 7 dias)" (baseados em `publication_queue`) continuam sendo exibidos normalmente, sem alteração de comportamento

**Cenário 1.3 — Nenhum produto publicado no momento**
- Given não existe nenhum produto com `Status = Published`
- When a tela `Reports` carrega
- Then os cards de resumo mostram zero (ex.: "0 produtos publicados") e a tabela/gráfico detalhado exibe um estado vazio claro, sem erro

## 2. Filtros combináveis

**Cenário 2.1 — Filtro único por Categoria**
- Given o operador está na tela `Reports` com o relatório de produtos publicados visível
- When o operador seleciona um valor no filtro de Categoria (ex.: "Eletrônicos")
- Then os cards de resumo e a tabela/gráfico detalhado são recalculados on-demand, refletindo apenas produtos `Published` daquela Categoria

**Cenário 2.2 — Filtro único por Subcategoria**
- Given o operador está na tela `Reports`
- When o operador seleciona um valor no filtro de Subcategoria
- Then o relatório é recalculado on-demand refletindo apenas produtos `Published` daquela Subcategoria

**Cenário 2.3 — Filtro único por Plataforma**
- Given o operador está na tela `Reports`
- When o operador seleciona uma Plataforma (Mercado Livre, Amazon ou Shopee)
- Then o relatório é recalculado on-demand refletindo apenas produtos `Published` originados daquela plataforma

**Cenário 2.4 — Filtro único por Status**
- Given o operador está na tela `Reports`
- When o operador seleciona um Status diferente de `Published` (ex.: `Pending`, `Error`, conforme os status existentes no domínio)
- Then o relatório é recalculado on-demand refletindo os produtos naquele Status (o relatório não fica restrito somente a `Published` quando o operador filtra explicitamente por outro status — o padrão sem filtro é `Published`, mas o filtro de Status permite consultar outros estados)

**Cenário 2.5 — Filtro único por Faixa de data de coleta**
- Given o operador está na tela `Reports`
- When o operador define uma faixa de data de coleta (data inicial e final)
- Then o relatório é recalculado on-demand refletindo apenas produtos coletados dentro dessa faixa (inclusive nos limites)

**Cenário 2.6 — Combinação de múltiplos filtros (lógica AND)**
- Given o operador está na tela `Reports`
- When o operador aplica simultaneamente Plataforma = "Mercado Livre", Categoria = "Eletrônicos" e Faixa de data de coleta = últimos 7 dias
- Then o relatório é recalculado on-demand refletindo apenas produtos que atendem a **todas** as condições simultaneamente (interseção, não união)

**Cenário 2.7 — Combinação de filtros sem resultados**
- Given o operador aplica uma combinação de filtros para a qual nenhum produto se qualifica
- When o relatório é recalculado
- Then os cards de resumo mostram zero e a tabela/gráfico detalhado exibe estado vazio, sem erro e sem dado de uma consulta anterior remanescente na tela

**Cenário 2.8 — Limpar filtros**
- Given o operador tem um ou mais filtros aplicados
- When o operador limpa os filtros (remove todos ou usa uma ação de "limpar")
- Then o relatório volta a refletir o universo completo de produtos `Published` (equivalente ao Cenário 1.1)

**Cenário 2.9 — Troca de filtro sem reload de página**
- Given o operador tem um filtro aplicado (ex.: Categoria = "Eletrônicos") e o relatório exibido
- When o operador troca o valor do filtro (ex.: Categoria = "Moda") sem recarregar a página
- Then o relatório recalcula automaticamente para refletir o novo filtro, sem exigir ação adicional além da própria mudança do filtro

## 3. Atualização on-demand (sem tempo real)

**Cenário 3.1 — Sem polling/atualização automática**
- Given o operador está com o relatório de produtos publicados aberto na tela `Reports`, sem interagir com nenhum filtro
- And um novo produto é publicado no site em outro processo (ex.: pipeline de coleta em background) enquanto a tela permanece aberta
- When nenhuma ação de filtro é realizada pelo operador
- Then o relatório na tela **não** atualiza automaticamente para refletir o novo produto (sem polling/websocket) — o operador precisa recarregar a página ou reaplicar/alterar um filtro para ver o dado atualizado

## 4. Sem exportação nesta versão

**Cenário 4.1 — Ausência de funcionalidade de exportação**
- Given o operador está na tela `Reports` com o relatório de produtos publicados visível
- When o operador procura por uma opção de exportar (CSV/Excel) ou imprimir o relatório
- Then essa opção não existe nesta versão — o uso é exclusivamente de consulta em tela

## 5. Tratamento de erro

**Cenário 5.1 — Falha de comunicação ao aplicar filtro**
- Given o operador aplica ou altera um filtro no relatório
- When a chamada ao backend para recalcular o relatório falha (erro de rede, timeout, erro do servidor)
- Then a tela indica claramente que houve falha ao carregar os dados (não exibe silenciosamente o dado antigo como se fosse atualizado, nem quebra a tela), e permite ao operador tentar novamente
