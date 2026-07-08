# Proposal — ISSUE-7: Publisher Telegram + Hangfire Scheduler

## Objetivo
Implementar `TelegramPublisher`, `PublisherJob` e `CollectorJob` (orquestrador de coleta, criação nova), e configurar o Hangfire (storage Postgres, dashboard protegido, recurring jobs), fechando o pipeline ponta a ponta: coleta → processamento → publicação.

## Usuários
- Operador/administrador (dashboard Hangfire + canal Telegram de teste).
- Sistema (execução automática via cron).

## Casos de uso
Ver `documentacoes/ISSUE-7-publisher-telegram/prd.md` (seção "Casos de uso principais") — consolidado após respostas do Gate 1.

## Regras de negócio
Ver `documentacoes/ISSUE-7-publisher-telegram/prd.md` (seção "Regras de negócio (confirmadas no Gate 1)").

## Integrações
- Telegram Bot API (sendVideo/sendPhoto).
- Hangfire.PostgreSql (reaproveita connection string existente).

## Restrições
- Depende de #4/#5 (collectors) e #6 (ProcessorJob), já em produção.
- Sem prazo explícito.

## Definição de pronto
Ver `documentacoes/ISSUE-7-publisher-telegram/prd.md` (seção "Definição de pronto") e `criterios-aceite.md` (CA1–CA26).

## Ambiguidade arquitetural avaliada pelo PM
Nenhuma ambiguidade que exija o Arquiteto. Dois pontos técnicos identificados foram resolvidos como decisão direta de implementação (documentados no PRD, a validar no refinamento do LT):
1. `CollectorJob` deve receber `IEnumerable<IPlatformCollector>` (DI resolve os 3 collectors automaticamente) — mais escalável que injetar os 3 tipos concretos individualmente. Requer ajuste aditivo no registro DI em `Program.cs` (hoje só `AmazonCollector` está vinculado à interface `IPlatformCollector`).
2. Configuração do Hangfire é configuração de biblioteca já prevista na stack do repo (`Hangfire.PostgreSql` consta no CLAUDE.md desde o início) — não é infraestrutura nova/surpresa, não configura risco arquitetural.

Nenhum risco de regressão identificado nos collectors/ProcessorJob já em produção: o `CollectorJob` apenas orquestra chamadas já existentes e o encadeamento ao `ProcessorJob` é aditivo (enfileiramento via Hangfire), sem alterar a lógica interna desses componentes.
