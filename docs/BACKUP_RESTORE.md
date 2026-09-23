# 生产数据备份 / 恢复

当前平台真正需要保护的是两部分：

```text
PostgreSQL
+
STORAGE_PATH
```

其中 `STORAGE_PATH` 保存安装包、自动更新包、工单附件、版本资料等文件。

## 备份

Linux 生产服务器：

```bash
chmod +x scripts/backup-production.sh

./scripts/backup-production.sh
```

默认输出：

```text
/opt/software-service-platform/backups/YYYYMMDD-HHMMSS/
├─ database.dump
├─ storage.tar.gz
└─ SHA256SUMS.txt
```

可以在 `.env` 或执行环境中设置：

```text
BACKUP_ROOT=/你的备份目录
```

## 恢复

```bash
chmod +x scripts/restore-production.sh

./scripts/restore-production.sh \
  /opt/software-service-platform/backups/20260923-120000
```

脚本要求手工输入：

```text
RESTORE
```

才会真正覆盖数据。

## 恢复演练

测试阶段至少完整做一次：

```text
备份
→ 恢复
→ 登录平台
→ 检查客户
→ 检查软件 / 版本
→ 检查工单
→ 检查下载文件
→ 检查自动更新包
```

没有真正恢复成功过的备份，不能算可靠备份。

## 定时任务示例

每天凌晨 03:30：

```cron
30 3 * * * /opt/software-service-platform/app/scripts/backup-production.sh >> /var/log/ssp-backup.log 2>&1
```

建议再定期把 `/opt/software-service-platform/backups` 复制到另一块磁盘或另一台服务器。
