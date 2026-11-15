-- 多人使用：每個專案和觀念卡都有擁有者，帳號沿用 RAG 的使用者編號。
CREATE TABLE users (
  id           integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
  rag_user_id  integer UNIQUE,               -- 原始擁有者第一次登入前是 NULL
  name         text NOT NULL CHECK (btrim(name) <> ''),
  is_original  boolean NOT NULL DEFAULT false,
  created_at   timestamptz NOT NULL DEFAULT now()
);
CREATE UNIQUE INDEX users_one_original_key ON users (is_original) WHERE is_original;

-- 多人化之前的筆記都歸給這個帳號，由 NOTES_OWNER_RAG_USER_ID 對應到真正的 RAG 使用者。
INSERT INTO users (name, is_original) VALUES ('原始擁有者', true);

-- 只存雜湊，資料庫外洩也拿不到可用的 token。
CREATE TABLE mcp_tokens (
  id           integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
  user_id      integer NOT NULL REFERENCES users (id),
  name         text NOT NULL CHECK (btrim(name) <> ''),
  token_hash   text NOT NULL UNIQUE,
  created_at   timestamptz NOT NULL DEFAULT now(),
  last_used_at timestamptz,
  revoked_at   timestamptz
);
CREATE INDEX mcp_tokens_user_idx ON mcp_tokens (user_id);

ALTER TABLE projects ADD COLUMN owner_id integer REFERENCES users (id);
UPDATE projects SET owner_id = (SELECT id FROM users WHERE is_original);
ALTER TABLE projects ALTER COLUMN owner_id SET NOT NULL;
DROP INDEX projects_name_key;
CREATE UNIQUE INDEX projects_owner_name_key ON projects (owner_id, lower(name));

ALTER TABLE concepts ADD COLUMN owner_id integer REFERENCES users (id);
UPDATE concepts SET owner_id = (SELECT id FROM users WHERE is_original);
ALTER TABLE concepts ALTER COLUMN owner_id SET NOT NULL;
DROP INDEX concepts_term_key;
CREATE UNIQUE INDEX concepts_owner_term_key ON concepts (owner_id, lower(term));
