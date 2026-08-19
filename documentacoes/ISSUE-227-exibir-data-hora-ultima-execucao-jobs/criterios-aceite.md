# Critérios de Aceite — ISSUE-227: Exibir data/hora da última execução de cada job na tela Jobs

## 1. Exibição da última execução por job

**Cenário 1.1 — Card mostra data/hora real vinda do backend após refresh**
- Given um job já foi executado (manual ou automaticamente) ao menos uma vez
- When o operador abre ou dá refresh na tela `Jobs`
- Then o card daquele job exibe a data/hora de início e a data/hora de fim da execução mais recente, com esses valores vindos do backend (não de estado local do componente)

**Cenário 1.2 — Dado persiste entre sessões/reinícios do dashboard**
- Given um job foi executado com sucesso
- When o dashboard é fechado e reaberto (nova sessão do navegador) posteriormente
- Then o card continua exibindo a data/hora real da última execução daquele job, sem depender de o navegador/sessão ter permanecido aberto

**Cenário 1.3 — Status da última execução é exibido junto com a data/hora**
- Given a última execução de um job terminou com sucesso
- When o card daquele job é exibido na tela Jobs
- Then o card mostra, além da data/hora de início/fim, um indicador de status de sucesso

## 2. Última execução com falha fica visível

**Cenário 2.1 — Falha é comunicada claramente no card**
- Given a última execução de um job terminou com falha (erro/exceção durante o processamento)
- When o operador visualiza o card daquele job na tela Jobs
- Then o card exibe claramente o status de falha, junto com a data/hora de início e (quando aplicável) de fim daquela execução que falhou

**Cenário 2.2 — Falha não é confundida com sucesso nem omitida**
- Given a última execução de um job falhou
- When o card é renderizado
- Then o card NÃO exibe indicador de sucesso nem omite a informação de execução — o status de falha é visível junto com o timestamp

## 3. Caso "nunca executado"

**Cenário 3.1 — Job sem nenhum registro de execução**
- Given um job (Collector geral, Collector Amazon/MercadoLivre/Shopee, Processor ou Publisher) nunca foi executado, nem manual nem automaticamente, desde que o registro próprio de execuções existe
- When o operador visualiza o card daquele job na tela Jobs
- Then o card exibe mensagem equivalente a "Nenhuma execução ainda" (sem erro, sem data/hora inválida ou vazia mal formatada)

## 4. Histórico de execuções persistido

**Cenário 4.1 — Cada execução gera um novo registro de histórico**
- Given um job já possui ao menos um registro de execução anterior
- When o job é executado novamente (manual ou automaticamente) e termina
- Then um novo registro de execução é persistido (status, início, fim), sem sobrescrever ou apagar os registros de execuções anteriores daquele job

**Cenário 4.2 — Histórico cobre tanto execuções manuais quanto automáticas**
- Given um job foi executado uma vez via disparo manual (botão "Disparar") e uma vez via agendamento automático do Hangfire
- When o histórico de execuções daquele job é consultado (nível de persistência/domínio, não necessariamente via tela)
- Then ambas as execuções aparecem registradas com a mesma estrutura de dados (status, início, fim), de forma consistente independentemente da origem do disparo

**Cenário 4.3 — Tela Jobs exibe apenas a execução mais recente, mas o histórico completo está persistido**
- Given um job com múltiplas execuções registradas ao longo do tempo (algumas com sucesso, algumas com falha)
- When o operador visualiza o card daquele job na tela Jobs
- Then o card exibe apenas a execução mais recente (a de data/hora de início mais alta), enquanto o histórico completo permanece persistido no backend (disponível para uso futuro em relatório, fora do escopo desta issue)

## 5. Registro desacoplado do Hangfire nativo

**Cenário 5.1 — Execução manual e automática usam o mesmo modelo de dados próprio**
- Given um job é disparado manualmente pelo operador via botão "Disparar"
- And o mesmo job também roda de forma agendada/automática pelo Hangfire em outro momento
- When ambas as execuções terminam
- Then ambas ficam registradas na mesma entidade/tabela própria da aplicação (não nas tabelas nativas do Hangfire storage), com a mesma estrutura de campos (status, início, fim)

**Cenário 5.2 — Consulta da tela Jobs não depende das tabelas internas do Hangfire**
- Given a tela Jobs busca a última execução de cada job
- When a consulta é feita ao backend
- Then a fonte de dados é o registro próprio da aplicação, não uma consulta direta às tabelas internas do Hangfire storage
