import uuid
from datetime import datetime, timedelta
from fastapi import APIRouter, Depends, HTTPException
from sqlalchemy import or_
from sqlalchemy.orm import Session
from ..db import get_db
from ..models import FriendInvite, Friendship, MealEntry, User
from ..schemas import InviteIn, FriendStoryOut
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


@router.get("/feed", response_model=list[FriendStoryOut])
def friends_feed(
    days: int = 2,
    limit: int = 40,
    user_id: uuid.UUID = Depends(get_current_user_id),
    db: Session = Depends(get_db),
):
    safe_days = max(1, min(days, 14))
    safe_limit = max(1, min(limit, 120))

    friendships = (
        db.query(Friendship)
        .filter((Friendship.user_a_id == user_id) | (Friendship.user_b_id == user_id))
        .all()
    )

    visible_user_ids: set[uuid.UUID] = {user_id}
    for row in friendships:
        if row.user_a_id == user_id:
            visible_user_ids.add(row.user_b_id)
        else:
            visible_user_ids.add(row.user_a_id)

    cutoff = datetime.utcnow() - timedelta(days=safe_days)

    rows = (
        db.query(MealEntry, User)
        .join(User, User.id == MealEntry.user_id)
        .filter(MealEntry.user_id.in_(list(visible_user_ids)))
        .filter(MealEntry.date_utc >= cutoff)
        .order_by(MealEntry.date_utc.desc())
        .limit(safe_limit)
        .all()
    )

    out: list[FriendStoryOut] = []
    for meal, user in rows:
        out.append(
            FriendStoryOut(
                meal_id=str(meal.id),
                user_id=str(user.id),
                display_name=user.display_name or user.email or "User",
                picture_url=user.picture_url or "",
                date_utc=meal.date_utc,
                raw_text=meal.raw_text or "",
                photo_url=meal.photo_url or "",
                total_calories=float(meal.total_calories or 0),
                total_carbs_g=float(meal.total_carbs_g or 0),
                total_protein_g=float(meal.total_protein_g or 0),
                quality_label=meal.quality_label or "",
            )
        )

    return out
