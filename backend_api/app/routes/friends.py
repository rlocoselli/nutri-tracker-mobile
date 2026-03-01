import uuid
from datetime import datetime, timedelta
from fastapi import APIRouter, Depends, HTTPException
from sqlalchemy import or_, func
from sqlalchemy.orm import Session
from ..db import get_db
from ..models import FriendInvite, Friendship, MealEntry, User, StoryLike, StoryComment, PrivateMessage
from ..schemas import InviteIn, FriendStoryOut, StoryLikeOut, StoryCommentIn, StoryCommentOut, PrivateMessageIn, PrivateMessageOut, FriendDirectoryOut, IncomingInviteOut
from ..security import get_current_user_id

router = APIRouter(prefix="/friends", tags=["friends"])


def _display_name(user: User | None) -> str:
    if not user:
        return "User"

    name = (user.display_name or "").strip()
    if name and name.lower() != "new user":
        return name

    email = (user.email or "").strip()
    if email and "@" in email:
        return email.split("@", 1)[0]

    return "User"


def _visible_user_ids(db: Session, user_id: uuid.UUID) -> set[uuid.UUID]:
    friendships = (
        db.query(Friendship)
        .filter((Friendship.user_a_id == user_id) | (Friendship.user_b_id == user_id))
        .all()
    )

    visible: set[uuid.UUID] = {user_id}
    for row in friendships:
        if row.user_a_id == user_id:
            visible.add(row.user_b_id)
        else:
            visible.add(row.user_a_id)

    return visible


@router.get("/invites")
def list_invites(user_id: uuid.UUID = Depends(get_current_user_id), db: Session = Depends(get_db)):
    rows = (
        db.query(FriendInvite)
        .filter(FriendInvite.inviter_user_id == user_id)
        .order_by(FriendInvite.created_at_utc.desc())
        .all()
    )
    return rows


@router.get("/invites/incoming", response_model=list[IncomingInviteOut])
def list_incoming_invites(user_id: uuid.UUID = Depends(get_current_user_id), db: Session = Depends(get_db)):
    me = db.query(User).filter(User.id == user_id).first()
    if not me or not (me.email or "").strip():
        return []

    my_email = (me.email or "").strip().lower()
    rows = (
        db.query(FriendInvite, User)
        .join(User, User.id == FriendInvite.inviter_user_id)
        .filter(func.lower(FriendInvite.invitee_email) == my_email)
        .filter(FriendInvite.status == "pending")
        .order_by(FriendInvite.created_at_utc.desc())
        .all()
    )

    out: list[IncomingInviteOut] = []
    for invite, inviter in rows:
        out.append(
            IncomingInviteOut(
                id=str(invite.id),
                inviter_user_id=str(invite.inviter_user_id),
                inviter_display_name=_display_name(inviter),
                inviter_email=(inviter.email or "").strip().lower(),
                invitee_email=(invite.invitee_email or "").strip().lower(),
                status=invite.status,
                created_at_utc=invite.created_at_utc,
            )
        )

    return out


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

    me = db.query(User).filter(User.id == user_id).first()
    my_email = ((me.email if me else "") or "").strip().lower()
    invitee_email = (row.invitee_email or "").strip().lower()
    if not my_email or not invitee_email or my_email != invitee_email:
        raise HTTPException(status_code=403, detail="Not allowed")

    if row.status != "pending":
        raise HTTPException(status_code=409, detail="Invite is not pending")

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


@router.post("/invites/{invite_id}/decline")
def decline_invite(invite_id: uuid.UUID, user_id: uuid.UUID = Depends(get_current_user_id), db: Session = Depends(get_db)):
    row = db.query(FriendInvite).filter(FriendInvite.id == invite_id).first()
    if not row:
        raise HTTPException(status_code=404, detail="Invite not found")

    me = db.query(User).filter(User.id == user_id).first()
    my_email = ((me.email if me else "") or "").strip().lower()
    invitee_email = (row.invitee_email or "").strip().lower()
    if not my_email or not invitee_email or my_email != invitee_email:
        raise HTTPException(status_code=403, detail="Not allowed")

    if row.status != "pending":
        raise HTTPException(status_code=409, detail="Invite is not pending")

    row.status = "declined"
    row.responded_at_utc = datetime.utcnow()
    db.commit()
    return {"declined": True}


@router.get("")
def list_friends(user_id: uuid.UUID = Depends(get_current_user_id), db: Session = Depends(get_db)):
    rows = (
        db.query(Friendship)
        .filter((Friendship.user_a_id == user_id) | (Friendship.user_b_id == user_id))
        .all()
    )
    return rows


