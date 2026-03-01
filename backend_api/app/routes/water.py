import uuid
from datetime import date
from fastapi import APIRouter, Depends, Query
from sqlalchemy.orm import Session
from ..db import get_db
from ..models import WaterIntakeDaily
from ..schemas import WaterIntakeIn, WaterIntakeOut
from ..security import get_current_user_id

router = APIRouter(prefix="/water-intake", tags=["water-intake"])


@router.put("", response_model=WaterIntakeOut)
def upsert_water_intake(
    payload: WaterIntakeIn,
    user_id: uuid.UUID = Depends(get_current_user_id),
    db: Session = Depends(get_db),
):
    liters = max(0.0, round(payload.liters * 2.0) / 2.0)

    row = (
        db.query(WaterIntakeDaily)
        .filter(WaterIntakeDaily.user_id == user_id)
        .filter(WaterIntakeDaily.day_key_utc == payload.day_key_utc)
        .first()
    )

    if row is None:
        row = WaterIntakeDaily(
            user_id=user_id,
            day_key_utc=payload.day_key_utc,
            liters=liters,
        )
        db.add(row)
    else:
        row.liters = liters

    db.commit()
    db.refresh(row)
    return WaterIntakeOut(day_key_utc=row.day_key_utc, liters=float(row.liters))


@router.get("", response_model=WaterIntakeOut)
def get_water_intake(
    day: date = Query(...),
    user_id: uuid.UUID = Depends(get_current_user_id),
    db: Session = Depends(get_db),
):
    row = (
        db.query(WaterIntakeDaily)
        .filter(WaterIntakeDaily.user_id == user_id)
        .filter(WaterIntakeDaily.day_key_utc == day)
        .first()
    )

    if row is None:
        return WaterIntakeOut(day_key_utc=day, liters=0)

    return WaterIntakeOut(day_key_utc=row.day_key_utc, liters=float(row.liters))
