import uuid
from datetime import datetime
from fastapi import APIRouter, Depends
from sqlalchemy.orm import Session
from ..db import get_db
from ..models import PointsWallet, PointsLedger
from ..schemas import PointsAwardIn
from ..security import get_current_user_id

router = APIRouter(prefix="/points", tags=["points"])


@router.get("/wallet")
def get_wallet(user_id: uuid.UUID = Depends(get_current_user_id), db: Session = Depends(get_db)):
    wallet = db.query(PointsWallet).filter(PointsWallet.user_id == user_id).first()
    if not wallet:
        wallet = PointsWallet(user_id=user_id, balance=0)
        db.add(wallet)
        db.commit()
        db.refresh(wallet)
    return {"balance": wallet.balance}


@router.post("/award")
def award_points(payload: PointsAwardIn, user_id: uuid.UUID = Depends(get_current_user_id), db: Session = Depends(get_db)):
    wallet = db.query(PointsWallet).filter(PointsWallet.user_id == user_id).first()
    if not wallet:
        wallet = PointsWallet(user_id=user_id, balance=0)
        db.add(wallet)
        db.flush()

    wallet.balance += max(0, payload.points_delta)
    wallet.updated_at_utc = datetime.utcnow()

    ledger = PointsLedger(
        user_id=user_id,
        event_type=payload.event_type,
        points_delta=max(0, payload.points_delta),
        reference_id=uuid.UUID(payload.reference_id) if payload.reference_id else None,
    )
    db.add(ledger)
    db.commit()

    return {"balance": wallet.balance}


@router.get("/ledger")
def list_ledger(user_id: uuid.UUID = Depends(get_current_user_id), db: Session = Depends(get_db)):
    rows = (
        db.query(PointsLedger)
        .filter(PointsLedger.user_id == user_id)
        .order_by(PointsLedger.created_at_utc.desc())
        .limit(50)
        .all()
    )
    return rows