@router.get("/directory", response_model=list[FriendDirectoryOut])
def friend_directory(user_id: uuid.UUID = Depends(get_current_user_id), db: Session = Depends(get_db)):
    visible = _visible_user_ids(db, user_id)
    visible.discard(user_id)

    users = db.query(User).filter(User.id.in_(list(visible))).all()
    out: list[FriendDirectoryOut] = []
    for user in users:
        out.append(
            FriendDirectoryOut(
                user_id=str(user.id),
                email=(user.email or "").strip().lower(),
                display_name=_display_name(user),
                picture_url=user.picture_url or "",
            )
        )

    return sorted(out, key=lambda x: x.display_name.lower())


@router.get("/users/search", response_model=list[FriendDirectoryOut])
def search_users(
    q: str,
    limit: int = 20,
    user_id: uuid.UUID = Depends(get_current_user_id),
    db: Session = Depends(get_db),
):
    query = (q or "").strip()
    if len(query) < 2:
        return []

    safe_limit = max(1, min(limit, 30))
    pattern = f"%{query.lower()}%"

    users = (
        db.query(User)
        .filter(User.id != user_id)
        .filter(
            or_(
                func.lower(User.email).like(pattern),
                func.lower(User.display_name).like(pattern),
            )
        )
        .order_by(User.created_at_utc.desc())
        .limit(safe_limit)
        .all()
    )

    return [
        FriendDirectoryOut(
            user_id=str(user.id),
            email=(user.email or "").strip().lower(),
            display_name=_display_name(user),
            picture_url=user.picture_url or "",
        )
        for user in users
    ]


@router.get("/feed", response_model=list[FriendStoryOut])
def friends_feed(
    days: int = 2,
    limit: int = 40,
    user_id: uuid.UUID = Depends(get_current_user_id),
    db: Session = Depends(get_db),
):
    safe_days = max(1, min(days, 14))
    safe_limit = max(1, min(limit, 120))

    visible_user_ids = _visible_user_ids(db, user_id)

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
        like_count = db.query(StoryLike).filter(StoryLike.meal_entry_id == meal.id).count()
        comment_count = db.query(StoryComment).filter(StoryComment.meal_entry_id == meal.id).count()
        liked_by_me = (
            db.query(StoryLike)
            .filter(StoryLike.meal_entry_id == meal.id, StoryLike.user_id == user_id)
            .first()
            is not None
        )

        out.append(
            FriendStoryOut(
                meal_id=str(meal.id),
                user_id=str(user.id),
                display_name=_display_name(user),
                author_email=user.email or "",
                picture_url=user.picture_url or "",
                date_utc=meal.date_utc,
                raw_text=meal.raw_text or "",
                photo_url=meal.photo_url or "",
                total_calories=float(meal.total_calories or 0),
                total_carbs_g=float(meal.total_carbs_g or 0),
                total_protein_g=float(meal.total_protein_g or 0),
                quality_label=meal.quality_label or "",
                like_count=like_count,
                comment_count=comment_count,
                liked_by_me=liked_by_me,
            )
        )

    return out


@router.post("/feed/{meal_id}/like", response_model=StoryLikeOut)
def toggle_story_like(meal_id: uuid.UUID, user_id: uuid.UUID = Depends(get_current_user_id), db: Session = Depends(get_db)):
    meal = db.query(MealEntry).filter(MealEntry.id == meal_id).first()
    if not meal:
        raise HTTPException(status_code=404, detail="Story not found")

    visible_user_ids = _visible_user_ids(db, user_id)
    if meal.user_id not in visible_user_ids:
        raise HTTPException(status_code=403, detail="Forbidden")

    existing = (
        db.query(StoryLike)
        .filter(StoryLike.meal_entry_id == meal_id, StoryLike.user_id == user_id)
        .first()
    )

    if existing:
        db.delete(existing)
        db.commit()
        liked = False
    else:
        db.add(StoryLike(meal_entry_id=meal_id, user_id=user_id))
        db.commit()
        liked = True

    like_count = db.query(StoryLike).filter(StoryLike.meal_entry_id == meal_id).count()
    return StoryLikeOut(liked=liked, like_count=like_count)


