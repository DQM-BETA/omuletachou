# PRD — ISSUE-3: Claude AI Service (Scoring + Geracao de Legendas)

## Objetivo de Negócio
Implementar o serviço de IA responsável por duas funções centrais do AfiliadoBot: (1) avaliar automaticamente se um produto coletado de plataformas de afiliados (Amazon, MercadoLivre, Shopee) é relevante o suficiente para publicação, atribuindo um score de 0 a 10; e (2) gerar legendas personalizadas para cada rede social ativa, com o tom e estilo de "O Mulet". Isso reduz intervenção manual e garante consistência de voz de marca nas publicações.

## Usuários / Sistemas Afetados
- **ProcessorJob (Hangfire)** — consome `IAiService` para avaliar produtos e decidir se entram na fila de publicação
- **PublisherJob (Hangfire)** — consome `IAiService` para gerar a legenda antes de publicar em cada rede social
- **AfiliadoBot.Infrastructure** — projeto que implementa `ClaudeAiService`
- **AfiliadoBot.Domain** — define o contrato `IAiService` (já existente em `Interfaces/IAiService.cs`)
- **PostgreSQL** — armazena `ai_score` e `ai_reason` na tabela `products`; a legenda gerada vai junto ao registro de publicação
- **Anthropic Claude API** — serviço externo consumido via `Anthropic.SDK` com o modelo `claude-haiku-4-5-20251001`

## Funcionalidades Principais

### F1 — Avaliação de Produto (Scoring)
- Enviar ao Claude um prompt com os dados do produto (título, preço, desconto, plataforma, categoria) e critérios de avaliação
- Receber resposta JSON com campos `score` (inteiro 0–10), `reason` (string) e `approve` (bool derivado do threshold `claude.min_score`)
- Parse resiliente: extrair o JSON mesmo que a resposta contenha texto adicional antes/depois do bloco JSON
- Retornar `ProductScore` com os três campos para o caller

### F2 — Geração de Legenda por Rede Social
- Enviar ao Claude um prompt com os dados do produto e o estilo do "Mulet" mais as orientações específicas da rede social alvo
- Suportar 5 estilos: Telegram (texto direto + link), Instagram (emojis + hashtags), TikTok (chamada de ação curta), YouTube (descricao + CTA), Facebook (texto amigável)
- Retornar a legenda como string pronta para publicação

### F3 — Configuração e DI
- Ler `claude.api_key` e `claude.min_score` (default: 6) de `AppSetting` (banco) via `ISettingsRepository` ou similar
- Registrar `ClaudeAiService` como implementação de `IAiService` no container DI de `AfiliadoBot.Infrastructure`

### F4 — Testes com Mock
- Testes unitários usando mock do `AnthropicClient` (sem chamadas reais ao Claude)
- Cobertura: resposta JSON válida, resposta com texto extra (parse resiliente), score abaixo do threshold (`approve = false`), geração de legenda por cada rede

## Critérios de Aceite

### CA-1: Score retornado dentro do range
- **Given** um produto com dados válidos (título, preço, desconto, link)
- **When** `IAiService.EvaluateProductAsync` (ou `ScoreProductAsync`) é chamado
- **Then** o `Score` retornado é um inteiro entre 0 e 10 (inclusive); `Reason` é uma string não vazia

### CA-2: Approve derivado do threshold configurável
- **Given** `claude.min_score` = 6 e um produto que recebe score 5 do Claude
- **When** `EvaluateProductAsync` é chamado
- **Then** `Approve = false`; dado score 7, `Approve = true`

### CA-3: Parse resiliente do JSON de scoring
- **Given** o mock do `AnthropicClient` retorna texto no formato `"Claro! Aqui está: { \"score\": 8, \"reason\": \"ótimo desconto\", \"approve\": true }"`
- **When** o `ClaudeAiService` processa a resposta
- **Then** extrai corretamente `Score = 8`, `Reason = "ótimo desconto"`, `Approve = true` sem lançar exceção

### CA-4: Legenda para Instagram contém emojis e hashtags
- **Given** um produto da plataforma MercadoLivre e a rede alvo `Instagram`
- **When** o método de geração de legenda é chamado
- **Then** a legenda retornada contém pelo menos um emoji e pelo menos uma hashtag (inspecionado via mock que retorna legenda de exemplo)

