import uuid
from datetime import datetime, timezone
from fastapi import APIRouter, Depends, Query
from sqlalchemy.orm import Session

from ..db import get_db
from ..models import UserGamificationState, UserGamificationEvent
from ..schemas import (
    GamificationStatePatchIn,
    GamificationStateOut,
    GamificationEventIn,
    GamificationEventOut,
)
from ..security import get_current_user_id

router = APIRouter(prefix="/gamification", tags=["gamification"])


def _default_season_key() -> str:
    now = datetime.now(timezone.utc)
    quarter = ((now.month - 1) // 3) + 1
    return f"{now.year}-Q{quarter}"


def _ensure_state(user_id: uuid.UUID, db: Session) -> UserGamificationState:
    state = db.query(UserGamificationState).filter(UserGamificationState.user_id == user_id).first()
    if state:
        return state

    state = UserGamificationState(
        user_id=user_id,
        season_key=_default_season_key(),
        league_tier="Bronze",
        shared_streak_days=0,
        weekly_shared_posts=0,
        weekly_mission_completed=0,
        weekly_mission_target=3,
        weekly_mission_status="",
        updated_at_utc=datetime.utcnow(),
    )
    db.add(state)
    db.commit()
    db.refresh(state)
    return state


@router.get("/season", response_model=GamificationStateOut)
def get_season_state(user_id: uuid.UUID = Depends(get_current_user_id), db: Session = Depends(get_db)):
    return _ensure_state(user_id, db)


@router.put("/season", response_model=GamificationStateOut)
def put_season_state(payload: GamificationStatePatchIn, user_id: uuid.UUID = Depends(get_current_user_id), db: Session = Depends(get_db)):
    state = _ensure_state(user_id, db)

    if payload.season_key is not None:
        state.season_key = payload.season_key.strip() or _default_season_key()
    if payload.league_tier is not None:
        state.league_tier = payload.league_tier.strip() or "Bronze"
    if payload.shared_streak_days is not None:
        state.shared_streak_days = max(0, payload.shared_streak_days)
    if payload.weekly_shared_posts is not None:
        state.weekly_shared_posts = max(0, payload.weekly_shared_posts)
    if payload.weekly_mission_completed is not None:
        state.weekly_mission_completed = max(0, payload.weekly_mission_completed)
    if payload.weekly_mission_target is not None:
        state.weekly_mission_target = max(1, payload.weekly_mission_target)
    if payload.weekly_mission_status is not None:
        state.weekly_mission_status = payload.weekly_mission_status

    state.updated_at_utc = datetime.utcnow()
    db.commit()
    db.refresh(state)
    return state


@router.post("/events", response_model=GamificationEventOut)
def post_event(payload: GamificationEventIn, user_id: uuid.UUID = Depends(get_current_user_id), db: Session = Depends(get_db)):
    row = UserGamificationEvent(
        user_id=user_id,
        event_type=(payload.event_type or "").strip(),
        title=(payload.title or "").strip(),
        message=(payload.message or "").strip(),
        metadata_json=payload.metadata_json or {},
        created_at_utc=datetime.utcnow(),
    )
    db.add(row)
    db.commit()
    db.refresh(row)

    return GamificationEventOut(
        id=str(row.id),
        event_type=row.event_type,
        title=row.title,
        message=row.message,
        metadata_json=row.metadata_json or {},
        created_at_utc=row.created_at_utc,
    )


@router.get("/events", response_model=list[GamificationEventOut])
def list_events(
    limit: int = Query(default=20, ge=1, le=200),
    user_id: uuid.UUID = Depends(get_current_user_id),
    db: Session = Depends(get_db),
):
    rows = (
        db.query(UserGamificationEvent)
        .filter(UserGamificationEvent.user_id == user_id)
        .order_by(UserGamificationEvent.created_at_utc.desc())
        .limit(limit)
        .all()
    )

    return [
        GamificationEventOut(
            id=str(x.id),
            event_type=x.event_type,
            title=x.title,
            message=x.message,
            metadata_json=x.metadata_json or {},
            created_at_utc=x.created_at_utc,
        )
        for x in rows
    ]
