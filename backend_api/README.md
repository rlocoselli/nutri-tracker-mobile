# backend_api (FastAPI + PostgreSQL)

Backend MVP aligned with the mobile app migration from SQLite to PostgreSQL.

## 1) Setup

```bash
cd backend_api
python -m venv .venv
source .venv/bin/activate
pip install -r requirements.txt
cp .env.example .env
```

Set `DATABASE_URL` in `.env` to your PostgreSQL connection.

## 2) Run

```bash
uvicorn app.main:app --reload --host 0.0.0.0 --port 8000
```

Health check:

```bash
curl http://localhost:8000/health
```

Swagger:

```bash
http://localhost:8000/swagger
```

OpenAPI JSON:

```bash
http://localhost:8000/api/openapi.json
```

## 2-bis) Run with Docker Compose

```bash
cd backend_api
cp .env.example .env
bash scripts/deploy_docker_compose.sh
```

This starts PostgreSQL + API and exposes:
- API: `http://localhost:8000`
- Swagger: `http://localhost:8000/swagger`

## 2-ter) Deploy with environment secrets (recommended)

Server-side script (already included):
- `scripts/deploy_from_env_secrets.sh`

It expects these environment variables at runtime:
- `POSTGRES_DB`
- `POSTGRES_USER`
- `POSTGRES_PASSWORD`
- `API_HOST` (optional, default `0.0.0.0`)
- `API_PORT` (optional, default `8000`)

Example manual run on server:

```bash
cd backend_api
POSTGRES_DB=nutrition_tracker \
POSTGRES_USER=nutrition_user \
POSTGRES_PASSWORD='super-secret' \
API_HOST=0.0.0.0 \
API_PORT=8000 \
bash scripts/deploy_from_env_secrets.sh
```

GitHub Actions workflow included:
- `.github/workflows/deploy-backend-api.yml`

Required GitHub repository secrets:
- `VPS_HOST`
- `VPS_PORT` (optional, default 22)
- `VPS_USER`
- `VPS_SSH_PRIVATE_KEY`
- `BACKEND_API_DIR` (absolute path on server)
- `POSTGRES_DB`
- `POSTGRES_USER`
- `POSTGRES_PASSWORD`
- `API_HOST` (optional)
- `API_PORT` (optional)

## 3) Auth model in this MVP

For protected routes, pass header:

- `X-User-Id: <uuid>`

Current `/api/auth/google` is a placeholder and returns a fake token.

## 4) Main routes

- `POST /api/auth/google`
- `POST /api/meals`
- `GET /api/meals?from=YYYY-MM-DD&to=YYYY-MM-DD`
- `PATCH /api/meals/{mealId}`
- `DELETE /api/meals/{mealId}`
- `GET /api/goals`
- `PUT /api/goals`
- `GET /api/points/wallet`
- `POST /api/points/award`
- `GET /api/points/ledger`
- `GET /api/reminders`
- `PUT /api/reminders`
- `GET /api/friends/invites`
- `POST /api/friends/invites`
- `POST /api/friends/invites/{inviteId}/accept`
- `DELETE /api/friends/invites/{inviteId}`
- `GET /api/friends`

## 5) Notes

- Tables are auto-created on startup from SQLAlchemy models.
- For production, replace with Alembic migrations and real JWT auth.
- You can also initialize DB from `docs/postgresql_schema.sql`.
- Production domain target: `https://api.nutritiontracker.fr`.
