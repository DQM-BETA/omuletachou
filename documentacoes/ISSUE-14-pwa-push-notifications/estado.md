issue: 14
titulo: "feat: PWA + Push Notifications"
rota: normal
etapa_atual: Refinamento Técnico
ultimo_agente: pm-analista-negocios
status_comment_id: 5061626934
openspec_change: repos/omuletachou/openspec/changes/issue-14-pwa-push-notifications
tech_stacks:
  - Next.js (next-pwa)
  - ASP.NET Core (WebPush NuGet)
repos:
  omuletachou: https://github.com/DQM-BETA/omuletachou
repo_path: repos/omuletachou
docs_path: repos/omuletachou/documentacoes/ISSUE-14-pwa-push-notifications
openspec_path: repos/omuletachou/openspec/changes/issue-14-pwa-push-notifications
sub_issues: []
desenv_tasks_merged: []
sub_issues_frontend: {}
pr_homologacao: ~
pr_release: ~
code_review_homolog_pr: ~
qa_status: ~
figma_url: ~
blockers: nenhum
closedAt: ~

## Levantamento (PM Fase 1)
Escopo tecnico ja veio detalhado do Gerente na Issue. Perguntas de negocio postadas em
https://github.com/DQM-BETA/omuletachou/issues/14#issuecomment-5061649138 cobrindo:
1. Armazenamento das VAPID keys (app_settings vs. secrets)
2. Migration/granularidade da tabela push_subscriptions
3. Rate-limit/anti-abuso no endpoint publico de subscribe
4. Placeholder de icones do manifest (design vs. dev)
5. Conteudo da notificacao push (titulo+link vs. imagem do produto)
6. Frequencia/throttling do PublisherJob ao disparar push
7. Confirmacao de que a dependencia de HTTPS (Issue #15) nao bloqueia dev/teste local (fallback ja previsto)
Aguardando resposta do Gerente (Gate 1) antes de seguir para PM Fase 2 (PRD/proposal.md).

## PM Fase 2 — PRD consolidado
Concluído.
- Respostas do Gerente ao Gate 1 resumidas e postadas em
  https://github.com/DQM-BETA/omuletachou/issues/14#issuecomment-5069985444
- `proposal.md`: repos/omuletachou/openspec/changes/issue-14-pwa-push-notifications/proposal.md
- `criterios-aceite.md`: repos/omuletachou/documentacoes/ISSUE-14-pwa-push-notifications/criterios-aceite.md
- Sumário do PRD postado como comentário na Issue #14:
  https://github.com/DQM-BETA/omuletachou/issues/14#issuecomment-5069995252

Decisões de negócio consolidadas no PRD:
1. VAPID keys em `app_settings` (`push.vapid_public_key`/`push.vapid_private_key`), geradas
   uma única vez via `webpush generate-vapid-keys`, cadastro manual pelo operador, private
   key mascarada (padrão das demais credenciais sensíveis).
2. Migration única `AddPushSubscriptions` (não fatiada), `endpoint` UNIQUE.
3. Rate-limit 10 req/min por IP (padrão Issue #11) em `/api/public/push/subscribe`; upsert
   silencioso em resubscribe do mesmo endpoint (nunca 409).
4. Ícones do manifest: placeholders (iniciais "OM" ou tag genérica vermelha, `#e63946`),
   troca futura não bloqueia entrega.
5. Push individual com imagem do produto (`image` = MediaUrl/MediaLocalPath) quando o
   `PublisherJob` publica 1 produto no ciclo.
6. Push consolidada ("N novas ofertas hoje!") quando o `PublisherJob` publica >1 produto no
   mesmo ciclo — nunca uma notificação por produto.
7. `localhost` é contexto seguro reconhecido pela spec — dev/teste local do fluxo completo
   de PWA+push não depende da Issue #15 (HTTPS só é pré-requisito real em produção).

### Avaliação de ambiguidade arquitetural (decisão: NÃO escalar ao Arquiteto)
Ponto que mais se aproximava de uma decisão de arquitetura: **agrupamento/throttling das
notificações push e sua interação com o `PublisherJob` existente** (Issue #7). Ponderado
explicitamente:
1. **Web Push é uma integração externa única e já mapeada** (NuGet `WebPush` + API padrão
   `PushManager`/`ServiceWorker` do browser) — não há múltiplas stacks concorrentes nem
   provedor de push proprietário (ex.: FCM/APNs dedicados) a escolher.
2. **O comportamento de throttling já foi decidido pelo Gerente com precisão de
   implementação** (regra: >1 produto no ciclo → 1 notificação consolidada; 1 produto → push
   detalhado), incluindo o payload exato de cada caso. Não sobra uma decisão de design em
   aberto para o Arquiteto resolver — o LT só precisa decidir o ponto de integração técnica
   (ex.: acumular candidatos publicados dentro da execução do job antes de decidir
   individual vs. consolidado), o que é um detalhe de implementação, não arquitetural.
3. **Sem infraestrutura nova**: reaproveita `app_settings` (padrão já existente de
   credenciais), tabela nova simples (sem relacionamentos complexos) e o próprio
   `PublisherJob` já existente — nenhuma decisão de infraestrutura/deploy não-óbvia.
- **Conclusão**: sem ambiguidade arquitetural relevante. Segue direto para o **Líder
  Técnico** (design.md resumido + task breakdown), sem passar pelo Arquiteto. Recomenda-se
  que o LT registre no design.md o ponto de integração exato do throttling dentro do
  `PublisherJob` (nota técnica, não arquitetural).

## Custo (ledger)
| # | Etapa | Agente | Modelo | Tokens | Tools | Tempo (s) |
|---|-------|--------|--------|--------|-------|-----------|
| 1 | Preparacao | Coordenador | haiku-4.5 | 26844 | 21 | 133s |
| 2 | PM Fase 1 | pm | sonnet | 27827 | 8 | 46s |
| 3 | PM Fase 2 | pm-analista-negocios | sonnet | (a preencher pelo orquestrador via usage) | - | - |
