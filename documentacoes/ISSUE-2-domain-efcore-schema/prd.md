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
3. `AppDbContext` com configurações Fluent API (tabelas, constraints, índices, relacionamentos)
4. Migration inicial criando todas as tabelas com colunas exatas (incluindo `ai_score`, `ai_reason`, `slug`, `category`)
5. Tabela `push_subscriptions` com `endpoint UNIQUE`
6. Seeds de `app_settings` cobrindo 25+ campos (Amazon, MercadoLivre, Shopee, Telegram, YouTube, Instagram, TikTok, Claude API, schedule, networks)
7. Interfaces de contrato: `IPlatformCollector`, `ISocialPublisher`, `IMediaStorage`, `IAiService`
8. Testes unitários validando entidades e regras de validação

## Critérios de Aceite

### CA-1: Tabelas criadas pela migration
- **Given** o container `db` em execução e a migration aplicada via `dotnet ef database update`
- **When** consultar o schema do PostgreSQL (`\dt` ou `information_schema.tables`)
- **Then** as tabelas `products`, `publication_queues`, `app_settings`, `push_subscriptions` existem com todas as colunas especificadas

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
- **Then** resultado >= 25

### CA-5: Testes de domínio passam
- **Given** o projeto `AfiliadoBot.Tests` com testes unitários de entidades
- **When** `dotnet test` é executado
- **Then** 0 falhas; testes de domínio (criação de entidade, validações, estado de enums) todos verdes

### CA-6: Interfaces de contrato presentes
- **Given** o projeto `AfiliadoBot.Domain`
- **When** `dotnet build` no projeto Domain
- **Then** compila sem erros e os arquivos `IPlatformCollector.cs`, `ISocialPublisher.cs`, `IMediaStorage.cs`, `IAiService.cs` existem em `AfiliadoBot.Domain/Interfaces/`

### CA-7: AppDbContext registrado na API
- **Given** `AppDbContext` configurado com a string de conexão via variável de ambiente
- **When** `docker compose up` e `GET localhost:5000/health`
- **Then** API inicia sem exceção de EF Core e health check retorna 200

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
| Conflito de namespaces entre projetos Domain e Infrastructure | Baixa | Revisão de arquitetura no design.md pelo LT/Arquiteto |

## Perguntas de Clarificação ao Gerente

1. **Relacionamentos entre entidades:** `PublicationQueue` referencia `Product` por FK? Se sim, qual a cardinalidade (um produto pode ter N publicações na fila)? Isso afeta os índices e constraints da migration.

2. **Valores exatos dos seeds de `app_settings`:** Os 25+ campos devem ter valores reais (ex.: chaves de API de exemplo, intervalos de schedule) ou apenas chaves com valor em branco/placeholder? Valores reais no seed implicam rotação de segredos.

3. **Enum `Platform` vs `SocialNetwork`:** Qual a distinção de negócio? `Platform` seria a fonte de coleta (Amazon, MercadoLivre, Shopee) e `SocialNetwork` o destino de publicação (Telegram, Instagram, TikTok, YouTube)? Ou há sobreposição?

4. **Localização das interfaces:** As interfaces `IPlatformCollector`, `ISocialPublisher`, `IMediaStorage`, `IAiService` devem viver em `AfiliadoBot.Domain/Interfaces/` ou em `AfiliadoBot.Application/Interfaces/`? A distinção define se o Domain tem dependência de abstração de infraestrutura.

5. **Escopo dos testes unitários:** Os testes devem cobrir apenas construtores e validações de entidade (estado interno), ou também testar comportamentos como transição de `ProductStatus` e `PublicationStatus`? Isso define o volume de testes esperados para o CA-5.
