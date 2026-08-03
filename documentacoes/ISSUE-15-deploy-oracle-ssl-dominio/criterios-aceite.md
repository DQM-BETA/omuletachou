# Critérios de aceite — ISSUE-15 Deploy Oracle Cloud + SSL + Domínio

> Nota geral: como o pipeline de agentes não tem acesso SSH à VM de produção, todos os
> critérios abaixo validam **artefatos versionados** (compose, script, configuração, runbook),
> testáveis via `docker compose` local simulando produção — não um deploy real na VM Oracle.
> A execução ao vivo na VM é manual, feita pelo Gerente fora do pipeline.

## Compose consolidado

**Given** o `docker-compose.yml` consolidado na raiz do monorepo
**When** executado `docker compose up -d --build` localmente
**Then** os 4 serviços da aplicação (`db`, `api`, `dashboard`, `website`) e o Nginx Proxy
Manager sobem saudáveis (`docker compose ps` mostra todos `Up`)

**Given** os serviços `api`, `dashboard`, `website` e `db` no compose consolidado
**When** inspecionado o arquivo
**Then** nenhum desses serviços publica porta diretamente ao host (sem chave `ports:`
mapeando para o host) — comunicação exclusivamente via rede Docker interna

**Given** o serviço `nginx-proxy-manager` no compose consolidado
**When** inspecionado o arquivo
**Then** é o único serviço com portas publicadas ao host (80 e 443; porta de administração
documentada no runbook quanto a exposição temporária/permanente)

**Given** o `.env.example` na raiz do repo
**When** inspecionado
**Then** contém todas as variáveis de infraestrutura necessárias (`DB_USER`, `DB_PASSWORD`,
`JWT_SIGNING_KEY`, variáveis de domínio/URL pública), com nomenclatura consistente entre os
4 serviços e comentários indicando quais são segredos (nunca commitados com valor real)

## Script de deploy

**Given** o script `deploy.sh` na raiz do repo
**When** executado localmente (simulando a VM) num diretório com o repo já clonado
**Then** executa `git pull` seguido de `docker compose up -d --build` sem erros, de forma
idempotente (pode ser executado múltiplas vezes sem efeito colateral destrutivo)

**Given** o `deploy.sh`
**When** inspecionado
**Then** não assume nenhuma credencial hardcoded, lê tudo do `.env` já existente na VM

## SSL e proxy

**Given** o Nginx Proxy Manager configurado com um proxy host apontando para um domínio
válido com DNS já propagado
**When** o certificado Let's Encrypt é solicitado pela interface do NPM
**Then** o certificado é emitido e renovado automaticamente, sem intervenção manual
recorrente (documentado no runbook como comportamento esperado, não testável sem domínio
real registrado)

**Given** a configuração de rede do compose
**When** inspecionada
**Then** os serviços `api` (porta 8080 interna), `dashboard` (porta 80 interna), `website`
(porta 3000 interna) e `db` (porta 5432 interna) são alcançáveis pelo NPM apenas via rede
Docker interna, nunca diretamente da internet

## Segredos

**Given** o primeiro boot da aplicação via `deploy.sh`
**When** o Gerente acessa o dashboard após configurar `DB_USER`/`DB_PASSWORD` no `.env`
**Then** o runbook documenta o preenchimento manual das credenciais de integrações externas
(Amazon, ML, Shopee, Telegram, YouTube, Instagram, TikTok, Claude) via Settings do dashboard,
armazenadas em `app_settings` — mesmo padrão já existente desde a Issue #11

**Given** o `.gitignore` do repo
**When** inspecionado
**Then** `.env` está listado (segredos de infraestrutura nunca commitados)

## Runbook

**Given** o runbook (`documentacoes/ISSUE-15-deploy-oracle-ssl-dominio/runbook-deploy.md`)
**When** seguido passo a passo por alguém sem conhecimento prévio do projeto
**Then** cobre, sem lacunas: provisionamento da VM Oracle (shape, OS, portas no Security
List), instalação de Docker/Docker Compose, registro de domínio e DNS (registro A manual),
configuração do Nginx Proxy Manager com Let's Encrypt, primeiro `deploy.sh`, preenchimento
de segredos pós-boot (infraestrutura via `.env`, integrações via dashboard), checklist de
verificação (containers `Up`, SSL válido sem aviso do browser, `/health` retorna 200,
dashboard carrega, `CollectorJob` dispara), e procedimento de rollback

**Given** a necessidade de reverter um deploy problemático
**When** o Gerente segue o procedimento de rollback do runbook
**Then** consiste em `git checkout` do commit anterior de `docker-compose.yml`/`.env` de
infraestrutura + `docker compose up -d --build`, sem nenhum comando destrutivo sobre o volume
`postgres_data`

## Firewall / portas

**Given** o Security List documentado no runbook para a VM Oracle
**When** inspecionado
**Then** lista apenas as portas 22 (SSH), 80 (HTTP, redirect HTTPS via NPM) e 443 (HTTPS)
como regras permanentes — nenhuma porta de serviço interno (8080/80-dashboard/3000/5432)
exposta externamente

**Given** o Hangfire Dashboard (`/hangfire`)
**When** acessado em produção
**Then** só é alcançável via HTTPS (proxy do NPM) e exige a senha já existente
(`hangfire.dashboard_password`), sem porta adicional exposta no Security List
