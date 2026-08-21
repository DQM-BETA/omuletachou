# Critérios de Aceite — ISSUE-231: Rastreio de cliques + faixa de produtos sugeridos (site público)

## Item 2 — Rastreio de cliques

### 2.1 Clique em card na listagem normal registra evento
**Given** o visitante está na tela de listagem de produtos do site público (fora da faixa de sugeridos)
**When** o visitante clica em um card de produto
**Then** um evento de clique anônimo é registrado, contendo ao menos o identificador do produto e o timestamp do clique (sem dado de usuário/sessão)
**And** o destino do clique (link de afiliado ou página, conforme já funciona hoje) permanece exatamente o mesmo — nenhuma mudança de fluxo de navegação

### 2.2 Clique em card dentro do carrossel de sugeridos registra evento igual
**Given** o visitante está visualizando a faixa/carrossel de produtos sugeridos
**When** o visitante clica em um card de produto dentro do carrossel
**Then** o mesmo tipo de evento de clique anônimo do item 2.1 é registrado, sem distinção de origem (carrossel vs. listagem normal)
**And** o destino do clique é o mesmo que o card teria na listagem normal (sem alteração de fluxo)

### 2.3 Evento é anônimo
**Given** um clique em qualquer card de produto (listagem normal ou carrossel de sugeridos)
**When** o evento de clique é registrado
**Then** o registro não contém identificador de usuário, sessão, cookie de rastreio pessoal ou qualquer dado que permita ligar o clique a uma pessoa específica
**And** o registro contém apenas produto + timestamp (e metadados técnicos não-pessoais definidos no refinamento técnico, se houver)

### 2.4 Falha no registro não bloqueia a navegação
**Given** o visitante clica em um card de produto
**When** ocorre uma falha (rede, timeout, erro no serviço de rastreio) ao tentar registrar o evento de clique
**Then** o visitante é levado ao destino do clique normalmente, sem atraso perceptível nem bloqueio da navegação
**And** nenhuma mensagem de erro é exibida ao visitante por conta da falha no registro (falha é tratada de forma transparente para a experiência do usuário)

## Item 1 — Faixa de produtos sugeridos (carrossel horizontal)

### 1.1 Faixa exibe produtos da categoria filtrada, ordenados por mais clicados
**Given** o visitante aplicou um filtro de categoria na tela de listagem de produtos e o filtro retorna ao menos 4 produtos
**When** a faixa de produtos sugeridos é renderizada
**Then** a faixa exibe produtos da mesma categoria do filtro atual
**And** os produtos são ordenados por contagem de cliques decrescente dentro dessa categoria (mais clicados primeiro)
**And** a faixa exibe até 10 produtos por carregamento

### 1.2 Fallback — filtro vazio exibe mais clicados em geral
**Given** o visitante aplicou um filtro que não retorna nenhum produto na listagem principal
**When** a faixa de produtos sugeridos é renderizada
**Then** a faixa exibe os produtos mais clicados em geral (todas as categorias, produtos ativos/disponíveis), ordenados por contagem de cliques decrescente
**And** não há restrição de categoria aplicada a essa lista de fallback

### 1.3 Carrossel horizontal com setas de navegação
**Given** a faixa de produtos sugeridos está sendo exibida com produtos suficientes para navegação
**When** o visitante interage com a faixa
**Then** os produtos são exibidos em formato de carrossel horizontal
**And** há uma seta de navegação para a esquerda e uma seta para a direita, permitindo percorrer os produtos sugeridos sem recarregar a página
**And** as setas ficam desabilitadas/ocultas nos extremos quando não há mais itens naquela direção (comportamento padrão de carrossel)

### 1.4 Clique em produto do carrossel funciona como um card normal
**Given** o visitante está navegando pela faixa de produtos sugeridos
**When** o visitante clica em um produto do carrossel
**Then** o comportamento é idêntico ao clique em um card da listagem normal (mesmo destino atual, sem mudança de fluxo)
**And** o clique é contabilizado conforme item 2.2

### 1.5 Mínimo de produtos para a faixa aparecer
**Given** a categoria do filtro atual (ou o fallback de mais clicados) tem menos de 4 produtos disponíveis
**When** a tela de listagem de produtos é renderizada
**Then** a faixa de produtos sugeridos não é exibida (nenhum carrossel vazio ou com poucos itens é mostrado)

### 1.6 Categoria com produtos insuficientes para completar 10, mas com pelo menos 4
**Given** a categoria do filtro atual tem entre 4 e 9 produtos disponíveis
**When** a faixa de produtos sugeridos é renderizada
**Then** a faixa exibe os produtos disponíveis dessa categoria (sem completar a lista com produtos de outras categorias)
**And** a faixa é exibida normalmente (respeita apenas o mínimo do item 1.5)

### 1.7 Sem cliques registrados ainda (catálogo novo / feature recém-lançada)
**Given** nenhum produto da categoria filtrada (ou do catálogo geral, no caso de fallback) tem cliques registrados
**When** a faixa de produtos sugeridos é renderizada
**Then** a faixa ainda é exibida (respeitando o mínimo do item 1.5), com os produtos empatados em 0 cliques desempatados por um critério existente no catálogo (ex.: mais recentes primeiro), sem erro ou lista vazia por falta de histórico de cliques

### 1.8 Faixa não quebra a tela em caso de indisponibilidade
**Given** o endpoint/serviço responsável por montar a faixa de sugeridos está indisponível ou retorna erro
**When** a tela de listagem de produtos é renderizada
**Then** a listagem principal de produtos continua funcionando normalmente
**And** a faixa de sugeridos é omitida de forma graciosa (sem erro visível ao usuário, sem quebrar o restante da página)
