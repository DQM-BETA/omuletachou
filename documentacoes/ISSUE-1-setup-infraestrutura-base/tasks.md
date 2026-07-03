# Tasks — ISSUE-1: Setup do Projeto e Infraestrutura Base

## Visao geral
3 sub-issues independentes por stack. Sub-A deve ser implementada primeiro (cria o docker-compose base e o servico db+api); Sub-B e Sub-C podem rodar em paralelo apos Sub-A.

---

## T-01 — Sub-A: Backend .NET 8 — Scaffolding e Docker
**Stack:** dotnet
**Label:** stack:dotnet

### O que fazer
- Criar solution `AfiliadoBot.sln` em `backend/` com 5 projetos: `AfiliadoBot.Api`, `AfiliadoBot.Application`, `AfiliadoBot.Domain`, `AfiliadoBot.Infrastructure`, `AfiliadoBot.Tests`
- `Program.cs` com endpoint `GET /health` retornando `{ status: "healthy", timestamp }` HTTP 200
- `appsettings.json` com `ConnectionStrings__Default` placeholder
- `backend/src/AfiliadoBot.Api/Dockerfile` multi-stage: `sdk:8.0` para build, `aspnet:8.0` para runtime
- `docker-compose.yml` na raiz com servicos `db` (postgres:16-alpine, porta 5432, volume postgres_data) e `api` (porta 5000:8080, volume media_files:/app/media)
- `.env.example` na raiz com `DB_USER=afiliado` e `DB_PASSWORD=TROQUE_POR_SENHA_FORTE`

### Criterios de aceite
- Given `docker compose build` When executado Then containers `db` e `api` buildam sem erro
- Given `docker compose up -d` When `GET localhost:5000/health` Then retorna HTTP 200
- Given `dotnet build` na solution Then compila os 5 projetos sem erro
- Given `dotnet test` Then suite roda sem falha (zero ou stub tests aceitos)

### Contexto tecnico
- Docs: `documentacoes/ISSUE-1-setup-infraestrutura-base/especificacao-tecnica.md`
- Estrutura de pastas: conforme `## Estrutura de pastas a criar` na espec
- Dependencias NuGet minimas: `Microsoft.AspNetCore.OpenApi`, `Swashbuckle.AspNetCore`
- Imagens .NET sao multi-arch (amd64+arm64) sem flags extras
- Branch: `feature/ISSUE-NNN-backend-dotnet-scaffolding` (NNN = numero desta sub-issue)
- Sub-issue GitHub: (ver abaixo)

---

## T-02 — Sub-B: Dashboard Angular 17 — Scaffolding e Docker
**Stack:** angular
**Label:** stack:angular
**Prerequisito:** Sub-A concluida (docker-compose base com db+api deve existir)

### O que fazer
- Scaffold Angular 17 em `dashboard/` com routing habilitado
- 5 componentes/rotas stub: `/products`, `/queue`, `/facebook-manual`, `/settings`, `/reports`
- `dashboard/Dockerfile` multi-stage: `node:20-alpine` para build (`npm ci && npm run build`), `nginx:alpine` para runtime com arquivos do dist
- `dashboard/nginx.conf` com `try_files` para SPA e `proxy_pass /api/` para `http://api:8080/api/`
- Adicionar servico `dashboard` ao `docker-compose.yml` (porta 4200:80, `depends_on: api`)

### Criterios de aceite
- Given `docker compose build` When executado Then container `dashboard` builda sem erro
- Given `docker compose up -d` When `GET localhost:4200` Then retorna 200 com HTML Angular
- Given navegar para `/products`, `/queue`, `/facebook-manual`, `/settings`, `/reports` Then cada rota renderiza sem erro 404
- Given `GET localhost:4200/api/health` Then proxy nginx redireciona para api e retorna 200
- Given `npm run build --configuration=production` Then compila sem erros TypeScript

### Contexto tecnico
- Docs: `documentacoes/ISSUE-1-setup-infraestrutura-base/especificacao-tecnica.md`
- nginx.conf template na espec (secao `## nginx.conf (dashboard)`)
- Branch: `feature/ISSUE-NNN-dashboard-angular-scaffolding` (NNN = numero desta sub-issue)
- Sub-issue GitHub: (ver abaixo)

---

## T-03 — Sub-C: Site Next.js 14 — Scaffolding e Docker
**Stack:** nodejs
**Label:** stack:nodejs
**Prerequisito:** Sub-A concluida (docker-compose base com db+api deve existir)

### O que fazer
- Scaffold Next.js 14 com TypeScript e App Router em `website/`
- Pagina stub `/` (`app/page.tsx`): componente React com titulo "O Mulet Achou — Em breve"
- Pagina stub `/oferta/[slug]/page.tsx`: exibe o slug recebido via `params`
- Pagina stub `/categoria/[categoria]/page.tsx`: exibe a categoria recebida via `params`
- `website/Dockerfile`: `node:20-alpine`, `npm ci`, `npm run build`, `CMD ["npm", "start"]`
- Adicionar servico `website` ao `docker-compose.yml` (porta 3000:3000, env `NEXT_PUBLIC_API_URL`)

### Criterios de aceite
- Given `docker compose build` When executado Then container `website` builda sem erro
- Given `docker compose up -d` When `GET localhost:3000` Then retorna 200 com HTML renderizado (SSR)
- Given `GET localhost:3000/oferta/teste` Then retorna 200 (nao 404)
- Given `GET localhost:3000/categoria/eletronicos` Then retorna 200 (nao 404)
- Given `npm run build` Then compila sem erros TypeScript

### Contexto tecnico
- Docs: `documentacoes/ISSUE-1-setup-infraestrutura-base/especificacao-tecnica.md`
- Usar App Router (nao Pages Router)
- Branch: `feature/ISSUE-NNN-website-nextjs-scaffolding` (NNN = numero desta sub-issue)
- Sub-issue GitHub: (ver abaixo)

---

## Numeros das sub-issues (preencher apos criacao)
- T-01 (dotnet): #16
- T-02 (angular): #17
- T-03 (nodejs): #18
