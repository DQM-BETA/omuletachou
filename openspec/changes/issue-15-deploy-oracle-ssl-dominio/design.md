# Design técnico — ISSUE-15 Deploy Oracle Cloud + SSL + Domínio

## Visão geral

Consolidar o `docker-compose.yml` existente (que hoje publica portas de cada serviço
diretamente ao host) num modelo de **borda única via Nginx Proxy Manager (NPM)**: os 4
serviços da aplicação (`db`, `api`, `dashboard`, `website`) passam a se comunicar
exclusivamente por rede Docker interna, sem `ports:` publicadas ao host, e o NPM se torna o
único container com portas expostas (80/443, mais 81 temporária — ver decisão 2). O NPM
resolve os hosts internos pelo nome do serviço Docker (DNS interno do Compose) e termina
TLS via Let's Encrypt antes de repassar para `dashboard:80`, `website:3000` e `api:8080`.

Esta decisão não exige mudança de código nos 4 serviços — apenas de configuração de
infraestrutura (compose, `.env`, DNS) — porque a inspeção do código existente já revelou
convenções compatíveis com subdomínios (ver decisão 3).

## Achados relevantes da inspeção do código (antes de decidir)

- `dashboard/nginx.conf` (dentro da imagem do próprio `dashboard`) já faz `proxy_pass
  http://api:8080/api/` — ou seja, o dashboard **já embute um proxy interno** para a API via
  nome de serviço Docker. O NPM não precisa rotear `/api` para o dashboard; o próprio
  container resolve.
- `website/lib/api.ts` (Server Components, SSR) usa `API_INTERNAL_URL` (default
  `http://api:8080`), **nunca exposto ao bundle do browser** — chamadas server-side não
  passam pelo NPM, vão direto pela rede Docker.
- `website/lib/push.ts` (client-side, Web Push) usa `NEXT_PUBLIC_API_URL`, que **é** exposto
  ao browser — precisa resolver um host público de verdade. Hoje aponta para
  `http://localhost:5000` (quebrado em produção).
