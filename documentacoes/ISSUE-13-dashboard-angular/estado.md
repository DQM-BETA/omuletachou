# Estado — ISSUE-13: Dashboard Angular (Todas as Paginas Admin)

## Campos principais
issue: 13
repo: omuletachou
titulo: feat: Dashboard Angular (Todas as Paginas Admin)
rota: normal
etapa_atual: Em Desenvolvimento
docs_path: repos/omuletachou/documentacoes/ISSUE-13-dashboard-angular
openspec_path: repos/omuletachou/openspec/changes/issue-13-dashboard-angular
ultimo_agente: lider-tecnico
status_comment_id: 5045887889
pr_homologacao: ~
code_review_homolog_pr: ~
pr_release: ~
closedAt: ~

## Contexto
Stack: Angular 17 + TypeScript + HttpClient
Repo: DQM-BETA/omuletachou
Branch base: desenv
Dependência: Issue #11 (REST API pública/administrativa: `POST /api/auth/login`, GET/PATCH `/api/products`, GET/POST `/api/queue`, GET/PUT `/api/settings`, GET `/api/reports`, POST `/api/jobs/retry`) — já entregue em main.
Dependência adicional: Issue #17 (Sub-B: Dashboard Angular — Scaffolding e Docker) — já concluída. O scaffold em `dashboard/` (Angular 17, TypeScript, estrutura de páginas stub em `dashboard/src/app/pages/`, Dockerfile, nginx.conf) já existe — confirmado por inspeção do diretório. Esta issue evolui esse scaffold, NÃO recria.

