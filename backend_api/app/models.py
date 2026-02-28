import uuid
from datetime import datetime, date, time
from sqlalchemy import String, ForeignKey, DateTime, Date, Numeric, Integer, Boolean, Text
from sqlalchemy.dialects.postgresql import UUID, JSONB
from sqlalchemy.orm import Mapped, mapped_column
from .db import Base


class User(Base):
    __tablename__ = "users"

    id: Mapped[uuid.UUID] = mapped_column(UUID(as_uuid=True), primary_key=True, default=uuid.uuid4)
    google_sub: Mapped[str | None] = mapped_column(String, unique=True)
    email: Mapped[str] = mapped_column(String, unique=True, index=True)
    display_name: Mapped[str] = mapped_column(String, default="")
    picture_url: Mapped[str] = mapped_column(Text, default="")
    language_code: Mapped[str] = mapped_column(String, default="fr")
    created_at_utc: Mapped[datetime] = mapped_column(DateTime(timezone=True), default=datetime.utcnow)
    updated_at_utc: Mapped[datetime] = mapped_column(DateTime(timezone=True), default=datetime.utcnow)


class UserGoals(Base):
    __tablename__ = "user_goals"

    user_id: Mapped[uuid.UUID] = mapped_column(UUID(as_uuid=True), ForeignKey("users.id", ondelete="CASCADE"), primary_key=True)
    calories_target: Mapped[float] = mapped_column(Numeric(10, 2), default=2000)
    carbs_g_target: Mapped[float] = mapped_column(Numeric(10, 2), default=220)
    protein_g_target: Mapped[float] = mapped_column(Numeric(10, 2), default=120)
    updated_at_utc: Mapped[datetime] = mapped_column(DateTime(timezone=True), default=datetime.utcnow)


class MealEntry(Base):
    __tablename__ = "meal_entries"

    id: Mapped[uuid.UUID] = mapped_column(UUID(as_uuid=True), primary_key=True, default=uuid.uuid4)
    user_id: Mapped[uuid.UUID] = mapped_column(UUID(as_uuid=True), ForeignKey("users.id", ondelete="CASCADE"), index=True)
    date_utc: Mapped[datetime] = mapped_column(DateTime(timezone=True), index=True)
    day_key_utc: Mapped[date] = mapped_column(Date, index=True)
    raw_text: Mapped[str] = mapped_column(Text, default="")
    description: Mapped[str] = mapped_column(Text, default="")
    ai_notes: Mapped[str] = mapped_column(Text, default="")
    photo_url: Mapped[str] = mapped_column(Text, default="")
    total_calories: Mapped[float] = mapped_column(Numeric(10, 2), default=0)
    total_carbs_g: Mapped[float] = mapped_column(Numeric(10, 2), default=0)
    total_protein_g: Mapped[float] = mapped_column(Numeric(10, 2), default=0)
    overall_confidence: Mapped[float] = mapped_column(Numeric(5, 4), default=0)
    quality_score: Mapped[float] = mapped_column(Numeric(5, 2), default=0)
    quality_label: Mapped[str] = mapped_column(String, default="")
    created_at_utc: Mapped[datetime] = mapped_column(DateTime(timezone=True), default=datetime.utcnow)
    updated_at_utc: Mapped[datetime] = mapped_column(DateTime(timezone=True), default=datetime.utcnow)


class MealItem(Base):
    __tablename__ = "meal_items"

    id: Mapped[uuid.UUID] = mapped_column(UUID(as_uuid=True), primary_key=True, default=uuid.uuid4)
    meal_entry_id: Mapped[uuid.UUID] = mapped_column(UUID(as_uuid=True), ForeignKey("meal_entries.id", ondelete="CASCADE"), index=True)
    name: Mapped[str] = mapped_column(Text, default="")
    quantity: Mapped[float] = mapped_column(Numeric(10, 3), default=0)
    unit: Mapped[str] = mapped_column(String, default="")
    estimated_grams: Mapped[float] = mapped_column(Numeric(10, 3), default=0)
    calories: Mapped[float] = mapped_column(Numeric(10, 2), default=0)
    carbs_g: Mapped[float] = mapped_column(Numeric(10, 2), default=0)
    protein_g: Mapped[float] = mapped_column(Numeric(10, 2), default=0)
    confidence: Mapped[float] = mapped_column(Numeric(5, 4), default=0)


