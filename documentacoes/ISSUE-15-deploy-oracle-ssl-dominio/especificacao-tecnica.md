# Especificação técnica — ISSUE-15 Deploy Oracle Cloud + SSL + Domínio

> Baseado nas decisões do Arquiteto em `openspec/changes/issue-15-deploy-oracle-ssl-dominio/design.md`.
> Este documento detalha os artefatos exatos a implementar: `docker-compose.yml`, `deploy.sh`,
> `.env.example`. O runbook operacional (passos manuais do Gerente na VM) está em
> `runbook-deploy.md`, neste mesmo diretório.

## 1. `docker-compose.yml` consolidado (raiz do monorepo)

Estrutura alvo — substitui o `docker-compose.yml` atual (que publica `5432/5000/3000/4200`
diretamente ao host):

```yaml
services:
  db:
    image: postgres:16-alpine
    container_name: afiliado_db
    restart: unless-stopped
    environment:
      POSTGRES_DB: afiliadoBot
      POSTGRES_USER: ${DB_USER}
      POSTGRES_PASSWORD: ${DB_PASSWORD}
    volumes:
      - postgres_data:/var/lib/postgresql/data
    networks:
      - omuletachou_net
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U ${DB_USER} -d afiliadoBot"]
      interval: 10s
      timeout: 5s
      retries: 5
    # SEM "ports:" — só alcançável via rede Docker interna (a api conecta por "Host=db")

  api:
    build:
      context: ./backend
      dockerfile: src/AfiliadoBot.Api/Dockerfile
    container_name: afiliado_api
    restart: unless-stopped
    depends_on:
      db:
        condition: service_healthy
    environment:
      ASPNETCORE_ENVIRONMENT: Production
      ASPNETCORE_URLS: http://+:8080
      ConnectionStrings__DefaultConnection: "Host=db;Port=5432;Database=afiliadoBot;Username=${DB_USER};Password=${DB_PASSWORD}"
      Jwt__SigningKey: "${JWT_SIGNING_KEY}"
      Seed__UserEmail: "${SEED_USER_EMAIL:-}"
      Seed__UserPassword: "${SEED_USER_PASSWORD:-}"
    volumes:
      - media_files:/app/media
    networks:
      - omuletachou_net
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:8080/health"]
      interval: 10s
      timeout: 5s
      retries: 5
      start_period: 20s
    # SEM "ports:" — NPM alcança em api:8080 via rede interna

  website:
    build:
      context: ./website
    container_name: afiliado_website
    restart: unless-stopped
    depends_on:
      - api
    environment:
      # Client-side (bundle do browser) — precisa de host público real, não mais localhost:5000
      NEXT_PUBLIC_API_URL: ${API_PUBLIC_URL}
      # Server-side (Server Components/SSR) — já existente, resolve via rede Docker interna
      API_INTERNAL_URL: http://api:8080
    networks:
      - omuletachou_net
    # SEM "ports:" — NPM alcança em website:3000

  dashboard:
    build:
      context: ./dashboard
    container_name: afiliado_dashboard
    restart: unless-stopped
    depends_on:
      - api
    networks:
      - omuletachou_net
    # SEM "ports:" — NPM alcança em dashboard:80
    # nginx.conf embutido na imagem já faz proxy_pass /api/ -> http://api:8080/api/

  nginx-proxy-manager:
    image: jc21/nginx-proxy-manager:latest
    container_name: afiliado_npm
    restart: unless-stopped
    ports:
      - "80:80"
      - "443:443"
      - "81:81"   # TEMPORÁRIO — comentar/remover após o setup inicial (ver runbook-deploy.md §7)
    volumes:
      - npm_data:/data
      - npm_letsencrypt:/etc/letsencrypt
    networks:
      - omuletachou_net

networks:
  omuletachou_net:
    driver: bridge

volumes:
  postgres_data:
  media_files:
  npm_data:
  npm_letsencrypt:
```

### Notas de implementação (para o Dev)

- **`curl` na imagem do `api`**: o healthcheck usa `curl -f http://localhost:8080/health`.
  A imagem base `mcr.microsoft.com/dotnet/aspnet:8.0` pode ou não trazer `curl` instalado
  (varia por tag/atualização da Microsoft). **Antes de finalizar**, o Dev deve validar com
  `docker compose exec api curl --version`. Se ausente, adicionar ao `Dockerfile` do backend
  (estágio final, antes do `ENTRYPOINT`):
  ```dockerfile
  RUN apt-get update && apt-get install -y --no-install-recommends curl \
      && rm -rf /var/lib/apt/lists/*
  ```
  Isso é uma exceção mínima e justificada à decisão do Arquiteto de "nenhuma mudança nos
  Dockerfiles" (design.md, decisão 1) — é pré-requisito técnico do próprio healthcheck que a
  decisão especificou, não uma mudança de escopo. Se preferir evitar a instalação, alternativa
  aceitável: `test: ["CMD-SHELL", "wget --no-verbose --tries=1 --spider http://localhost:8080/health || exit 1"]`
  se `wget` já estiver presente na imagem (validar da mesma forma). Escolher uma das duas e
  documentar no PR qual foi usada.
- `dashboard`/`website` não recebem `condition: service_healthy` porque não expõem
  healthcheck próprio nesta issue (decisão do Arquiteto — não é bloqueio para o deploy).
