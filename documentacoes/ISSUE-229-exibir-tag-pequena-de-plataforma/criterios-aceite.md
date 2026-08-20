# Critérios de Aceite — ISSUE-229: Tag pequena de plataforma de origem nos cards de produto

## 1. Exibição da tag na home
**Given** o visitante está na home do site público
**And** existem produtos publicados com plataforma de origem identificada (ex.: Mercado Livre, Amazon, Shopee)
**When** a home carrega e renderiza os cards de produto
**Then** cada card exibe uma tag de texto pequena e discreta próxima ao preço, indicando a plataforma de origem do produto
**And** a tag não interfere na leitura do título, imagem ou preço do produto

## 2. Exibição da tag na página de categoria
**Given** o visitante está navegando em uma página de categoria
**And** existem produtos publicados com plataforma de origem identificada
**When** a página de categoria carrega e renderiza os cards de produto
**Then** cada card exibe a mesma tag de texto de plataforma, próxima ao preço, com o mesmo padrão visual usado na home

## 3. Exibição da tag na página de oferta/detalhe
**Given** o visitante abre a página de detalhe/oferta de um produto (ou uma tela que reutiliza o componente de card de produto)
**And** o produto tem plataforma de origem identificada
**When** a página renderiza o card do produto
**Then** a tag de texto de plataforma é exibida próxima ao preço, consistente com as demais telas

## 4. Produto sem plataforma de origem identificada
**Given** um produto não possui plataforma de origem identificada (campo nulo/vazio)
**When** o card desse produto é renderizado em qualquer tela (home, categoria ou oferta)
**Then** a tag não é exibida
**And** o layout do card permanece íntegro, sem espaço vazio quebrado ou deslocamento de outros elementos no lugar da tag

## 5. Plataforma com valor não mapeado
**Given** um produto possui um valor de plataforma de origem que não está mapeado para um texto de exibição conhecido
**When** o card desse produto é renderizado
**Then** a tag não é exibida (mesmo tratamento do critério 4)
**And** nenhum valor técnico/cru (enum, código interno) é exibido na tela

## 6. Legibilidade em mobile
**Given** o visitante acessa o site público por um dispositivo mobile (viewport estreito, cards em layout compacto)
**When** os cards de produto com plataforma identificada são renderizados
**Then** a tag de texto permanece visível, legível e não é cortada, sobreposta ou espremida a ponto de ficar ilegível
**And** a tag mantém a posição relativa ao preço mesmo no layout compacto

## 7. Tag não é interativa / não é filtro
**Given** o visitante vê a tag de plataforma em qualquer card
**When** o visitante clica ou toca na tag
**Then** nada acontece em termos de navegação/filtro (a tag não dispara busca, filtro por plataforma ou navegação para outra página)
**And** nenhuma nova opção de filtro/navegação por plataforma é introduzida em nenhuma tela do site público

## 8. Consistência de texto entre telas
**Given** o mesmo produto aparece em mais de uma tela (ex.: home e categoria)
**When** os cards desse produto são comparados entre as telas
**Then** o texto/estilo da tag de plataforma exibido é idêntico em todas as instâncias (mesma fonte de dado, mesmo mapeamento de texto — definido pelo UX/UI)
