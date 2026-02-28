from datetime import datetime, time
from pydantic import BaseModel, Field, EmailStr


class MealItemIn(BaseModel):
    name: str = ""
    quantity: float = 0
    unit: str = ""
    estimated_grams: float = 0
    calories: float = 0
    carbs_g: float = 0
    protein_g: float = 0
    confidence: float = 0


class MealCreateIn(BaseModel):
    date_utc: datetime
    raw_text: str = ""
    description: str = ""
    ai_notes: str = ""
    photo_url: str = ""
    total_calories: float = 0
    total_carbs_g: float = 0
    total_protein_g: float = 0
    overall_confidence: float = 0
    quality_score: float = 0
    quality_label: str = ""
    items: list[MealItemIn] = Field(default_factory=list)


class GoalsIn(BaseModel):
    calories_target: float
    carbs_g_target: float
    protein_g_target: float


class PointsAwardIn(BaseModel):
    event_type: str
    points_delta: int
    reference_id: str | None = None


class ReminderIn(BaseModel):
    enabled: bool
    breakfast_time_local: time
    lunch_time_local: time
    dinner_time_local: time
    timezone_name: str = "UTC"


class InviteIn(BaseModel):
    invitee_email: EmailStr


class GoogleAuthIn(BaseModel):
    id_token: str


class MessageOut(BaseModel):
    message: str
