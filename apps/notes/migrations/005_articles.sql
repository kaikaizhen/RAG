-- 一般筆記：不限程式改動的長篇整理，例如「ERP 怎麼串起來」。
-- 一定屬於某個專案，才能跟著專案的分享範圍同步到問答；同一個專案裡用標題判斷是不是同一篇。
CREATE TABLE articles (
  id              integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
  project_id      integer NOT NULL REFERENCES projects (id),
  module_id       integer REFERENCES modules (id),
  title           text NOT NULL CHECK (btrim(title) <> ''),
  body            text NOT NULL CHECK (btrim(body) <> ''),
  open_questions  text,                     -- 還不懂、要去問人的地方
  confidence      text NOT NULL
                  CHECK (confidence IN ('general_definition', 'code_inferred', 'colleague_confirmed')),
  confirmed_by    text,
  created_at      timestamptz NOT NULL DEFAULT now(),
  updated_at      timestamptz NOT NULL DEFAULT now(),
  search_text     text GENERATED ALWAYS AS (title || ' ' || body || ' ' || coalesce(open_questions, '')) STORED,
  CONSTRAINT articles_confirmed_by_check
    CHECK ((confidence = 'colleague_confirmed') = (confirmed_by IS NOT NULL))
);
CREATE UNIQUE INDEX articles_project_title_key ON articles (project_id, lower(title));
CREATE INDEX articles_updated_idx ON articles (updated_at DESC);
CREATE INDEX articles_search_idx ON articles USING gin (search_text gin_trgm_ops);

CREATE TABLE article_concepts (
  article_id integer NOT NULL REFERENCES articles (id),
  concept_id integer NOT NULL REFERENCES concepts (id),
  PRIMARY KEY (article_id, concept_id)
);
CREATE INDEX article_concepts_concept_idx ON article_concepts (concept_id);
