# Proposal — ISSUE-227: Exibir data/hora da última execução de cada job na tela Jobs

## Objetivo
Persistir e exibir, na tela `Jobs` do dashboard (`/jobs`), a data/hora real da última execução de cada job (Collector geral, Collector Amazon/MercadoLivre/Shopee, Processor, Publisher), incluindo o status dessa execução (sucesso/falha/em andamento). Hoje o card mostra "Nenhuma execução disparada ainda." mesmo após o job já ter rodado, porque esse estado é local ao componente Angular e se perde no refresh — não vem do backend.

Além de exibir a última execução, o sistema deve persistir um **histórico** de execuções (não só a última), guardando status + início + fim de cada run, para viabilizar um relatório futuro (fora do escopo desta issue — só a persistência é necessária agora). O registro deve ser uma **entidade própria**, desacoplada das tabelas nativas do Hangfire, porque precisa tratar de forma consistente tanto execuções automatizadas (agendadas pelo Hangfire) quanto execuções manuais (disparo pelo operador via botão "Disparar").

## Usuários
- Operador/administrador do dashboard interno (`/jobs`) — passa a ver, de forma confiável e persistente, quando cada job rodou pela última vez, se funcionou, e quando começou/terminou.
- Sistema (Hangfire, jobs agendados e disparo manual) — passa a registrar cada execução (início, fim, status) num registro próprio, independente da origem do disparo (agendado ou manual).

## Casos de uso principais
1. Ao abrir ou dar refresh na tela `Jobs`, cada card exibe a data/hora de início e fim da última execução daquele job, junto com o status (sucesso/falha), vindos do backend — sobrevivendo a refresh e a reinícios do dashboard.
2. Um job é disparado manualmente pelo operador (botão "Disparar") — a execução resultante fica registrada com a mesma estrutura de uma execução agendada (início, fim, status), e o card reflete essa execução como a mais recente assim que ela terminar (ou "em andamento" enquanto ainda está rodando, se aplicável ao escopo).
3. Um job roda de forma agendada/automatizada (Hangfire) sem intervenção do operador — a execução também é registrada da mesma forma, garantindo que o histórico e a tela Jobs reflitam tanto disparos manuais quanto automáticos de forma unificada.
4. Cada execução registrada (manual ou automática) fica persistida num histórico, não apenas a mais recente — pensando em uso futuro para relatório (a tela/funcionalidade de relatório em si está fora do escopo desta issue; só a persistência do histórico é necessária agora).

## Casos de uso de exceção
- Job que **nunca foi executado** (nem manual, nem automaticamente) desde que o registro próprio existe — o card continua exibindo "Nenhuma execução ainda" (ou mensagem equivalente), sem erro.
- **Última execução falhou** — o card deve deixar isso visível (status de falha), junto com a data/hora de início/fim daquela execução que falhou, em vez de simplesmente omitir a informação ou mostrar como se tivesse funcionado.
- Job com histórico de execuções mistas (algumas com sucesso, algumas com falha) — a tela Jobs exibe sempre a execução mais recente (por data/hora de início ou fim, a definir na especificação técnica), independentemente do status dela.

## Regras de negócio (confirmadas no Gate 1)
1. **Escopo do dado**: para cada execução de job, persistir status, data/hora de início e data/hora de fim.
2. **Histórico, não só a última**: todas as execuções são persistidas (não há substituição/sobrescrita do registro anterior), para viabilizar relatório futuro. Retenção/expurgo de histórico antigo é decisão técnica (ver ambiguidade abaixo), não uma restrição de negócio explícita nesta issue.
3. **Registro próprio, desacoplado do Hangfire**: não reaproveitar as tabelas nativas do Hangfire para este propósito. Motivo de negócio: execução automatizada (agendada) e execução manual (disparo pelo operador) são dois fluxos distintos hoje, e a nova entidade deve tratar ambos de forma unificada e consistente, independente da origem do disparo.
4. **Escopo de exibição**: o dado aparece apenas na tela `Jobs` do dashboard interno — não em nenhum outro lugar do sistema (site público, notificações, etc.).
5. **Falha deve ficar visível**: quando a última execução de um job falhou, isso precisa ser comunicado claramente no card (não apenas a data/hora, mas também o status de falha).
6. **Caso "nunca executado"**: mensagem equivalente à atual ("Nenhuma execução ainda") permanece válida quando não há nenhum registro de execução para aquele job.
7. **Não é sobre confiabilidade do disparo**: esta issue não altera a lógica de quando/como um job é disparado (agendamento, retries do Hangfire, etc.) — é estritamente sobre persistir e exibir o histórico/última execução.

