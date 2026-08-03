# Runbook de deploy — ISSUE-15 (Oracle Cloud + SSL + Domínio)

> Passo a passo manual para o **Gerente** colocar o `omuletachou` em produção. Nenhum passo
> deste runbook é executado pelo pipeline de agentes (sem acesso SSH à VM real). Pré-requisito:
> os artefatos da sub-issue (`docker-compose.yml`, `deploy.sh`, `.env.example`) já mergeados em
> `main`.

## 1. Provisionar a VM Oracle Cloud

1. Criar conta Oracle Cloud (Free Tier), se ainda não existir.
2. Criar instância: shape `VM.Standard.A1.Flex` (Ampere ARM, Always Free — recomendado 2
   OCPU / 12 GB RAM, dentro do limite grátis de 4 OCPU / 24 GB total), imagem **Ubuntu 22.04**.
3. Gerar/associar par de chaves SSH no provisionamento (download da chave privada).
4. Anotar o **IP público** da instância — usado nos registros DNS (§3) e no acesso SSH.
5. **Security List** (ou Network Security Group) da VCN — liberar apenas:
   - `22/tcp` (SSH) — permanente.
   - `80/tcp` (HTTP) — permanente.
   - `443/tcp` (HTTPS) — permanente.
   - `81/tcp` (admin do Nginx Proxy Manager) — **temporária**, liberar agora, remover no §7.
   Nenhuma outra porta (nem `8080`, `4200`, `3000`, `5432`) deve estar na lista — os serviços
   da aplicação não são alcançáveis diretamente da internet.

## 2. Instalar Docker + Docker Compose na VM

Via SSH (`ssh -i <chave> ubuntu@<IP-VM>`):

```bash
sudo apt-get update
sudo apt-get install -y ca-certificates curl gnupg
sudo install -m 0755 -d /etc/apt/keyrings
curl -fsSL https://download.docker.com/linux/ubuntu/gpg | sudo gpg --dearmor -o /etc/apt/keyrings/docker.gpg
sudo chmod a+r /etc/apt/keyrings/docker.gpg
echo \
  "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.gpg] https://download.docker.com/linux/ubuntu \
  $(. /etc/os-release && echo "$VERSION_CODENAME") stable" | \
  sudo tee /etc/apt/sources.list.d/docker.list > /dev/null
sudo apt-get update
sudo apt-get install -y docker-ce docker-ce-cli containerd.io docker-compose-plugin
sudo usermod -aG docker $USER
# encerrar e reabrir a sessão SSH para o grupo "docker" ter efeito
```

Validar: `docker compose version` deve reportar Compose v2.

## 3. Registrar domínio e configurar DNS

1. Registrar `omuletachou.com.br` em registro.br (se ainda não registrado).
2. No painel de DNS do registro.br, criar **3 registros tipo A**, todos apontando para o
   IP público da VM (§1.4):
   - `omuletachou.com.br` → IP da VM
   - `www.omuletachou.com.br` → IP da VM
   - `dashboard.omuletachou.com.br` → IP da VM
   - `api.omuletachou.com.br` → IP da VM
3. Aguardar propagação (pode levar de minutos a até 24-48h). Verificar com:
   ```bash
   dig +short api.omuletachou.com.br
   dig +short dashboard.omuletachou.com.br
   dig +short omuletachou.com.br
   ```
   Cada comando deve retornar o IP da VM. **Não prosseguir para o §6 (emissão de SSL) antes
   da propagação confirmada** — Let's Encrypt falha se o DNS ainda não resolver para a VM.

## 4. Clonar o repositório e configurar segredos

```bash
git clone https://github.com/DQM-BETA/omuletachou.git
cd omuletachou
cp .env.example .env
nano .env   # preencher DB_USER, DB_PASSWORD, JWT_SIGNING_KEY, SEED_USER_EMAIL, SEED_USER_PASSWORD
            # DOMAIN_ROOT/WEBSITE_PUBLIC_URL/DASHBOARD_PUBLIC_URL/API_PUBLIC_URL já vêm
            # corretos no .env.example — ajustar só se o domínio final for diferente
chmod +x deploy.sh
```

`DB_PASSWORD` e `JWT_SIGNING_KEY` devem ser gerados fortes, ex.: `openssl rand -base64 32`.
`.env` nunca é commitado (já coberto por `.gitignore`).

## 5. Primeiro deploy

```bash
./deploy.sh
docker compose ps
```

Todos os serviços (`db`, `api`, `website`, `dashboard`, `nginx-proxy-manager`) devem aparecer
`Up` (`db` e `api` como `healthy` após alguns segundos).

