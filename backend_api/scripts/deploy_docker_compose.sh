#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT_DIR"

if [[ ! -f .env ]]; then
  cp .env.example .env
  echo "Created .env from .env.example. Please update credentials in backend_api/.env"
fi

echo "[1/4] Build and start containers"
docker compose up -d --build

echo "[2/4] Wait for API health"
for i in {1..30}; do
  if curl -fsS http://localhost:8000/health >/dev/null 2>&1; then
    echo "API is healthy"
    break
  fi
  sleep 2
  if [[ $i -eq 30 ]]; then
    echo "API health check timeout"
    exit 1
  fi
done

echo "[3/4] Swagger URL"
echo "Swagger: http://localhost:8000/swagger"
echo "OpenAPI JSON: http://localhost:8000/api/openapi.json"

echo "[4/4] Running containers"
docker compose ps
