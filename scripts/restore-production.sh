#!/usr/bin/env bash
set -euo pipefail

if [[ $# -lt 1 ]]; then
  echo "用法：$0 <备份目录> [项目目录]"
  exit 1
fi

BACKUP_DIR="$(readlink -f "$1")"
ROOT_DIR="${2:-/opt/software-service-platform/app}"

cd "$ROOT_DIR"

if [[ ! -f ".env" ]]; then
  echo "找不到 $ROOT_DIR/.env"
  exit 1
fi

set -a
source .env
set +a

if [[ ! -f "$BACKUP_DIR/database.dump" ]]; then
  echo "缺少 $BACKUP_DIR/database.dump"
  exit 1
fi

echo "即将恢复：$BACKUP_DIR"
echo "这会覆盖当前数据库和 STORAGE_PATH。"
echo

read -r -p "请输入 RESTORE 确认：" CONFIRM

if [[ "$CONFIRM" != "RESTORE" ]]; then
  echo "已取消"
  exit 1
fi

echo "[1/4] 停止 API / 前端..."

docker compose \
  -f compose.prod.yaml \
  --env-file .env \
  stop api frontend

echo "[2/4] 重建数据库..."

docker compose \
  -f compose.prod.yaml \
  --env-file .env \
  exec -T postgres \
  dropdb \
  -U "$POSTGRES_USER" \
  --if-exists \
  "$POSTGRES_DB"

docker compose \
  -f compose.prod.yaml \
  --env-file .env \
  exec -T postgres \
  createdb \
  -U "$POSTGRES_USER" \
  "$POSTGRES_DB"

cat "$BACKUP_DIR/database.dump" \
  | docker compose \
      -f compose.prod.yaml \
      --env-file .env \
      exec -T postgres \
      pg_restore \
      -U "$POSTGRES_USER" \
      -d "$POSTGRES_DB" \
      --no-owner \
      --no-privileges

echo "[3/4] 恢复 storage..."

if [[ -f "$BACKUP_DIR/storage.tar.gz" ]]; then
  rm -rf "$STORAGE_PATH"
  mkdir -p "$(dirname "$STORAGE_PATH")"

  tar \
    -C "$(dirname "$STORAGE_PATH")" \
    -xzf "$BACKUP_DIR/storage.tar.gz"
fi

echo "[4/4] 启动服务..."

docker compose \
  -f compose.prod.yaml \
  --env-file .env \
  up -d

echo
echo "恢复完成。"
