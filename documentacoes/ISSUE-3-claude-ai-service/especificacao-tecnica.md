# Especificacao Tecnica — ISSUE-3: Claude AI Service

## Visao Geral

Refatora `IAiService` de um metodo monolitico (`EvaluateProductAsync`) para dois contratos separados, e implementa `ClaudeAiService` em `AfiliadoBot.Infrastructure` consumindo a Anthropic Claude API via `Anthropic.SDK`.

---

## 1. Mudancas no Domain (`AfiliadoBot.Domain`)

### 1.1 Refatorar `IAiService`

**Arquivo:** `backend/src/AfiliadoBot.Domain/Interfaces/IAiService.cs`

Substituir o metodo `EvaluateProductAsync` pelos dois abaixo:

```csharp
using AfiliadoBot.Domain.Entities;
using AfiliadoBot.Domain.DTOs;

namespace AfiliadoBot.Domain.Interfaces;

public interface IAiService
{
    Task<ProductScore> ScoreProductAsync(Product product, CancellationToken ct = default);
    Task<string> GenerateCaptionAsync(Product product, SocialNetwork network, CancellationToken ct = default);
}
```

**Impacto:** qualquer caller de `EvaluateProductAsync` (ex.: `ProcessorJob`) deve ser atualizado para chamar os dois metodos separados.

### 1.2 Criar `ProductScore`

**Arquivo:** `backend/src/AfiliadoBot.Domain/DTOs/ProductScore.cs` (criar pasta `DTOs/` se nao existir)

```csharp
namespace AfiliadoBot.Domain.DTOs;

public record ProductScore(int Score, string Reason, bool Approve);
```

- `Score`: inteiro 0–10 retornado pelo Claude
- `Reason`: justificativa textual retornada pelo Claude
- `Approve`: derivado em C# via `Score >= minScore` — NUNCA confiar no booleano do JSON da IA

---

## 2. Implementacao em Infrastructure (`AfiliadoBot.Infrastructure`)

### 2.1 Adicionar NuGet `Anthropic.SDK`

**Arquivo:** `backend/src/AfiliadoBot.Infrastructure/AfiliadoBot.Infrastructure.csproj`

Adicionar dentro de `<ItemGroup>`:
```xml
<PackageReference Include="Anthropic.SDK" Version="3.*" />
```

Verificar a versao mais recente compativel com .NET 8 em https://www.nuget.org/packages/Anthropic.SDK

### 2.2 `ClaudeAiService`

**Arquivo:** `backend/src/AfiliadoBot.Infrastructure/Services/ClaudeAiService.cs`

**Dependencias injetadas:**
- `AnthropicClient` (ou wrapper/interface do SDK — ver nota sobre testabilidade abaixo)
- `ISettingsRepository` (ou acesso direto ao `AppDbContext`) para ler `claude.min_score`, `claude.min_score_fallback`, `claude.api_key`, `claude.model`

**Nota sobre testabilidade:** o `Anthropic.SDK` pode nao expor interface publica para `AnthropicClient`. Nesse caso, o Dev deve criar um wrapper fino `IAnthropicClientWrapper` (com `Messages.GetClaudeMessageAsync`) e injetar a interface — isso permite mock nos testes sem depender de detalhes internos do SDK.

#### 2.2.1 `ScoreProductAsync`

Fluxo:
1. Ler `claude.min_score` e `claude.min_score_fallback` do banco via `ISettingsRepository`.
2. Construir system prompt com criterios de scoring (D4 do PRD):
   - Desconto real minimo 15%; precos inflados penalizam
   - Categorias preferidas: eletronicos, casa/cozinha, beleza, brinquedos, moda
   - Titulo sem nome descritivo (so codigo de modelo) penaliza
   - Preco final acima de R$ 2.000 penaliza
   - Prazo de entrega longo penaliza
3. Construir mensagem do usuario com dados do produto: `Title`, `SalePrice`, `OriginalPrice`, `DiscountPercent`, `Platform`, `Category`.
4. Instrucao ao Claude: responder **apenas** JSON `{"score": <0-10>, "reason": "<texto>"}`.
5. Chamar Claude com retry ate 3 tentativas (backoff exponencial simples: 1s, 2s, 4s).
6. **Parse resiliente:** extrair o bloco JSON da resposta mesmo que haja texto adicional (usar `Regex` para localizar `{...}` com os campos `score` e `reason`).
7. Derivar `Approve = score >= minScore` em C#.
8. Retornar `new ProductScore(score, reason, approve)`.
9. **Fallback (3 falhas):** retornar `new ProductScore(minScoreFallback, "Claude API unavailable", false)`.

#### 2.2.2 `GenerateCaptionAsync`

Fluxo:
1. Construir system prompt com persona Mulet:
   - Brasileiro, 30 anos, Rio de Janeiro, entusiasta de tecnologia e economia, tom de grupo de familia
   - **Proibido em qualquer legenda:** mencionar comissao/vinculo de afiliado, usar "oferta imperdivel", superlativos sem base factual
2. Adicionar instrucoes especificas da rede social (via `switch` no `network`):

| `SocialNetwork` | Instrucao ao Claude |
|---|---|
| `Telegram` | Direto, preco em destaque, maximo 200 caracteres antes do link |
| `Instagram` | Emocional, 3–5 hashtags, CTA forte, sem limite rigido |
| `TikTok` | Linguagem jovem, urgencia, maximo 150 caracteres |
| `YouTube` | Foco em beneficios, SEO-friendly, 200–400 caracteres |
| `Facebook` | Conversacional, sem hashtags, como dica entre amigos |

