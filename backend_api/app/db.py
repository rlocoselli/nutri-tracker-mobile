from sqlalchemy import create_engine
from sqlalchemy.orm import DeclarativeBase, sessionmaker
from .config import DATABASE_URL
from pathlib import Path
import logging


class Base(DeclarativeBase):
    pass


engine = create_engine(DATABASE_URL, pool_pre_ping=True)
SessionLocal = sessionmaker(bind=engine, autoflush=False, autocommit=False)
logger = logging.getLogger(__name__)


def ensure_postgres_objects() -> None:
    backend = (engine.url.get_backend_name() or "").lower()
    if "postgres" not in backend:
        return

    schema_path = Path(__file__).resolve().parents[2] / "docs" / "postgresql_schema.sql"
    if not schema_path.exists():
        return

    sql_text = schema_path.read_text(encoding="utf-8")
    statements = _split_sql_statements(sql_text)
    if not statements:
        return

    for statement in statements:
        try:
            with engine.begin() as conn:
                conn.exec_driver_sql(statement)
        except Exception as exc:
            statement_head = statement.split("\n", 1)[0].strip().lower()
            is_extension_stmt = statement_head.startswith("create extension")
            if is_extension_stmt:
                logger.warning("Skipping PostgreSQL extension statement due to permissions: %s", exc)
                continue
            raise


def ensure_postgres_required_columns() -> None:
    backend = (engine.url.get_backend_name() or "").lower()
    if "postgres" not in backend:
        return

    statements = [
        "ALTER TABLE IF EXISTS users ADD COLUMN IF NOT EXISTS default_story_visibility TEXT NOT NULL DEFAULT 'friends'",
        "ALTER TABLE IF EXISTS meal_entries ADD COLUMN IF NOT EXISTS story_visibility TEXT NOT NULL DEFAULT 'friends'",
        "ALTER TABLE IF EXISTS users DROP CONSTRAINT IF EXISTS chk_users_default_story_visibility",
        "ALTER TABLE IF EXISTS users ADD CONSTRAINT chk_users_default_story_visibility CHECK (default_story_visibility IN ('friends','public','self'))",
        "ALTER TABLE IF EXISTS meal_entries DROP CONSTRAINT IF EXISTS chk_meal_entries_story_visibility",
        "ALTER TABLE IF EXISTS meal_entries ADD CONSTRAINT chk_meal_entries_story_visibility CHECK (story_visibility IN ('friends','public','self'))",
    ]

    for statement in statements:
        try:
            with engine.begin() as conn:
                conn.exec_driver_sql(statement)
        except Exception as exc:
            logger.warning("PostgreSQL schema guard skipped statement '%s': %s", statement, exc)


def _split_sql_statements(sql_text: str) -> list[str]:
    cleaned_lines: list[str] = []
    for raw_line in sql_text.splitlines():
        stripped = raw_line.strip()
        if not stripped or stripped.startswith("--"):
            continue
        cleaned_lines.append(raw_line)

    joined = "\n".join(cleaned_lines)
    parts = [part.strip() for part in joined.split(";")]
    return [part for part in parts if part]


def get_db():
    db = SessionLocal()
    try:
        yield db
    finally:
        db.close()
