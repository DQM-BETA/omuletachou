# Tasks — ISSUE-15 Deploy Oracle Cloud + SSL + Domínio

> Decisão de fatiamento (ver seção "Decisão sobre fatiamento" no fim): **uma única
> sub-issue**. Trabalho coeso de infraestrutura sobre 3 artefatos pequenos e interdependentes
> (o `docker-compose.yml` referencia as mesmas variáveis do `.env.example`; o `deploy.sh`
> depende do compose já estar correto) — dividir geraria PRs que não sobem sozinhos.

## Sub-A: Artefatos de deploy — compose, script, .env, validação local

**Contexto técnico:**
- Design: `openspec/changes/issue-15-deploy-oracle-ssl-dominio/design.md`
- Especificação exata dos arquivos: `documentacoes/ISSUE-15-deploy-oracle-ssl-dominio/especificacao-tecnica.md`
- Runbook (já escrito, não faz parte desta sub-issue — apenas referenciar/validar consistência): `documentacoes/ISSUE-15-deploy-oracle-ssl-dominio/runbook-deploy.md`
- Stack: infra (Docker Compose + bash) — repo `omuletachou`, arquivos na raiz do monorepo

### O que fazer
1. Substituir `docker-compose.yml` pela estrutura descrita em `especificacao-tecnica.md` §1:
   remover `ports:` de `db`/`api`/`website`/`dashboard`; adicionar rede `omuletachou_net`;
   adicionar serviço `nginx-proxy-manager` com volumes `npm_data`/`npm_letsencrypt`; adicionar
   `healthcheck` em `db` (`pg_isready`) e `api` (`curl -f http://localhost:8080/health`);
   `depends_on: condition: service_healthy` do `api` em relação ao `db`; `website` recebe
   `NEXT_PUBLIC_API_URL: ${API_PUBLIC_URL}` no lugar do `http://localhost:5000` hardcoded.
2. Validar disponibilidade de `curl` na imagem `mcr.microsoft.com/dotnet/aspnet:8.0` usada
   pelo `api` (`docker compose exec api curl --version`). Se ausente, aplicar a exceção
   documentada em `especificacao-tecnica.md` §1 (instalar `curl` via `apt-get` no Dockerfile,
   ou usar `wget` como alternativa) e registrar no PR qual opção foi usada.
3. Criar `deploy.sh` na raiz conforme `especificacao-tecnica.md` §2 (idempotente, sem
   credenciais hardcoded, `set -euo pipefail`, falha cedo se `.env` ausente). Marcar
   executável (`chmod +x deploy.sh`) e versionar a permissão no commit.
4. Atualizar `.env.example` conforme `especificacao-tecnica.md` §3 (adicionar `DOMAIN_ROOT`,
   `WEBSITE_PUBLIC_URL`, `DASHBOARD_PUBLIC_URL`, `API_PUBLIC_URL`, mantendo as variáveis
   existentes sem prefixo).
5. Validar localmente conforme `especificacao-tecnica.md` §5 (`docker compose up -d --build`,
   `docker compose ps`, healthchecks `healthy`, nenhuma porta de app publicada, NPM responde
   na 80, `curl` interno ao `/health` da api retorna 200). Capturar evidência (output do
   `docker compose ps`) para o PR.
6. Conferir que `.gitignore` já lista `.env` (não deve precisar de mudança — apenas confirmar).

### Critérios de aceite (Given/When/Then)

**Given** o `docker-compose.yml` consolidado
**When** executado `docker compose up -d --build` localmente
**Then** os 4 serviços da aplicação e o `nginx-proxy-manager` sobem, todos `Up`
(`db`/`api` como `healthy`)

**Given** os serviços `api`, `dashboard`, `website`, `db`
**When** inspecionado o `docker-compose.yml`
**Then** nenhum deles tem chave `ports:` — só `nginx-proxy-manager` publica portas (80, 443,
81 temporária e comentada como tal)

**Given** o `.env.example`
**When** inspecionado
**Then** contém `DB_USER`, `DB_PASSWORD`, `JWT_SIGNING_KEY`, `SEED_USER_EMAIL`,
`SEED_USER_PASSWORD`, `DOMAIN_ROOT`, `WEBSITE_PUBLIC_URL`, `DASHBOARD_PUBLIC_URL`,
`API_PUBLIC_URL`, com comentários indicando quais são segredos

**Given** o `deploy.sh`
**When** executado localmente (repo já clonado, `.env` presente)
**Then** roda `git pull` + `docker compose up -d --build` sem erros, sem credencial
hardcoded, de forma idempotente (reexecução não destrutiva)

**Given** a rede do compose
**When** inspecionada
**Then** `api` (8080), `dashboard` (80), `website` (3000) e `db` (5432) só são alcançáveis via
rede Docker interna `omuletachou_net`

**Given** o `.gitignore`
**When** inspecionado
**Then** `.env` está listado

### Fora do escopo desta sub-issue
- Provisionamento real da VM Oracle, DNS, configuração da UI do NPM, emissão real de
  certificado — cobertos pelo runbook, execução manual do Gerente fora do pipeline.
- Qualquer mudança em código de aplicação dos 4 serviços além do necessário no Dockerfile do
  backend para o healthcheck (item 2 acima).

---

## Decisão sobre fatiamento

Avaliado dividir em sub-issues por tipo de artefato (ex.: "compose", "script", "env/docs"),
mas rejeitado: os 3 artefatos são pequenos (dezenas de linhas cada), compartilham o mesmo
conjunto de variáveis e não têm fronteira de PR independente — um PR só de `.env.example`
sem o `docker-compose.yml` correspondente não é testável isoladamente (os critérios de aceite
exigem `docker compose up -d --build` com os dois já consistentes). Não há paralelismo real
a ganhar (não é trabalho por stack como nas issues de feature) nem revisão facilitada por
fatiar — pelo contrário, fatiar aumentaria o overhead de merge sequencial do LT sem benefício.
Mantida uma única sub-issue coesa, "Sub-A: Artefatos de deploy".
