# Especificacao Tecnica — ISSUE-1: Setup do Projeto e Infraestrutura Base

## Estrutura de pastas a criar

```
/
├── docker-compose.yml
├── .env.example                    ← DB_USER e DB_PASSWORD
├── backend/
│   ├── AfiliadoBot.sln
│   └── src/
│       ├── AfiliadoBot.Api/
│       │   ├── AfiliadoBot.Api.csproj
│       │   ├── Program.cs          ← health check + placeholder Hangfire
│       │   ├── appsettings.json
│       │   └── Dockerfile
│       ├── AfiliadoBot.Application/
│       │   └── AfiliadoBot.Application.csproj
│       ├── AfiliadoBot.Domain/
│       │   └── AfiliadoBot.Domain.csproj
│       ├── AfiliadoBot.Infrastructure/
│       │   └── AfiliadoBot.Infrastructure.csproj
│       └── AfiliadoBot.Tests/
│           └── AfiliadoBot.Tests.csproj
├── dashboard/
│   ├── Dockerfile
│   ├── nginx.conf
│   └── src/app/pages/             ← stubs: products, queue, facebook-manual, settings, reports
└── website/
    ├── Dockerfile
    └── src/app/                   ← stubs: page.tsx, oferta/[slug]/page.tsx, categoria/[categoria]/page.tsx
```

## docker-compose.yml — servicos

| Servico | Imagem base | Porta | Notas |
|---|---|---|---|
| db | postgres:16-alpine | 5432 | volume postgres_data |
| api | build: ./backend | 5000:8080 | volume media_files em /app/media |
| dashboard | build: ./dashboard | 4200:80 | nginx com proxy /api/ |
| website | build: ./website | 3000:3000 | NEXT_PUBLIC_API_URL |

## Dockerfiles

### backend/src/AfiliadoBot.Api/Dockerfile
- Stage build: `mcr.microsoft.com/dotnet/sdk:8.0`
- Stage runtime: `mcr.microsoft.com/dotnet/aspnet:8.0`
- Multi-arch (amd64 + arm64 — sem flags especiais, as imagens oficiais .NET ja sao multi-arch)

### dashboard/Dockerfile
- Stage build: `node:20-alpine` → `npm ci` → `npm run build`
- Stage runtime: `nginx:alpine` com arquivos do dist e nginx.conf

### website/Dockerfile
- `node:20-alpine`
- `npm ci && npm run build`
- `CMD ["npm", "start"]`

## nginx.conf (dashboard)

```nginx
server {
    listen 80;
    root /usr/share/nginx/html;
    index index.html;
    location / { try_files $uri $uri/ /index.html; }
    location /api/ { proxy_pass http://api:8080/api/; }
}
```

## Program.cs — endpoints minimos

```csharp
app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }));
// Hangfire dashboard placeholder — implementado na Issue #7
```

## Dependencias NuGet (AfiliadoBot.Api — apenas para compilar)
- Microsoft.AspNetCore.OpenApi
- Swashbuckle.AspNetCore

## .env.example

```
DB_USER=afiliado
DB_PASSWORD=TROQUE_POR_SENHA_FORTE
```