- `backend/.../Cors/CorsConfigurator.cs` e `appsettings.json` **já têm hardcoded**
  `https://dashboard.omuletachou.com.br`, `https://omuletachou.com.br`,
  `https://www.omuletachou.com.br` na allowlist de CORS (decisão de subdomínio tomada
  implicitamente desde a Issue #11, nunca revisitada). Não há entrada para
  `api.omuletachou.com.br` porque a API não precisa de CORS para si mesma.
- `appsettings.json` já tem `ForwardedHeaders.KnownNetworks: 172.16.0.0/12` — o backend já
  foi preparado para rodar atrás de um reverse proxy (`UseForwardedHeaders`), reforçando que
  a topologia NPM → serviços internos é o modelo assumido desde o início.
- `Program.cs` expõe `GET /health` — usado no critério de aceite de verificação pós-deploy.

Conclusão prática: o código já pressupõe subdomínios e proxy reverso; a decisão de
arquitetura confirma e formaliza esse pressuposto em vez de introduzir um novo padrão.

---

## Decisão 1 — Reestruturação do compose consolidado

**Escolha**: manter um único `docker-compose.yml` na raiz (sem quebrar `context:` nem
`Dockerfile` de nenhum serviço), com as seguintes mudanças:

- Remover `ports:` de `db`, `api`, `website`, `dashboard`.
- Adicionar serviço `nginx-proxy-manager` (imagem `jc21/nginx-proxy-manager:latest`), com
  `ports: ["80:80", "443:443"]` (+ `81:81` documentado como temporário — decisão 2) e volumes
  `npm_data:/data` e `npm_letsencrypt:/etc/letsencrypt` (nomeados, análogos ao padrão já usado
  em `postgres_data`/`media_files`).
- Nenhuma mudança nos `Dockerfile`s de `api`/`dashboard`/`website`/`db` — eles continuam
  expondo a porta interna via `EXPOSE`/`ASPNETCORE_URLS`/`listen`, só deixam de publicá-la ao
  host no compose.
- Healthchecks: adicionar `healthcheck:` ao `db` (`pg_isready`) e `api` (`curl -f
  http://localhost:8080/health`) — hoje inexistentes — para permitir `depends_on: condition:
  service_healthy` do `api` em relação ao `db`, evitando a corrida de inicialização (a app já
  tem `/health`, custo zero de implementação nova).
- `dashboard`/`website` mantêm `depends_on: [api]` simples (sem `condition`, pois não expõem
  healthcheck próprio nesta issue — não é bloqueio para o objetivo do deploy).

**Justificativa**: zero retrabalho nos Dockerfiles/builds; a única superfície de mudança é o
compose e o `.env`. Alternativa rejeitada: reescrever Dockerfiles para não expor portas
internamente — desnecessário, pois `ports:` (mapeamento host) é ortogonal a `EXPOSE`
(documentação/rede interna); bastava remover o mapeamento.

## Decisão 2 — Desenho da rede Docker

**Escolha**: **rede única** (`bridge` custom, nome `omuletachou_net`, padrão do Compose —
sem `internal: true`) para todos os serviços + NPM, em vez de redes segmentadas
(`frontend`/`backend`).

**Justificativa**: para uma squad pequena/solo com 4 serviços e sem múltiplos tenants, a
segmentação `frontend`/`backend` adiciona uma camada de complexidade operacional (dois
`networks:` por serviço, mais uma rede para manter no runbook) sem ganho real de segurança —
nenhum serviço interno é publicamente exposto de qualquer forma (é esse justamente o ponto
desta issue), então o vetor que a segmentação mitigaria (um serviço comprometido pivotando
para o `db`) já é mitigado por outras camadas (rede Docker isolada do host, sem SSH lateral).
Se o projeto crescer (múltiplos ambientes, mais serviços com dados sensíveis), a segmentação
vira uma melhoria futura de baixo custo (registrada como tal, não implementada agora).

**Porta de admin do NPM (81)**: **exposta apenas durante o setup inicial**, não
permanentemente. Justificativa: a porta de admin do NPM não tem MFA nativo e roda com a
senha padrão até o primeiro login — manter aberta 24/7 amplia a superfície de ataque sem
necessidade operacional (configuração de proxy hosts é esporádica, não recorrente). O
runbook documenta:
1. Setup inicial: `docker compose up -d nginx-proxy-manager` com `81:81` publicada
   temporariamente → Gerente acessa `http://<IP-VM>:81`, troca a senha padrão, configura os
   proxy hosts com SSL.
2. Pós-setup: Gerente comenta a linha `"81:81"` no compose (ou remove do Security List da VM
   Oracle) e reaplica `docker compose up -d`. Reabertura pontual (comentar a linha de novo)
   sempre que precisar reconfigurar um proxy host — documentado no runbook como procedimento,
   não como automação.

Alternativa considerada e rejeitada: acesso permanente via túnel SSH (`ssh -L
8081:localhost:81 user@vm`) sem nunca publicar a porta — mais seguro, mas adiciona fricção
operacional real para o Gerente (não-técnico em infra) toda vez que precisar mexer num proxy
host; dado que o Security List da Oracle já é o controle primário (a porta só é alcançável
publicamente enquanto a regra 81/tcp existir nele), a documentação de "abrir
temporariamente/fechar depois" no runbook é suficiente para o contexto desta squad.

### Diagrama textual da topologia

```
Internet
   │
   ├── 22/tcp (SSH) ──────────────────────────► VM Oracle (host, fora do Docker)
   │
   ├── 80/tcp (HTTP, redirect→HTTPS) ─┐
   ├── 443/tcp (HTTPS) ───────────────┤
   └── 81/tcp (admin NPM, SOMENTE     │
       durante setup inicial) ────────┤
                                       ▼
                          ┌─────────────────────────┐
                          │  nginx-proxy-manager     │
                          │  (rede: omuletachou_net) │
                          └───────────┬─────────────┘
                                      │ resolução DNS interna por nome do serviço
        ┌─────────────────┬──────────┼──────────────┬───────────────┐
        ▼                 ▼          ▼               ▼
 dashboard.omule-   omuletachou.  api.omule-      (db não é
 tachou.com.br      com.br /www  tachou.com.br    alcançável via
        │                 │          │            NPM — sem
        ▼                 ▼          ▼            proxy host)
  dashboard:80      website:3000  api:8080
  (nginx interno          │          │
  já faz proxy            │          │
  /api → api:8080)        │          │
        │                 │          │
        └────────┬────────┴──────────┘
                  ▼
        rede Docker interna "omuletachou_net"
                  │
                  ▼
                db:5432
      (sem publish; só api conecta via
       ConnectionStrings__DefaultConnection
       Host=db)
```

## Decisão 3 — Nomeação de variáveis de ambiente e estratégia de URL pública

**Escolha A — convenção de nomeação**: manter as variáveis de infraestrutura **sem prefixo**
quando já existentes e sem risco de colisão (`DB_USER`, `DB_PASSWORD`, `JWT_SIGNING_KEY`,
`SEED_USER_EMAIL`, `SEED_USER_PASSWORD` — nenhuma colide entre serviços hoje), e introduzir
prefixo **apenas para as novas variáveis de URL pública/domínio**, que são multiplicadas por
serviço e colidiriam em conceito (não em nome literal, mas em ambiguidade de leitura) se não
forem prefixadas:

```
# Domínio raiz (1 variável, usada para derivar os subdomínios no .env — ver Escolha B)
DOMAIN_ROOT=omuletachou.com.br

# URLs públicas por serviço (consumidas pelo compose/app):
WEBSITE_PUBLIC_URL=https://omuletachou.com.br
DASHBOARD_PUBLIC_URL=https://dashboard.omuletachou.com.br
API_PUBLIC_URL=https://api.omuletachou.com.br

# Variáveis de app já existentes, sem renomear (baixo risco, evita diff desnecessário):
DB_USER=...
DB_PASSWORD=...
JWT_SIGNING_KEY=...
SEED_USER_EMAIL=...
SEED_USER_PASSWORD=...

# Variável de app já existente, apenas o VALOR muda (de localhost:5000 para produção):
NEXT_PUBLIC_API_URL=${API_PUBLIC_URL}
```

**Justificativa**: renomear `DB_USER`→`DB_DB_USER` ou similar não agrega clareza (o
prefixo do bloco já deixa óbvio que são variáveis de banco, mesmo sem prefixo formal) e
geraria diff desnecessário em `Program.cs`/`appsettings.json`/testes que já referenciam esses
nomes. Prefixar só as variáveis novas (`*_PUBLIC_URL`) mantém o `.env` legível sem reescrever
código estável.

**Escolha B — subdomínios, não path-routing.** Confirma-se a estratégia de subdomínios já
implícita no código (`CorsConfigurator.cs`), rejeitando path-routing (`omuletachou.com.br/api`,
`/dashboard`) por três razões concretas encontradas na inspeção:

1. **Custo de mudança**: path-routing exigiria reescrever `dashboard/nginx.conf` (que hoje
   assume estar servindo do root `/`, com `try_files $uri $uri/ /index.html` e chamadas
   relativas `/api/...`), o `base href` do build Angular, e a lógica de roteamento do
   Next.js (`website`) para conviver sob um prefixo — trabalho de código, não só de infra,
   fora do escopo desta issue (que é infraestrutura).
2. **CORS já configurado para subdomínios**: `appsettings.json` já teria que ser revisto de
   qualquer forma nesta issue (adicionar a URL de produção correta), mas reescrevê-lo para
   path-routing significaria remover uma decisão já tomada e testada (`CorsTests.cs`
   existente) sem necessidade.
3. **Simetria de custo operacional**: subdomínios exigem 3 registros DNS tipo A
   (`omuletachou.com.br`, `dashboard.omuletachou.com.br`, `api.omuletachou.com.br`) — mais um
   passo manual no runbook, mas de baixo custo (todos apontam para o mesmo IP da VM; o NPM
   distingue por `Host` header) e sem custo de manutenção contínua.

`www.omuletachou.com.br` (já presente no CORS) recebe o mesmo tratamento: proxy host no NPM
redirecionando para `website:3000`, sem variável de ambiente adicional (é alias de
`WEBSITE_PUBLIC_URL`).

`dashboard.omuletachou.com.br` no NPM aponta para `dashboard:80` — o proxy interno do próprio
`nginx.conf` do dashboard cuida de repassar `/api/*` para `api:8080` sem envolver o NPM
novamente (já resolvido pelo código existente, decisão zero-custo).

## Dependências
- Imagem `jc21/nginx-proxy-manager:latest` (Docker Hub).
- DNS: 3 registros A manuais em registro.br apontando para o IP da VM (ação do Gerente,
  documentada no runbook).
- Nenhuma dependência de código nova nos 4 serviços — apenas variáveis de ambiente e o
  compose consolidado.

## Riscos
- **Propagação de DNS lenta** pode atrasar a emissão do certificado Let's Encrypt no primeiro
  boot — mitigado com passo de verificação de propagação no runbook antes de configurar o
  proxy host.
- **Porta de admin do NPM esquecida aberta** após o setup — mitigado com passo explícito de
  "fechar a porta 81" no checklist de verificação do runbook (critério de aceite já cobre
  isso).
- **Rede única sem segmentação** é uma decisão consciente de trade-off (ver decisão 2);
  registrar como melhoria futura se o projeto crescer em superfície de ataque.
- **Ordem de inicialização** (`api` antes de estar pronto o `db`) mitigada pelo healthcheck
  novo em `db` + `depends_on: condition: service_healthy` no `api` (mudança mínima, sem
  impacto em código de aplicação).

## Contrato de componentes globais
Não aplicável — esta issue é de infraestrutura de deploy (compose/proxy/DNS), não introduz
nem altera componentes de UI/layout nos frontends.
