# Proposal — ISSUE-229: Tag pequena de plataforma de origem nos cards de produto (site público)

## Objetivo
Exibir, no card de produto do site público (`website/`, Next.js), uma **tag de texto pequena e discreta** indicando a plataforma de origem do produto (Mercado Livre, Amazon, Shopee), posicionada **próxima ao preço**. É puramente sinalização visual — não é filtro, categoria ou opção de navegação, e não altera a decisão da Issue #167 (que removeu a distinção de plataforma da navegação/filtros do site público).

## Usuários
- Visitante do site público (`omuletachou.com.br`) — ao navegar pela home, por uma categoria, ou ao abrir a página de uma oferta, passa a ver de forma discreta qual plataforma (Mercado Livre/Amazon/Shopee) originou aquele produto, sem que isso vire um mecanismo de busca/filtro.

## Casos de uso principais
1. O visitante acessa a home e vê os cards de produto com uma tag de texto pequena próxima ao preço, indicando a plataforma de origem (ex.: "Mercado Livre", "Amazon", "Shopee").
2. O visitante navega até uma página de categoria — os cards de produto listados seguem o mesmo padrão: tag de texto da plataforma próxima ao preço.
3. O visitante abre a página de detalhe/oferta de um produto — o card de produto ali exibido (ou elemento equivalente que reutiliza o componente de card) também mostra a tag.
4. O visitante acessa o site pelo celular (mobile) — a tag continua visível e legível mesmo no layout compacto do card, sem sobrepor ou colidir com o preço ou outros elementos.

## Casos de uso de exceção
- Produto sem plataforma de origem identificada (campo nulo/vazio no banco) — a tag **não é exibida** (card renderiza normalmente, sem tag e sem espaço vazio/quebrado no lugar dela). Decisão de bom senso de produto: mostrar um estado "desconhecido" teria menos valor para o usuário do que simplesmente omitir, e evita comunicar informação incorreta.
- Plataforma de origem com valor não mapeado/desconhecido (ex.: novo valor de enum ainda não previsto no texto de exibição) — mesmo tratamento acima: ocultar a tag em vez de exibir um valor cru/técnico (ex.: não vazar enum interno tipo `UNKNOWN` ou código bruto na tela).

## Regras de negócio (confirmadas no Gate 1)
1. **Sinalização visual apenas** — a tag não é clicável como filtro, não gera navegação, não introduz nova categoria. Não conflita com a Issue #167.
2. **Posição**: próxima ao preço no card, com destaque discreto (não deve competir visualmente com preço, título ou imagem do produto).
3. **Formato**: texto (não ícone/logo de plataforma). Texto pode ser o nome da plataforma ou uma abreviação (ex.: "Mercado Livre" ou "ML") — o texto exato e o estilo visual (cor, tipografia, badge/pill) ficam a cargo do UX/UI, consultando o design system do Figma.
4. **Escopo de telas**: a tag aparece em **todas** as instâncias do card de produto no site público — home, página de categoria e página de oferta/detalhe (qualquer tela que reutilize o componente de card).
5. **Produto sem plataforma identificada**: tag oculta (ver caso de uso de exceção acima).
6. **Mobile**: a tag deve permanecer legível em cards compactos — não pode ser cortada, sobreposta ou tornar-se ilegível em telas pequenas.

## Integrações
- Nenhuma integração externa nova. O dado de plataforma de origem já existe no schema do produto (consumido hoje internamente, ex. nos publishers/coleta) e já está disponível via API pública consumida pelo `website/` — a mudança é de exibição no componente de card existente, não requer novo endpoint nem novo campo de API (a confirmar no refinamento técnico se o campo já está exposto na resposta da API pública consumida pelo `website/`; se não estiver, é ajuste simples de exposição, não uma nova integração).

## Restrições
- Sem prazo formal definido (rota `normal` — segue o pipeline completo de priorização e execução).
- Não pode reintroduzir filtro/navegação por plataforma no site público (fora de escopo, decisão já tomada na Issue #167).
- Mudança isolada ao componente de card de produto do `website/` (Next.js) — sem impacto em backend/dashboard além da eventual exposição do campo de plataforma na API pública, se ainda não exposto.
- Texto exato e estilo visual da tag (cor, abreviação vs. nome completo, badge/pill) dependem de definição do UX/UI antes da implementação.

## Definição de pronto
Ver `documentacoes/ISSUE-229-exibir-tag-pequena-de-plataforma/criterios-aceite.md` para os critérios Given/When/Then completos. Resumo:
- Tag de texto pequena e discreta, próxima ao preço, exibida em todos os cards de produto com plataforma de origem identificada.
- Presente em todas as telas que usam o card de produto: home, categoria e página de oferta/detalhe.
- Produto sem plataforma identificada (ou valor não mapeado): card renderiza sem a tag, sem quebra de layout.
- Tag legível e sem sobreposição/corte em mobile (cards compactos).
- Tag não é clicável nem funciona como filtro/navegação.

## Ambiguidade arquitetural avaliada pelo PM
**Não há ambiguidade arquitetural que justifique o Arquiteto.** É uma mudança de exibição num componente já existente do `website/` (Next.js) — o dado de plataforma de origem já existe no domínio do produto (usado hoje na coleta/publishers), a única verificação técnica pendente é se o campo já está exposto na resposta da API pública consumida pelo card (ajuste simples de serialização, se necessário — não é decisão de arquitetura, é detalhe de implementação a resolver no refinamento do LT). Não há integração externa nova, não há decisão de stack, não há trade-off de performance/infraestrutura envolvido. Segue direto para o **Líder Técnico** (design.md resumido + task breakdown), com apoio do **UX/UI** para definir o texto exato/estilo da tag antes da implementação.
