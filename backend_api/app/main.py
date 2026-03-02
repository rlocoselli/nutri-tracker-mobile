from fastapi import FastAPI
from fastapi.responses import RedirectResponse
from .db import Base, engine, ensure_postgres_objects, ensure_postgres_required_columns
from . import models  # noqa: F401
from .routes import auth, meals, goals, points, reminders, friends, water


app = FastAPI(
    title="NutritionTracker API",
    version="0.1.0",
    docs_url="/swagger",
    redoc_url="/redoc",
    openapi_url="/api/openapi.json",
)


@app.on_event("startup")
def startup_event():
    ensure_postgres_objects()
    Base.metadata.create_all(bind=engine)
    ensure_postgres_required_columns()


@app.get("/health")
def health():
    return {"status": "ok"}


@app.get("/", include_in_schema=False)
def root_redirect():
    return RedirectResponse(url="/swagger")


app.include_router(auth.router, prefix="/api")
app.include_router(meals.router, prefix="/api")
app.include_router(goals.router, prefix="/api")
app.include_router(points.router, prefix="/api")
app.include_router(reminders.router, prefix="/api")
app.include_router(friends.router, prefix="/api")
app.include_router(water.router, prefix="/api")
