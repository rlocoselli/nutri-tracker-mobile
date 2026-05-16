-- Seed 100 demo users + stories + photos for local/demo environments.
-- Data is fully fictitious and uses placeholder avatar/photo URLs.

BEGIN;

CREATE EXTENSION IF NOT EXISTS pgcrypto;

-- Idempotent reset for this seed namespace
DELETE FROM users
WHERE email LIKE 'seed.demo.%@nutritiontracker.local';

WITH generated_users AS (
    SELECT
        gs AS idx,
        gen_random_uuid() AS id,
        CASE
            WHEN gs % 2 = 0 THEN
                (ARRAY[
                    'Lina', 'Emma', 'Mia', 'Nora', 'Lea', 'Sofia', 'Clara', 'Ines', 'Aya', 'Eva',
                    'Jade', 'Luna', 'Mila', 'Sara', 'Nina', 'Anna', 'Lola', 'Zoey', 'Elena', 'Iris'
                ])[((gs - 1) % 20) + 1]
            ELSE
                (ARRAY[
                    'Leo', 'Noah', 'Ethan', 'Lucas', 'Hugo', 'Adam', 'Liam', 'Milan', 'Yanis', 'Theo',
                    'Nolan', 'Oscar', 'Aaron', 'Elio', 'Max', 'Sami', 'Enzo', 'Ryan', 'Ilyas', 'Elias'
                ])[((gs - 1) % 20) + 1]
        END AS first_name,
        CASE
            WHEN gs % 2 = 0 THEN
                (ARRAY[
                    'Moreau', 'Bernard', 'Faure', 'Mercier', 'Leroux', 'Garcia', 'Perrin', 'Blanc', 'Colin', 'Dupont'
                ])[((gs - 1) % 10) + 1]
            ELSE
                (ARRAY[
                    'Martin', 'Durand', 'Petit', 'Roux', 'Fournier', 'Garnier', 'Robin', 'Chevalier', 'Lopez', 'Boyer'
                ])[((gs - 1) % 10) + 1]
        END AS last_name,
        CASE
            WHEN gs % 4 = 0 THEN 'en'
            WHEN gs % 4 = 1 THEN 'fr'
            WHEN gs % 4 = 2 THEN 'es'
            ELSE 'pt'
        END AS lang,
        CASE
            WHEN gs % 3 = 0 THEN 'public'
            WHEN gs % 3 = 1 THEN 'friends'
            ELSE 'friends'
        END AS visibility,
        ((gs - 1) % 70) + 1 AS avatar_id
    FROM generate_series(1, 100) AS gs
), inserted_users AS (
    INSERT INTO users (
        id,
        google_sub,
        email,
        display_name,
        picture_url,
        language_code,
        default_story_visibility,
        created_at_utc,
        updated_at_utc
    )
    SELECT
        id,
        NULL,
        format('seed.demo.%s@nutritiontracker.local', lpad(idx::text, 3, '0')),
        trim(first_name || ' ' || last_name),
        format('https://i.pravatar.cc/400?img=%s', avatar_id),
        lang,
        visibility,
        NOW() - make_interval(days => ((idx - 1) % 45)),
        NOW()
    FROM generated_users
    RETURNING id, email
)
INSERT INTO user_goals (user_id, calories_target, carbs_g_target, protein_g_target, updated_at_utc)
SELECT
    u.id,
    1800 + ((row_number() OVER (ORDER BY u.email) % 8) * 100),
    180 + ((row_number() OVER (ORDER BY u.email) % 6) * 20),
    90 + ((row_number() OVER (ORDER BY u.email) % 6) * 10),
    NOW()
FROM inserted_users u;

WITH demo_users AS (
    SELECT
        u.id,
        u.email,
        row_number() OVER (ORDER BY u.email) AS rn
    FROM users u
    WHERE u.email LIKE 'seed.demo.%@nutritiontracker.local'
), friendships_seed AS (
    SELECT
        LEAST(a.id, b.id) AS user_a_id,
        GREATEST(a.id, b.id) AS user_b_id
    FROM demo_users a
    JOIN demo_users b
      ON b.rn IN (
          ((a.rn + 1 - 1) % 100) + 1,
          ((a.rn + 7 - 1) % 100) + 1,
          ((a.rn + 21 - 1) % 100) + 1
      )
    WHERE a.id <> b.id
)
INSERT INTO friendships (id, user_a_id, user_b_id, created_at_utc)
SELECT
    gen_random_uuid(),
    f.user_a_id,
    f.user_b_id,
    NOW() - INTERVAL '30 days'
FROM (
    SELECT DISTINCT user_a_id, user_b_id
    FROM friendships_seed
) f;