## Integrações
- Nenhuma integração externa nova. A integração existente é interna: o Hangfire (já usado no projeto para orquestrar os jobs) precisa, na prática, alimentar o novo registro próprio de execuções — seja via hook/filtro de execução do Hangfire, seja via código explícito no início/fim de cada job. A forma exata é decisão técnica (ver ambiguidade abaixo).
- O disparo manual (botão "Disparar" no dashboard) também precisa alimentar o mesmo registro, de forma consistente com o disparo automático.

## Restrições
- Sem prazo específico declarado pelo Gerente além de seguir o pipeline normal (rota `normal`, sem urgência excepcional).
- A solução não deve depender de consultar diretamente as tabelas internas do Hangfire storage (decisão de negócio confirmada no Gate 1, item 5).
- Não é necessário implementar a tela/funcionalidade de relatório nesta issue — apenas garantir que o histórico fique persistido de forma que um relatório futuro seja viável sem retrabalho de modelagem.
- Decisões técnicas específicas (onde e como registrar cada execução dado que há dois disparadores distintos — Hangfire agendado e disparo manual via API/controller; se precisa de índice por job+data para consultas eficientes do histórico; se há necessidade de política de retenção/expurgo do histórico; como representar "em andamento" quando um job manual está rodando) ficam para a etapa de Arquitetura/Refinamento Técnico — ver avaliação de ambiguidade abaixo.

## Definição de pronto
Ver `documentacoes/ISSUE-227-exibir-data-hora-ultima-execucao-jobs/criterios-aceite.md` para os critérios Given/When/Then completos. Resumo:
- Ao dar refresh na tela Jobs (sem disparar nada novo), cada card mostra a data/hora real (início + fim) e o status (sucesso/falha) da última execução daquele job, vinda do backend.
- Job nunca executado continua exibindo "Nenhuma execução ainda".
- Última execução com falha fica visivelmente marcada como falha no card, junto com a data/hora.
- Toda execução (manual ou agendada) fica persistida num histórico (não só a última), num registro próprio desacoplado das tabelas nativas do Hangfire.
- Disparo manual (via botão) e disparo automático (agendado pelo Hangfire) alimentam o mesmo modelo de dados de forma consistente.

## Ambiguidade arquitetural avaliada pelo PM
**Existe ambiguidade real que exige o Arquiteto antes do refinamento técnico do LT.** As regras de negócio (escopo do dado, histórico, registro desacoplado do Hangfire, exibição de falha) já foram decididas pelo Gerente no Gate 1. Mas restam decisões técnicas não-óbvias:
1. **Onde/como capturar o início e fim de cada execução**, dado que há dois disparadores distintos hoje: jobs agendados pelo Hangfire (que já tem seu próprio ciclo de vida de job) e disparo manual pelo operador (via controller/endpoint do dashboard). Opções incluem usar um `IElectStateFilter`/`IServerFilter` do Hangfire para capturar automaticamente todas as execuções (incluindo as manuais, já que o disparo manual também passa pelo Hangfire como enqueue) versus instrumentar cada job manualmente no início/fim do método `Execute`.
2. **Modelagem da nova entidade** (ex.: tabela `job_runs` com `JobName`, `Status`, `StartedAt`, `FinishedAt`) — nomes de campos/enum de status, se cobre estados intermediários como "em andamento" (relevante para jobs de longa duração), e se precisa de índice composto (job + data) para consultas eficientes de "última execução por job" e de histórico.
3. **Política de retenção do histórico** — se há necessidade de expurgo/particionamento a médio prazo (o histórico crescerá indefinidamente sem uma rotina de limpeza), ou se isso fica para uma issue futura quando o volume justificar.
4. **Como o endpoint/DTO que alimenta a tela Jobs deve agregar "última execução por job"** — query com `GROUP BY`/`MAX(StartedAt)`, ou uma view/campo desnormalizado, considerando que há poucos jobs (6 hoje) e a consulta é de baixo volume.

Essas são decisões de arquitetura/modelagem de domínio, não de negócio — encaminhado ao Arquiteto antes do refinamento técnico do LT.
