import uuid
from datetime import date
from fastapi import APIRouter, Depends, HTTPException, Query
from sqlalchemy.orm import Session
from ..db import get_db
from ..models import MealEntry, MealItem
from ..schemas import MealCreateIn, MealOut, MealItemOut
from ..security import get_current_user_id

router = APIRouter(prefix="/meals", tags=["meals"])


@router.post("")
def create_meal(payload: MealCreateIn, user_id: uuid.UUID = Depends(get_current_user_id), db: Session = Depends(get_db)):
    meal = MealEntry(
        user_id=user_id,
        date_utc=payload.date_utc,
        day_key_utc=payload.date_utc.date(),
        raw_text=payload.raw_text,
        description=payload.description,
        ai_notes=payload.ai_notes,
        photo_url=payload.photo_url,
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
            items=items_by_meal.get(row.id, []),
        ))

    return out


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
