# PRD — Issue #6: Processor Job (Mídia + Fila de Publicação)

Status: rascunho inicial (Fase 1 — aguardando respostas do Gerente no Gate 1)

## Objetivo
Implementar o job assíncrono que processa produtos aprovados: baixa a mídia associada
para armazenamento local, gera legendas por rede social via IA, e monta a fila de
publicação (`PublicationQueue`) com o agendamento adequado por rede.

## Usuários afetados
- Operação/negócio: depende do job para que produtos aprovados cheguem à fila de
  publicação sem intervenção manual (exceto Facebook, que é `ManualPending`).
- Equipe técnica: mantém o job como parte da pipeline Collector → Processor → Publisher.

## Casos de uso principais
1. Produto tem mídia (`MediaUrl` preenchida) → job baixa o arquivo para `/app/media/`,
   detecta tipo (`image`/`video`) por extensão, associa ao produto.
2. Job gera legenda por rede habilitada via `IAiService.GenerateCaptionAsync`.
3. Job cria uma entrada em `PublicationQueue` por rede habilitada:
   - Facebook → `Status = ManualPending` (fica pendente de ação manual, sem agendamento automático).
   - Demais redes → `Status = Scheduled`, com `ScheduledAt` distribuído nos horários
     configurados em `app_settings.schedule.publisher_cron`.
4. Produto processado tem `Status = Queued` ao final (mantendo o já setado por `UpdateAiResult`, ver ambiguidade #1 abaixo).
5. `CollectorJob` encadeia a execução do `ProcessorJob` via `BackgroundJob.Enqueue`
   (Hangfire) ao final de cada ciclo de coleta.

## Casos de exceção (a confirmar com o Gerente — ver seção de ambiguidades)
- Falha no download de mídia.
- Produto rejeitado pelo scoring de IA (`Status = Rejected`).
- Rede habilitada em `app_settings` mas sem credenciais configuradas (Publisher, Issue #9, ainda não existe).

## Regras de negócio já claras
- Tipo de mídia por extensão: `.mp4`/`.webm` → `"video"`; demais extensões → `"image"`.
- Facebook sempre gera item de fila com `Status = ManualPending` (não é agendado automaticamente).
- Demais redes (Instagram, MercadoLivre?, etc. conforme `networks.*.enabled`) geram
  itens com `Status = Scheduled` e `ScheduledAt` no futuro.
- Volume `/app/media` é persistido entre restarts via docker-compose (já configurado).

## Ambiguidades identificadas (bloqueiam início da Fase 2 — ver perguntas no Gate 1)
1. **Status de entrada do job**: a Issue descreve buscar produtos `Status = Pending`,
   mas a entidade `Product.UpdateAiResult` (já implementada) seta `Status = Queued`
   quando o score é aprovado (`>= AiScoreThreshold`) e `Rejected` quando reprovado — não existe
   caminho para um produto aprovado permanecer `Pending`. Precisa esclarecer o status real de entrada do ProcessorJob.
2. **Slug**: já gerado pelos collectors na criação do produto (Amazon, ML, Shopee).
   A Issue #6 descreve gerar de novo — redundância, correção de padrão anterior, ou o job deve pular produtos com slug já preenchido?
3. **AffiliateLink do MercadoLivre**: Issue #5 decidiu que o preenchimento ocorreria
   no ProcessorJob via `Product.SetAffiliateLink`, mas a Issue #6 não menciona essa etapa.
4. **Campo `MediaLocalPath`**: não existe na entidade `Product` atual — precisa de nova migration.
5. **Detecção de `Category`**: os collectors já setam `"Geral"` hardcoded; a Issue #6 pede
   detecção real no ProcessorJob, mas não especifica a fonte (título? palavras-chave? IA?).
6. **Distribuição de `ScheduledAt`**: mecânica de distribuição entre os horários do cron
   do publisher não especificada (round-robin, por score, 1 produto por horário/dia, etc.).
7. **Produtos `Rejected`**: confirmar se ficam definitivamente parados no banco (sem
   retry/limpeza) ou há processo futuro para eles.
8. **Falha no download de mídia**: comportamento não especificado (retry no próximo ciclo vs. seguir sem mídia local).
9. **Rede habilitada sem credenciais**: comportamento não especificado (criar item de fila que falhará depois no Publisher vs. pular a rede).

## Integrações externas
- `IAiService.GenerateCaptionAsync` (já existente, usado também no scoring).
- Potencialmente API de geração de link de afiliado do MercadoLivre (`POST /affiliate-tools/links`), a confirmar (ambiguidade #3).

## Restrições / prazo
- Dependência direta das Issues #2, #3, #4 e #5 (já concluídas).
- Sem prazo explícito informado na Issue.

## Definição de pronto (preliminar)
- `LocalMediaStorage` implementado e testado (download + detecção de tipo).
- `ProcessorJob` implementado processando o status de entrada correto (a confirmar).
- `PublicationQueue` criada corretamente por rede habilitada, com `Status` e `ScheduledAt` conforme regra definida.
- Migration de `MediaLocalPath` (se confirmada) aplicada.
- Testes cobrindo os critérios de aceite da Issue.
- Todas as ambiguidades acima resolvidas e refletidas no proposal.md antes do refinamento técnico.
