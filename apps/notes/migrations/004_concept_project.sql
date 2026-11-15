-- 觀念卡可以指定所屬專案，跟著那個專案的分享範圍同步。
-- 沒指定專案的觀念卡仍可透過連到的改動紀錄歸到專案；兩者都沒有就只留在筆記。
ALTER TABLE concepts ADD COLUMN project_id integer REFERENCES projects (id) ON DELETE SET NULL;
CREATE INDEX concepts_project_idx ON concepts (project_id);
