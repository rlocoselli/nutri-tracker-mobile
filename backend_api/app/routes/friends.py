import uuid
from datetime import datetime
from fastapi import APIRouter, Depends, HTTPException
from sqlalchemy.orm import Session
from ..db import get_db
from ..models import FriendInvite, Friendship
from ..schemas import InviteIn
from ..security import get_current_user_id

router = APIRouter(prefix="/friends", tags=["friends"])


@router.get("/invites")
def list_invites(user_id: uuid.UUID = Depends(get_current_user_id), db: Session = Depends(get_db)):
    rows = (
        db.query(FriendInvite)
        .filter(FriendInvite.inviter_user_id == user_id)
        .order_by(FriendInvite.created_at_utc.desc())
        .all()
    )
    return rows


@router.post("/invites")
def create_invite(payload: InviteIn, user_id: uuid.UUID = Depends(get_current_user_id), db: Session = Depends(get_db)):
    exists = (
        db.query(FriendInvite)
        .filter(FriendInvite.inviter_user_id == user_id, FriendInvite.invitee_email == payload.invitee_email)
        .first()
    )
    if exists:
        raise HTTPException(status_code=409, detail="Invite already exists")

    row = FriendInvite(inviter_user_id=user_id, invitee_email=payload.invitee_email, status="pending")
    db.add(row)
    db.commit()
    db.refresh(row)
    return row


@router.post("/invites/{invite_id}/accept")
def accept_invite(invite_id: uuid.UUID, user_id: uuid.UUID = Depends(get_current_user_id), db: Session = Depends(get_db)):
    row = db.query(FriendInvite).filter(FriendInvite.id == invite_id).first()
    if not row:
        raise HTTPException(status_code=404, detail="Invite not found")

    row.status = "accepted"
    row.responded_at_utc = datetime.utcnow()

    pair_a = min(str(row.inviter_user_id), str(user_id))
    pair_b = max(str(row.inviter_user_id), str(user_id))

    existing_friendship = (
        db.query(Friendship)
        .filter(Friendship.user_a_id == uuid.UUID(pair_a), Friendship.user_b_id == uuid.UUID(pair_b))
        .first()
    )
    if not existing_friendship:
        db.add(Friendship(user_a_id=uuid.UUID(pair_a), user_b_id=uuid.UUID(pair_b)))

    db.commit()
    return {"accepted": True}


@router.delete("/invites/{invite_id}")
def delete_invite(invite_id: uuid.UUID, user_id: uuid.UUID = Depends(get_current_user_id), db: Session = Depends(get_db)):
    row = db.query(FriendInvite).filter(FriendInvite.id == invite_id, FriendInvite.inviter_user_id == user_id).first()
    if not row:
        raise HTTPException(status_code=404, detail="Invite not found")

    db.delete(row)
    db.commit()
    return {"deleted": True}


@router.get("")
def list_friends(user_id: uuid.UUID = Depends(get_current_user_id), db: Session = Depends(get_db)):
    rows = (
        db.query(Friendship)
        .filter((Friendship.user_a_id == user_id) | (Friendship.user_b_id == user_id))
        .all()
    )
    return rows
