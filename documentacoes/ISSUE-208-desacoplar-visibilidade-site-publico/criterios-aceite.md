# Critérios de Aceite — ISSUE-208: Desacoplar visibilidade do site público do requisito de rede social configurada

## 1. Site público independente de rede social

**Cenário 1.1 — Produto visível no site sem nenhuma rede social configurada**
- Given nenhuma rede social (Telegram, Instagram, Facebook, TikTok, YouTube) está configurada com credenciais completas no ambiente
- And um produto (de qualquer plataforma de origem: Mercado Livre, Amazon ou Shopee) foi aprovado pela IA e possui um link de afiliado válido
- When o produto é processado
- Then o produto aparece em `GET /api/public/deals`

**Cenário 1.2 — Produto visível no site com rede(s) social(is) configurada(s)**
- Given ao menos uma rede social está configurada e qualificada
- And um produto foi aprovado pela IA e possui um link de afiliado válido
- When o produto é processado
- Then o produto aparece em `GET /api/public/deals`, independentemente do resultado da publicação social (sucesso, pendente ou erro em qualquer rede)

**Cenário 1.3 — Produto sem link de afiliado válido continua bloqueado do site**
- Given um produto foi aprovado pela IA, mas a geração do link de afiliado falhou ou não produziu um link válido
- When o produto é processado
- Then o produto NÃO aparece em `GET /api/public/deals` (condição de bloqueio existente e fora de escopo desta issue)

**Cenário 1.4 — Escopo universal por plataforma de origem**
- Given produtos aprovados com link de afiliado válido oriundos de Mercado Livre, Amazon e Shopee, sem nenhuma rede social configurada
- When cada um é processado
- Then todos aparecem em `GET /api/public/deals`, sem distinção de comportamento por plataforma de origem

## 2. Fila de publicação social preservada (sem regressão)

**Cenário 2.1 — Produto com rede qualificada entra na fila normalmente**
- Given ao menos uma rede social está configurada e qualificada (credenciais completas)
- And um produto foi aprovado pela IA e possui link de afiliado válido
- When o produto é processado
- Then o produto entra em `PublicationQueue` para a(s) rede(s) qualificada(s), com o mesmo comportamento de hoje (sem regressão)

**Cenário 2.2 — Produto sem rede qualificada não entra na fila social**
- Given nenhuma rede social está qualificada no momento do processamento
- And um produto foi aprovado pela IA e possui link de afiliado válido
- When o produto é processado
- Then o produto é publicado no site (Cenário 1.1), mas não é enfileirado em `PublicationQueue` para nenhuma rede (não há rede elegível)

**Cenário 2.3 — Falha em uma rede social não afeta o site nem as demais redes**
- Given um produto elegível para múltiplas redes sociais qualificadas
- When a publicação falha em uma rede específica (ex.: erro de API do Telegram)
- Then a visibilidade do produto no site público não é afetada, e a publicação nas demais redes segue seu fluxo normal (sucesso/pendente/erro independentes por rede)

## 3. Status por destino rastreado separadamente

**Cenário 3.1 — Status do site e de cada rede social são independentes**
- Given um produto processado
- When o status de publicação é consultado (via domínio/API interna)
- Then é possível determinar, para esse produto, o status de publicação no site separadamente do status de publicação em cada rede social individual (Telegram, Instagram, Facebook, TikTok, YouTube)

**Cenário 3.2 — Ausência de rede qualificada não é registrada como erro**
- Given um produto processado sem nenhuma rede social qualificada
- When o status por destino é consultado
- Then o produto não aparece com status de "erro" para as redes sociais — o estado correto é "não aplicável"/"não elegível", distinto de uma falha de publicação

## 4. Exibição simplificada no dashboard com tooltip

**Cenário 4.1 — Status consolidado "Published"**
- Given um produto publicado em pelo menos um destino (site e/ou alguma rede social)
- When o dashboard lista o produto
- Then o status exibido na listagem principal é o rótulo consolidado "Published", independentemente de quantos ou quais destinos especificamente foram atingidos

**Cenário 4.2 — Tooltip detalha os destinos efetivos**
- Given um produto com status consolidado "Published" no dashboard
- When o operador interage com o tooltip/indicador de status
- Then é exibida a lista de destinos onde o produto foi de fato publicado (ex.: "Site", "Telegram") e, para os destinos não aplicáveis ou pendentes, essa informação também fica visível (não omitida silenciosamente)

**Cenário 4.3 — Produto ainda não publicado em nenhum destino**
- Given um produto que ainda não foi aprovado, ou foi aprovado mas ainda não processado para nenhum destino
- When o dashboard lista o produto
- Then o status exibido não é "Published" (reflete o estado real do ciclo de vida do produto: pendente, em processamento, rejeitado, erro, etc., conforme o fluxo já existente)

## 5. Sem reprocessamento retroativo dos dados atuais

**Cenário 5.1 — Reset dos dados atuais no deploy**
- Given o estado atual do banco (incluindo os 111 produtos em `Error` por falta de rede social qualificada)
- When a mudança é implantada
- Then os dados de produtos/status atuais são apagados/resetados como parte do processo de deploy (não é necessária nenhuma rotina de migração/correção automática dos dados legados)

**Cenário 5.2 — Fluxo funciona do zero pós-deploy**
- Given o banco resetado pós-deploy
- When o pipeline de coleta/processamento roda normalmente a partir daquele ponto
- Then novos produtos são processados seguindo as regras desta issue (site independente de rede social; fila social condicional a rede qualificada), sem qualquer dependência dos dados anteriores ao reset

## 6. Sem retroatividade quando rede social futura for configurada

**Cenário 6.1 — Rede social configurada não reenfileira produtos antigos**
- Given produtos já publicados no site (sem rede social qualificada no momento em que foram processados)
- When uma nova rede social é configurada e passa a ficar qualificada
- Then esses produtos antigos NÃO são automaticamente enfileirados em `PublicationQueue` para a rede recém-qualificada

**Cenário 6.2 — Rede social configurada vale para produtos novos/atualizados a partir dali**
- Given uma rede social recém-qualificada
- When um produto é processado (novo) ou atualizado após o momento da qualificação da rede
- Then esse produto passa a considerar a rede recém-qualificada normalmente na decisão de enfileiramento para `PublicationQueue`

## 7. Sem bloqueio adicional de qualidade escondido

**Cenário 7.1 — Nenhuma condição de bloqueio de site além de aprovação + link válido**
- Given um produto aprovado pela IA e com link de afiliado válido, de qualquer plataforma de origem, em qualquer situação de configuração de rede social
- When o produto é processado
- Then não existe nenhuma outra condição (categoria, faixa de preço, plataforma específica, etc.) que bloqueie sua exibição no site além de aprovação pela IA + link de afiliado válido
