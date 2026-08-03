issue: 131
titulo: "fix: Vazamento de segredo curto no mascaramento de Settings"
etapa_atual: Code Review aprovado — PR #136 mergeado em homolog; aguardando QA
ultimo_agente: code-review
openspec_change: ~
tech_stacks:
  - dotnet
repos:
  omuletachou: "repos/omuletachou"
repo_path: repos/omuletachou
docs_path: repos/omuletachou/documentacoes/ISSUE-131-fix-vazamento-segredo
openspec_path: ~
sub_issues: []
desenv_tasks_merged: []
sub_issues_frontend: {}
pr_homologacao: 136
pr_release: ~
code_review_homolog_pr: 136
qa_status: ~
figma_url: ~
blockers: nenhum
status_comment_id: ~

## Historico
- Dev (rota rapido): corrigido `SettingsMasker.Mask` em `backend/src/AfiliadoBot.Api/Settings/SettingsMasker.cs` —
  quando `value.Length <= 4`, `last4` virava o valor inteiro e o segredo completo aparecia em claro apos os
  16 asteriscos. Agora, para `Length <= 4`, mascara totalmente (`16 + Length` asteriscos, nenhum char real
  revelado); comportamento de valores longos preservado (16 asteriscos + ultimos 4 reais). TDD: RED confirmado
  (4 casos falhando antes do fix) -> GREEN (16/16 testes de `SettingsMaskerTests` passando). Suite completa:
  310/310 (100%). Validado manualmente ponta-a-ponta via `docker compose up -d --build db api` (boot sem
  excecoes) + `PUT`/`GET /api/settings/{key}` reais: `"abcd"` -> `"********************"`,
  `"a"` -> `"*****************"`, valor longo -> `"****************a1b2"` (preservado). PR
  `fix/131-secret-masking` -> `desenv` aberto: https://github.com/DQM-BETA/omuletachou/pull/134. Worktree
  `.worktrees/131-fix-masking` removido ao final.
- LT (rota rapido): PR #134 revisado (diff conferido — fix minimo + testes de regressao) e mergeado via squash
  em `desenv` (commit `b99cf04`), branch `fix/131-secret-masking` deletada. Comentario postado na Issue #131
  informando o merge (issue mantida aberta — rota rapido ainda passa por Code Review + QA + Gate 2). Criado PR
  de homologacao `desenv` -> `homolog` (merge commit, nao-squash):
  https://github.com/DQM-BETA/omuletachou/pull/136. Repo local (`repos/omuletachou`) atualizado e checked out
  em `desenv`.
