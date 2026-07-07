# Critérios de Aceite — Issue #6: Processor Job

## LocalMediaStorage

**CA1 — Download com sucesso**
- Given produto `Queued` com `MediaUrl` válida apontando para uma imagem `.jpg`
- When `ProcessorJob` processa o produto
- Then o arquivo é baixado para `/app/media/`, `MediaLocalPath` é preenchido com o caminho
  local, e `MediaType = "image"`

**CA2 — Detecção de vídeo por extensão**
- Given produto `Queued` com `MediaUrl` terminando em `.mp4` ou `.webm`
- When a mídia é baixada
- Then `MediaType = "video"`

**CA3 — Falha no download não bloqueia o processamento**
- Given produto `Queued` com `MediaUrl` inválida (404, timeout ou URL malformada)
- When `ProcessorJob` tenta baixar a mídia
- Then o produto continua sendo processado (não é pulado nem reenfileirado),
  `MediaLocalPath` permanece nulo, `MediaType` é inferido pela URL original, e um log
  Warning é registrado

## Status do produto (máquina de estados)

**CA4 — Entrada Queued vira Processing**
- Given produto com `Status = Queued`
- When `ProcessorJob` inicia o processamento desse produto
- Then `Status` é imediatamente atualizado para `Processing`, antes de qualquer outra operação

**CA5 — Conclusão com sucesso vira Published**
- Given produto `Processing` com todas as etapas concluídas sem erro
- When a última entrada de `PublicationQueue` é criada com sucesso
- Then `Status = Published`

**CA6 — Falha não recuperável vira Error**
- Given produto `Processing` cuja chamada de link de afiliado do MercadoLivre falha
- When a falha ocorre
- Then `Status = Error` e uma mensagem descritiva do erro é persistida

**CA7 — Produtos Rejected nunca são processados**
- Given produto com `Status = Rejected`
- When `ProcessorJob` executa uma busca por produtos elegíveis
- Then o produto `Rejected` não é retornado nem processado

## Slug

**CA8 — Slug preenchido é preservado**
- Given produto `Queued` cujo `Slug` já está preenchido (coletado por Amazon/ML/Shopee)
- When `ProcessorJob` processa o produto
- Then o `Slug` original é mantido, sem regeração

**CA9 — Slug nulo é gerado**
- Given produto `Queued` com `Slug` nulo ou vazio
- When `ProcessorJob` processa o produto
- Then um `Slug` é gerado no formato `Slugify(Title) + "-" + Id[..6]` e persistido

## Categoria

**CA10 — Detecção por palavra-chave**
- Given produto com título contendo "Fone de Ouvido Bluetooth"
- When `CategoryDetector` processa o título
- Then `Category = "Eletrônicos"` (ou categoria equivalente mapeada para a palavra-chave "fone")

**CA11 — Fallback sem match**
- Given produto com título sem nenhuma palavra-chave mapeada
- When `CategoryDetector` processa o título
- Then `Category = "Geral"`

## AffiliateLink MercadoLivre

**CA12 — Preenchimento automático via API real**
- Given produto `Processing` com `Platform = MercadoLivre` e `AffiliateLink` nulo
- When `ProcessorJob` executa a etapa de link de afiliado
- Then `POST /affiliate-tools/links` é chamado, e o link retornado é persistido via
  `Product.SetAffiliateLink`

**CA13 — Amazon/Shopee não sofrem nova chamada**
- Given produto `Processing` com `Platform = Amazon` ou `Shopee` e `AffiliateLink` já
  preenchido pelo collector
- When `ProcessorJob` executa a etapa de link de afiliado
- Then nenhuma chamada HTTP adicional é feita para gerar link, o valor existente é preservado

**CA14 — Falha na chamada de link gera Error**
- Given produto `Processing` de `Platform = MercadoLivre` cuja chamada a
  `POST /affiliate-tools/links` retorna erro (HTTP não-2xx ou exceção de rede)
- When a falha ocorre
- Then `Status = Error` com mensagem descritiva, e nenhuma entrada de `PublicationQueue`
  é criada para esse produto

## Fila de publicação (PublicationQueue)

**CA15 — Facebook sempre ManualPending**
- Given rede Facebook habilitada em `app_settings` (`networks.facebook.enabled = true`)
  com credenciais configuradas
- When `ProcessorJob` cria a entrada de fila para Facebook
- Then a entrada tem `Status = ManualPending`, sem `ScheduledAt` automático

**CA16 — Demais redes recebem Scheduled com ScheduledAt futuro**
- Given rede Instagram habilitada com credenciais configuradas
- When `ProcessorJob` cria a entrada de fila
- Then a entrada tem `Status = Scheduled` e `ScheduledAt` no futuro, dentro dos horários
  do cron do publisher (9h/12h/15h/18h/20h) com offset de 0-10min

**CA17 — Round-robin por AiScore desc**
- Given 6 produtos elegíveis no mesmo ciclo, ordenados por `AiScore` desc
- When `ProcessorJob` distribui os agendamentos
- Then produto 1→9h, 2→12h, 3→15h, 4→18h, 5→20h (do mesmo dia), 6→9h do dia seguinte

**CA18 — Rede sem credenciais é pulada**
- Given rede Instagram habilitada (`networks.instagram.enabled = true`) mas sem
  `instagram.access_token` configurado em `app_settings`
- When `ProcessorJob` processa o produto
- Then nenhuma entrada de `PublicationQueue` é criada para Instagram nesse ciclo, e um
  log Warning é registrado

**CA19 — Uma entrada por rede habilitada e com credenciais**
- Given produto `Processing` com Facebook e Instagram habilitados e com credenciais
- When `ProcessorJob` conclui o processamento
- Then existem exatamente 2 entradas em `PublicationQueue` para esse produto (uma por rede)

## Migration

**CA20 — Migration incremental aplicada**
- Given banco de dados na versão anterior à Issue #6
- When a migration `AddMediaLocalPathToProducts` é aplicada
- Then a coluna `MediaLocalPath` (nullable) existe na tabela `Products`, sem alterar a
  migration inicial já existente

## Encadeamento de jobs

**CA21 — CollectorJob enfileira ProcessorJob**
- Given `CollectorJob` concluiu um ciclo de coleta com sucesso
- When o ciclo termina
- Then `ProcessorJob` é enfileirado via `BackgroundJob.Enqueue` (Hangfire)
