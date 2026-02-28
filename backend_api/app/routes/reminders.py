import uuid
from datetime import datetime
from fastapi import APIRouter, Depends
from sqlalchemy.orm import Session
from ..db import get_db
from ..models import UserReminderSettings
from ..schemas import ReminderIn
from ..security import get_current_user_id

router = APIRouter(prefix="/reminders", tags=["reminders"])


@router.get("")
def get_reminders(user_id: uuid.UUID = Depends(get_current_user_id), db: Session = Depends(get_db)):
    row = db.query(UserReminderSettings).filter(UserReminderSettings.user_id == user_id).first()
    if not row:
        row = UserReminderSettings(user_id=user_id)
        db.add(row)
        db.commit()
        db.refresh(row)
    return row


@router.put("")
def put_reminders(payload: ReminderIn, user_id: uuid.UUID = Depends(get_current_user_id), db: Session = Depends(get_db)):
    row = db.query(UserReminderSettings).filter(UserReminderSettings.user_id == user_id).first()
    if not row:
        row = UserReminderSettings(user_id=user_id)
        db.add(row)

    row.enabled = payload.enabled
    row.breakfast_time_local = payload.breakfast_time_local
    row.lunch_time_local = payload.lunch_time_local
    row.dinner_time_local = payload.dinner_time_local
    row.timezone_name = payload.timezone_name
    row.updated_at_utc = datetime.utcnow()
    db.commit()
    return {"saved": True}
