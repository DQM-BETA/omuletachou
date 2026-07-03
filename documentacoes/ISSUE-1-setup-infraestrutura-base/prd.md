# PRD — ISSUE-1: Setup do Projeto e Infraestrutura Base

## Objetivo
Criar a estrutura inicial do repositório omuletachou: solution .NET 8, projetos Angular 17 (dashboard admin) e Next.js 14 (site público), e Docker Compose com 4 containers funcionais. Esta issue é a fundação técnica sobre a qual todas as demais features serão construídas. Não há lógica de negócio nesta entrega.

## Escopo
- Solution `AfiliadoBot.sln` com 5 projetos .NET: `AfiliadoBot.Api`, `AfiliadoBot.Application`, `AfiliadoBot.Domain`, `AfiliadoBot.Infrastructure`, `AfiliadoBot.Tests`
- Scaffold Angular 17 (dashboard admin) com rotas stub: `/products`, `/queue`, `/facebook-manual`, `/settings`, `/reports`
- Scaffold Next.js 14 (site público) com páginas stub: `/`, `/oferta/[slug]`, `/categoria/[categoria]`
- `docker-compose.yml` com 4 containers: `db` (PostgreSQL 16-alpine), `api`, `dashboard`, `website`
- Dockerfiles para cada serviço (`backend/src/AfiliadoBot.Api/Dockerfile`, `dashboard/Dockerfile`, `website/Dockerfile`)
- `dashboard/nginx.conf` com SPA routing e proxy `/api/` → `api:8080`
- `.env.example` com variáveis `DB_USER` e `DB_PASSWORD`

## Usuários / Consumidores
- Desenvolvedores da squad que vão trabalhar na feature branch
- Pipeline de CI/CD que roda `docker compose build`
- Servidor Oracle Cloud A1 (ARM Ampere) onde o compose será deployado

## Critérios de Aceite

### CA-1: Build completo sem erros
- **Given** o repositório clonado
- **When** `docker compose build` é executado
- **Then** todos os 4 containers (`db`, `api`, `dashboard`, `website`) buildam sem erro

### CA-2: Dashboard Angular acessível
- **Given** `docker compose up -d` executado com sucesso
- **When** acessar `localhost:4200`
- **Then** Angular exibe página inicial (stub) sem erro 500

### CA-3: Site Next.js acessível
- **Given** `docker compose up -d` executado com sucesso
- **When** acessar `localhost:3000`
- **Then** Next.js exibe página inicial (stub) sem erro 500

### CA-4: API com health check
- **Given** `docker compose up -d` executado com sucesso
- **When** acessar `localhost:5000/health`
- **Then** API retorna HTTP 200

### CA-5: Compatibilidade ARM64
- **Given** a máquina host é ARM (Oracle Cloud A1, linux/arm64)
- **When** `docker compose build` é executado nessa plataforma
- **Then** todos os containers buildam sem erro (imagens multi-arch ou nativas arm64)

## Fora de Escopo
- Lógica de negócio: collectors, publishers, AI service
- Autenticação e autorização
- Banco de dados populado ou migrations
- Integrações com plataformas externas (Facebook, Shopee, etc.)
- Configuração de ambiente de produção ou SSL
- Testes automatizados de aplicação (a não ser o health check)

## Riscos e Dependências

### Riscos
| Risco | Probabilidade | Mitigação |
|---|---|---|
| Imagens Docker não compatíveis com ARM64 | Média | Usar imagens multi-arch (`--platform linux/amd64,linux/arm64`) ou fixar tags arm64 |
| Versões desalinhadas entre projetos | Baixa | Fixar versões: .NET 8 LTS, Angular 17, Next.js 14, PostgreSQL 16-alpine |
| Conflito de portas no ambiente local | Baixa | Documentar mapeamento de portas no README |

### Dependências
- Nenhuma dependência externa (esta é a issue fundação)
- Repositório `DQM-BETA/omuletachou` já criado com branches `desenv`, `homolog`, `main` e proteções configuradas

## Definição de Pronto
- [ ] `docker compose build` passa sem erros em amd64 e arm64
- [ ] Os 4 endpoints (4200, 3000, 5000/health) respondem após `docker compose up -d`
- [ ] PR feature → desenv criado e aprovado (squash merge)
- [ ] Nenhum segredo hardcoded (apenas `.env.example` com placeholders)
