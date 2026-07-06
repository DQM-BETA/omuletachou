# PRD — Issue #6: Processor Job (Mídia + Fila de Publicação)

Status: **consolidado (Fase 2 — PM, pós Gate 1)**

## Objetivo
Implementar o job assíncrono que processa produtos aprovados pelo scoring de IA: baixa a
mídia associada para armazenamento local, detecta categoria por palavras-chave, preenche o
link de afiliado do MercadoLivre (quando ausente), gera legendas por rede social via IA, e
monta a fila de publicação (`PublicationQueue`) com o agendamento adequado por rede.

## Usuários afetados
- Operação/negócio: depende do job para que produtos aprovados cheguem à fila de
  publicação sem intervenção manual (exceto Facebook, que fica `ManualPending`).
- Equipe técnica: mantém o job como elo central da pipeline Collector → Processor → Publisher.

## Fluxo de status do produto (máquina de estados — confirmado no Gate 1)
```
Pending → Queued (CollectorJob, apos scoring aprovado) → Processing (ProcessorJob ao iniciar)
       → Published (ao concluir todas as entradas de fila) | Error (falha em qualquer etapa)
```
- `Rejected` é terminal e não entra no fluxo do ProcessorJob (scoring reprovado).
- `Processing` é setado **imediatamente** ao pegar o produto, antes de qualquer operação —
  evita colisão entre execuções paralelas do Hangfire (lock otimista via update de status).
