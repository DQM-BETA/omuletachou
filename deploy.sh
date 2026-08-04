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

# Serviços com healthcheck definido no docker-compose.yml (db, api). Aguarda todos
# reportarem "healthy" antes de considerar o deploy bem-sucedido — falha (exit 1) se
# algum ficar "unhealthy" ou não sair de "starting" dentro do timeout.
MONITORED_SERVICES="db api"
MAX_ATTEMPTS=30
SLEEP_SECONDS=2

echo "==> aguardando healthcheck dos serviços: ${MONITORED_SERVICES}"
for attempt in $(seq 1 "${MAX_ATTEMPTS}"); do
  all_healthy=true
  for service in ${MONITORED_SERVICES}; do
    container_id=$(docker compose ps -a -q "${service}")
    if [ -z "${container_id}" ]; then
      echo "ERRO: container do serviço '${service}' não encontrado." >&2
      docker compose ps -a
      exit 1
    fi

    running=$(docker inspect --format '{{.State.Running}}' "${container_id}")
    if [ "${running}" != "true" ]; then
      echo "ERRO: container do serviço '${service}' não está em execução (parou/crashou)." >&2
      docker compose ps -a
      docker compose logs --tail=50 "${service}" >&2
      exit 1
    fi

    health=$(docker inspect --format '{{if .State.Health}}{{.State.Health.Status}}{{else}}no-healthcheck{{end}}' "${container_id}")

    if [ "${health}" = "unhealthy" ]; then
      echo "ERRO: serviço '${service}' ficou unhealthy após o deploy." >&2
      docker compose ps
      docker compose logs --tail=50 "${service}" >&2
      exit 1
    fi

    if [ "${health}" != "healthy" ] && [ "${health}" != "no-healthcheck" ]; then
      all_healthy=false
    fi
  done

  if [ "${all_healthy}" = true ]; then
    echo "==> todos os serviços monitorados estão healthy"
    break
  fi

  if [ "${attempt}" -eq "${MAX_ATTEMPTS}" ]; then
    echo "ERRO: timeout aguardando healthcheck dos serviços monitorados." >&2
    docker compose ps
    exit 1
  fi

  sleep "${SLEEP_SECONDS}"
done

echo "==> status dos containers"
docker compose ps

echo "==> deploy concluído"