**Nota técnica:** Esta é a primeira issue de frontend administrativo Angular com conteúdo real do projeto. O dashboard consumirá a API administrativa protegida por JWT (Issue #11), nunca expondo URLs internas ao browser. Scaffold de páginas (`/products`, `/queue`, `/facebook-manual`, `/settings`, `/reports`) e `services/` já estruturados; esta issue implementa os serviços e templates componentes reais, mais as 2 telas novas definidas no Gate 1 (`/login`, `/jobs`).

## PM Fase 1 — levantamento
Concluído. Perguntas postadas na Issue #13 (comentário https://github.com/DQM-BETA/omuletachou/issues/13#issuecomment-5045926492).

## Gate 1 — respostas do Gerente
Concluído. Respostas completas no comentário https://github.com/DQM-BETA/omuletachou/issues/13#issuecomment-5046210646. Resumo:
1. Escopo: 7 telas — as 5 do scaffold + `/login` (dedicada, sem menu lateral) + `/jobs` (disparo manual de Collector/Processor/Publisher).
2. Auth: JWT em memória (`AuthService` singleton) + `sessionStorage` (não `localStorage`); `HttpInterceptor` captura 401 global, limpa token, redireciona `/login` com mensagem de sessão expirada; sem refresh token.
3. Scaffold da Issue #17 confirmado — evoluir, não recriar.
4. Mascaramento em Settings: placeholder mascarado, campo vazio no submit não é enviado no PUT.
5. Design livre (sem Figma), priorizando Angular Material/PrimeNG.
6. Desktop-only, sem responsividade mobile/tablet.
7. "Testar conexão": escopo novo, endpoint não existe — autorizado entregar Settings sem esse botão, vira follow-up.
8. Fatiamento sugerido: Sub-A Auth, Sub-B Products+Queue, Sub-C Settings+Jobs, Sub-D Facebook Manual+Reports (não fechado, LT decide).
9. Sem Playwright nesta issue — testes unitários Angular + validação manual.

## PM Fase 2 — PRD consolidado
Concluído.
- `proposal.md`: repos/omuletachou/openspec/changes/issue-13-dashboard-angular/proposal.md
- `criterios-aceite.md`: repos/omuletachou/documentacoes/ISSUE-13-dashboard-angular/criterios-aceite.md
- Sumário do PRD postado como comentário na Issue #13.

### Investigação do contrato `PUT /api/settings/{key}` (Issue #11) — decisão sobre ajuste retroativo
Lido `especificacao-tecnica.md` e `criterios-aceite.md` da Issue #11 (CA-C1 a CA-C6, seção 5 da espec técnica). Achado: `PUT /api/settings/{key}` já é um endpoint **por chave individual** (`{ "value": "novo-valor" }`), não um payload em lote. Logo, "campo vazio não sobrescreve" (exigência do Gate 1) é **inteiramente resolvida no front** — basta não disparar a chamada PUT para chaves deixadas em branco no formulário. **Nenhum ajuste retroativo no contrato da API #11 é necessário** — decisão documentada em `proposal.md` (seção Regras de negócio + Avaliação de ambiguidade arquitetural) e em CA-T4 de `criterios-aceite.md`. Diferente do padrão dos fixes retroativos das Issues #8/#9 (aqueles exigiam mudança de comportamento no backend em produção; aqui o contrato já suporta o comportamento desejado sem alteração).

### Avaliação de ambiguidade arquitetural (decisão: NÃO escalar ao Arquiteto)
Dois pontos foram ponderados explicitamente antes da decisão:
1. **Ajuste retroativo em `PUT /api/settings/{key}`**: investigado e descartado — ver acima. Não há mudança de contrato de API em produção, então não se aplica o mesmo cuidado redobrado das Issues #8/#9 (que alteravam comportamento já em `main`).
2. **Estratégia de sessão no browser (JWT em memória + `sessionStorage` + `HttpInterceptor` de 401)**: é a primeira feature de sessão de usuário do projeto, mas o padrão (interceptor + guard + storage) é prática consolidada e amplamente documentada do ecossistema Angular, não uma decisão de arquitetura não-óbvia ou com múltiplas stacks/integrações em jogo. Os riscos residuais já foram endereçados explicitamente pelo Gerente no Gate 1: `sessionStorage` (não `localStorage`) reduz superfície de XSS residual; ausência de refresh token foi aceita conscientemente; CSRF não se aplica porque o token vai em header `Authorization` anexado manualmente pelo interceptor, não em cookie enviado automaticamente pelo browser; múltiplas abas usam `sessionStorage` por aba (comportamento nativo esperado, sem necessidade de sincronização cross-tab nesta issue de uso interno por um único operador).
- **Conclusão**: sem ambiguidade arquitetural relevante. Segue direto para o **Líder Técnico** (design.md resumido + task breakdown), sem passar pelo Arquiteto.

## Refinamento Técnico (LT)
Concluído.
- `design.md` (resumido, PM roteou sem Arquiteto): openspec/changes/issue-13-dashboard-angular/design.md
- `especificacao-tecnica.md`: documentacoes/ISSUE-13-dashboard-angular/especificacao-tecnica.md
- `tasks.md`: openspec/changes/issue-13-dashboard-angular/tasks.md
- Decisão de UI: Angular Material (justificativa em especificacao-tecnica.md §0).
- Decisão UX/UI da squad: NÃO acionado — ferramenta interna desktop-only sem exigência de marca; Gate 1 já resolveu design ("priorizando Angular Material/PrimeNG"); critérios de aceite já detalham comportamento visual suficiente (cores de badge/status, agrupamento de seções). Segue direto para os Devs.
- 3 gaps de contrato descobertos por inspeção direta dos controllers da Issue #11 (`main`), resolvidos como extensões aditivas (sem reabrir a Issue #11 nem exigir aprovação do Gerente — não são mudanças de comportamento em produção): (1) `ai_score`/`ai_reason` ausentes em `GET /api/products` → Sub-B estende `ProductListItemDto`; (2) sem endpoint para marcar item da fila `ManualPending → Published` → Sub-D adiciona `PATCH /api/queue/{id}/status`; (3) sem endpoint de totais hoje/semana/mês em Reports → Sub-D adiciona `GET /api/reports/totals` (tabela de falhas recentes reaproveita `GET /api/queue?status=Failed`, já existente, sem endpoint novo).
- Sumário técnico postado na Issue #13: https://github.com/DQM-BETA/omuletachou/issues/13#issuecomment-5046388558
- Comentário 📍 Status atualizado para "Em Desenvolvimento": https://github.com/DQM-BETA/omuletachou/issues/13#issuecomment-5045887889

## Sub-issues
sub_issues: [#103 (stack:angular, task_id:T-01, Sub-A Autenticação, bloqueante), #104 (stack:angular, task_id:T-02, Sub-B Products+Queue), #105 (stack:angular, task_id:T-03, Sub-C Settings+Jobs manual), #106 (stack:angular, task_id:T-04, Sub-D Facebook Manual+Reports)]
desenv_tasks_merged: []

## Merge e Encerramento
Aguardando fluxo da rota normal (Dev, LT, Code Review, QA, Gate 2).

### Sub-A (#103) — Autenticação — Dev concluído
- Angular Material 17.3 instalado (`ng add @angular/material`). Nota técnica: a especificacao-tecnica.md §0 descreve a API M3 (`mat.theme(...)`), disponível a partir do Angular Material 18+; o scaffold está fixado em 17.3 (mesma major do restante do dashboard, Issue #17), cuja API estável é M2. Aplicado o mesmo espírito (tema light, paleta azul, sem guideline de marca) via `mat.define-light-theme` com `$blue-palette` em `src/styles.scss`. Registrado para o LT avaliar se aceita a divergência ou decide upgrade de major em issue futura.
- `AuthService`, `authGuard`/`loginGuard`, `authInterceptor` implementados conforme especificacao-tecnica.md §1.1-1.3, em `dashboard/src/app/core/auth/`.
- `ShellComponent` (`dashboard/src/app/core/shell/`) com `MatSidenav` + 6 itens de navegação (Products, Queue, Facebook Manual, Settings, Jobs, Reports) + botão de Logout.
- `LoginComponent` (`dashboard/src/app/pages/login/`) com formulário reativo Material, sem menu lateral (CA-A8), redirecionamento em sucesso e mensagem de erro em credenciais inválidas.
- `JobsComponent` criado como stub (rota `/jobs`, ainda sem lógica — escopo de Sub-C).
- `app.routes.ts`/`app.config.ts` atualizados: `provideHttpClient(withInterceptors([authInterceptor]))`, `provideAnimationsAsync()`, rotas com `authGuard`/`loginGuard`.
- `proxy.conf.json` criado para `ng serve` local (`/api` → `http://localhost:8080`), registrado em `angular.json` (`serve.options.proxyConfig`).
- Testes: 31/31 passando (Jasmine/Karma) cobrindo CA-A1 a CA-A7 (login sucesso/falha, storage, guard, interceptor 401 dentro/fora de `/api/auth/login`).
- Build (`ng build`): sem erros de TypeScript (1 warning de orçamento de bundle — 712kb vs budget de 500kb — não bloqueante, `maximumError` é 1mb).
- Boot Docker validado: `docker compose up -d --build db api dashboard` a partir do worktree — smoke test real via `curl` contra a API real (seed `admin@omuletachou.com.br`, Issue #11): login válido (200 + JWT), login inválido (401), chamada sem token a `/api/products` (401), chamada com token (200) — todos via proxy nginx do container `dashboard` (porta 4200). Containers derrubados (`docker compose down -v`) ao final.
- PR: feature/103-auth → desenv.
- Worktree `.worktrees/feature-103-auth` a ser removido pelo LT após o merge (ou pela sessão principal).

## Historico de etapas
| # | Etapa | Agente | Status |
|---|---|---|---|
| 1 | Preparacao | Coordenador | ativo — Issue preparada, estado.md criado, comentário 📍 Status criado, card adicionado ao board em 💻 Em Desenvolvimento |
| 2 | PM Fase 1 | pm-analista-negocios | concluído — perguntas de levantamento postadas na Issue, comentário 📍 Status atualizado para Gate 1 |
| 4 | Refinamento Técnico (LT) | lider-tecnico | concluído — design.md + especificacao-tecnica.md + tasks.md escritos, decisão Angular Material, UX/UI não acionado, 4 sub-issues criadas (#103-#106), 3 gaps de contrato com a API #11 identificados e resolvidos como extensões aditivas |
| 3 | PM Fase 2 | pm-analista-negocios | concluído — proposal.md e criterios-aceite.md escritos, investigação do contrato PUT /api/settings/{key} concluída (sem ajuste retroativo necessário), sem ambiguidade arquitetural identificada, sumário do PRD postado na Issue, comentário 📍 Status atualizado para Refinamento Técnico |
| 4 | Refinamento Técnico | lider-tecnico | concluído — design.md/especificacao-tecnica.md/tasks.md escritos, 4 sub-issues criadas (#103-#106), 3 gaps de contrato com a API #11 resolvidos como extensões aditivas, UX/UI da squad não acionado (justificativa registrada), sumário postado na Issue, comentário 📍 Status atualizado para Em Desenvolvimento |

## Custo (ledger)
| # | Etapa | Agente | Modelo | Tokens | Tools | Tempo_s |
|---|---|---|---|---|---|---|
| 1 | Preparacao | Coordenador | haiku-4.5 | 45607 | 37 | 210s |
| 2 | PM Fase 1 | pm | sonnet | 32806 | 10 | 85s |
| 3 | PM Fase 2 | pm | sonnet | 66779 | 26 | 256s |
| 4 | Refinamento LT | lt | sonnet | 109958 | 49 | 458s |
