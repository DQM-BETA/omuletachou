# ISSUE-15 — Deploy Oracle Cloud + SSL + Domínio

## Objetivo
Entregar os **artefatos versionados** (compose consolidado, script de deploy, configuração
de proxy/SSL e runbook operacional) necessários para o Gerente colocar o sistema
`omuletachou` no ar 24/7 no Oracle Cloud Free Tier, com HTTPS via domínio próprio, de forma
manual e sem pipeline automatizado de CI/CD. O pipeline de agentes NÃO tem acesso SSH à VM
real — o deploy ao vivo é executado pelo Gerente fora do pipeline, seguindo os artefatos
produzidos por esta issue.

## Usuários afetados
- **Gerente/operador**: provisiona a VM manualmente (conta Oracle Cloud, shape
  `VM.Standard.A1.Flex`, Ubuntu 22.04), registra o domínio em registro.br, aponta o DNS,
  executa o `deploy.sh` na VM e preenche segredos/credenciais pós-boot.
- **Usuários finais do site público**: passam a acessar `https://omuletachou.com.br` com
  certificado SSL válido, condição necessária para PWA instalável e push notifications
  (Issue #14) funcionarem em produção.

## Casos de uso principais
1. Gerente provisiona a VM Oracle Cloud (fora do escopo de artefatos — ação manual prévia,
   documentada no runbook como pré-requisito) e instala Docker + Docker Compose.
2. Gerente registra `omuletachou.com.br` em registro.br e cria o registro DNS tipo A
   apontando para o IP público da VM (ação manual, documentada no runbook, sem automação).
3. Gerente clona o repositório na VM, copia `.env.example` → `.env`, preenche
   `DB_USER`/`DB_PASSWORD` (segredos de infraestrutura, nunca commitados).
4. Gerente executa `./deploy.sh` na raiz do repo → `git pull` + `docker compose up -d --build`
   sobe todos os serviços (db, api, dashboard, website) mais o Nginx Proxy Manager.
5. Gerente acessa a interface admin do Nginx Proxy Manager (porta interna, exposta apenas
   durante o setup inicial conforme runbook) e configura os proxy hosts com SSL automático
   via Let's Encrypt para o(s) domínio(s) do projeto.
6. Gerente acessa `https://omuletachou.com.br` (dashboard e API deste primeiro deploy — ver
   nota de escalonamento sobre subdomínios vs. path routing) e preenche via UI as credenciais
   de integrações externas (Amazon, ML, Shopee, Telegram, YouTube, Instagram, TikTok,
   Claude), armazenadas em `app_settings`.
7. Gerente dispara o `CollectorJob` manualmente pelo Hangfire Dashboard (acessível apenas via
   HTTPS, protegido pela senha já existente `hangfire.dashboard_password`) para validar a
   coleta fim a fim.
8. Em caso de necessidade de reverter, Gerente executa `git checkout` do commit anterior de
   `docker-compose.yml`/`.env` de infraestrutura + `docker compose up -d --build`, sem tocar
   no volume `postgres_data`.

## Casos de exceção
- Segredos de infraestrutura (`DB_USER`/`DB_PASSWORD`) ausentes no `.env` → `docker compose
  up` falha ao subir o `db` (comportamento padrão do Postgres, documentado no runbook como
  passo obrigatório antes do primeiro `deploy.sh`).
- Certificado Let's Encrypt falha na emissão (DNS ainda não propagado) → runbook documenta
  troubleshooting (validação de propagação DNS antes de configurar o proxy host).
- Segredos de API externas ausentes pós-boot → sistema sobe normalmente, mas os jobs que
  dependem dessas integrações falham de forma isolada (comportamento já coberto pelas
  issues anteriores, apenas referenciado aqui).

## Regras de negócio
- **Sem pipeline automatizado com SSH**: nenhum agente do pipeline executa comandos na VM
  real. Toda validação desta issue ocorre via `docker compose` local, simulando produção.
- **Compose consolidado único** na raiz do monorepo (`docker-compose.yml`), substituindo/
  consolidando qualquer compose por serviço pré-existente — ver nota de escalonamento (a).
- **Portas expostas ao host**: apenas as do Nginx Proxy Manager (80, 443, e a porta de admin
  do NPM apenas durante o setup — documentar se deve ser fechada após configuração inicial).
  Os serviços da aplicação (`api`, `dashboard`, `website`, `db`) NÃO publicam portas para o
  host — o compose atual expõe `5432`, `5000`, `3000`, `4200` diretamente, o que CONTRADIZ o
  requisito confirmado pelo Gerente e precisa ser corrigido (rede Docker interna + NPM como
  único ponto de entrada externo).
- **Segredos**: infraestrutura (`DB_USER`, `DB_PASSWORD`) via `.env` na VM, nunca commitado
  (já coberto pelo `.gitignore`); credenciais de integrações externas via dashboard
  (`app_settings`), padrão já existente desde a Issue #11. Sem secrets manager.
- **CI/CD automático fora de escopo** desta issue — deploy é sempre `deploy.sh` manual na VM.
- **Rollback simples**: nunca comando destrutivo em `postgres_data`; reversão via
  `git checkout` + `docker compose up -d --build`. Sem blue-green nesta fase (registrar como
  melhoria futura).

## Integrações externas
- **Nginx Proxy Manager** (container Docker) + **Let's Encrypt** (renovação automática via
  NPM) para SSL.
