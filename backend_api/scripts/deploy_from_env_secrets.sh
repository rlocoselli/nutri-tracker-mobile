#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT_DIR"

DB_HOST_VALUE="${DB_HOST:-${POSTGRES_HOST:-}}"
DB_PORT_VALUE="${DB_PORT:-${POSTGRES_PORT:-5432}}"
DB_NAME_VALUE="${DB_NAME:-${POSTGRES_DB:-}}"
DB_USER_VALUE="${DB_USER:-${POSTGRES_USER:-}}"
DB_PASSWORD_VALUE="${DB_PASSWORD:-${POSTGRES_PASSWORD:-}}"

: "${DB_HOST_VALUE:?DB_HOST (or POSTGRES_HOST) is required}"
: "${DB_NAME_VALUE:?DB_NAME (or POSTGRES_DB) is required}"
: "${DB_USER_VALUE:?DB_USER (or POSTGRES_USER) is required}"
: "${DB_PASSWORD_VALUE:?DB_PASSWORD (or POSTGRES_PASSWORD) is required}"

API_HOST_VALUE="${API_HOST:-0.0.0.0}"
API_PORT_VALUE="${API_PORT:-8000}"

cat > .env <<EOF
DB_HOST=${DB_HOST_VALUE}
DB_PORT=${DB_PORT_VALUE}
DB_NAME=${DB_NAME_VALUE}
DB_USER=${DB_USER_VALUE}
DB_PASSWORD=${DB_PASSWORD_VALUE}
API_HOST=${API_HOST_VALUE}
API_PORT=${API_PORT_VALUE}
DATABASE_URL=postgresql+psycopg://${DB_USER_VALUE}:${DB_PASSWORD_VALUE}@${DB_HOST_VALUE}:${DB_PORT_VALUE}/${DB_NAME_VALUE}
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