class ExerciseEntry(Base):
    __tablename__ = "exercise_entries"

    id: Mapped[uuid.UUID] = mapped_column(UUID(as_uuid=True), primary_key=True, default=uuid.uuid4)
    user_id: Mapped[uuid.UUID] = mapped_column(UUID(as_uuid=True), ForeignKey("users.id", ondelete="CASCADE"), index=True)
    date_utc: Mapped[datetime] = mapped_column(DateTime(timezone=True), index=True)
    day_key_utc: Mapped[date] = mapped_column(Date, index=True)
    google_fit_steps: Mapped[int] = mapped_column(Integer, default=0)
    exercise_minutes: Mapped[float] = mapped_column(Numeric(10, 2), default=0)
    burned_calories: Mapped[float] = mapped_column(Numeric(10, 2), default=0)
    source: Mapped[str] = mapped_column(String, default="manual-google-fit-test")
    notes: Mapped[str] = mapped_column(Text, default="")


class PointsWallet(Base):
    __tablename__ = "points_wallet"

    user_id: Mapped[uuid.UUID] = mapped_column(UUID(as_uuid=True), ForeignKey("users.id", ondelete="CASCADE"), primary_key=True)
    balance: Mapped[int] = mapped_column(Integer, default=0)
    updated_at_utc: Mapped[datetime] = mapped_column(DateTime(timezone=True), default=datetime.utcnow)


class PointsLedger(Base):
    __tablename__ = "points_ledger"

    id: Mapped[uuid.UUID] = mapped_column(UUID(as_uuid=True), primary_key=True, default=uuid.uuid4)
    user_id: Mapped[uuid.UUID] = mapped_column(UUID(as_uuid=True), ForeignKey("users.id", ondelete="CASCADE"), index=True)
    event_type: Mapped[str] = mapped_column(String)
    points_delta: Mapped[int] = mapped_column(Integer)
    reference_id: Mapped[uuid.UUID | None] = mapped_column(UUID(as_uuid=True))
    metadata_json: Mapped[dict] = mapped_column(JSONB, default=dict)
    created_at_utc: Mapped[datetime] = mapped_column(DateTime(timezone=True), default=datetime.utcnow)


class UserReminderSettings(Base):
    __tablename__ = "user_reminder_settings"

    user_id: Mapped[uuid.UUID] = mapped_column(UUID(as_uuid=True), ForeignKey("users.id", ondelete="CASCADE"), primary_key=True)
    enabled: Mapped[bool] = mapped_column(Boolean, default=False)
    breakfast_time_local: Mapped[time] = mapped_column(default=time(8, 0))
    lunch_time_local: Mapped[time] = mapped_column(default=time(13, 0))
    dinner_time_local: Mapped[time] = mapped_column(default=time(20, 0))
    timezone_name: Mapped[str] = mapped_column(String, default="UTC")
    updated_at_utc: Mapped[datetime] = mapped_column(DateTime(timezone=True), default=datetime.utcnow)


class FriendInvite(Base):
    __tablename__ = "friend_invites"

    id: Mapped[uuid.UUID] = mapped_column(UUID(as_uuid=True), primary_key=True, default=uuid.uuid4)
    inviter_user_id: Mapped[uuid.UUID] = mapped_column(UUID(as_uuid=True), ForeignKey("users.id", ondelete="CASCADE"), index=True)
    invitee_email: Mapped[str] = mapped_column(String)
    status: Mapped[str] = mapped_column(String, default="pending")
    created_at_utc: Mapped[datetime] = mapped_column(DateTime(timezone=True), default=datetime.utcnow)
    responded_at_utc: Mapped[datetime | None] = mapped_column(DateTime(timezone=True))


class Friendship(Base):
    __tablename__ = "friendships"

    id: Mapped[uuid.UUID] = mapped_column(UUID(as_uuid=True), primary_key=True, default=uuid.uuid4)
    user_a_id: Mapped[uuid.UUID] = mapped_column(UUID(as_uuid=True), ForeignKey("users.id", ondelete="CASCADE"), index=True)
    user_b_id: Mapped[uuid.UUID] = mapped_column(UUID(as_uuid=True), ForeignKey("users.id", ondelete="CASCADE"), index=True)
    created_at_utc: Mapped[datetime] = mapped_column(DateTime(timezone=True), default=datetime.utcnow)