- Mudança no enum `ProductStatus`: adicionar `Processing` e `Error` (novos valores).
  `Published` **já existe** no enum atual (`Pending, Queued, Published, Rejected`) e já é
  usado por `Product.MarkAsPublished()` — não é um valor novo, apenas passa a ser setado
  também ao final do ProcessorJob (antes só era usado por outro fluxo/futuro Publisher).
  Adição de enum values é aditiva (sem remoção/renomeação de existentes) — sem impacto nos
  collectors já em produção (Issues #4/#5), que só escrevem `Pending`/`Queued`/`Rejected`.

## Casos de uso principais
1. **Busca de produtos**: ProcessorJob busca produtos `Status = Queued`. Ao pegar cada um,
   seta `Status = Processing` imediatamente (evita disputa entre execuções concorrentes).
2. **Download de mídia**: se `MediaUrl != null`, `LocalMediaStorage.DownloadAsync` baixa o
   arquivo para `/app/media/`, retorna caminho local. Detecção de tipo por extensão:
   `.mp4`/`.webm` → `"video"`; demais → `"image"`. Seta `MediaLocalPath` e `MediaType`.
3. **Slug**: gerar apenas se `Product.Slug` estiver nulo/vazio (produtos coletados antes da
   lógica de slug existir nos collectors). Se já preenchido (caso comum, collectors atuais já
   geram), **pular** — nunca regerar (links externos podem já circular com aquele slug).
   Fórmula quando necessário: `Slugify(Title) + "-" + Id.ToString()[..6]`.
4. **Categoria**: `CategoryDetector` (classe estática, `AfiliadoBot.Application`) detecta por
   palavras-chave no título — dicionário categoria→lista de palavras-chave (case-insensitive),
   primeira categoria com match; sem match → fallback `"Geral"`. Sem dependência de IA/banco.
   Substitui o `"Geral"` hardcoded dos collectors quando há match mais específico.
5. **AffiliateLink MercadoLivre**: se `Product.Platform == MercadoLivre` e `AffiliateLink`
   nulo, ProcessorJob chama `POST /affiliate-tools/links` (API real do ML) e preenche via
   `Product.SetAffiliateLink(link)` antes de gerar a legenda. Amazon e Shopee já chegam com
   `AffiliateLink` preenchido pelo collector — não requerem esta etapa.
6. **Geração de legendas e fila**: ler redes habilitadas de `app_settings` (`networks.*.enabled`).
   Para cada rede habilitada **com credenciais configuradas**:
   - Chamar `IAiService.GenerateCaptionAsync` para gerar a legenda.
   - Criar uma entrada em `PublicationQueue`:
     - Facebook → `Status = ManualPending` (pendente de ação manual, sem agendamento automático).
     - Demais redes → `Status = Scheduled`, com `ScheduledAt` calculado por round-robin
       (ver regra de distribuição abaixo).
   Rede habilitada mas **sem credenciais**: pular a rede (não cria entrada na fila), log Warning.
7. **Finalização**: ao concluir a criação de todas as entradas de fila com sucesso,
   `Product.Status = Published`. Se qualquer etapa falhar de forma não recuperável (ex.: falha
   na chamada de link de afiliado do ML), `Product.Status = Error` com mensagem descritiva
   persistida (campo de erro — reaproveitar/estender `AiReason` ou novo campo, a definir pelo LT).
8. **Encadeamento**: `CollectorJob` enfileira `ProcessorJob` via `BackgroundJob.Enqueue`
   (Hangfire) ao final de cada ciclo de coleta.

## Regra de distribuição de `ScheduledAt` (round-robin)
- Horários do cron do publisher: `0 9,12,15,18,20 * * *` (9h, 12h, 15h, 18h, 20h, horário
  de Brasília / UTC-3).
- Produtos elegíveis no ciclo são ordenados por `AiScore` desc (maior score publica primeiro).
- Round-robin pelos 5 horários: produto 1→9h (hoje ou próximo horário futuro disponível),
  2→12h, 3→15h, 4→18h, 5→20h, 6→9h do dia seguinte, e assim por diante.
- Offset aleatório de 0-10 minutos dentro de cada horário (evita parecer automatizado).
- **Nota de implementação (não-arquitetural, decisão do LT):** o ProcessorJob roda 1x por
  ciclo (acionado pelo CollectorJob) e deve calcular todos os slots de `ScheduledAt` de uma
  vez para o lote de produtos processados naquele ciclo — não depende de reexecução contínua
  do Hangfire para "avançar" o round-robin. O contador de posição round-robin pode ser
  local ao lote (baseado na ordem por `AiScore` dentro da própria execução) ou persistido
  (ex. em `app_settings`) se o LT identificar necessidade de continuidade entre ciclos
  distintos do job — detalhe de implementação, sem impacto no contrato de negócio.

## Casos de exceção
- **Falha no download de mídia**: produto é processado mesmo assim (não é pulado nem
  reenfileirado). `MediaLocalPath` fica nulo; `MediaType` inferido pela URL original.
  `PublisherJob` (Issue #9, fora de escopo aqui) deve usar `MediaUrl` original como fallback
  quando `MediaLocalPath` for nulo. Log Warning registrado.
- **Falha na chamada de link de afiliado do ML**: produto vai para `Status = Error` com
  mensagem descritiva. Não cria entradas de fila para nenhuma rede desse produto.
- **Rede habilitada sem credenciais**: pular a rede (sem criar entrada de fila), log Warning.
  Evita transferir a falha para o `PublisherJob` (que ainda não existe, Issue #9) e gerar
  entradas `Failed` desnecessárias.
- **Produtos `Rejected`**: nunca entram no ProcessorJob. Ficam definitivamente `Rejected`
  no banco — sem retry automático (scoring do Claude é determinístico). `AiReason` visível
  no dashboard para reversão manual, se necessário. Limpeza/arquivamento fora de escopo.

## Regras de negócio
- Tipo de mídia por extensão: `.mp4`/`.webm` → `"video"`; demais → `"image"`.
- Facebook sempre gera item de fila com `Status = ManualPending` (nunca agendado automaticamente).
- Demais redes (Instagram, etc., conforme `networks.*.enabled`) geram itens `Status = Scheduled`
  com `ScheduledAt` no futuro, calculado por round-robin (ver acima).
- Slug nunca é regerado se já existente.
- Volume `/app/media` é persistido entre restarts via docker-compose (já configurado).

## Mudanças na entidade `Product`
- Novo campo `MediaLocalPath: string?` — requer **nova migration incremental**
  `AddMediaLocalPathToProducts` (não alterar a migration inicial).
- Enum `ProductStatus`: adicionar `Processing` e `Error` (aditivo, sem remoção de valores
  existentes: `Pending, Queued, Published, Rejected` seguem intactos).
- Possível novo método de domínio equivalente a `MarkAsProcessing()` / `MarkAsError(string reason)`
  análogos ao já existente `MarkAsPublished()` — detalhe de implementação do LT/Dev.

## Integrações externas
- `IAiService.GenerateCaptionAsync` (já existente, mesma interface usada no scoring).
- API de geração de link de afiliado do MercadoLivre: `POST /affiliate-tools/links`
  (nova integração HTTP — seguir padrão de client HTTP já usado em `MercadoLivreCollector`,
  incluindo tratamento de erro sem exception não capturada e log estruturado).

## Critérios de aceite
Ver `criterios-aceite.md` no mesmo diretório (Given/When/Then detalhado por funcionalidade).

## Restrições / prazo
- Dependência direta das Issues #2, #3, #4 e #5 (já concluídas).
- Sem prazo explícito informado na Issue.

## Definição de pronto
- `LocalMediaStorage` implementado e testado (download + detecção de tipo + tratamento de falha).
- `CategoryDetector` implementado e testado (palavras-chave, fallback `"Geral"`).
- `ProcessorJob` implementado processando `Status = Queued` → `Processing` → `Published`/`Error`.
- Preenchimento real do `AffiliateLink` do MercadoLivre via `POST /affiliate-tools/links`.
- `PublicationQueue` criada corretamente por rede habilitada e com credenciais, com
  `Status`/`ScheduledAt` conforme regra de round-robin.
- Migration `AddMediaLocalPathToProducts` aplicada.
- Enum `ProductStatus` atualizado com `Processing` e `Error`.
- Testes cobrindo todos os critérios de aceite (given/when/then) do `criterios-aceite.md`.