WITH demo_users AS (
    SELECT id, email
    FROM users
    WHERE email LIKE 'seed.demo.%@nutritiontracker.local'
), meal_seed AS (
    SELECT
        gen_random_uuid() AS id,
        u.id AS user_id,
        NOW() - make_interval(days => (gs % 21), hours => (gs % 12), mins => ((gs * 7) % 60)) AS date_utc,
        (ARRAY['breakfast', 'lunch', 'dinner', 'snack'])[((gs - 1) % 4) + 1] AS meal_type,
        (ARRAY[
            'Salade quinoa avocat saumon',
            'Chicken bowl with rice and greens',
            'Pasta integral con tomate y albahaca',
            'Tapioca com ovo, fruta e iogurte',
            'Wrap dinde houmous crudites',
            'Greek yogurt granola banana',
            'Tofu salteado con arroz integral y brocoli',
            'Poke de salmon con mango y edamame'
        ])[((gs - 1) % 8) + 1] AS raw_text,
        (ARRAY[
            'Repas equilibre et colore.',
            'Balanced plate with clean composition.',
            'Plato equilibrado con ingredientes frescos.',
            'Prato equilibrado com boa apresentacao.'
        ])[((gs - 1) % 4) + 1] AS description,
        (ARRAY[
            'Bonne repartition glucides/proteines.',
            'Good macro split for energy and satiety.',
            'Buena distribucion de macros para energia.',
            'Boa distribuicao de macros para o dia.'
        ])[((gs - 1) % 4) + 1] AS ai_notes,
        (ARRAY['friends', 'public', 'friends', 'self'])[((gs - 1) % 4) + 1] AS story_visibility,
        380 + ((gs * 23) % 420) AS total_calories,
        22 + ((gs * 11) % 55) AS total_carbs_g,
        18 + ((gs * 13) % 42) AS total_protein_g,
        ROUND((0.62 + ((gs % 28) / 100.0))::numeric, 4) AS overall_confidence,
        48 + ((gs * 5) % 48) AS quality_score,
        (ARRAY['A', 'B', 'A', 'C'])[((gs - 1) % 4) + 1] AS quality_label
    FROM demo_users u
    CROSS JOIN generate_series(1, 4) gs
), inserted_meals AS (
    INSERT INTO meal_entries (
        id,
        user_id,
        date_utc,
        day_key_utc,
        raw_text,
        description,
        ai_notes,
        story_visibility,
        total_calories,
        total_carbs_g,
        total_protein_g,
        overall_confidence,
        quality_score,
        quality_label,
        created_at_utc,
        updated_at_utc,
        meal_type
    )
    SELECT
        m.id,
        m.user_id,
        m.date_utc,
        (m.date_utc AT TIME ZONE 'UTC')::date,
        m.raw_text,
        m.description,
        m.ai_notes,
        m.story_visibility,
        m.total_calories,
        m.total_carbs_g,
        m.total_protein_g,
        m.overall_confidence,
        m.quality_score,
        m.quality_label,
        m.date_utc,
        NOW(),
        m.meal_type
    FROM meal_seed m
    RETURNING id, user_id, date_utc
)
INSERT INTO meal_entry_media (meal_entry_id, photo_url, created_at_utc, updated_at_utc)
SELECT
    m.id,
    format('https://loremflickr.com/1080/1080/food?lock=%s', abs(('x' || substr(md5(m.id::text), 1, 8))::bit(32)::int)),
    m.date_utc,
    NOW()
FROM inserted_meals m;

WITH demo_users AS (
    SELECT id
    FROM users
    WHERE email LIKE 'seed.demo.%@nutritiontracker.local'
), demo_meals AS (
    SELECT m.id, m.user_id, m.date_utc
    FROM meal_entries m
    JOIN demo_users u ON u.id = m.user_id
)
INSERT INTO story_likes (id, meal_entry_id, user_id, created_at_utc)
SELECT
    gen_random_uuid(),
    dm.id,
    liker.id,
    dm.date_utc + INTERVAL '20 minutes'
FROM demo_meals dm
JOIN LATERAL (
    SELECT u.id
    FROM demo_users u
    WHERE u.id <> dm.user_id
    ORDER BY random()
    LIMIT 2
) liker ON TRUE;

WITH demo_users AS (
    SELECT id, display_name
    FROM users
    WHERE email LIKE 'seed.demo.%@nutritiontracker.local'
), demo_meals AS (
    SELECT m.id, m.user_id, m.date_utc
    FROM meal_entries m
    JOIN demo_users u ON u.id = m.user_id
)
INSERT INTO story_comments (id, meal_entry_id, user_id, text, created_at_utc)
SELECT
    gen_random_uuid(),
    dm.id,
    commenter.id,
    (ARRAY[
        'Super idee repas',
        'Belle presentation, ca donne faim.',
        'Top equilibre nutritionnel.',
        'Je vais tester cette version ce soir.'
    ])[(1 + floor(random() * 4))::int],
    dm.date_utc + INTERVAL '35 minutes'
FROM demo_meals dm
JOIN LATERAL (
    SELECT u.id
    FROM demo_users u
    WHERE u.id <> dm.user_id
    ORDER BY random()
    LIMIT 1
) commenter ON TRUE;

COMMIT;

-- Quick check
SELECT
    (SELECT COUNT(*) FROM users WHERE email LIKE 'seed.demo.%@nutritiontracker.local') AS seeded_users,
    (SELECT COUNT(*) FROM meal_entries m JOIN users u ON u.id = m.user_id WHERE u.email LIKE 'seed.demo.%@nutritiontracker.local') AS seeded_meals,
    (SELECT COUNT(*) FROM meal_entry_media mm JOIN meal_entries m ON m.id = mm.meal_entry_id JOIN users u ON u.id = m.user_id WHERE u.email LIKE 'seed.demo.%@nutritiontracker.local') AS seeded_photos;