3. Chamar Claude com o prompt construido.
4. Retornar a string da legenda.
5. **Fallback (excecao):** retornar `$"Achei essa oferta: {product.Title} por R$ {product.SalePrice:F2} ({product.DiscountPercent}% OFF) {product.Link}"`.

### 2.3 Registro no DI

**Arquivo:** `backend/src/AfiliadoBot.Infrastructure/DependencyInjection.cs` (ou arquivo de extensao de servicos existente)

```csharp
services.AddScoped<IAiService, ClaudeAiService>();
// Se usar wrapper:
services.AddScoped<IAnthropicClientWrapper>(sp => new AnthropicClientWrapper(apiKey));
```

O `apiKey` deve vir de `ISettingsRepository` ou de `IConfiguration` (variavel de ambiente `CLAUDE__API_KEY`).

### 2.4 Nova migration de seed — `claude.min_score_fallback`

O seed existente (`AppSettingConfiguration.cs`) ja tem 30 registros (Id=1..30) e a migration correspondente ja foi aplicada. **Nao alterar o seed existente** — criar nova migration EF Core:

```
dotnet ef migrations add AddClaudeMinScoreFallbackSeed --project AfiliadoBot.Infrastructure --startup-project AfiliadoBot.Api
```

Na migration gerada, no metodo `Up`, inserir via `migrationBuilder.InsertData`:

```csharp
migrationBuilder.InsertData(
    table: "app_settings",
    columns: new[] { "id", "key", "value", "updated_at" },
    values: new object[] { 31, "claude.min_score_fallback", "5", new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc) }
);
```

E no `Down`, `migrationBuilder.DeleteData(table: "app_settings", keyColumn: "id", keyValue: 31)`.

**Tambem atualizar o seed no `AppSettingConfiguration.cs`** para incluir o Id=31 (para consistencia em ambientes novos — `HasData` e idempotente).

---

## 3. Testes (`AfiliadoBot.Tests`)

**Arquivo:** `backend/tests/AfiliadoBot.Tests/Services/ClaudeAiServiceTests.cs`

**Framework:** xUnit + Moq (ou NSubstitute, conforme existente no projeto).

**Mock:** `IAnthropicClientWrapper` (nunca `AnthropicClient` direto — ver 2.2).

### Casos de teste obrigatorios

| Metodo de teste | Cenario | Assercao |
|---|---|---|
| `ScoreProductAsync_RetornaApprove_QuandoScoreAcimaDoThreshold` | Mock retorna `{"score": 8, "reason": "bom produto"}`, `min_score=6` | `Approve=true`, `Score=8` |
| `ScoreProductAsync_RetornaReject_QuandoScoreAbaixoDoThreshold` | Mock retorna `{"score": 4, "reason": "preco alto"}`, `min_score=6` | `Approve=false`, `Score=4` |
| `ScoreProductAsync_ParseResilienteComTextoExtra` | Mock retorna `"Claro! {\"score\":8,\"reason\":\"otimo\"}"` | Extrai `Score=8` sem excecao (CA-3) |
| `ScoreProductAsync_UsaFallback_QuandoApiIndisponivel` | Mock lanca excecao em todas as 3 tentativas, `min_score_fallback=5` | `Score=5`, `Approve=false`, `Reason="Claude API unavailable"` |
| `GenerateCaptionAsync_RetornaLegenda_QuandoApiDisponivel` | Mock retorna string de legenda valida | Retorno nao vazio, sem excecao |
| `GenerateCaptionAsync_RetornaTemplate_QuandoApiFalha` | Mock lanca excecao | Retorno contem `Title` e `SalePrice` do produto |
| `GenerateCaptionAsync_SuportaTodasAsRedes_SemExcecao` | Chamar para cada valor de `SocialNetwork` | Nenhuma `NotImplementedException`/`ArgumentOutOfRangeException` (CA-5) |
| `GenerateCaptionAsync_Instagram_ContemHashtagsEEmojis` | Mock retorna legenda com # e emoji | `result.Contains('#')` e contem emoji (CA-4) |

---

## 4. Contratos de Configuracao (AppSettings)

| Chave | Tipo | Descricao | Seed padrao |
|---|---|---|---|
| `claude.api_key` | string | Chave de API da Anthropic | `""` (configurar por env var) |
| `claude.model` | string | Modelo Claude a usar | `""` (runtime: `claude-haiku-4-5-20251001`) |
| `claude.min_score` | int | Threshold de aprovacao (0–10) | `"6"` |
| `claude.min_score_fallback` | int | Score usado quando API falha | `"5"` — **novo (Id=31)** |

---

## 5. Padroes Obrigatorios

- `Approve` sempre derivado em C# (`Score >= minScore`) — nunca confiar no JSON da IA
- Parse resiliente: `Regex` para extrair `{...}` mesmo com texto adicional na resposta
- Retry ate 3 tentativas com backoff exponencial antes do fallback
- Sem chamadas reais a API nos testes (mock obrigatorio)
- Cobertura minima: 80% (conforme CLAUDE.md do repo)
- `IAnthropicClientWrapper` para desacoplar do SDK concreto
