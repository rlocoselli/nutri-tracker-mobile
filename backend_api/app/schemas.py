from datetime import datetime, time, date
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


class MealItemOut(BaseModel):
    id: str
    meal_entry_id: str
    name: str
    quantity: float
    unit: str
    estimated_grams: float
    calories: float
    carbs_g: float
    protein_g: float
    confidence: float


class MealOut(BaseModel):
    id: str
    date_utc: datetime
    day_key_utc: str
    raw_text: str
    description: str
    ai_notes: str
    photo_url: str
    total_calories: float
    total_carbs_g: float
    total_protein_g: float
    overall_confidence: float
    quality_score: float
    quality_label: str
    items: list[MealItemOut] = Field(default_factory=list)


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


class WaterIntakeIn(BaseModel):
    day_key_utc: date
    liters: float = 0


class WaterIntakeOut(BaseModel):
    day_key_utc: date
    liters: float


class FriendStoryOut(BaseModel):
    meal_id: str
    user_id: str
    display_name: str
    author_email: str
    picture_url: str
    date_utc: datetime
    raw_text: str
    photo_url: str
    total_calories: float
    total_carbs_g: float
    total_protein_g: float
    quality_label: str
    like_count: int = 0
    comment_count: int = 0
    liked_by_me: bool = False


class StoryLikeOut(BaseModel):
    liked: bool
    like_count: int


class StoryCommentIn(BaseModel):
    text: str = ""


class StoryCommentOut(BaseModel):
    id: str
    meal_id: str
    user_id: str
    author_name: str
    text: str
    created_at_utc: datetime


class PrivateMessageIn(BaseModel):
    text: str = ""


class PrivateMessageOut(BaseModel):
    id: str
    sender_user_id: str
    recipient_user_id: str
    text: str
    created_at_utc: datetime
