# Critérios de Aceite — ISSUE-260: Busca textual inteligente (fonética/fuzzy) na tela de produtos do site público

## Funcionalidade 1 — Campo de busca na filter-bar

### 1.1 Campo visível e disponível
**Given** o visitante acessa a tela de listagem de produtos do site público
**When** a barra de filtros (`filter-bar`) é renderizada
**Then** um novo campo de busca textual é exibido, sem substituir nenhum filtro existente (categoria, preço, plataforma etc.)

### 1.2 Campo vazio não filtra
**Given** o visitante está na tela de listagem de produtos
**When** o campo de busca está vazio (estado inicial ou após ser limpo)
**Then** a listagem exibe os produtos normalmente, sem filtro de busca textual aplicado, respeitando apenas os demais filtros ativos

## Funcionalidade 2 — Busca em tempo real com debounce

### 2.1 Disparo automático ao digitar
**Given** o visitante está com o campo de busca em foco
**When** o visitante digita um termo de busca
**Then** a listagem de produtos é filtrada automaticamente, sem necessidade de clicar em botão ou pressionar Enter
**And** a busca não dispara uma requisição a cada tecla digitada — usa debounce para evitar disparos excessivos durante a digitação contínua

### 2.2 Resposta percebida como instantânea
**Given** o visitante termina de digitar (ou pausa a digitação) no campo de busca
**When** o debounce expira e a busca é disparada
**Then** o resultado é exibido em tempo percebido como instantâneo pelo usuário (alvo técnico de referência: resposta em até 300-500ms, a validar pelo Arquiteto/LT)
**And** a interface sinaliza estado de carregamento (loading) se a resposta ultrapassar um tempo perceptível, evitando a sensação de tela travada

## Funcionalidade 3 — Escopo de campos buscados e ranking

### 3.1 Busca cobre título, categoria e descrição
**Given** o catálogo de produtos possui itens com termos distintos em título, categoria e descrição
**When** o visitante busca um termo que existe em apenas um desses campos (ex.: só na descrição de um produto)
**Then** o produto correspondente aparece nos resultados

### 3.2 Ranking prioriza o título
**Given** um termo de busca tem match tanto no título de um produto quanto na descrição de outro produto (sem match no título deste último)
**When** a busca é executada
**Then** o produto com match no título aparece antes do produto com match apenas na descrição no ranking de resultados

### 3.3 Ordem de prioridade completa
**Given** um termo de busca gera matches em título, categoria e descrição de produtos diferentes
**When** os resultados são ordenados
**Then** a ordem de prioridade é: match no título primeiro, depois match em categoria, depois match em descrição (podendo haver combinação/score quando um produto tem match em mais de um campo)

## Funcionalidade 4 — Comportamento sugestivo (sem match exato)

### 4.1 Resultados aproximados quando não há match exato
**Given** o visitante digita um termo com erro de digitação comum (ex.: troca de letra, letra faltando/sobrando, plural/singular) que não corresponde exatamente a nenhum produto
**When** a busca é executada
**Then** a aplicação não retorna lista vazia automaticamente — busca resultados aproximados usando um threshold de similaridade mais permissivo
**And** produtos relevantes (aproximados ao termo buscado) são exibidos como sugestão

### 4.2 Sinalização de resultado aproximado
**Given** a busca retornou resultados apenas por aproximação (sem nenhum match exato)
**When** os resultados são exibidos ao visitante
**Then** a interface sinaliza claramente que os resultados são aproximados (ex.: mensagem "resultados aproximados para 'X'" ou equivalente visual), evitando que o usuário entenda como match exato

### 4.3 Cobertura qualitativa de variações
**Given** o visitante digita variações comuns de escrita ou erros de digitação de um termo existente no catálogo
**When** a busca é executada
**Then** a aplicação tenta o máximo de correspondências possível dentro da técnica de banco de dados definida pelo Arquiteto (meta qualitativa — não é exigida cobertura de 100% dos casos fonéticos extremos, mas erros de digitação comuns devem funcionar na maioria dos casos)

## Funcionalidade 5 — Estado de vazio genuíno

### 5.1 Nenhum resultado, nem aproximado
**Given** o visitante digita um termo que não corresponde a nenhum produto, nem mesmo por aproximação (abaixo do menor threshold de similaridade aceitável)
**When** a busca é executada
**Then** a aplicação exibe um estado de vazio genuíno, com mensagem clara ao usuário (ex.: "nenhum produto encontrado para 'X'")
**And** esse estado é visualmente distinto do estado de "resultados aproximados" (funcionalidade 4.2)

## Funcionalidade 6 — Composição com outros filtros

### 6.1 Busca textual combina com filtros existentes
**Given** o visitante tem um ou mais filtros ativos (ex.: categoria, faixa de preço, plataforma)
**When** o visitante digita um termo no campo de busca textual
**Then** a listagem resultante respeita tanto o termo de busca quanto os demais filtros ativos (composição lógica AND), não um substituindo o outro

## Funcionalidade 7 — Restrição de negócio "sem IA"

### 7.1 Nenhuma chamada à IA por requisição de busca
**Given** qualquer busca textual disparada pelo visitante
**When** a requisição é processada no backend
**Then** nenhuma chamada à API de IA (Claude) é realizada como parte do fluxo de busca — a similaridade/fuzzy matching é resolvida inteiramente via técnica de banco de dados (ex.: `pg_trgm`, full-text search no PostgreSQL, ou combinação definida pelo Arquiteto)

## Casos de exceção

### E.1 Termo muito curto
**Given** o visitante digita um termo de busca muito curto (ex.: 1 caractere)
**When** a busca é avaliada
**Then** a aplicação trata o caso de forma consistente (buscar normalmente ou aguardar mais caracteres, a critério do refinamento técnico), sem gerar erro ou resultado enganoso

### E.2 Erro de rede/timeout
**Given** o visitante digita um termo de busca
**When** ocorre erro de rede ou timeout na requisição de busca
**Then** a aplicação trata o erro seguindo o padrão já existente para falha de carregamento de listagem, sem quebrar a tela (sem página de erro genérica sem mensagem)
