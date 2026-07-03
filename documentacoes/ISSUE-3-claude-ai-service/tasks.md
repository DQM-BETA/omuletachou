# Tasks — ISSUE-3: Claude AI Service

## Leitura obrigatoria antes de implementar
- `documentacoes/ISSUE-3-claude-ai-service/prd.md`
- `documentacoes/ISSUE-3-claude-ai-service/especificacao-tecnica.md`
- `repos/omuletachou/CLAUDE.md`

---

## T-01 — ClaudeAiService: Scoring e Geracao de Legendas
**Stack:** dotnet
**Branch:** `feature/ISSUE-SUB-NNN-claude-ai-service` (base: `desenv`)

### O que fazer
1. **Refatorar `IAiService`** (`AfiliadoBot.Domain/Interfaces/IAiService.cs`):
   - Remover `EvaluateProductAsync`
   - Adicionar `ScoreProductAsync` e `GenerateCaptionAsync` conforme spec

2. **Criar `ProductScore`** (`AfiliadoBot.Domain/DTOs/ProductScore.cs`):
   - `public record ProductScore(int Score, string Reason, bool Approve);`

3. **Criar `IAnthropicClientWrapper`** (`AfiliadoBot.Infrastructure/Services/IAnthropicClientWrapper.cs`):
   - Interface fina sobre `AnthropicClient` para permitir mock nos testes

4. **Adicionar NuGet** `Anthropic.SDK` (versao compativel com .NET 8) ao `AfiliadoBot.Infrastructure.csproj`

5. **Implementar `ClaudeAiService`** (`AfiliadoBot.Infrastructure/Services/ClaudeAiService.cs`):
   - Ler settings via `ISettingsRepository`: `claude.api_key`, `claude.model`, `claude.min_score`, `claude.min_score_fallback`
   - `ScoreProductAsync`: system prompt com criterios D4 + parse resiliente (Regex) + retry 3x backoff exponencial + fallback
   - `GenerateCaptionAsync`: system prompt persona Mulet + regras por rede (switch) + fallback template
   - `Approve = Score >= minScore` sempre em C# (nunca do JSON)

6. **Registrar no DI** (`AfiliadoBot.Infrastructure/DependencyInjection.cs` ou equivalente):
   - `IAiService → ClaudeAiService`
   - `IAnthropicClientWrapper → AnthropicClientWrapper`

7. **Nova migration de seed** (`claude.min_score_fallback = "5"`, Id=31):
   - `dotnet ef migrations add AddClaudeMinScoreFallbackSeed`
   - Inserir via `migrationBuilder.InsertData` no `Up`; `DeleteData` no `Down`
   - Tambem adicionar Id=31 no `HasData` do `AppSettingConfiguration.cs` (consistencia em ambientes novos)

8. **Testes unitarios** (`AfiliadoBot.Tests/Services/ClaudeAiServiceTests.cs`):
   - 8 casos de teste com mock de `IAnthropicClientWrapper` (sem chamadas reais)
   - Ver tabela completa em `especificacao-tecnica.md` secao 3

### Criterios de Aceite (Given/When/Then)

**CA-1:** Score no range 0–10
- Given produto com dados validos
- When `ScoreProductAsync` e chamado
- Then `Score` entre 0 e 10; `Reason` nao vazia

**CA-2:** Approve derivado do threshold
- Given `claude.min_score=6`, mock retorna score 5
- When `ScoreProductAsync` e chamado
- Then `Approve=false`; com score 7, `Approve=true`

**CA-3:** Parse resiliente com texto extra
- Given mock retorna `"Claro! {\"score\":8,\"reason\":\"otimo desconto\"}"`
- When `ClaudeAiService` processa
- Then `Score=8`, `Reason="otimo desconto"`, sem excecao

**CA-5:** Suporte a 5 redes sem excecao
- Given cada valor de `SocialNetwork`
- When `GenerateCaptionAsync` para cada um
- Then nenhuma `NotImplementedException`; todas retornam string nao vazia

**CA-6:** Fallback de scoring apos 3 falhas
- Given mock lanca excecao em todas as tentativas, `min_score_fallback=5`
- When `ScoreProductAsync` e chamado
- Then `Score=5`, `Approve=false`, `Reason="Claude API unavailable"`

**CA-7:** Fallback de legenda retorna template fixo
- Given mock lanca excecao
- When `GenerateCaptionAsync` e chamado
- Then retorna string com `Title`, `SalePrice`, `DiscountPercent` sem excecao

**CA-9:** DI registrado, API inicializa
- Given `ClaudeAiService` no container, `claude.api_key` configurado
- When `docker compose up` e `GET /health`
- Then 200 OK sem `InvalidOperationException`

**CA-10:** Testes passam sem chamadas reais
- Given mocks configurados
- When `dotnet test`
- Then 0 falhas; 0 chamadas reais a Anthropic

### Contexto tecnico
- **Spec completa:** `documentacoes/ISSUE-3-claude-ai-service/especificacao-tecnica.md`
- **PRD:** `documentacoes/ISSUE-3-claude-ai-service/prd.md`
- **Contrato atual (a refatorar):** `backend/src/AfiliadoBot.Domain/Interfaces/IAiService.cs`
- **Seeds existentes:** `backend/src/AfiliadoBot.Infrastructure/Data/Configurations/AppSettingConfiguration.cs` (Id=1..30; o novo e Id=31)
- **Stack:** .NET 8, EF Core 8, PostgreSQL, Anthropic.SDK
- **Cobertura minima:** 80%
- **Commit pattern:** `feat(ISSUE-SUB-NNN): descricao`
