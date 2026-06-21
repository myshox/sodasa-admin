-- GM 後台帳號表（與 EXE / WebApi 一致）。請在「遊戲資料庫」的 MySQL / MariaDB 執行，勿在 PostgreSQL 執行。
-- 密碼欄位：明文之 UTF-8 MD5，小寫十六進位（32 字元）。例：1234 → e10adc3949ba59abbe56e057f20f883e

CREATE TABLE IF NOT EXISTS `admin_users` (
  `id`         INT UNSIGNED NOT NULL AUTO_INCREMENT,
  `username`   VARCHAR(64)  NOT NULL,
  `password`   VARCHAR(128) NOT NULL COMMENT 'MD5(UTF-8 plaintext) lowercase hex',
  `nickname`   VARCHAR(128) NOT NULL DEFAULT '',
  `status`     TINYINT(1)   NOT NULL DEFAULT 1 COMMENT '1=啟用 0=停用',
  `created_at` TIMESTAMP    NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (`id`),
  UNIQUE KEY `uk_username` (`username`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- 若表為空，可選：建立預設 admin（密碼 1234）。已有帳號時請勿重複執行 INSERT。
-- INSERT INTO admin_users (username, password, nickname, status)
-- VALUES ('admin', 'e10adc3949ba59abbe56e057f20f883e', '系統管理員', 1);
