-- ═══════════════════════════════════════════════════════════════════════════
-- 累積儲值循環換季遷移：25,000 / 輪 → 20,000 / 輪
--
-- 影響：
--   舊規則下 paydata.point 介於 0 ~ 25,000（每滿 25,000 進位 totalcheck）
--   新規則下 paydata.point 介於 0 ~ 20,000（每滿 20,000 進位 totalcheck）
--
-- 邏輯（與 DatabaseManager.FixPaydataCheckAsync 完全相同）：
--   completed = (point - 1) / 20000   ← 剛好 20000 留在當前輪
--   newPoint  = point - completed * 20000
--   newTotal  = totalcheck + completed
--   check     = 0  （讓玩家在新輪次可重新領取）
--   lifetime_total 不動
-- ═══════════════════════════════════════════════════════════════════════════

USE sodagame;

-- ───────────────────────────────────────────────────────────────────────────
-- STEP 1：先 DRY-RUN 預覽（不會寫入），確認影響筆數與範例
-- ───────────────────────────────────────────────────────────────────────────
SELECT
    '受影響玩家統計' AS section,
    COUNT(*)                                                AS total_players,
    SUM(CASE WHEN point > 20000 THEN 1 ELSE 0 END)          AS players_to_carry,
    SUM(CASE WHEN point BETWEEN 1 AND 20000 THEN 1 ELSE 0 END) AS players_unchanged,
    SUM(CASE WHEN point = 0 THEN 1 ELSE 0 END)              AS players_zero,
    MAX(point)                                              AS max_point_now
FROM paydata;

SELECT
    '前 30 名 point > 20000 的玩家（預覽）' AS section,
    cdkey,
    point                                       AS old_point,
    totalcheck                                  AS old_totalcheck,
    FLOOR((point - 1) / 20000)                  AS will_add_cycles,
    point - FLOOR((point - 1) / 20000) * 20000  AS new_point,
    totalcheck + FLOOR((point - 1) / 20000)     AS new_totalcheck,
    `check`                                     AS old_check,
    0                                           AS new_check,
    lifetime_total
FROM paydata
WHERE point > 20000
ORDER BY point DESC
LIMIT 30;

-- ───────────────────────────────────────────────────────────────────────────
-- STEP 2：確認預覽 OK 後，移除下面 /* */ 才會真的執行更新
--         建議先在低峰時段做、做完馬上 SELECT 驗證
-- ───────────────────────────────────────────────────────────────────────────

/*
START TRANSACTION;

UPDATE paydata
SET
    totalcheck = totalcheck + FLOOR((point - 1) / 20000),
    point      = point - FLOOR((point - 1) / 20000) * 20000,
    `check`    = 0
WHERE point > 20000;

-- 驗證：應該已經沒有 point > 20000
SELECT COUNT(*) AS remaining_overflow FROM paydata WHERE point > 20000;

-- 確認沒問題就 COMMIT；有問題就 ROLLBACK
COMMIT;
-- ROLLBACK;
*/

-- ───────────────────────────────────────────────────────────────────────────
-- STEP 3：（可選）查看遷移後 top 30 玩家
-- ───────────────────────────────────────────────────────────────────────────
-- SELECT cdkey, point, totalcheck, `check`, lifetime_total
-- FROM paydata
-- WHERE point > 0
-- ORDER BY lifetime_total DESC
-- LIMIT 30;