- Dev (rota dev, achado do `/code-review` no PR #136): confirmado o achado — `Mask()` usava
  `16 + value.Length` para valores com `Length <= 4`, produzindo respostas de 17 a 20 asteriscos,
  o que vaza o comprimento real do segredo por inferencia (contradiz o proprio doc comment da
  classe). Worktree isolado `.worktrees/136-fix-mask-length` (branch `fix/136-mask-fixed-length`
  a partir de `desenv`). Corrigido para comprimento FIXO (`ShortValueMaskLength = 20`, constante
  nomeada), doc comment atualizado documentando a regressao da Issue #136. Adicionados 2 testes
  de regressao em `SettingsMaskerTests`: `Mask_ValorCurto_SempreRetornaMesmoComprimento_NaoVazaTamanhoReal`
  (todos os 4 tamanhos produzem string de 20 chars) e
  `Mask_ValoresCurtosDeTamanhosDiferentes_ProduzemAMesmaStringDeSaida` (1/2/3/4 chars produzem
  exatamente a mesma mascara). `dotnet test`: 318/318 passando (100%). Validado manualmente via
  boot Docker real (`docker compose up -d --build db api`, container `healthy`, sem excecao de
  boot/DI) + seed de 4 chaves sensiveis (`test1..4.api_key`, valores `"a"`/`"ab"`/`"abc"`/`"abcd"`)
  diretamente em `app_settings` + login via usuario seed + `GET /api/settings` real dentro do
  container: os 4 retornaram exatamente `"********************"` (len=20) — comprovado que o
  comprimento da resposta nao varia mais com o tamanho real do segredo. Ambiente Docker limpo
  (`docker compose down -v`, `.env` local removido) e worktree removido ao final. PR
  `fix/136-mask-fixed-length` -> `desenv` aberto (sem merge, aguardando LT):
  https://github.com/DQM-BETA/omuletachou/pull/137.
- LT: PR #137 revisado (diff conferido — comprimento fixo `ShortValueMaskLength = 20` + 2 testes de
  regressao novos; 318/318 passando; validado via boot Docker real com 4 chaves de tamanhos
  diferentes confirmando saida identica). Merge squash de `fix/136-mask-fixed-length` -> `desenv`
  (commit `0c1c41b`), branch remota deletada. `desenv` local atualizado sem conflitos. Confirmado
  que o PR #136 (`desenv` -> `homolog`, ainda OPEN) absorveu automaticamente o commit `0c1c41b`
  como seu ultimo commit (mesmo padrao ja usado para consolidar #131+#132 anteriormente) — nenhuma
  acao de merge foi necessaria em #136. Repo local checked out em `desenv`.

## Custo (ledger)
| # | Etapa | Agente | Modelo | Tokens | Tools | Tempo (s) |
|---|-------|--------|--------|--------|-------|-----------|
| 1 | Preparacao (compartilhada com #130/#131/#132/#133) | coordenador | haiku-4.5 | 34090 | 19 | 271s |
| 2 | Dev (fix + TDD + validacao Docker + PR) | dev-dotnet | sonnet | 48620 | 32 | 313s |
| 3 | Merge PR #134 + PR homologação #136 | lt | sonnet | 36637 | 14 | 90s |
| 4 | Dev (fix comprimento fixo, achado /code-review PR #136) | dev-dotnet | sonnet | 51282 | 31 | 286s |
| 5 | Merge PR #137 -> desenv (absorvido em #136) | lt | sonnet | 38046 | 16 | 82s |
## Code Review — PR #136 (validacao final)
- `git pull origin desenv`: já atualizado (HEAD do PR #136 = `desenv`). `dotnet test`: **318/318 passando** (100%).
- Boot Docker real: `docker compose up -d --build db api` (`.env` local temporario, nunca commitado) — build da imagem `omuletachou-api` OK, containers `afiliado_db` e `afiliado_api` **healthy**, sem exceção de boot/DI, seed do usuário operador executado.
- Validação ao vivo do fix de mascaramento (fixo desta issue + regressão #136): login via `/api/auth/login`, `PUT /api/settings/claude.api_key` com valores de 1 a 4 caracteres (`"a"`, `"ab"`, `"abc"`, `"abcd"`) — todos retornaram exatamente `"********************"` (20 asteriscos, len=20) tanto na resposta do PUT quanto no `GET /api/settings` subsequente; nenhum caractere real revelado, comprimento da resposta constante (não vaza o tamanho real do segredo por inferência). Valor longo (`"abcde12345longvalue"`, 19 chars) retornou `"****************alue"` (16 asteriscos + últimos 4 chars reais), comportamento preservado.
- Achados do `/code-review` (plugin) no PR #136: 1 achado inicial (mascara de valor curto usava `16 + value.Length`, vazando o comprimento) — corrigido no commit `0c1c41b` e **reconfirmado pela re-verificação do próprio plugin** (comentário https://github.com/DQM-BETA/omuletachou/pull/136#issuecomment-5167773990); validação ao vivo acima corrobora. Nenhum achado pendente.
- Checklist de veto: sem segredos commitados no diff (`.env` gerado localmente para o teste, gitignored, removido ao final); código aderente ao CLAUDE.md (stack, convenções de commit/merge); integração real (não mock-only) — endpoints exercitados ponta-a-ponta contra Postgres real em container.
- Containers derrubados (`docker compose down -v`) e `.env` local removido ao final. Repo checked out em `desenv`.
- **Veredito: APROVADO.** Merge `desenv` -> `homolog` (PR #136, merge commit) autorizado.
