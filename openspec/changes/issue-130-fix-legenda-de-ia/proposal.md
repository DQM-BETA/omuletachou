# Proposal — ISSUE-130: fix: Legenda de IA nunca é persistida

## Objetivo
Corrigir o bug em que a legenda de IA gerada pelo `ProcessorJob` (chamada paga à API Claude) é descartada em vez de persistida, fazendo com que todo post saia sem legenda em todas as redes (Telegram, YouTube, Instagram, TikTok) e no Facebook Manual (dashboard). A correção introduz um campo `Caption` por item de `PublicationQueue` (a legenda é gerada por rede social, não é um valor único por produto), ajusta os 4 publishers e o Facebook Manual para lerem dessa nova fonte, e corrige a cobertura de teste que mascarou o bug.

## Usuários
- Sistema (jobs `ProcessorJob`/`PublisherJob`, execução automática via Hangfire).
- Operador/administrador (dashboard, tela de publicação manual do Facebook — botão "copiar legenda").
- Indiretamente, a audiência das redes sociais publicadas (passam a receber posts com legenda gerada por IA, não mais vazios).

## Casos de uso principais
1. `ProcessorJob` gera a legenda por rede (`GenerateCaptionAsync`) ao enfileirar um item em `PublicationQueue` e persiste o resultado no novo campo `PublicationQueue.Caption` daquele item.
2. Cada um dos 4 publishers automáticos (`TelegramPublisher`, `YoutubePublisher`, `InstagramPublisher`, `TikTokPublisher`) lê `PublicationQueue.Caption` (não mais `Product.AiCaption`) para montar o post.
3. Operador abre a tela de publicação manual do Facebook (Facebook Manual) e clica em "copiar legenda": o texto copiado é a legenda de IA real (de `PublicationQueue.Caption` do item correspondente à rede Facebook), não a descrição original do produto.

## Casos de uso de exceção
- Item de `PublicationQueue` processado antes da migration (legenda vazia por default) — aceito como dado legado, sem backfill (ver "Restrições").
- Falha na geração da legenda (retry da API Claude esgotado): comportamento de erro/retry do `ProcessorJob` já existente é mantido; fora do escopo desta correção redesenhar a política de retry.

## Regras de negócio (confirmadas no Gate 1)
1. **Fonte de verdade da legenda por publicação é `PublicationQueue.Caption`**, não `Product.AiCaption`. `Product.AiCaption` pode ser removido ou mantido apenas para propósitos não-autoritativos (ex.: preview genérico no dashboard) — nunca mais é usado por nenhum publisher.
2. **Geração continua no `ProcessorJob`** (momento do enfileiramento), não é movida para o `PublisherJob`. Motivo: mover geração para a publicação multiplicaria chamadas pagas à API Claude (até 3 tentativas de retry por item) e quebraria a separação de responsabilidades (Processor prepara conteúdo, Publisher só publica). O risco de legenda desatualizada (ex.: mudança de preço entre geração e publicação) é mitigado pelo agendamento em si (distribuição em até 5 horários no mesmo dia — janela de horas, não dias); se o preço mudar nessa janela, o impacto já atinge outras partes do sistema (site público, etc.), fora do escopo desta correção.
3. **Facebook Manual está no escopo.** O botão "copiar legenda" deve expor a legenda real gerada para Facebook (`PublicationQueue.Caption` do item da rede Facebook), não a descrição original do produto — mesmo bug na ponta do dashboard. Requer ajuste em `ProductDetailDto` (backend, incluir a caption por rede, ao menos a de Facebook) e em `ProductDetail` (frontend, consumir o novo campo).
4. **Sem retrocompatibilidade/backfill.** Produtos já publicados sem legenda ficam como estão — apenas seguir em frente, com uma linha no PR/changelog registrando o contexto histórico.
5. **Cobertura de teste corrigida como parte do escopo:** `ProcessorJobTests.cs` deve validar o estado persistido (`PublicationQueue.Caption` não vazio, correspondente ao mock de `GenerateCaptionAsync` por rede), não apenas contagem de chamadas de mock. Padrão a virar lição aprendida: testes de jobs que persistem dados devem sempre validar o estado final salvo.

## Integrações
- Anthropic Claude API (`GenerateCaptionAsync`, já existente — nenhuma mudança na integração externa em si, só no destino de persistência do retorno).
- Nenhuma integração externa nova.

## Restrições
- Migration de banco: `ALTER TABLE publication_queue ADD COLUMN caption TEXT NOT NULL DEFAULT ''`. Todo item deve ter a coluna populada antes de ser processado pelo `PublisherJob` (garantido pela ordem natural do pipeline: Processor gera e persiste antes do Publisher consumir).
- Sem backfill de dados históricos.
- Sem prazo explícito além do fluxo normal do pipeline.

## Definição de pronto
Ver `documentacoes/ISSUE-130-fix-legenda-de-ia/criterios-aceite.md` para os critérios Given/When/Then completos. Resumo:
- Migration aplicada, `PublicationQueue.Caption` persistido pelo `ProcessorJob` com a legenda correta por rede.
- Os 4 publishers automáticos consomem `PublicationQueue.Caption`.
- Facebook Manual (backend `ProductDetailDto` + frontend `ProductDetail`) expõe e usa a legenda real de IA no botão "copiar legenda".
- `ProcessorJobTests.cs` corrigido para validar persistência (não apenas chamada de mock).
- PR contém nota de changelog sobre a ausência de backfill para publicações históricas.

## Ambiguidade arquitetural avaliada pelo PM
Nenhuma ambiguidade que exija o Arquiteto. As decisões de design já vieram integralmente definidas pelo Gerente no Gate 1:
1. **Local de persistência definido:** novo campo `Caption` em `PublicationQueue` (não uma alternativa a avaliar — o Gerente já apontou isso como correção do design original, já que cada item da fila é por-rede por natureza).
2. **Ponto de geração definido:** mantido no `ProcessorJob`, com justificativa de negócio explícita (custo de API + separação de responsabilidades) — não há decisão técnica em aberto.
3. **Migration é aditiva e simples:** `ALTER TABLE ... ADD COLUMN ... DEFAULT ''`, sem necessidade de estratégia de migração de dados complexa (sem backfill).
4. **Escopo do Facebook Manual já delimitado:** ajuste pontual em DTO existente (`ProductDetailDto`) e interface existente (`ProductDetail`) — não introduz nova tela, endpoint ou fluxo.

Não há múltiplas stacks em conflito, integração externa nova, ou trade-off de arquitetura não-óbvio. Segue direto para o Líder Técnico (refinamento técnico: task breakdown por sub-issue, cobrindo migration, `ProcessorJob`, os 4 publishers, `ProductDetailDto`/`ProductDetail`, e `ProcessorJobTests.cs`).
