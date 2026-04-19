import uuid
from collections import defaultdict
from datetime import date, datetime, timedelta
from fastapi import APIRouter, Depends, HTTPException, Query
from sqlalchemy.orm import Session
from ..db import get_db
from ..models import MealEntry, MealItem, User
from ..schemas import MealCreateIn, MealOut, MealItemOut, MealDailySummaryOut
from ..security import get_current_user_id

router = APIRouter(prefix="/meals", tags=["meals"])


def _normalize_story_visibility(value: str | None, fallback: str = "friends") -> str:
    normalized = (value or "").strip().lower()
    if normalized in {"friends", "public", "self"}:
        return normalized
    return fallback if fallback in {"friends", "public", "self"} else "friends"


@router.post("")
def create_meal(payload: MealCreateIn, user_id: uuid.UUID = Depends(get_current_user_id), db: Session = Depends(get_db)):
    user = db.query(User).filter(User.id == user_id).first()
    user_default = _normalize_story_visibility((user.default_story_visibility if user else "friends"), "friends")
    story_visibility = _normalize_story_visibility(payload.story_visibility, user_default)

    meal = MealEntry(
        user_id=user_id,
        date_utc=payload.date_utc,
        day_key_utc=payload.date_utc.date(),
        raw_text=payload.raw_text,
        description=payload.description,
        ai_notes=payload.ai_notes,
        photo_url=payload.photo_url,
        story_visibility=story_visibility,
        total_calories=payload.total_calories,
        total_carbs_g=payload.total_carbs_g,
        total_protein_g=payload.total_protein_g,
        overall_confidence=payload.overall_confidence,
        quality_score=payload.quality_score,
        quality_label=payload.quality_label,
    )
    db.add(meal)
    db.flush()

    for item in payload.items:
        db.add(MealItem(
            meal_entry_id=meal.id,
            name=item.name,
            quantity=item.quantity,
            unit=item.unit,
            estimated_grams=item.estimated_grams,
            calories=item.calories,
            carbs_g=item.carbs_g,
            protein_g=item.protein_g,
            confidence=item.confidence,
        ))

    db.commit()
    db.refresh(meal)
    return {"id": str(meal.id)}


@router.get("", response_model=list[MealOut])
def list_meals(
    from_date: date = Query(alias="from"),
    to_date: date = Query(alias="to"),
    user_id: uuid.UUID = Depends(get_current_user_id),
    db: Session = Depends(get_db),
):
    rows = (
        db.query(MealEntry)
        .filter(MealEntry.user_id == user_id)
        .filter(MealEntry.day_key_utc >= from_date)
        .filter(MealEntry.day_key_utc <= to_date)
        .order_by(MealEntry.date_utc.desc())
        .all()
    )

    if not rows:
        return []

    meal_ids = [row.id for row in rows]
    item_rows = db.query(MealItem).filter(MealItem.meal_entry_id.in_(meal_ids)).all()
    items_by_meal: dict[uuid.UUID, list[MealItemOut]] = {}
    for item in item_rows:
        items_by_meal.setdefault(item.meal_entry_id, []).append(MealItemOut(
            id=str(item.id),
            meal_entry_id=str(item.meal_entry_id),
            name=item.name,
            quantity=float(item.quantity),
            unit=item.unit,
            estimated_grams=float(item.estimated_grams),
            calories=float(item.calories),
            carbs_g=float(item.carbs_g),
            protein_g=float(item.protein_g),
            confidence=float(item.confidence),
        ))

    out: list[MealOut] = []
    for row in rows:
        out.append(MealOut(
            id=str(row.id),
            date_utc=row.date_utc,
            day_key_utc=row.day_key_utc.isoformat(),
            raw_text=row.raw_text,
            description=row.description,
            ai_notes=row.ai_notes,
            photo_url=row.photo_url,
            total_calories=float(row.total_calories),
            total_carbs_g=float(row.total_carbs_g),
            total_protein_g=float(row.total_protein_g),
            overall_confidence=float(row.overall_confidence),
            quality_score=float(row.quality_score),
            quality_label=row.quality_label,
            story_visibility=_normalize_story_visibility(row.story_visibility, "friends"),
            items=items_by_meal.get(row.id, []),
        ))

    return out


@router.get("/daily-summary", response_model=list[MealDailySummaryOut])
def list_meal_daily_summary(
    from_utc: datetime = Query(alias="fromUtc"),
    to_utc: datetime = Query(alias="toUtc"),
    tz_offset_minutes: int = Query(0, alias="tzOffsetMinutes"),
    user_id: uuid.UUID = Depends(get_current_user_id),
    db: Session = Depends(get_db),
):
    safe_offset_minutes = max(-840, min(tz_offset_minutes, 840))
    offset = timedelta(minutes=safe_offset_minutes)

    rows = (
        db.query(MealEntry)
        .filter(MealEntry.user_id == user_id)
        .filter(MealEntry.date_utc >= from_utc)
        .filter(MealEntry.date_utc < to_utc)
        .order_by(MealEntry.date_utc.asc())
        .all()
    )

    buckets: dict[str, dict[str, float]] = defaultdict(
        lambda: {
            "meal_count": 0,
            "total_calories": 0.0,
            "total_carbs_g": 0.0,
            "total_protein_g": 0.0,
        }
    )

    for meal in rows:
        day_key_local = (meal.date_utc + offset).date().isoformat()
        bucket = buckets[day_key_local]
        bucket["meal_count"] += 1
        bucket["total_calories"] += float(meal.total_calories or 0)
        bucket["total_carbs_g"] += float(meal.total_carbs_g or 0)
        bucket["total_protein_g"] += float(meal.total_protein_g or 0)

    return [
        MealDailySummaryOut(
            day_key_local=day_key_local,
            meal_count=int(values["meal_count"]),
            total_calories=float(values["total_calories"]),
            total_carbs_g=float(values["total_carbs_g"]),
            total_protein_g=float(values["total_protein_g"]),
        )
        for day_key_local, values in sorted(buckets.items(), key=lambda item: item[0])
    ]


@router.patch("/{meal_id}")
def patch_meal(meal_id: uuid.UUID, payload: MealCreateIn, user_id: uuid.UUID = Depends(get_current_user_id), db: Session = Depends(get_db)):
    row = db.query(MealEntry).filter(MealEntry.id == meal_id, MealEntry.user_id == user_id).first()
    if not row:
        raise HTTPException(status_code=404, detail="Meal not found")

    row.date_utc = payload.date_utc
    row.day_key_utc = payload.date_utc.date()
    row.raw_text = payload.raw_text
    row.description = payload.description
    row.ai_notes = payload.ai_notes
    row.photo_url = payload.photo_url
    row.story_visibility = _normalize_story_visibility(payload.story_visibility, row.story_visibility or "friends")
    row.total_calories = payload.total_calories
    row.total_carbs_g = payload.total_carbs_g
    row.total_protein_g = payload.total_protein_g
    row.overall_confidence = payload.overall_confidence
    row.quality_score = payload.quality_score
    row.quality_label = payload.quality_label
    db.commit()
    return {"updated": True}


@router.delete("/{meal_id}")
def delete_meal(meal_id: uuid.UUID, user_id: uuid.UUID = Depends(get_current_user_id), db: Session = Depends(get_db)):
    row = db.query(MealEntry).filter(MealEntry.id == meal_id, MealEntry.user_id == user_id).first()
    if not row:
        raise HTTPException(status_code=404, detail="Meal not found")

    db.query(MealItem).filter(MealItem.meal_entry_id == meal_id).delete()
    db.delete(row)
    db.commit()
    return {"deleted": True}
