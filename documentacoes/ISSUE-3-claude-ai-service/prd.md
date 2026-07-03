# PRD — ISSUE-3: Claude AI Service (Scoring + Geracao de Legendas)

## Objetivo de Negócio
Implementar o serviço de IA responsável por duas funções centrais do AfiliadoBot: (1) avaliar automaticamente se um produto coletado de plataformas de afiliados (Amazon, MercadoLivre, Shopee) é relevante o suficiente para publicação, atribuindo um score de 0 a 10; e (2) gerar legendas personalizadas para cada rede social ativa, com o tom e estilo de "O Mulet". Isso reduz intervenção manual e garante consistência de voz de marca nas publicações.

## Usuários / Sistemas Afetados
- **ProcessorJob (Hangfire)** — consome `IAiService.ScoreProductAsync` para avaliar produtos e decidir se entram na fila de publicação
- **PublisherJob (Hangfire)** — consome `IAiService.GenerateCaptionAsync` para gerar a legenda antes de publicar em cada rede social (somente produtos aprovados)
- **AfiliadoBot.Infrastructure** — projeto que implementa `ClaudeAiService`
- **AfiliadoBot.Domain** — define o contrato `IAiService` (refatorado conforme D1)
- **PostgreSQL** — armazena `ai_score` e `ai_reason` na tabela `products`; a legenda gerada vai junto ao registro de publicação
- **Anthropic Claude API** — serviço externo consumido via `Anthropic.SDK` com o modelo `claude-haiku-4-5-20251001`

## Decisões de Design

### D1 — Contrato da Interface IAiService
O contrato em `IAiService.cs` é **refatorado** para dois métodos separados:

```csharp
Task<ProductScore> ScoreProductAsync(Product product, CancellationToken ct = default);
Task<string> GenerateCaptionAsync(Product product, SocialNetwork network, CancellationToken ct = default);
```

- `ProcessorJob` chama apenas `ScoreProductAsync`
- `PublisherJob` chama `GenerateCaptionAsync` somente para produtos aprovados, por rede social, no momento da publicação
- Produtos rejeitados (abaixo do threshold) não chegam ao `PublisherJob` — sem desperdício de tokens

### D2 — DTO ProductScore
Estrutura de retorno do scoring:

```csharp
public record ProductScore(int Score, string Reason, bool Approve);
```

- `Score`: inteiro 0–10
- `Reason`: texto justificativo retornado pelo Claude
- `Approve`: derivado em C# via comparação `Score >= claude.min_score` (nunca confiar no booleano vindo do JSON da IA)

### D3 — Persona e Tom por Rede Social
Persona "Mulet": brasileiro, 30 anos, Rio de Janeiro, entusiasta de tecnologia e economia, tom de grupo de família.

Restrições absolutas (proibido em qualquer legenda):
- Mencionar comissão ou vínculo de afiliado
- Usar expressão "oferta imperdível"
- Superlativos sem base factual

Orientações por rede social (embutidas como instrução de sistema no prompt — não via banco):

| Rede | Estilo | Limite de caracteres |
|---|---|---|
| Telegram | Direto, preço em destaque | Máx 200 chars antes do link |
| Instagram | Emocional, 3–5 hashtags, CTA forte | — |
| TikTok | Linguagem jovem, urgência | Máx 150 chars |
| YouTube | Foco em benefícios, SEO-friendly | 200–400 chars |
| Facebook | Conversacional, sem hashtags, como dica entre amigos | — |

### D4 — Critérios de Scoring
O Dev tem liberdade de estruturar o prompt de scoring, respeitando os critérios obrigatórios:

| Critério | Direção |
|---|---|
| Desconto real mínimo 15% | Abaixo penaliza; preço inflado penaliza |
| Categorias preferidas | Eletrônicos, casa/cozinha, beleza, brinquedos, moda |
| Qualidade do título | Só código de modelo (sem nome descritivo) penaliza |
| Preço final acessível | Acima de R$ 2.000 penaliza |
| Disponibilidade | Prazo de entrega longo penaliza |

Score: inteiro 0–10. Threshold padrão: 6 (configurável via `claude.min_score` em `AppSetting`).

### D5 — Fallback em Falha da API Claude

**Scoring (`ScoreProductAsync`):**
- Após 3 tentativas com falha: status do produto = `Error`, `ai_reason = "Claude API unavailable"`
- Novo `AppSetting`: `claude.min_score_fallback = "5"` (padrão abaixo do threshold → rejeição segura)
- O fallback não aprova produtos — garante que nenhum produto de qualidade desconhecida vaze para publicação

**Geração de Legenda (`GenerateCaptionAsync`):**
- Em falha: usar template fixo `"Achei essa oferta: {Title} por R$ {SalePrice} ({DiscountPct}% OFF)"` + link
- Publicação **prossegue** com a legenda de fallback
- Campo `error_message = "Caption generated via fallback template"` para rastreabilidade

## Funcionalidades Principais

### F1 — Avaliação de Produto (Scoring)
- Enviar ao Claude um prompt com os dados do produto (título, preço, desconto, plataforma, categoria) e os critérios de avaliação definidos em D4
- Receber resposta JSON com campos `score` (inteiro 0–10) e `reason` (string)
- Parse resiliente: extrair o JSON mesmo que a resposta contenha texto adicional antes/depois do bloco JSON
- Derivar `approve` em C# comparando `Score >= claude.min_score`
- Retornar `ProductScore` com os três campos para o caller