@router.get("/feed/{meal_id}/comments", response_model=list[StoryCommentOut])
def list_story_comments(
    meal_id: uuid.UUID,
    limit: int = 40,
    user_id: uuid.UUID = Depends(get_current_user_id),
    db: Session = Depends(get_db),
):
    meal = db.query(MealEntry).filter(MealEntry.id == meal_id).first()
    if not meal:
        raise HTTPException(status_code=404, detail="Story not found")

    visible_user_ids = _visible_user_ids(db, user_id)
    if meal.user_id not in visible_user_ids:
        raise HTTPException(status_code=403, detail="Forbidden")

    safe_limit = max(1, min(limit, 120))
    rows = (
        db.query(StoryComment, User)
        .join(User, User.id == StoryComment.user_id)
        .filter(StoryComment.meal_entry_id == meal_id)
        .order_by(StoryComment.created_at_utc.asc())
        .limit(safe_limit)
        .all()
    )

    out: list[StoryCommentOut] = []
    for comment, author in rows:
        author_name = _display_name(author)
        out.append(
            StoryCommentOut(
                id=str(comment.id),
                meal_id=str(comment.meal_entry_id),
                user_id=str(comment.user_id),
                author_name=author_name,
                text=comment.text or "",
                created_at_utc=comment.created_at_utc,
            )
        )

    return out


@router.post("/feed/{meal_id}/comments", response_model=StoryCommentOut)
def add_story_comment(
    meal_id: uuid.UUID,
    payload: StoryCommentIn,
    user_id: uuid.UUID = Depends(get_current_user_id),
    db: Session = Depends(get_db),
):
    text = (payload.text or "").strip()
    if not text:
        raise HTTPException(status_code=400, detail="Comment text required")

    meal = db.query(MealEntry).filter(MealEntry.id == meal_id).first()
    if not meal:
        raise HTTPException(status_code=404, detail="Story not found")

    visible_user_ids = _visible_user_ids(db, user_id)
    if meal.user_id not in visible_user_ids:
        raise HTTPException(status_code=403, detail="Forbidden")

    row = StoryComment(meal_entry_id=meal_id, user_id=user_id, text=text)
    db.add(row)
    db.commit()
    db.refresh(row)

    me = db.query(User).filter(User.id == user_id).first()
    author_name = _display_name(me)

    return StoryCommentOut(
        id=str(row.id),
        meal_id=str(row.meal_entry_id),
        user_id=str(row.user_id),
        author_name=author_name,
        text=row.text,
        created_at_utc=row.created_at_utc,
    )


@router.post("/messages/{other_user_id}", response_model=PrivateMessageOut)
def send_private_message(
    other_user_id: uuid.UUID,
    payload: PrivateMessageIn,
    user_id: uuid.UUID = Depends(get_current_user_id),
    db: Session = Depends(get_db),
):
    if other_user_id == user_id:
        raise HTTPException(status_code=400, detail="Cannot message yourself")

    text = (payload.text or "").strip()
    if not text:
        raise HTTPException(status_code=400, detail="Message text required")

    visible_user_ids = _visible_user_ids(db, user_id)
    if other_user_id not in visible_user_ids:
        raise HTTPException(status_code=403, detail="Not allowed")

    row = PrivateMessage(sender_user_id=user_id, recipient_user_id=other_user_id, text=text)
    db.add(row)
    db.commit()
    db.refresh(row)

    return PrivateMessageOut(
        id=str(row.id),
        sender_user_id=str(row.sender_user_id),
        recipient_user_id=str(row.recipient_user_id),
        text=row.text,
        created_at_utc=row.created_at_utc,
    )


@router.get("/messages/{other_user_id}", response_model=list[PrivateMessageOut])
def list_private_messages(
    other_user_id: uuid.UUID,
    limit: int = 80,
    user_id: uuid.UUID = Depends(get_current_user_id),
    db: Session = Depends(get_db),
):
    if other_user_id == user_id:
        raise HTTPException(status_code=400, detail="Invalid peer")

    visible_user_ids = _visible_user_ids(db, user_id)
    if other_user_id not in visible_user_ids:
        raise HTTPException(status_code=403, detail="Not allowed")

    safe_limit = max(1, min(limit, 200))
    rows = (
        db.query(PrivateMessage)
        .filter(
            or_(
                (PrivateMessage.sender_user_id == user_id) & (PrivateMessage.recipient_user_id == other_user_id),
                (PrivateMessage.sender_user_id == other_user_id) & (PrivateMessage.recipient_user_id == user_id),
            )
        )
        .order_by(PrivateMessage.created_at_utc.asc())
        .limit(safe_limit)
        .all()
    )

    return [
        PrivateMessageOut(
            id=str(row.id),
            sender_user_id=str(row.sender_user_id),
            recipient_user_id=str(row.recipient_user_id),
            text=row.text,
            created_at_utc=row.created_at_utc,
        )
        for row in rows
    ]
