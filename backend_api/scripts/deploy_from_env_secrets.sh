#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT_DIR"

: "${POSTGRES_DB:?POSTGRES_DB is required}"
: "${POSTGRES_USER:?POSTGRES_USER is required}"
: "${POSTGRES_PASSWORD:?POSTGRES_PASSWORD is required}"

API_HOST_VALUE="${API_HOST:-0.0.0.0}"
API_PORT_VALUE="${API_PORT:-8000}"

cat > .env <<EOF
POSTGRES_DB=${POSTGRES_DB}
POSTGRES_USER=${POSTGRES_USER}
POSTGRES_PASSWORD=${POSTGRES_PASSWORD}
API_HOST=${API_HOST_VALUE}
API_PORT=${API_PORT_VALUE}
DATABASE_URL=postgresql+psycopg://${POSTGRES_USER}:${POSTGRES_PASSWORD}@postgres:5432/${POSTGRES_DB}
EOF

echo "[1/4] Pull latest API image build context and start"
docker compose pull || true
docker compose up -d --build

echo "[2/4] Wait for API health"
for i in {1..45}; do
  if curl -fsS "http://localhost:${API_PORT_VALUE}/health" >/dev/null 2>&1; then
    echo "API healthy"
    break
  fi
  sleep 2
  if [[ $i -eq 45 ]]; then
    echo "API health timeout"
    docker compose logs --tail=100 api || true
    exit 1
  fi
done

echo "[3/4] Swagger"
echo "Swagger: http://localhost:${API_PORT_VALUE}/swagger"
echo "OpenAPI: http://localhost:${API_PORT_VALUE}/api/openapi.json"

echo "[4/4] Containers"
docker compose ps