### F2 — Geração de Legenda por Rede Social
- Enviar ao Claude um prompt com os dados do produto, a persona Mulet (D3) e as orientações específicas da rede social alvo
- Suportar 5 redes: Telegram, Instagram, TikTok, YouTube, Facebook
- Em falha, aplicar fallback definido em D5
- Retornar a legenda como string pronta para publicação

### F3 — Configuração e DI
- Ler `claude.api_key`, `claude.min_score` (default: 6) e `claude.min_score_fallback` (default: 5) de `AppSetting` via `ISettingsRepository` ou similar
- Registrar `ClaudeAiService` como implementação de `IAiService` no container DI de `AfiliadoBot.Infrastructure`

### F4 — Testes com Mock
- Testes unitários usando mock do `AnthropicClient` (sem chamadas reais ao Claude)
- Cobertura: resposta JSON válida, resposta com texto extra (parse resiliente), score abaixo do threshold (`Approve = false`), geração de legenda por cada rede, comportamento de fallback em falha de API

## Critérios de Aceite

### CA-1: Score retornado dentro do range
- **Given** um produto com dados válidos (título, preço, desconto, link)
- **When** `IAiService.ScoreProductAsync` é chamado
- **Then** o `Score` retornado é um inteiro entre 0 e 10 (inclusive); `Reason` é uma string não vazia

### CA-2: Approve derivado do threshold configurável
- **Given** `claude.min_score` = 6 e um produto que recebe score 5 do Claude
- **When** `ScoreProductAsync` é chamado
- **Then** `Approve = false`; dado score 7, `Approve = true`

### CA-3: Parse resiliente do JSON de scoring
- **Given** o mock do `AnthropicClient` retorna texto no formato `"Claro! Aqui está: { \"score\": 8, \"reason\": \"ótimo desconto\" }"`
- **When** o `ClaudeAiService` processa a resposta
- **Then** extrai corretamente `Score = 8`, `Reason = "ótimo desconto"`, `Approve = true` sem lançar exceção

### CA-4: Legenda para Instagram contém emojis e hashtags
- **Given** um produto da plataforma MercadoLivre e a rede alvo `Instagram`
- **When** `GenerateCaptionAsync` é chamado
- **Then** a legenda retornada contém pelo menos um emoji e pelo menos uma hashtag (inspecionado via mock que retorna legenda de exemplo)

### CA-5: Suporte às 5 redes sociais sem exceção
- **Given** os 5 valores do enum `SocialNetwork` (Telegram, Instagram, TikTok, Youtube, Facebook)
- **When** `GenerateCaptionAsync` é chamado para cada um
- **Then** nenhuma chamada lança `NotImplementedException` ou `ArgumentOutOfRangeException`; todas retornam string não vazia (via mock)

### CA-6: Fallback de scoring após 3 falhas
- **Given** o mock do `AnthropicClient` lança exceção em todas as tentativas
- **When** `ScoreProductAsync` é chamado
- **Then** após 3 tentativas, retorna `ProductScore` com `Score = claude.min_score_fallback`, `Approve = false`, `Reason = "Claude API unavailable"`

### CA-7: Fallback de legenda retorna template fixo
- **Given** o mock do `AnthropicClient` lança exceção
- **When** `GenerateCaptionAsync` é chamado
- **Then** retorna string no formato `"Achei essa oferta: {Title} por R$ {SalePrice} ({DiscountPct}% OFF) {Link}"` sem lançar exceção

### CA-8: AppSetting claude.min_score_fallback configurável
- **Given** `claude.min_score_fallback = "3"` configurado no banco
- **When** a API Claude falha e o fallback é acionado
- **Then** o score de fallback utilizado é 3 (lido do banco, não hardcoded)

### CA-9: DI registrado e API inicializa
- **Given** `ClaudeAiService` registrado no container e `claude.api_key` configurado
- **When** `docker compose up` e `GET localhost:5000/health`
- **Then** API inicia sem `InvalidOperationException` de DI; health check retorna 200

### CA-10: Testes unitários passam sem chamadas reais
- **Given** o projeto `AfiliadoBot.Tests` com mocks do `AnthropicClient`
- **When** `dotnet test` é executado (sem variáveis de ambiente reais da Claude API)
- **Then** 0 falhas; 0 chamadas reais à API Anthropic

## Riscos e Dependências

### Dependências
- Issue #2 concluída: entidades `Product`, enum `SocialNetwork`, interface `IAiService` e `AppSetting` presentes
- `Anthropic.SDK` NuGet disponível e compatível com .NET 8
- Variável de ambiente / `AppSetting` `claude.api_key` configurada no ambiente de execução
- `AppSetting` `claude.min_score_fallback` adicionado ao seed/migrations

### Riscos
| Risco | Probabilidade | Mitigação |
|---|---|---|
| Resposta do Claude fora do formato JSON esperado | Alta | Parse resiliente com regex/extração parcial + CA-3 |
| Rate limit da Anthropic API em picos de coleta | Média | Retry com backoff exponencial no `ClaudeAiService` (até 3 tentativas) |
| Inconsistência entre `claude.min_score` no banco e a lógica de `approve` | Baixa | Derivar `approve` sempre em C# (D2) — nunca confiar no booleano do JSON |
| Mock inadequado → testes passam mas produção falha (schema real do SDK diferente) | Baixa | Usar wrappers testáveis ou interfaces do próprio SDK |
