# PRD — ISSUE-2: Domain, EF Core e Schema de Banco

## Objetivo de Negócio
Criar a camada de domínio do AfiliadoBot: entidades C#, enums, interfaces de contratos, contexto EF Core com mapeamentos Fluent API, migration inicial e seeds de configuração. Esta entrega transforma a estrutura vazia da Issue #1 em um banco PostgreSQL com schema funcional, pronto para receber as implementações de collectors, publishers e o scheduler Hangfire.

## Usuários / Sistemas Afetados
- **Devs da squad** — trabalharão sobre este domínio em todas as issues seguintes
- **AfiliadoBot.Application / Infrastructure** — consomem as entidades e interfaces definidas aqui
- **Hangfire** — usa `AppDbContext` para persistir jobs e filas
- **PostgreSQL 16** (container `db`) — schema criado pelas migrations desta issue
- **`dotnet test`** — suite de testes unitários de domínio roda a partir desta entrega

## Funcionalidades Principais
1. Entidades de domínio: `Product`, `PublicationQueue`, `AppSetting`, `PushSubscription`
2. Enums de domínio: `Platform`, `SocialNetwork`, `ProductStatus`, `PublicationStatus`
3. `AppDbContext` com configurações Fluent API (tabelas, constraints, índices, relacionamentos, FK com ON DELETE CASCADE)
4. Migration inicial criando todas as tabelas com colunas exatas (incluindo `ai_score`, `ai_reason`, `slug`, `category`)
5. Tabela `push_subscriptions` com `endpoint UNIQUE`
6. Seeds de `app_settings` cobrindo 25+ campos (Amazon, MercadoLivre, Shopee, Telegram, YouTube, Instagram, TikTok, Claude API, schedule, networks) — credenciais com valor em branco, campos não-sensíveis com valores reais
7. Interfaces de contrato: `IPlatformCollector`, `ISocialPublisher`, `IMediaStorage`, `IAiService` em `AfiliadoBot.Domain/Interfaces/`
8. Testes unitários validando entidades, validações de estado e transições de status

## Decisões de Design

### D1 — Relacionamento Product → PublicationQueue
`PublicationQueue` referencia `Product` por FK (`product_id`). Cardinalidade: 1 produto → N entradas na fila, máximo 5 por rede social ativa. A migration deve criar a FK com `ON DELETE CASCADE`. Índice composto obrigatório em `publication_queue`: `(status, scheduled_at)` para otimizar as queries do PublisherJob.

### D2 — Seeds de `app_settings`
Apenas campos não-sensíveis recebem valores reais no seed:
- `schedule.collector_cron` = `0 6 * * *`
- `schedule.publisher_cron` = `0 9,12,15,18,20 * * *`
- `publish.max_per_day` = `10`
- `claude.min_score` = `6`
- `networks.*.enabled` = `true`

Credenciais (chaves de API, tokens, secrets) = valor em branco no seed. Valores reais são configurados via variáveis de ambiente em tempo de execução.

### D3 — Enums: Platform vs SocialNetwork
Dois enums distintos sem sobreposição:
- `Platform` — origem da coleta: `Amazon`, `MercadoLivre`, `Shopee`. Campo de `Product`.
- `SocialNetwork` — destino de publicação: `Telegram`, `Youtube`, `Instagram`, `TikTok`, `Facebook`. Campo de `PublicationQueue`.

### D4 — Localização das Interfaces de Domínio
As interfaces `IPlatformCollector`, `ISocialPublisher`, `IMediaStorage`, `IAiService` vivem em `AfiliadoBot.Domain/Interfaces/`. O Domain define os contratos; a Infrastructure os implementa. O projeto Domain não deve referenciar nenhum NuGet de infraestrutura (sem dependências externas além do .NET runtime).

### D5 — Escopo dos Testes Unitários
Os testes cobrem validações de estado e transições de status, não apenas construtores:
- **Product:** `SalePrice >= 0`, `DiscountPct` entre 0 e 100, `AffiliateLink` não nulo
- **ProductStatus:** `Pending → Rejected` (score < min_score), `Pending → Queued` (após processor aprovar)
- **PublicationStatus:** `Scheduled → Published` (sucesso), `Scheduled → Failed` (falha); `Failed` com `retry_count >= 3` não retenta
- **ClaudeAiService:** mock do `AnthropicClient`, deserialização do JSON de resposta, score abaixo do threshold resulta em `Approve = false`