## 6. Configurar o Nginx Proxy Manager (SSL via Let's Encrypt)

1. Acessar `http://<IP-VM>:81` no navegador (porta ainda liberada no Security List, §1.5).
2. Login padrão: `admin@example.com` / `changeme` — **trocar a senha imediatamente** no
   primeiro login (o NPM força a troca).
3. Criar 3 **Proxy Hosts** (menu Hosts → Proxy Hosts → Add Proxy Host):

   | Domain Names | Forward Hostname/IP | Forward Port | SSL |
   |---|---|---|---|
   | `omuletachou.com.br`, `www.omuletachou.com.br` | `website` | `3000` | Request new SSL cert, Force SSL, HTTP/2 |
   | `dashboard.omuletachou.com.br` | `dashboard` | `80` | Request new SSL cert, Force SSL, HTTP/2 |
   | `api.omuletachou.com.br` | `api` | `8080` | Request new SSL cert, Force SSL, HTTP/2 |

   Em cada host: aba **SSL** → "Request a new SSL Certificate" → marcar "Force SSL" e "HTTP/2
   Support" → aceitar termos do Let's Encrypt → Save. O NPM emite o certificado
   automaticamente (requer DNS já propagado, §3) e renova sozinho antes do vencimento — sem
   intervenção manual recorrente.
4. Validar no navegador: `https://omuletachou.com.br`, `https://dashboard.omuletachou.com.br`,
   `https://api.omuletachou.com.br/health` — todos com cadeado válido, sem aviso do browser.

## 7. Fechar a porta de administração do NPM (81)

Após confirmar os 3 proxy hosts funcionando com SSL válido:

1. No `docker-compose.yml` da VM, comentar a linha `"81:81"` do serviço
   `nginx-proxy-manager` (ou remover a regra `81/tcp` do Security List da VCN — qualquer uma
   das duas opções fecha o acesso público; comentar no compose é mais simples de reverter).
2. Reaplicar: `docker compose up -d` (recria só o container do NPM).
3. Confirmar que `http://<IP-VM>:81` não responde mais externamente.

**Reabertura pontual**: sempre que precisar reconfigurar um proxy host no futuro, descomentar
a linha, `docker compose up -d`, fazer a alteração via UI, fechar de novo (repetir este §7).
Não é automação — é procedimento manual documentado.

## 8. Preencher segredos de integrações externas

Acessar `https://dashboard.omuletachou.com.br`, fazer login com o usuário seed (§4), ir em
**Settings** e preencher as credenciais de cada integração (Amazon, Mercado Livre, Shopee,
Telegram, YouTube, Instagram, TikTok, Claude API) — armazenadas em `app_settings` (mesmo
padrão desde a Issue #11). Sem essas credenciais preenchidas, o sistema sobe normalmente, mas
os jobs que dependem de cada integração falham de forma isolada (comportamento esperado,
coberto pelas issues anteriores).

## 9. Checklist de verificação pós-deploy

- [ ] `docker compose ps` → todos os serviços `Up` (`db`/`api` `healthy`).
- [ ] `https://omuletachou.com.br`, `https://www.omuletachou.com.br`,
      `https://dashboard.omuletachou.com.br`, `https://api.omuletachou.com.br` respondem com
      certificado SSL válido (sem aviso do browser).
- [ ] `curl -s -o /dev/null -w "%{http_code}" https://api.omuletachou.com.br/health` → `200`.
- [ ] Dashboard carrega a tela de login em `https://dashboard.omuletachou.com.br`.
- [ ] `https://api.omuletachou.com.br/hangfire` exige a senha (`hangfire.dashboard_password`)
      e, após login, permite disparar o `CollectorJob` manualmente — validar coleta fim a fim.
- [ ] Porta `81` não responde mais externamente (§7 concluído).
- [ ] Security List da VM lista apenas `22`, `80`, `443` como regras permanentes.

## 10. Rollback

Em caso de deploy problemático:

```bash
cd omuletachou
git log --oneline -- docker-compose.yml .env.example   # identificar o commit anterior estável
git checkout <commit-anterior> -- docker-compose.yml
docker compose up -d --build
```

- **Nunca** rodar `docker compose down -v` nem qualquer comando que remova o volume
  `postgres_data` — os dados do banco devem sobreviver ao rollback.
- Se o problema for de segredo (`.env`), ajustar manualmente o `.env` (arquivo não versionado,
  não tem "commit anterior" a reverter) e reexecutar `./deploy.sh`.
- Sem blue-green nesta fase — o rollback assume um breve período de indisponibilidade durante
  o `docker compose up -d --build`. Registrado como melhoria futura se o projeto crescer.