### CA-5: Suporte às 5 redes sociais sem exceção
- **Given** os 5 valores do enum `SocialNetwork` (Telegram, Instagram, TikTok, Youtube, Facebook)
- **When** o método de geração de legenda é chamado para cada um
- **Then** nenhuma chamada lança `NotImplementedException` ou `ArgumentOutOfRangeException`; todas retornam string não vazia (via mock)

### CA-6: DI registrado e API inicializa
- **Given** `ClaudeAiService` registrado no container e `claude.api_key` configurado
- **When** `docker compose up` e `GET localhost:5000/health`
- **Then** API inicia sem `InvalidOperationException` de DI; health check retorna 200

### CA-7: Testes unitários passam sem chamadas reais
- **Given** o projeto `AfiliadoBot.Tests` com mocks do `AnthropicClient`
- **When** `dotnet test` é executado (sem variáveis de ambiente reais da Claude API)
- **Then** 0 falhas; 0 chamadas reais à API Anthropic

## Perguntas de Clarificação ao Gerente (Gate 1)

### P1 — Contrato da interface IAiService
O contrato definido na Issue #2 e já commitado em `IAiService.cs` expõe um único método `EvaluateProductAsync` que retorna `(bool Approve, int Score, string Reason, string Caption)` — scoring e legenda numa única chamada. A Issue #3 descreve dois métodos separados: `ScoreProductAsync` e `GenerateCaptionAsync`.

**Qual prevalece?** (a) manter o método único `EvaluateProductAsync` (uma chamada ao Claude gera score + legenda ao mesmo tempo); ou (b) refatorar a interface para dois métodos separados (o ProcessorJob chama só o scoring; o PublisherJob chama só a geração de legenda, reduzindo custo de tokens)?

### P2 — Prompt e campo `Caption` no fluxo de avaliação
Se prevalecer o método único (`EvaluateProductAsync`), a legenda é gerada durante a avaliação — antes de saber se o produto será aprovado. Isso significa que produtos reprovados também geram legenda (desperdício de tokens).

**Isso é aceitável** ou o Gerente prefere a separação de responsabilidades (P1-b)?

### P3 — Persona e tom do "Mulet" para o prompt
O prompt de geração de legenda precisa instruir o Claude sobre o estilo de comunicação ("tom do Mulet"). Onde estão descritas as regras de tom (formal/informal, gírias, nível de entusiasmo, comprimento máximo por rede)? Existe um documento ou essa definição será criada agora?

### P4 — Critérios de scoring no prompt
Quais critérios o Claude deve usar para atribuir o score de 0 a 10? Exemplos: desconto mínimo, categorias preferidas, popularidade da plataforma, qualidade do título. Existe uma lista de critérios já definida ou o Dev tem liberdade para criar o prompt de scoring?

### P5 — Fallback de falha na API Claude
Se a chamada ao Claude falhar (timeout, rate limit, erro 5xx), o produto deve: (a) ser marcado como `Rejected` automaticamente; (b) ser reenfileirado para retry pelo Hangfire; ou (c) ter um score padrão configurável aplicado? O mesmo para geração de legenda: falha cancela a publicação ou usa legenda de fallback?

## Riscos e Dependências

### Dependências
- Issue #2 concluída: entidades `Product`, enum `SocialNetwork`, interface `IAiService` e `AppSetting` presentes
- `Anthropic.SDK` NuGet disponível e compatível com .NET 8
- Variável de ambiente / `AppSetting` `claude.api_key` configurada no ambiente de execução

### Riscos
| Risco | Probabilidade | Mitigação |
|---|---|---|
| Resposta do Claude fora do formato JSON esperado | Alta | Parse resiliente com regex/extração parcial + CA-3 |
| Custo de tokens maior que estimado se método único gera legenda + score juntos para todos os produtos | Média | Definir threshold de volume; separar chamadas (ver P1) |
| Rate limit da Anthropic API em picos de coleta | Média | Adicionar retry com backoff exponencial no `ClaudeAiService` |
| Inconsistência entre `claude.min_score` no banco e a lógica de `approve` no retorno da IA | Baixa | Derivar `approve` sempre no C# (nunca confiar no booleano vindo do JSON da IA) |
| Mock inadequado → testes passam mas produção falha (schema real do SDK diferente) | Baixa | Usar wrappers testáveis ou interfaces do próprio SDK |
