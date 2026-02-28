# Migration SQLite → PostgreSQL via API

## 1) Structure actuelle (mobile local)
Les données persistées en local aujourd'hui:
- `MealEntry` (repas)
- `MealItem` (détails d'aliments)
- `ExerciseEntry` (activité)
- `UserGoals` (objectifs)
- `Preferences`:
  - `app_points_balance`
  - `meal_reminders_enabled`, `meal_reminder_breakfast`, `meal_reminder_lunch`, `meal_reminder_dinner`
  - `social_friend_invites_v1` (JSON)

## 2) Cible PostgreSQL
Le schéma SQL proposé est dans:
- [docs/postgresql_schema.sql](docs/postgresql_schema.sql)

Il introduit `users` pour passer à un vrai mode multi-utilisateur (obligatoire côté API).

## 3) Mapping direct SQLite -> PostgreSQL

### MealEntry -> meal_entries
- `Id` -> `id`
- `DateUtc` -> `date_utc`
- `DayKeyUtc (yyyy-MM-dd)` -> `day_key_utc (DATE)`
- `RawText` -> `raw_text`
- `Description` -> `description`
- `AiNotes` -> `ai_notes`
- `PhotoPath` -> `photo_url` (ou clé objet S3/GCS)
- `TotalCalories` -> `total_calories`
- `TotalCarbsG` -> `total_carbs_g`
- `TotalProteinG` -> `total_protein_g`
- `OverallConfidence` -> `overall_confidence`
- `QualityScore` -> `quality_score`
- `QualityLabel` -> `quality_label`

### MealItem -> meal_items
- `Id` -> `id`
- `MealEntryId` -> `meal_entry_id`
- `Name` -> `name`
- `Quantity` -> `quantity`
- `Unit` -> `unit`
- `EstimatedGrams` -> `estimated_grams`
- `Calories` -> `calories`
- `CarbsG` -> `carbs_g`
- `ProteinG` -> `protein_g`
- `Confidence` -> `confidence`

### ExerciseEntry -> exercise_entries
- `Id` -> `id`
- `DateUtc` -> `date_utc`
- `DayKeyUtc` -> `day_key_utc`
- `GoogleFitSteps` -> `google_fit_steps`
- `ExerciseMinutes` -> `exercise_minutes`
- `BurnedCalories` -> `burned_calories`
- `Source` -> `source`
- `Notes` -> `notes`

### UserGoals -> user_goals
- SQLite local: une ligne fixe `Id=1`
- PostgreSQL: une ligne par utilisateur (`user_id` PK)

### Preferences -> tables dédiées
- `app_points_balance` -> `points_wallet` + `points_ledger`
- rappels horaires -> `user_reminder_settings`
- `social_friend_invites_v1` -> `friend_invites` (+ `friendships`)

## 4) API minimale recommandée

## Auth (Google)
- `POST /api/auth/google`
  - Entrée: `{ idToken }`
  - Effet: vérifie token Google, crée/retourne `user`, renvoie JWT API.

## Meals
- `POST /api/meals`
- `GET /api/meals?from=...&to=...`
- `PATCH /api/meals/{mealId}`
- `DELETE /api/meals/{mealId}`

`POST /api/meals` body exemple:
```json
{
  "dateUtc": "2026-02-28T12:30:00Z",
  "rawText": "salade poulet",
  "description": "salade poulet",
  "aiNotes": "bonne source protéique",
  "photoUrl": "https://...",
  "totals": { "calories": 520, "carbsG": 22, "proteinG": 41 },
  "quality": { "score": 78, "label": "Bon" },
  "overallConfidence": 0.91,
  "items": [
    { "name": "Poulet", "quantity": 150, "unit": "g", "estimatedGrams": 150, "calories": 240, "carbsG": 0, "proteinG": 45, "confidence": 0.94 }
  ]
}
```

## Exercise / Google Fit sync
- `POST /api/exercise`
- `GET /api/exercise?from=...&to=...`

## Goals
- `GET /api/goals`
- `PUT /api/goals`

## Points (monnaie)
- `GET /api/points/wallet`
- `POST /api/points/award` (idempotent côté backend via `event_id`)
- `GET /api/points/ledger?limit=50`

## Reminders
- `GET /api/reminders`
- `PUT /api/reminders`

## Social
- `POST /api/friends/invites`
- `GET /api/friends/invites`
- `POST /api/friends/invites/{inviteId}/accept`
- `DELETE /api/friends/invites/{inviteId}`
- `GET /api/friends`

## 5) Stratégie de migration côté app
1. Ajouter couche `RemoteDbService` (API).
2. Login: échanger Google `idToken` contre JWT backend.
3. Dual-write temporaire (SQLite + API) pendant transition.
4. Job de backfill: envoyer historique SQLite vers API (`meal_entries`, `meal_items`, `exercise_entries`, `goals`, points/rappels/social).
5. Vérifier cohérence (totaux journaliers identiques).
6. Passer en mode API-only, conserver SQLite en cache offline.

## 6) Recommandations importantes
- Toujours stocker en UTC (`TIMESTAMPTZ`) + `day_key_utc` pour agrégations rapides.
- Utiliser des `UUID` partout pour éviter collisions offline.
- Ajouter idempotence API pour événements de points (`event_id` unique).
- Créer indexes `(user_id, day_key_utc)` pour repas/exercice.
- Séparer `wallet` et `ledger` pour audit des monnaies.

## 7) Ce qu'il faut adapter dans ton code mobile
- Remplacer `LocalDb` (source principale) par appels API auth.
- Garder `LocalDb` comme cache lecture/offline uniquement.
- Remplacer `Preferences` de points/rappels/social par endpoints dédiés.
