-- Cleanup demo seed users and all related data.
-- Safe for repeated execution (idempotent).
-- Scope: users seeded with email pattern seed.demo.%@nutritiontracker.local

BEGIN;

-- Preview counts before delete
SELECT
    (SELECT COUNT(*) FROM users WHERE email LIKE 'seed.demo.%@nutritiontracker.local') AS seeded_users_before,
    (SELECT COUNT(*) FROM meal_entries m JOIN users u ON u.id = m.user_id WHERE u.email LIKE 'seed.demo.%@nutritiontracker.local') AS seeded_meals_before,
    (SELECT COUNT(*) FROM meal_entry_media mm JOIN meal_entries m ON m.id = mm.meal_entry_id JOIN users u ON u.id = m.user_id WHERE u.email LIKE 'seed.demo.%@nutritiontracker.local') AS seeded_photos_before,
    (SELECT COUNT(*) FROM friendships f JOIN users u ON u.id IN (f.user_a_id, f.user_b_id) WHERE u.email LIKE 'seed.demo.%@nutritiontracker.local') AS seeded_friendships_before,
    (SELECT COUNT(*) FROM friend_invites fi JOIN users u ON u.id = fi.inviter_user_id WHERE u.email LIKE 'seed.demo.%@nutritiontracker.local') AS seeded_invites_before;

-- Cascades remove meals/media/items/likes/comments/goals/friendships/invites and other dependent rows
DELETE FROM users
WHERE email LIKE 'seed.demo.%@nutritiontracker.local';

COMMIT;

-- Verify after delete
SELECT
    (SELECT COUNT(*) FROM users WHERE email LIKE 'seed.demo.%@nutritiontracker.local') AS seeded_users_after,
    (SELECT COUNT(*) FROM meal_entries m JOIN users u ON u.id = m.user_id WHERE u.email LIKE 'seed.demo.%@nutritiontracker.local') AS seeded_meals_after,
    (SELECT COUNT(*) FROM meal_entry_media mm JOIN meal_entries m ON m.id = mm.meal_entry_id JOIN users u ON u.id = m.user_id WHERE u.email LIKE 'seed.demo.%@nutritiontracker.local') AS seeded_photos_after;
