-- Post-migration verification for meal photo storage split
-- Run on PostgreSQL after deploying backend changes.

-- 1) Confirm legacy column is removed
SELECT
    EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'meal_entries'
          AND column_name = 'photo_url'
    ) AS has_legacy_photo_column;

-- 2) Confirm new media table exists
SELECT
    EXISTS (
        SELECT 1
        FROM information_schema.tables
        WHERE table_schema = 'public'
          AND table_name = 'meal_entry_media'
    ) AS has_meal_entry_media_table;

-- 3) Count meal entries and media rows
SELECT
    (SELECT COUNT(*) FROM meal_entries) AS meal_entries_count,
    (SELECT COUNT(*) FROM meal_entry_media) AS media_rows_count;

-- 4) Detect orphan media rows (should be 0 due to FK)
SELECT COUNT(*) AS orphan_media_rows
FROM meal_entry_media m
LEFT JOIN meal_entries e ON e.id = m.meal_entry_id
WHERE e.id IS NULL;

-- 5) Detect empty photo payloads in media table
SELECT COUNT(*) AS empty_media_rows
FROM meal_entry_media
WHERE COALESCE(photo_url, '') = '';

-- 6) Sample recent media links
SELECT
    e.id AS meal_entry_id,
    e.user_id,
    e.date_utc,
    LEFT(m.photo_url, 64) AS photo_url_preview
FROM meal_entry_media m
JOIN meal_entries e ON e.id = m.meal_entry_id
ORDER BY e.date_utc DESC
LIMIT 20;
