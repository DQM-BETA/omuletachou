# Critérios de Aceite — ISSUE-230: Revisar filtros da tela de produtos do site público (desconto, preço)

## Item 1 — Remover filtro de desconto mínimo

### 1.1 Seletor removido da barra de filtros
**Given** o visitante acessa a tela de listagem de produtos do site público
**When** a barra de filtros é renderizada
**Then** o seletor "Desconto mínimo" (10%+/30%+/50%+) não é exibido
**And** nenhum outro filtro da barra é afetado (layout permanece íntegro, sem espaço vazio quebrado no lugar do seletor removido)

### 1.2 Sem código órfão
**Given** o filtro de desconto mínimo foi removido da UI
**When** o código do componente `filter-bar` e dependências diretas são revisados
**Then** não restam referências órfãs (estado não utilizado, chamadas de API exclusivas ao filtro de desconto, props/tipos mortos) relacionadas exclusivamente a esse filtro

## Item 2 — Corrigir bug do filtro de preço (slider)

### 2.1 Arrastar lentamente dentro dos limites
**Given** o visitante está na tela de listagem de produtos com o slider de preço visível
**When** o visitante arrasta o slider lentamente, dentro da faixa de valores válida
**Then** o filtro de preço é aplicado corretamente à faixa selecionada, sem erro e sem navegação para página de erro

### 2.2 Clicar em um ponto do trilho
**Given** o visitante está com o slider de preço visível
**When** o visitante clica diretamente em um ponto do trilho (sem arrastar)
**Then** o valor correspondente é assumido pelo slider e o filtro é aplicado sem erro

### 2.3 Valores extremos
**Given** o visitante está com o slider de preço visível
**When** o visitante seleciona o valor mínimo absoluto e o valor máximo absoluto disponíveis
**Then** o filtro aceita e aplica esses valores extremos sem erro

### 2.4 Arrastar rápido (caso obrigatório — pista do Gerente)
**Given** o visitante está na tela de listagem de produtos com o slider de preço visível
**When** o visitante arrasta o slider de preço **rapidamente** (movimento veloz, sem pausas)
**Then** a tela **não** navega para uma página de erro
**And** o slider responde ao gesto de forma consistente (aplica o valor correspondente ao ponto onde o arrasto termina, ou no mínimo não trava/quebra a interface)
**And**, se qualquer condição inesperada ocorrer durante o gesto, ela é tratada de forma controlada (sem exceção não tratada estourando para uma página de erro genérica sem mensagem)

### 2.5 Causa raiz documentada
**Given** o bug do item 2.4 foi investigado no refinamento técnico
**When** a correção é implementada
**Then** a causa raiz identificada está documentada (no PR, no `design.md` ou em comentário técnico associado à issue)
**And** a correção resolve a causa raiz, não apenas suprime o sintoma (ex.: não é aceitável apenas capturar a exceção genérica sem entender por que ela ocorre)

## Item 3 — Digitar preço mínimo e máximo

### 3.1 Digitar valor mínimo atualiza o slider
**Given** o visitante está na tela de listagem de produtos com os campos de preço mínimo/máximo visíveis
**When** o visitante digita um valor numérico válido no campo de preço mínimo
**Then** o slider se move para refletir esse valor como limite inferior da faixa
**And** o filtro de preço é aplicado à listagem com o novo valor mínimo

### 3.2 Digitar valor máximo atualiza o slider
**Given** o visitante está com os campos de preço mínimo/máximo visíveis
**When** o visitante digita um valor numérico válido no campo de preço máximo
**Then** o slider se move para refletir esse valor como limite superior da faixa
**And** o filtro de preço é aplicado à listagem com o novo valor máximo

### 3.3 Arrastar o slider atualiza os campos de texto
**Given** o visitante está com o slider e os campos de texto min/max visíveis
**When** o visitante arrasta o slider para uma nova faixa
**Then** os campos de texto min/max são atualizados para refletir os valores correspondentes à posição do slider
**And** slider e campos de texto nunca ficam com valores divergentes na tela

### 3.4 Validação — mínimo maior que máximo
**Given** o visitante está com os campos de preço mínimo/máximo visíveis
**When** o visitante digita um valor de mínimo maior que o valor atual de máximo (ou vice-versa)
**Then** a aplicação impede a aplicação do filtro nesse estado inválido
**And** o usuário recebe uma indicação clara do erro de validação (mensagem visível, não apenas bloqueio silencioso)

### 3.5 Validação — valor negativo
**Given** o visitante está com os campos de preço mínimo/máximo visíveis
**When** o visitante digita um valor negativo em qualquer um dos campos
**Then** a aplicação não aplica um filtro com valor negativo
**And** o usuário recebe uma indicação do erro/correção (mensagem visível ou normalização perceptível, ex.: valor ajustado para zero)

### 3.6 Entrada inválida não numérica ou vazia
**Given** o visitante está com os campos de preço mínimo/máximo visíveis
**When** o visitante apaga o valor de um campo ou digita algo não numérico
**Then** a aplicação não gera exceção nem aplica um filtro inválido
**And** o comportamento (reverter ao último valor válido, desabilitar aplicação do filtro, ou equivalente) é consistente e não quebra a tela

### 3.7 Valor fora dos limites reais do catálogo
**Given** o visitante está com os campos de preço mínimo/máximo visíveis
**When** o visitante digita um valor acima do preço máximo real disponível no catálogo (ou abaixo do mínimo real)
**Then** a aplicação trata o caso sem erro (ex.: ajusta ao limite válido mais próximo ou retorna lista vazia de resultados)
**And** nenhuma exceção não tratada ou página de erro é exibida
