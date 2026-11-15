-- 中文搜尋用 pg_trgm（ILIKE 搭配 trigram 索引），內建全文檢索不會斷中文詞。
CREATE EXTENSION IF NOT EXISTS pg_trgm;

CREATE TABLE projects (
  id          integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
  name        text NOT NULL CHECK (btrim(name) <> ''),
  created_at  timestamptz NOT NULL DEFAULT now()
);
CREATE UNIQUE INDEX projects_name_key ON projects (lower(name));

CREATE TABLE modules (
  id          integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
  project_id  integer NOT NULL REFERENCES projects (id),
  name        text NOT NULL CHECK (btrim(name) <> ''),
  created_at  timestamptz NOT NULL DEFAULT now()
);
CREATE UNIQUE INDEX modules_project_name_key ON modules (project_id, lower(name));

-- 改動紀錄。只增不改，是專案的歷史。
CREATE TABLE entries (
  id             integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
  project_id     integer NOT NULL REFERENCES projects (id),
  module_id      integer REFERENCES modules (id),
  title          text NOT NULL CHECK (btrim(title) <> ''),
  what_changed   text NOT NULL,            -- 改了什麼
  why            text NOT NULL,            -- 為什麼
  logic_notes    text,                     -- 邏輯判斷
  data_notes     text,                     -- 資料面（動到哪些表、欄位）
  test_status    text NOT NULL DEFAULT 'not_tested'
                 CHECK (test_status IN ('not_tested', 'manual', 'automated', 'production_verified')),
  test_notes     text,
  how_to_explain text,                     -- 別人問時怎麼答
  pr_url         text,
  happened_on    date NOT NULL DEFAULT ((now() AT TIME ZONE 'Asia/Taipei')::date),
  created_at     timestamptz NOT NULL DEFAULT now(),
  search_text    text GENERATED ALWAYS AS (
    title || ' ' || what_changed || ' ' || why || ' '
    || coalesce(logic_notes, '') || ' ' || coalesce(data_notes, '') || ' '
    || coalesce(test_notes, '') || ' ' || coalesce(how_to_explain, '')
  ) STORED
);
CREATE INDEX entries_project_date_idx ON entries (project_id, happened_on DESC);
CREATE INDEX entries_module_idx ON entries (module_id);
CREATE INDEX entries_search_idx ON entries USING gin (search_text gin_trgm_ops);

-- 每個模組「現在」的規則，會被覆寫；演變過程看 entries。
-- confidence 沒有預設值：每次寫入都要明講來源，推測不會被當成事實。
CREATE TABLE rules (
  id            integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
  module_id     integer NOT NULL REFERENCES modules (id),
  title         text NOT NULL CHECK (btrim(title) <> ''),
  body          text NOT NULL,
  confidence    text NOT NULL
                CHECK (confidence IN ('general_definition', 'code_inferred', 'colleague_confirmed')),
  confirmed_by  text,
  retired_at    timestamptz,               -- 規則不再適用時標記，不刪除
  created_at    timestamptz NOT NULL DEFAULT now(),
  updated_at    timestamptz NOT NULL DEFAULT now(),
  -- 「已向同事確認」一定要有確認者；其他可信度不能留著舊的確認者。
  CONSTRAINT rules_confirmed_by_check
    CHECK ((confidence = 'colleague_confirmed') = (confirmed_by IS NOT NULL))
);
CREATE UNIQUE INDEX rules_module_title_key ON rules (module_id, lower(title));

-- 觀念卡。跨專案共用（料號、BOM 不屬於單一專案），所以沒有 project_id。
CREATE TABLE concepts (
  id                integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
  term              text NOT NULL CHECK (btrim(term) <> ''),
  plain_explanation text NOT NULL,         -- 白話
  why_needed        text,                  -- 為什麼需要
  example           text,                  -- 實際例子
  where_in_system   text,                  -- 系統裡在哪
  open_questions    text,                  -- 我還不懂的
  confidence        text NOT NULL
                    CHECK (confidence IN ('general_definition', 'code_inferred', 'colleague_confirmed')),
  confirmed_by      text,
  created_at        timestamptz NOT NULL DEFAULT now(),
  updated_at        timestamptz NOT NULL DEFAULT now(),
  search_text       text GENERATED ALWAYS AS (
    term || ' ' || plain_explanation || ' ' || coalesce(why_needed, '') || ' '
    || coalesce(example, '') || ' ' || coalesce(where_in_system, '') || ' '
    || coalesce(open_questions, '')
  ) STORED,
  CONSTRAINT concepts_confirmed_by_check
    CHECK ((confidence = 'colleague_confirmed') = (confirmed_by IS NOT NULL))
);
CREATE UNIQUE INDEX concepts_term_key ON concepts (lower(term));
CREATE INDEX concepts_search_idx ON concepts USING gin (search_text gin_trgm_ops);

-- 相關觀念是雙向的，固定小的 id 在前，避免 A-B、B-A 存兩筆。
CREATE TABLE concept_relations (
  concept_a integer NOT NULL REFERENCES concepts (id),
  concept_b integer NOT NULL REFERENCES concepts (id),
  PRIMARY KEY (concept_a, concept_b),
  CHECK (concept_a < concept_b)
);
CREATE INDEX concept_relations_b_idx ON concept_relations (concept_b);

CREATE TABLE entry_concepts (
  entry_id   integer NOT NULL REFERENCES entries (id),
  concept_id integer NOT NULL REFERENCES concepts (id),
  PRIMARY KEY (entry_id, concept_id)
);
CREATE INDEX entry_concepts_concept_idx ON entry_concepts (concept_id);
