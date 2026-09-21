# SoftwareServicePlatform.Updater

通用 Windows 桌面软件文件级增量更新器。

## 推荐部署目录

```text
YourApp/
├─ YourApp.exe
├─ *.dll
├─ version.txt
└─ updater/
   ├─ SoftwareServicePlatform.Updater.exe
   └─ updater.json
```

`updater/` 与 `version.txt` 默认不会被服务器更新 ZIP 覆盖。

## 发布 Updater

在本目录执行：

```powershell
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

生成目录类似：

```text
bin\Release\net10.0\win-x64\publish\
```

把 `SoftwareServicePlatform.Updater.exe` 放到业务软件的 `updater` 目录。

如果客户机器需要 32 位：

```powershell
dotnet publish -c Release -r win-x86 --self-contained true -p:PublishSingleFile=true
```

## updater.json

复制：

```text
updater.json.example
```

改名为：

```text
updater.json
```

填写：

- `serverUrl`：软件服务平台后端地址。
- `softwareCode`：平台 Software.Code。
- `updateToken`：管理员在“客户端更新”页面生成的 Token。
- `mainExecutable`：升级后需要重新启动的主程序。
- `versionFile`：默认 `version.txt`。

## 主程序启动 Updater

主程序确认用户要立即更新后：

```text
SoftwareServicePlatform.Updater.exe
  --config updater.json
  --app-root ..
  --wait-pid 当前主程序PID
```

然后主程序正常退出。

Updater 会：

1. 再次向服务器确认目标版本；
2. 比较本地 SHA256；
3. 只下载变化文件；
4. 等待主程序退出；
5. 备份旧文件；
6. 替换新文件；
7. 删除 `.update-delete.txt` 声明的废弃文件；
8. 再次校验 SHA256；
9. 写入 `version.txt`；
10. 自动重启主程序；
11. 任意一步失败时尽最大努力回滚；
12. 增量不可用/失败时可下载完整安装包兜底。

## 安全说明

`UpdateToken` 会保存在客户端，因此它不是“绝对秘密”。

它的安全边界是：

- 只能访问一个客户绑定的一款软件；
- 不能访问平台工单、用户、客户等 API；
- 服务器只保存 Token SHA256；
- 管理员可随时停用或重置；
- 正式环境必须使用 HTTPS。
