import uuid
from fastapi import APIRouter, Depends
from sqlalchemy.orm import Session
from ..db import get_db
from ..models import User
from ..schemas import GoogleAuthIn

router = APIRouter(prefix="/auth", tags=["auth"])


@router.post("/google")
def auth_google(payload: GoogleAuthIn, db: Session = Depends(get_db)):
    fake_email = f"user-{payload.id_token[:8]}@example.com"
    existing = db.query(User).filter(User.email == fake_email).first()
    if existing:
        return {"user_id": str(existing.id), "email": existing.email, "token": "replace-with-real-jwt"}

    user = User(
        id=uuid.uuid4(),
        email=fake_email,
        google_sub=payload.id_token[:32],
        display_name="New User",
    )
    db.add(user)
    db.commit()
    db.refresh(user)

    return {"user_id": str(user.id), "email": user.email, "token": "replace-with-real-jwt"}
