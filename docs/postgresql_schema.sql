-- PostgreSQL schema for NutritionTracker API migration
-- Date: 2026-02-28

CREATE EXTENSION IF NOT EXISTS pgcrypto;

-- =========================
-- Users / identity
-- =========================
CREATE TABLE IF NOT EXISTS users (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    google_sub TEXT UNIQUE,
    email TEXT UNIQUE NOT NULL,
    display_name TEXT NOT NULL DEFAULT '',
    picture_url TEXT NOT NULL DEFAULT '',
    language_code TEXT NOT NULL DEFAULT 'fr',
    created_at_utc TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at_utc TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_users_email ON users(email);

-- =========================
-- Nutrition goals
-- (SQLite: UserGoals)
-- =========================
CREATE TABLE IF NOT EXISTS user_goals (
    user_id UUID PRIMARY KEY REFERENCES users(id) ON DELETE CASCADE,
    calories_target NUMERIC(10,2) NOT NULL DEFAULT 2000,
    carbs_g_target NUMERIC(10,2) NOT NULL DEFAULT 220,
    protein_g_target NUMERIC(10,2) NOT NULL DEFAULT 120,
    updated_at_utc TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- =========================
-- Meals
-- (SQLite: MealEntry)
-- =========================
CREATE TABLE IF NOT EXISTS meal_entries (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    date_utc TIMESTAMPTZ NOT NULL,
    day_key_utc DATE NOT NULL,
    raw_text TEXT NOT NULL DEFAULT '',
    description TEXT NOT NULL DEFAULT '',
    ai_notes TEXT NOT NULL DEFAULT '',
    photo_url TEXT NOT NULL DEFAULT '',

    total_calories NUMERIC(10,2) NOT NULL DEFAULT 0,
    total_carbs_g NUMERIC(10,2) NOT NULL DEFAULT 0,
    total_protein_g NUMERIC(10,2) NOT NULL DEFAULT 0,

    overall_confidence NUMERIC(5,4) NOT NULL DEFAULT 0,
    quality_score NUMERIC(5,2) NOT NULL DEFAULT 0,
    quality_label TEXT NOT NULL DEFAULT '',

    created_at_utc TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at_utc TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_meal_entries_user_day ON meal_entries(user_id, day_key_utc);
CREATE INDEX IF NOT EXISTS idx_meal_entries_user_date ON meal_entries(user_id, date_utc);

-- =========================
-- Meal items
-- (SQLite: MealItem)
-- =========================
CREATE TABLE IF NOT EXISTS meal_items (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    meal_entry_id UUID NOT NULL REFERENCES meal_entries(id) ON DELETE CASCADE,
    name TEXT NOT NULL DEFAULT '',
    quantity NUMERIC(10,3) NOT NULL DEFAULT 0,
    unit TEXT NOT NULL DEFAULT '',
    estimated_grams NUMERIC(10,3) NOT NULL DEFAULT 0,
    calories NUMERIC(10,2) NOT NULL DEFAULT 0,
    carbs_g NUMERIC(10,2) NOT NULL DEFAULT 0,
    protein_g NUMERIC(10,2) NOT NULL DEFAULT 0,
    confidence NUMERIC(5,4) NOT NULL DEFAULT 0
);

CREATE INDEX IF NOT EXISTS idx_meal_items_meal_entry ON meal_items(meal_entry_id);

-- =========================
-- Exercise entries
-- (SQLite: ExerciseEntry)
-- =========================
CREATE TABLE IF NOT EXISTS exercise_entries (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    date_utc TIMESTAMPTZ NOT NULL,
    day_key_utc DATE NOT NULL,
    google_fit_steps INTEGER NOT NULL DEFAULT 0,
    exercise_minutes NUMERIC(10,2) NOT NULL DEFAULT 0,
    burned_calories NUMERIC(10,2) NOT NULL DEFAULT 0,
    source TEXT NOT NULL DEFAULT 'manual-google-fit-test',
    notes TEXT NOT NULL DEFAULT ''
);

CREATE INDEX IF NOT EXISTS idx_exercise_entries_user_day ON exercise_entries(user_id, day_key_utc);
CREATE INDEX IF NOT EXISTS idx_exercise_entries_user_date ON exercise_entries(user_id, date_utc);

-- =========================
-- Water intake (liters/day)
-- =========================
CREATE TABLE IF NOT EXISTS water_intake_daily (
    user_id UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    day_key_utc DATE NOT NULL,
    liters NUMERIC(6,2) NOT NULL DEFAULT 0,
    updated_at_utc TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    PRIMARY KEY (user_id, day_key_utc)
);

CREATE INDEX IF NOT EXISTS idx_water_intake_daily_user_day ON water_intake_daily(user_id, day_key_utc);

-- =========================
-- Points / currency
-- (SQLite current: Preferences key app_points_balance)
-- =========================
CREATE TABLE IF NOT EXISTS points_wallet (
    user_id UUID PRIMARY KEY REFERENCES users(id) ON DELETE CASCADE,
    balance INTEGER NOT NULL DEFAULT 0,
    updated_at_utc TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS points_ledger (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    event_type TEXT NOT NULL,
    points_delta INTEGER NOT NULL,
    reference_id UUID,
    metadata_json JSONB NOT NULL DEFAULT '{}'::jsonb,
    created_at_utc TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_points_ledger_user_time ON points_ledger(user_id, created_at_utc DESC);

-- =========================
-- Meal reminders
-- (SQLite current: Preferences meal_reminders_enabled + hours)
-- =========================
CREATE TABLE IF NOT EXISTS user_reminder_settings (
    user_id UUID PRIMARY KEY REFERENCES users(id) ON DELETE CASCADE,
    enabled BOOLEAN NOT NULL DEFAULT FALSE,
    breakfast_time_local TIME NOT NULL DEFAULT TIME '08:00',
    lunch_time_local TIME NOT NULL DEFAULT TIME '13:00',
    dinner_time_local TIME NOT NULL DEFAULT TIME '20:00',
    timezone_name TEXT NOT NULL DEFAULT 'UTC',
    updated_at_utc TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- =========================
-- Friends / invites
-- (SQLite current: Preferences JSON social_friend_invites_v1)
-- =========================
CREATE TABLE IF NOT EXISTS friend_invites (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    inviter_user_id UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    invitee_email TEXT NOT NULL,
    status TEXT NOT NULL DEFAULT 'pending' CHECK (status IN ('pending', 'accepted', 'declined')),
    created_at_utc TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    responded_at_utc TIMESTAMPTZ
);

CREATE UNIQUE INDEX IF NOT EXISTS uq_friend_invites_inviter_email ON friend_invites(inviter_user_id, invitee_email);
CREATE INDEX IF NOT EXISTS idx_friend_invites_email_status ON friend_invites(invitee_email, status);

CREATE TABLE IF NOT EXISTS friendships (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_a_id UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    user_b_id UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    created_at_utc TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT chk_friendships_order CHECK (user_a_id <> user_b_id)
);

CREATE UNIQUE INDEX IF NOT EXISTS uq_friendships_pair ON friendships(LEAST(user_a_id, user_b_id), GREATEST(user_a_id, user_b_id));

-- =========================
-- Optional helper view
-- =========================
CREATE OR REPLACE VIEW v_daily_nutrition AS
SELECT
    m.user_id,
    m.day_key_utc,
    SUM(m.total_calories) AS calories_in,
    SUM(m.total_carbs_g) AS carbs_in,
    SUM(m.total_protein_g) AS protein_in
FROM meal_entries m
GROUP BY m.user_id, m.day_key_utc;
