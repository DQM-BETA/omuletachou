# Proposal — ISSUE-208: Desacoplar visibilidade do site público do requisito de rede social configurada

## Objetivo
Desacoplar a exibição de um produto no site público (`omuletachou.com.br` / `GET /api/public/deals`) do requisito hoje existente de "pelo menos uma rede social qualificada com credenciais completas". Um produto aprovado pela IA e com link de afiliado válido deve aparecer no site público independentemente de haver rede social configurada. O fluxo de publicação social (fila de publicação por rede) continua existindo normalmente para os produtos que têm rede(s) social(is) qualificada(s) — as duas coisas passam a ser rastreadas e avaliadas separadamente, mas exibidas de forma simplificada e unificada ("Published") no dashboard, com detalhe sob demanda via tooltip.

Motivação: no teste end-to-end mais recente em ambiente local, 111 produtos aprovados pela IA e com link de afiliado real e válido ficaram presos em `Error` — não por problema de qualidade do produto, mas porque nenhuma rede social estava configurada no ambiente. Isso deixou o site público vazio mesmo com produtos prontos para gerar receita de afiliado (achado original nas Issues #182/#199/#204).

## Usuários
- Visitante do site público (`omuletachou.com.br`) — passa a ver produtos aprovados + com link de afiliado válido, mesmo em ambientes/momentos sem nenhuma rede social configurada.
- Operador/administrador (dashboard Angular) — continua vendo o status de publicação, agora simplificado ("Published") com detalhe de "onde" via tooltip, em vez de um único status combinado que hoje pode ficar preso em `Error` por causa de uma rede social ausente.
- Sistema (`ProcessorJob`, fila de publicação social `PublicationQueue`, publishers por rede) — passa a tratar "publicação no site" e "publicação em cada rede social" como decisões independentes, sem que a ausência da segunda bloqueie a primeira.

## Casos de uso principais
1. Um produto de qualquer plataforma de origem (Mercado Livre, Amazon, Shopee) é aprovado pela IA e recebe um link de afiliado válido. Independentemente de haver ou não rede social qualificada configurada no momento, o produto se torna visível no site público (`GET /api/public/deals`).
2. Se, no momento do processamento, existir ao menos uma rede social qualificada (credenciais completas), o produto segue normalmente para a fila de publicação social (`PublicationQueue`) — sem regressão do comportamento atual dessa fila.
3. Se não existir nenhuma rede social qualificada no momento do processamento, o produto ainda assim é publicado no site; ele simplesmente não entra na fila de publicação social (não há rede para publicar).
4. No dashboard, o operador vê o produto com status simplificado "Published" assim que ele está visível em pelo menos um destino (site e/ou alguma rede social). Ao interagir com um tooltip/indicador, o operador vê o detalhamento por destino: site (sim/não) e cada rede social configurada (publicado / pendente / erro / não aplicável).
5. Quando uma nova rede social é configurada no futuro (credenciais completas adicionadas), a regra de qualificação passa a valer **somente para produtos novos ou atualizados a partir daquele momento** — produtos já existentes/publicados anteriormente não são reprocessados retroativamente para a fila social.

## Casos de uso de exceção
- Produto aprovado pela IA, mas **sem** link de afiliado válido (falha de geração de link) — continua sem ser publicado no site; essa condição de bloqueio não muda com esta issue (fora de escopo: o desacoplamento é especificamente da exigência de rede social, não das demais condições de qualidade já existentes).
- Produto processado num momento em que nenhuma rede social está qualificada, e depois (dias/semanas) uma rede é configurada: o produto **não** entra retroativamente na fila social (confirmado pelo Gerente no Gate 1 — sem retroatividade automática).
- Falha em uma rede social específica na fila de publicação (ex.: erro de API do Telegram) não deve, em nenhuma hipótese, afetar a visibilidade do produto no site — os destinos são independentes entre si.
- Não há nenhuma exceção adicional de qualidade/bloqueio de site além de "aprovado pela IA + link de afiliado válido" (confirmado pelo Gerente: "não há motivo para bloquear um produto aprovado do site").

## Regras de negócio (confirmadas no Gate 1)
1. **Site público independente de rede social**: a exibição de um produto no site (`GET /api/public/deals`) depende apenas de (a) aprovação pela IA e (b) link de afiliado válido — nunca da existência de rede social configurada/qualificada.
2. **Status rastreado separadamente por destino**: o sistema deve manter conhecimento de "publicado no site" separado de "publicado em cada rede social" (Telegram, Instagram, Facebook, TikTok, YouTube — todas as redes suportadas hoje ou no futuro). A modelagem concreta (novo(s) campo(s), enum, tabela de tracking por destino) é decisão técnica, não de negócio — ver seção de ambiguidade abaixo.
3. **Exibição simplificada no dashboard**: apesar do rastreio granular por destino, a UI do dashboard mostra um único rótulo consolidado "Published" para o produto assim que ele está visível em pelo menos um destino (site conta como destino). Detalhe por destino (site + cada rede social, com seu status individual) fica disponível via tooltip/hover, não poluindo a listagem principal.
4. **Escopo universal**: a regra vale para todas as plataformas de origem do produto (Mercado Livre, Amazon, Shopee) e todas as redes sociais suportadas — não é específica do cenário do Mercado Livre que motivou a descoberta.
5. **Sem reprocessamento retroativo dos dados atuais**: os 111 produtos hoje em `Error` (e demais dados de status atuais) serão apagados/resetados para recomeçar o fluxo do zero após o deploy desta mudança — não há necessidade de rotina de migração/correção de dados legados.
6. **Sem bloqueio adicional de qualidade**: não existe nenhuma regra de bloqueio de site escondida atrás do requisito de rede social — o único gate para o site é aprovação da IA + link de afiliado válido.
7. **Rede social configurada no futuro não é retroativa**: quando uma rede social passar a ser qualificada (credenciais completas), a regra vale apenas para produtos novos ou atualizados dali em diante. Produtos já processados antes daquele momento não são automaticamente enfileirados para a rede recém-configurada.
8. **Fila de publicação social (`PublicationQueue`) não é removida nem tem seu comportamento de negócio alterado** para o caso em que há rede(s) qualificada(s) — produtos com rede social qualificada continuam entrando na fila normalmente, exatamente como hoje. A única mudança de negócio é que a ausência de rede qualificada deixa de ser uma condição de bloqueio para o site.

## Integrações
- Nenhuma integração externa nova. As integrações existentes com as redes sociais (Telegram, Instagram, Facebook, TikTok, YouTube via seus respectivos publishers) permanecem inalteradas em seu funcionamento — a mudança é sobre quando/como o sistema decide se o produto é elegível para cada destino, não sobre como cada publisher se comunica com sua rede.
- Sem mudança na integração de geração de link de afiliado (marketplaces de origem: Mercado Livre, Amazon, Shopee).

## Restrições
- **Sem reprocessamento retroativo**: dados atuais (incluindo os 111 produtos em `Error`) serão apagados; a mudança vale a partir do próximo ciclo de coleta/processamento pós-deploy. Não é necessário desenhar migração de dados legados.
- **Sem retroatividade quando rede social futura for configurada**: qualquer solução técnica precisa garantir que produtos antigos não sejam automaticamente reenfileirados para uma rede social recém-qualificada.
- **Compatibilidade com o fluxo existente de `PublicationQueue`**: a fila de publicação social já rastreia publicação por rede social hoje; a solução deve preservar esse rastreio e o comportamento de fila para produtos com rede(s) qualificada(s), apenas removendo o acoplamento de bloqueio do site.
- Sem urgência — segue o fluxo normal de priorização (rota `normal`).
- Decisões técnicas específicas (modelagem do "status por destino" no domínio — hoje `Product`/`ProductStatus` tem um único campo `Status`; nome de campos/enum novos; como `ProcessorJob` e `PublicController` devem mudar para refletir a nova lógica de elegibilidade; como a tooltip do dashboard deve buscar/agregar os dados de "onde foi publicado") ficam para a etapa de Arquitetura/Refinamento Técnico — não são bloqueantes de negócio, mas envolvem decisões de modelagem não-óbvias.

## Definição de pronto
Ver `documentacoes/ISSUE-208-desacoplar-visibilidade-site-publico/criterios-aceite.md` para os critérios Given/When/Then completos. Resumo:
- Produto aprovado pela IA + com link de afiliado válido aparece em `GET /api/public/deals` independentemente de haver rede social configurada/qualificada, para as 3 plataformas de origem (Mercado Livre, Amazon, Shopee).
- Produto com rede(s) social(is) qualificada(s) continua entrando normalmente na `PublicationQueue`, sem regressão do comportamento atual, para todas as redes sociais suportadas.
- Status por destino (site + cada rede social) é rastreado separadamente no domínio.
- Dashboard exibe status consolidado "Published" (produto visível em pelo menos um destino), com tooltip detalhando os destinos efetivos de publicação (site sim/não, cada rede social com seu status).
- Dados atuais (incluindo os 111 produtos em `Error`) são resetados/limpos como parte do deploy — sem necessidade de rotina de correção retroativa.
- Quando uma rede social nova for qualificada no futuro, apenas produtos novos/atualizados a partir daquele momento passam a considerá-la — sem enfileiramento retroativo de produtos antigos.

## Ambiguidade arquitetural avaliada pelo PM
**Existe ambiguidade real que exige o Arquiteto antes do refinamento técnico do LT.** As regras de negócio (desacoplamento, exibição simplificada com tooltip, escopo universal, sem retroatividade) já foram decididas pelo Gerente no Gate 1. Mas restam decisões técnicas de modelagem de domínio não-óbvias, sem resposta única de negócio:
1. **Como representar "status por destino" no domínio.** Hoje `Product`/`ProductStatus` parece ter um único campo `Status` (usado tanto para o ciclo de vida geral do produto quanto, indiretamente, para a publicação). Não é óbvio se a solução deve: (a) adicionar um campo booleano/enum simples tipo `IsPublishedOnSite`, mantendo `Status` para o restante do ciclo de vida; (b) introduzir uma tabela/entidade de tracking por destino (ex. `ProductPublication { Destination, Status, PublishedAt }`), reaproveitando ou estendendo o padrão já usado por `PublicationQueue`; ou (c) outra abordagem. A escolha afeta migrations, o `ProcessorJob` (onde a decisão de "publicar no site" é tomada) e o `PublicController` (query de elegibilidade para `GET /api/public/deals`).
2. **Nome dos campos/enum novos** e se o valor consolidado "Published" do dashboard é calculado em tempo de leitura (agregando os status por destino) ou persistido/cacheado — trade-off de consistência vs. performance na listagem do dashboard.
3. **Como a tooltip do dashboard deve obter os dados de "onde foi publicado"** — endpoint dedicado, campo agregado no DTO existente do dashboard, ou join calculado. Também decidir o formato de exibição (ex. lista de destinos com ícone de status: publicado / pendente / erro / não aplicável).
4. **Como `ProcessorJob` deve mudar** para separar a decisão "publicar no site" (agora incondicional a rede social) da decisão "enfileirar para publicação social" (ainda condicional a rede qualificada) — hoje aparentemente essas decisões estão acopladas num único fluxo/status que gera o bug relatado (comentário no código referenciando Issues #133/#145 sobre não marcar `Published` incondicionalmente sem rede qualificada).
5. **Como evitar retroatividade** quando uma rede social nova for qualificada — precisa de um critério técnico claro (ex. "produto criado/atualizado após a data de qualificação da rede") que não exija reprocessar produtos antigos.

Essas são decisões de arquitetura/modelagem de domínio, não de negócio — encaminhado ao Arquiteto antes do refinamento técnico do LT.
