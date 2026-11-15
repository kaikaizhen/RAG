#!/usr/bin/env bash
# 每日備份資料庫，由 cron 執行。備份放在 apps/notes/backups/（不進 git）。
# 只有檢查通過的檔案才會留下，所以半途失敗不會留下壞掉的備份，也不會刪到舊備份。
set -euo pipefail
export PATH=/usr/local/sbin:/usr/local/bin:/usr/sbin:/usr/bin:/sbin:/bin
umask 077

# 這支腳本在 apps/notes/scripts/，docker-compose.yml 在上一層
PROJECT_DIR="$(cd "$(dirname "$0")/.." && pwd)"
BACKUP_DIR="$PROJECT_DIR/backups"
KEEP_DAYS=14

mkdir -p "$BACKUP_DIR"
chmod 700 "$BACKUP_DIR"
cd "$PROJECT_DIR"

file="$BACKUP_DIR/learning_notes_$(date +%F_%H%M%S).sql.gz"
tmp="$file.tmp"
trap 'rm -f "$tmp"' EXIT

docker compose exec -T notes-db pg_dump -U notes -d learning_notes --no-owner | gzip > "$tmp"

gzip -t "$tmp"
if ! gzip -dc "$tmp" | tail -n 10 | grep -q 'PostgreSQL database dump complete'; then
  echo "$(date '+%F %T') 失敗：備份內容不完整" >&2
  exit 1
fi

mv "$tmp" "$file"
find "$BACKUP_DIR" -name 'learning_notes_*.sql.gz' -mtime +"$KEEP_DAYS" -delete
echo "$(date '+%F %T') 完成：$(basename "$file") $(du -h "$file" | cut -f1)"