## Critérios de Aceite

### CA-1: Tabelas criadas pela migration
- **Given** o container `db` em execução e a migration aplicada via `dotnet ef database update`
- **When** consultar o schema do PostgreSQL (`\dt` ou `information_schema.tables`)
- **Then** as tabelas `products`, `publication_queue`, `app_settings`, `push_subscriptions` existem com todas as colunas especificadas

### CA-2: Colunas especiais em `products`
- **Given** a tabela `products` criada pela migration
- **When** verificar as colunas da tabela
- **Then** existem `ai_score INT`, `ai_reason VARCHAR(300)`, `slug VARCHAR(300) UNIQUE`, `category VARCHAR(100)`

### CA-3: Constraint UNIQUE em `push_subscriptions`
- **Given** a tabela `push_subscriptions` criada
- **When** tentar inserir dois registros com o mesmo `endpoint`
- **Then** o banco rejeita com violação de constraint UNIQUE

### CA-4: Seeds de `app_settings`
- **Given** os seeds aplicados (via `HasData` ou script de seed)
- **When** executar `SELECT COUNT(*) FROM app_settings`
- **Then** resultado >= 25; campos não-sensíveis têm os valores reais definidos em D2; campos de credenciais têm valor em branco

### CA-5: Testes de domínio passam
- **Given** o projeto `AfiliadoBot.Tests` com testes unitários de entidades e transições de status
- **When** `dotnet test` é executado
- **Then** 0 falhas; testes cobrem: validações de `Product` (SalePrice, DiscountPct, AffiliateLink), transições de `ProductStatus` (Pending→Rejected, Pending→Queued), transições de `PublicationStatus` (Scheduled→Published/Failed, retry_count>=3 bloqueia retentativa), mock de `ClaudeAiService` com score abaixo do threshold

### CA-6: Interfaces de contrato presentes no Domain
- **Given** o projeto `AfiliadoBot.Domain`
- **When** `dotnet build` no projeto Domain
- **Then** compila sem erros; os arquivos `IPlatformCollector.cs`, `ISocialPublisher.cs`, `IMediaStorage.cs`, `IAiService.cs` existem em `AfiliadoBot.Domain/Interfaces/`; o projeto Domain não referencia NuGet de infraestrutura

### CA-7: AppDbContext registrado na API
- **Given** `AppDbContext` configurado com a string de conexão via variável de ambiente
- **When** `docker compose up` e `GET localhost:5000/health`
- **Then** API inicia sem exceção de EF Core e health check retorna 200

### CA-8: FK e índice em `publication_queue`
- **Given** a migration aplicada
- **When** consultar `information_schema.table_constraints` e `pg_indexes`
- **Then** existe FK `product_id → products.id` com `ON DELETE CASCADE`; existe índice composto em `(status, scheduled_at)`

### CA-9: Enums distintos sem sobreposição
- **Given** o código compilado
- **When** inspecionar os enums em `AfiliadoBot.Domain/Enums/`
- **Then** `Platform` contém exatamente `Amazon`, `MercadoLivre`, `Shopee`; `SocialNetwork` contém exatamente `Telegram`, `Youtube`, `Instagram`, `TikTok`, `Facebook`; sem sobreposição de valores

## Riscos e Dependências

### Dependências
- Issue #1 concluída (estrutura de projetos e `docker-compose.yml` funcionais)
- Container `db` PostgreSQL 16 operacional

### Riscos
| Risco | Probabilidade | Mitigação |
|---|---|---|
| Drift entre nomes de colunas C# e SQL gerado pelo EF | Média | Configurar nomes explicitamente via Fluent API (`HasColumnName`) |
| Seeds de `app_settings` incompletos (< 25) | Baixa | Definir lista completa antes do código; validar com CA-4 |
| Migration não aplicável ao schema do container (volume sujo) | Baixa | Documentar `dotnet ef database drop --force` antes de recriar |
| Conflito de namespaces entre projetos Domain e Infrastructure | Baixa | Revisão de arquitetura no design.md pelo LT |
