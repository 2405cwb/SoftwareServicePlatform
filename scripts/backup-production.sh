#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="${1:-/opt/software-service-platform/app}"

cd "$ROOT_DIR"

if [[ ! -f ".env" ]]; then
  echo "找不到 $ROOT_DIR/.env"
  exit 1
fi

set -a
source .env
set +a

STAMP="$(date +%Y%m%d-%H%M%S)"
BACKUP_ROOT="${BACKUP_ROOT:-/opt/software-service-platform/backups}"
TARGET="$BACKUP_ROOT/$STAMP"

mkdir -p "$TARGET"

echo "[1/3] 备份 PostgreSQL..."

docker compose \
  -f compose.prod.yaml \
  --env-file .env \
  exec -T postgres \
  pg_dump \
  -U "$POSTGRES_USER" \
  -d "$POSTGRES_DB" \
  -Fc \
  > "$TARGET/database.dump"

echo "[2/3] 备份 storage..."

if [[ -d "$STORAGE_PATH" ]]; then
  tar \
    -C "$(dirname "$STORAGE_PATH")" \
    -czf "$TARGET/storage.tar.gz" \
    "$(basename "$STORAGE_PATH")"
else
  echo "警告：STORAGE_PATH 不存在：$STORAGE_PATH"
fi

echo "[3/3] 生成 SHA256..."

(
  cd "$TARGET"
  sha256sum database.dump > SHA256SUMS.txt

  if [[ -f storage.tar.gz ]]; then
    sha256sum storage.tar.gz >> SHA256SUMS.txt
  fi
)

echo
echo "备份完成：$TARGET"
echo "建议定期把备份复制到另一块磁盘或另一台机器。"
