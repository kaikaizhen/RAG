-- 專案分享範圍：off＝只留在筆記；private＝同步到自己的問答；public＝所有人都問得到。
-- 預設 off：公司內容不會因為忘了設定就離開這台主機。
ALTER TABLE projects ADD COLUMN sharing text NOT NULL DEFAULT 'off' CHECK (sharing IN ('off', 'private', 'public'));

-- 多人化之前已經在同步的專案維持原狀
UPDATE projects SET sharing = 'private'
WHERE lower(name) = 'learning-notes' AND owner_id = (SELECT id FROM users WHERE is_original);

-- 公開區用專案名稱當規則的識別，所以公開的專案名稱不能重複
CREATE UNIQUE INDEX projects_public_name_key ON projects (lower(name)) WHERE sharing = 'public';

-- 背景同步要代替使用者寫入他的 Space，登入時留下加密過的 refresh token
ALTER TABLE users ADD COLUMN rag_refresh_enc text;
ALTER TABLE users ADD COLUMN rag_synced_at timestamptz;
ALTER TABLE users ADD COLUMN rag_sync_error text;

-- 知識缺口：問答答不出來的問題，等 Claude 或使用者補成筆記
CREATE TABLE knowledge_gaps (
  id          integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
  owner_id    integer NOT NULL REFERENCES users (id),
  question    text NOT NULL CHECK (btrim(question) <> ''),
  resolution  text,
  created_at  timestamptz NOT NULL DEFAULT now(),
  resolved_at timestamptz
);
CREATE UNIQUE INDEX knowledge_gaps_open_key ON knowledge_gaps (owner_id, lower(question)) WHERE resolved_at IS NULL;