- Container names (`afiliado_*`) mantidos como já estão — não fazem parte do escopo de
  mudança.
- `docker-compose.yml` atual deve ser **substituído inteiramente** por esta estrutura, não
  incrementado por cima (evita sobra de `ports:` esquecida).

## 2. `deploy.sh` (raiz do monorepo, novo arquivo)

Script idempotente, sem credenciais hardcoded (lê tudo do `.env` já presente na VM via
`docker compose`, que carrega `.env` automaticamente do diretório corrente):

```bash
#!/usr/bin/env bash
# deploy.sh — atualiza o código e sobe/atualiza os containers em produção.
# Uso: ./deploy.sh   (executar a partir da raiz do repo, com .env já preenchido)
set -euo pipefail

cd "$(dirname "$0")"

if [ ! -f .env ]; then
  echo "ERRO: .env não encontrado. Copie .env.example para .env e preencha os valores antes de continuar." >&2
  exit 1
fi

echo "==> git pull (--ff-only: nunca sobrescreve histórico local)"
git pull --ff-only

echo "==> docker compose up -d --build"
docker compose up -d --build

echo "==> status dos containers"
docker compose ps

echo "==> deploy concluído"
```

- `chmod +x deploy.sh` deve ser aplicado no PR (permissão executável versionada pelo git).
- `set -euo pipefail` garante que qualquer falha (git pull, build, etc.) interrompe o script
  com código de saída != 0, sem seguir silenciosamente.
- Idempotente: reexecutar não tem efeito destrutivo — `docker compose up -d --build` apenas
  recria containers cujo build/config mudou; volumes (`postgres_data`, `media_files`,
  `npm_data`, `npm_letsencrypt`) nunca são tocados por este comando.
- Falha explícita e cedo se `.env` não existir (evita `docker compose up` falhando de forma
  confusa no meio do processo).

## 3. `.env.example` (raiz do monorepo — atualizar o existente)

```bash
# --- Infraestrutura (segredos — NUNCA commitar valores reais) ---
DB_USER=afiliado
DB_PASSWORD=TROQUE_POR_SENHA_FORTE

# Issue #11 / Sub-A — Autenticação (JWT)
# Chave de assinatura HS256, >=256 bits. Gerar com: openssl rand -base64 32
# NUNCA versionar o valor real; sem esta variável a API falha no startup (fail-fast).
JWT_SIGNING_KEY=TROQUE_POR_CHAVE_ALEATORIA_256_BITS

# Seed do usuário operador único (só roda se a tabela "users" estiver vazia).
# Deixe em branco para pular o seed (login retorna 401 até configurar manualmente).
SEED_USER_EMAIL=
SEED_USER_PASSWORD=

# --- Domínio e URLs públicas (ISSUE-15 — novo) ---
# Domínio raiz registrado em registro.br, usado apenas como referência/documentação
# (não é lido diretamente por nenhum serviço; os 3 registros A abaixo dependem dele).
DOMAIN_ROOT=omuletachou.com.br

# URL pública do site (Next.js) — subdomínio raiz + www (configurados no NPM, mesmo destino)
WEBSITE_PUBLIC_URL=https://omuletachou.com.br

# URL pública do dashboard (Angular)
DASHBOARD_PUBLIC_URL=https://dashboard.omuletachou.com.br

# URL pública da API — consumida pelo "website" como NEXT_PUBLIC_API_URL (client-side,
# exposta ao bundle do browser; substitui o antigo "http://localhost:5000" hardcoded)
API_PUBLIC_URL=https://api.omuletachou.com.br
```

Nomenclatura consistente com `design.md` decisão 3: variáveis de infraestrutura já existentes
mantêm o nome sem prefixo (evita diff em código estável); variáveis novas de URL pública usam
sufixo `_PUBLIC_URL`.

## 4. Fora do escopo dos artefatos de código (cobertos no runbook)

- Criação da VM Oracle Cloud, instalação de Docker/Docker Compose na VM.
- Registro do domínio e criação dos 3 registros DNS tipo A.
- Configuração dos Proxy Hosts no Nginx Proxy Manager via UI (`admin@example.com` /
  `changeme` no primeiro acesso) e emissão dos certificados Let's Encrypt.
- Security List da VM Oracle (portas 22/80/443 permanentes, 81 temporária).
- Preenchimento de segredos de integrações externas via dashboard (`app_settings`).

Ver `runbook-deploy.md` para o passo a passo completo desses itens.

## 5. Validação local (sem VM real) — o que o Dev deve rodar antes de abrir o PR

```bash
cp .env.example .env   # preencher DB_USER/DB_PASSWORD/JWT_SIGNING_KEY com valores de teste
docker compose up -d --build
docker compose ps      # todos os serviços "Up" (api/db "healthy")
docker compose ps --format json | grep -i port   # confirmar que só nginx-proxy-manager publica porta
curl -f http://localhost/                         # NPM responde na 80 (mesmo sem proxy host configurado, default page do NPM)
docker compose exec api curl -f http://localhost:8080/health   # 200 dentro da rede interna
docker compose down    # não usar "-v" (preservaria o hábito de nunca derrubar volumes sem necessidade)
```

Isso cobre os critérios de aceite de "compose consolidado" e "SSL e proxy" (rede interna) que
são testáveis sem a VM — os critérios de emissão real de certificado Let's Encrypt e Security
List ficam documentados no runbook como validação manual do Gerente.
