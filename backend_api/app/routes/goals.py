import uuid
from datetime import datetime
from fastapi import APIRouter, Depends
from sqlalchemy.orm import Session
from ..db import get_db
from ..models import UserGoals
from ..schemas import GoalsIn
from ..security import get_current_user_id

router = APIRouter(prefix="/goals", tags=["goals"])


@router.get("")
def get_goals(user_id: uuid.UUID = Depends(get_current_user_id), db: Session = Depends(get_db)):
    row = db.query(UserGoals).filter(UserGoals.user_id == user_id).first()
    if not row:
        row = UserGoals(user_id=user_id)
        db.add(row)
        db.commit()
        db.refresh(row)
    return row


@router.put("")
def put_goals(payload: GoalsIn, user_id: uuid.UUID = Depends(get_current_user_id), db: Session = Depends(get_db)):
    row = db.query(UserGoals).filter(UserGoals.user_id == user_id).first()
    if not row:
        row = UserGoals(user_id=user_id)
        db.add(row)

    row.calories_target = payload.calories_target
    row.carbs_g_target = payload.carbs_g_target
    row.protein_g_target = payload.protein_g_target
    row.updated_at_utc = datetime.utcnow()

    db.commit()
    return {"saved": True}
