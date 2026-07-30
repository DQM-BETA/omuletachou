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
