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
- `DB_HOST`
- `DB_PORT` (optional, default `5432`)
- `DB_NAME`
- `DB_USER`
- `DB_PASSWORD`
- `API_HOST` (optional, default `0.0.0.0`)
- `API_PORT` (optional, default `8000`)

Backward compatibility is kept with `POSTGRES_*` names, but `DB_*` is preferred.

Example manual run on server:

```bash
cd backend_api
DB_HOST=82.165.153.80 \
DB_PORT=5432 \
DB_NAME=nutritiontracker \
DB_USER=ecom_admin \
DB_PASSWORD='super-secret' \
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
- `DB_HOST`
- `DB_PORT`
- `DB_NAME`
- `DB_USER`
- `DB_PASSWORD`
- `API_HOST` (optional)
- `API_PORT` (optional)

## 3) Auth model in this MVP

For protected routes, pass header:

- `X-User-Id: <uuid>`

Current `/api/auth/google` is a placeholder and returns a fake token.

Email auth endpoints (multistep):
- `POST /api/auth/email/register` (create account + send verification code by email)
- `POST /api/auth/email/verify` (validate code)
- `POST /api/auth/email/login`
- `POST /api/auth/email/password/forgot` (send reset code)
- `POST /api/auth/email/password/reset` (apply new password with code)
- `POST /api/auth/email/password/change` (requires `X-User-Id`)
- `DELETE /api/auth/account` (delete account + related data, requires `X-User-Id`)

SMTP environment variables required for real emails:
- `SMTP_HOST`
- `SMTP_PORT`
- `SMTP_USERNAME`
- `SMTP_PASSWORD`
- `SMTP_FROM_EMAIL`
- `SMTP_USE_TLS`

## 4) Main routes

- `POST /api/auth/google`
- `POST /api/auth/email/register`
- `POST /api/auth/email/verify`
- `POST /api/auth/email/login`
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
- `POST /api/friends/invites` (body: `invitee_email`, optional `locale` = `fr|en|pt|es` for invitation email language)
- `POST /api/friends/invites/{inviteId}/accept`
- `DELETE /api/friends/invites/{inviteId}`
- `GET /api/friends`

## 5) Notes

- Tables are auto-created on startup from SQLAlchemy models.
- PostgreSQL bootstrap also runs `docs/postgresql_schema.sql` at startup (objects created with `IF NOT EXISTS`).
- For production, replace with Alembic migrations and real JWT auth.
- You can also initialize DB from `docs/postgresql_schema.sql`.
- Production domain target: `https://api.nutritiontracker.fr`.