- **Oracle Cloud** (VM ARM Ampere A1, Free Tier) — provisionamento manual, fora do pipeline.
- **registro.br** — registro de domínio manual, fora do pipeline.
- Dependência funcional com Issue #14 (PWA + Push): HTTPS real deste deploy é pré-requisito
  para push notifications e instalação do PWA funcionarem em produção.

## Restrições / prazo
- VM e domínio ainda não existem — são pré-requisitos manuais, primeira tarefa da issue,
  fora do que o pipeline de agentes pode executar.
- Nenhum agente tem acesso SSH à VM de produção — critério de aceite não inclui boot Docker
  real validado por Dev/QA na VM; a validação é de artefatos (compose válido, script
  funcional, runbook completo), testável localmente.
- Custo esperado: zero (Oracle Cloud Always Free) + ~R$40/ano domínio + custo já existente de
  Claude API (~R$10/mês, fora do escopo desta issue).

## Definição de pronto
- `docker-compose.yml` consolidado na raiz builda e sobe os 4 serviços + Nginx Proxy Manager
  localmente via `docker compose up -d --build`, com `api`/`dashboard`/`website`/`db` SEM
  portas publicadas ao host (apenas rede Docker interna), e NPM como único ponto de entrada
  (80/443 no host).
- `deploy.sh` documentado e testável localmente (idempotente: `git pull` + `docker compose up
  -d --build`).
- `.env.example` atualizado com todas as variáveis de infraestrutura necessárias, nomeação
  consistente entre os 4 serviços (ver nota de escalonamento (c)).
- Runbook (`{docs_path}/runbook-deploy.md` — a ser detalhado no refinamento técnico) cobre,
  passo a passo e sem lacunas: provisionamento da VM, registro de domínio/DNS, configuração
  do Nginx Proxy Manager com Let's Encrypt, primeiro `deploy.sh`, preenchimento de segredos
  pós-boot, checklist de verificação, e procedimento de rollback.
- Checklist de verificação da spec original (containers `Up`, SSL válido, `/health` 200,
  dashboard carrega, jobs disparam) presente no runbook como passo manual do Gerente — não
  executado por QA (sem VM disponível ao pipeline).

## Nota de escalonamento — ambiguidade arquitetural (avaliação do PM)
Identificados três pontos que exigem decisão de arquitetura antes do refinamento técnico do
LT, todos com potencial de retrabalho se decididos incorretamente:

**(a) Estrutura do compose consolidado**: já existe um `docker-compose.yml` na raiz do
monorepo (de issues anteriores) expondo portas diretamente ao host (5432/5000/3000/4200) —
incompatível com o requisito confirmado de portas 22/80/443 apenas. Decisão necessária: como
reestruturar (remover `ports:`, usar `expose:` ou rede interna, adicionar serviço do Nginx
Proxy Manager, volumes de configuração/certificados do NPM) sem quebrar os builds/Dockerfiles
já existentes de cada serviço.

**(b) Rede Docker e isolamento**: desenho da rede interna (network única vs. segmentada),
como o NPM alcança os 4 serviços internamente, e se a porta de administração do NPM
(normalmente 81) deve ficar exposta permanentemente ou só durante o setup inicial (trade-off
segurança vs. usabilidade operacional para o Gerente).

**(c) Nomeação de variáveis de ambiente**: o compose atual já usa `DB_USER`/`DB_PASSWORD`,
`JWT_SIGNING_KEY`, `SEED_USER_*` sem prefixo por serviço; ao consolidar com NPM e possíveis
variáveis de domínio/subdomínio (`NEXT_PUBLIC_API_URL` aponta hoje para `localhost:5000`,
incompatível com produção via domínio), é preciso decidir o esquema de nomeação e como cada
serviço recebe a URL pública correta sem duplicar configuração.

Adicionalmente, a especificação original da issue menciona subdomínios
(`dashboard.omuletachou.com.br`, `api.omuletachou.com.br`), mas as respostas do Gerente ao
Gate 1 não confirmam explicitamente se essa estratégia de subdomínios permanece ou se o NPM
deve rotear por path sob um único domínio — decisão técnica com impacto direto na
configuração do NPM e nas variáveis de ambiente do front-end/dashboard.

**Decisão do PM**: escalar para o **Arquiteto** antes do Líder Técnico, dado que os três
pontos acima envolvem decisões de infraestrutura/rede não-óbvias, com impacto em todos os 4
serviços do monorepo e sem repositório de referência interno para copiar o padrão (issue
inédita neste projeto).
