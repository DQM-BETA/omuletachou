# Proposal — ISSUE-230: Revisar filtros da tela de produtos do site público (desconto, preço)

## Objetivo
Ajustar a barra de filtros (`filter-bar`) da tela de listagem de produtos do site público (`website/`, Next.js): remover o filtro de desconto mínimo, corrigir o bug do filtro de preço (slider) e permitir a digitação direta dos valores mínimo/máximo de preço, complementando o slider.

**Fora de escopo desta issue:** a busca textual "inteligente" (item 4 do pedido original) foi separada para a **Issue #260**, por ter complexidade e decisão arquitetural distintas (não trava a entrega destes 3 itens).

## Usuários
- Visitante do site público (`omuletachou.com.br`) — usa a barra de filtros na tela de listagem de produtos para refinar a busca por faixa de preço, hoje via slider; deixa de ter a opção de filtrar por desconto mínimo; passa a poder digitar os valores de preço além de arrastar.

## Casos de uso principais

### Item 1 — Remover filtro de desconto mínimo
1. O visitante acessa a tela de listagem de produtos — a barra de filtros não exibe mais o seletor "Desconto mínimo" (10%+/30%+/50%+).
2. Nenhum outro filtro ou funcionalidade da barra é afetado pela remoção.

### Item 2 — Corrigir bug do filtro de preço (slider)
1. O visitante arrasta o slider de preço lentamente, dentro dos limites normais — o filtro aplica a faixa selecionada corretamente, sem erro.
2. O visitante clica em um ponto específico do trilho do slider — o valor é ajustado para aquele ponto, sem erro.
3. O visitante seleciona os valores extremos (mínimo absoluto e máximo absoluto) — o filtro aceita e aplica normalmente.
4. **Caso obrigatório (reportado pelo Gerente):** o visitante arrasta o slider **rapidamente** — hoje isso leva a tela para uma **página de erro sem mensagem clara** (indício de exceção não tratada no client). Após a correção, o arrasto rápido não deve mais quebrar a tela — o slider deve responder normalmente ou, na pior hipótese, degradar sem crashar a navegação (sem página de erro).

### Item 3 — Digitar preço mínimo e máximo
1. O visitante digita um valor numérico no campo de preço mínimo — o slider se move para refletir esse valor.
2. O visitante digita um valor numérico no campo de preço máximo — o slider se move para refletir esse valor.
3. O visitante arrasta o slider — os campos de texto min/max são atualizados para refletir os valores correspondentes (sincronização nos dois sentidos).
4. O filtro é aplicado à listagem de produtos tanto quando o valor vem da digitação quanto do arrasto do slider.

## Casos de uso de exceção

### Item 2
- Erro de rede/timeout ao aplicar o filtro de preço (independente do bug do slider) — segue o padrão de tratamento de erro já existente na aplicação para falha de carregamento de listagem (fora do escopo desta correção específica, mas não deve ser mascarado pela correção do slider).

### Item 3
- Usuário digita um valor de **mínimo maior que o máximo** — a aplicação impede a aplicação do filtro nesse estado e sinaliza o erro de validação ao usuário (mensagem clara), sem quebrar a tela.
- Usuário digita um valor **negativo** — a aplicação impede/normaliza o valor (não aplica filtro negativo), sinalizando ao usuário.
- Usuário digita um valor não numérico ou deixa o campo vazio — a aplicação trata de forma graciosa (não gera exceção, não aplica filtro inválido); comportamento exato (ex.: reverter ao último valor válido vs. desabilitar aplicação) fica a critério do refinamento técnico/UX, desde que não quebre a tela.
- Usuário digita um valor de preço acima do máximo real disponível no catálogo (ou abaixo do mínimo real) — a aplicação trata sem erro (ex.: clamping ao limite válido ou filtro retorna lista vazia), sem crashar.

## Regras de negócio (confirmadas no Gate 1)
1. O filtro de desconto mínimo é removido por completo da UI — sem manter código morto/órfão (componente, estado, chamadas de API relacionadas exclusivamente a ele) após a remoção.
2. O bug do item 2 deve ter causa raiz identificada e documentada antes da correção — não é aceitável "silenciar" o sintoma sem entender a causa (ex.: só adicionar um try/catch genérico sem entender por que a exceção ocorre).
3. Os campos de texto min/max (item 3) e o slider representam o mesmo estado de filtro — sempre sincronizados nos dois sentidos, nunca podem divergir na tela.
4. Validação de min > max e valores negativos é obrigatória e deve ser comunicada ao usuário de forma clara (não apenas bloqueio silencioso).
5. Pista de reprodução do bug (Gate 1): o erro do item 2 ocorre ao **arrastar rápido** o slider — priorizar esse cenário na reprodução ao vivo do refinamento técnico.

## Integrações
- Nenhuma integração externa nova. Mudança contida no componente `filter-bar` existente do site público (`website/`, Next.js) e na chamada de filtro de listagem de produtos já existente (API consumida pelo `website/`). Não deve haver mudança de contrato de API além do que já existe para faixa de preço, salvo achado do refinamento técnico ao investigar a causa raiz do bug.

## Restrições
- Rota `normal` — pipeline completo, sem prazo formal adicional além do fluxo padrão.
- Escopo restrito aos itens 1-3; item 4 (busca inteligente) é tratado na Issue #260, sem dependência entre as duas — esta issue não deve ser bloqueada pela decisão arquitetural do item 4.
- Mudança isolada à tela de listagem de produtos do site público (`website/`) — sem impacto esperado em dashboard/backend além do necessário para corrigir a causa raiz do bug do slider (a confirmar no refinamento técnico: se o bug for client-side puro, não há impacto de backend).

## Definição de pronto
Ver `documentacoes/ISSUE-230-revisar-filtros-site-publico/criterios-aceite.md` para os critérios Given/When/Then completos. Resumo (confirmado no Gate 1):
- **Item 1:** seletor de desconto mínimo desaparece da barra de filtros, sem quebrar o layout, sem código órfão.
- **Item 2:** causa raiz documentada + slider funciona sem erro em arrastar rápido, clicar, valores extremos (incluindo o caso "arrastar rápido → página de erro sem mensagem", hoje reproduzível).
- **Item 3:** campos min/max sincronizados com o slider nos dois sentidos, com validação de min > max e valores negativos.

## Ambiguidade arquitetural avaliada pelo PM
**Não há ambiguidade arquitetural que justifique o Arquiteto.** Os 3 itens são mudanças de UI/bugfix pontuais dentro do componente `filter-bar` já existente do `website/` (Next.js): remoção de um seletor, correção de um bug de manipulação de estado/evento no client (a causa raiz específica é trabalho de investigação do refinamento técnico, não uma decisão de arquitetura — não há indício de que envolva escolha de stack, integração externa ou infraestrutura), e adição de campos de input sincronizados a um slider já existente. Segue direto para o **Líder Técnico** (design.md resumido + task breakdown), sem necessidade de UX/UI dedicado além de eventuais ajustes triviais de layout já cobertos pelo design system existente (a confirmar no refinamento do LT se cabe consultar UX/UI para os campos de input do item 3).
