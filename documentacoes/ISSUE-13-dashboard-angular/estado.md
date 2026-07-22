# Estado — ISSUE-13: Dashboard Angular (Todas as Paginas Admin)

## Campos principais
issue: 13
repo: omuletachou
titulo: feat: Dashboard Angular (Todas as Paginas Admin)
rota: normal
etapa_atual: Refinamento Técnico
docs_path: repos/omuletachou/documentacoes/ISSUE-13-dashboard-angular
openspec_path: repos/omuletachou/openspec/changes/issue-13-dashboard-angular
ultimo_agente: pm-analista-negocios
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
Aguardando LT (design.md, especificacao-tecnica.md, tasks.md, sub-issues).

## Sub-issues
sub_issues: []
desenv_tasks_merged: []

## Merge e Encerramento
Aguardando fluxo da rota normal (Dev, LT, Code Review, QA, Gate 2).

## Historico de etapas
| # | Etapa | Agente | Status |
|---|---|---|---|
| 1 | Preparacao | Coordenador | ativo — Issue preparada, estado.md criado, comentário 📍 Status criado, card adicionado ao board em 💻 Em Desenvolvimento |
| 2 | PM Fase 1 | pm-analista-negocios | concluído — perguntas de levantamento postadas na Issue, comentário 📍 Status atualizado para Gate 1 |
| 3 | PM Fase 2 | pm-analista-negocios | concluído — proposal.md e criterios-aceite.md escritos, investigação do contrato PUT /api/settings/{key} concluída (sem ajuste retroativo necessário), sem ambiguidade arquitetural identificada, sumário do PRD postado na Issue, comentário 📍 Status atualizado para Refinamento Técnico |

## Custo (ledger)
| # | Etapa | Agente | Modelo | Tokens | Tools | Tempo_s |
|---|---|---|---|---|---|---|
| 1 | Preparacao | Coordenador | haiku-4.5 | 45607 | 37 | 210s |
| 2 | PM Fase 1 | pm | sonnet | 32806 | 10 | 85s |
| 3 | PM Fase 2 | pm | sonnet | 66779 | 26 | 256s |
