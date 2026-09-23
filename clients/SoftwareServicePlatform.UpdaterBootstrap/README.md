# UpdaterBootstrap

用于更新正在运行的 `SoftwareServicePlatform.Updater.exe`。

完整安装包中固定放：

```text
updater/
├─ SoftwareServicePlatform.Updater.exe
└─ SoftwareServicePlatform.UpdaterBootstrap.exe
```

当某个目标版本需要更新 Updater 自身时，把新版 Updater 放入目标版本 ZIP：

```text
.updater-self/
└─ SoftwareServicePlatform.Updater.exe
```

业务文件更新完成后：

```text
旧 Updater
→ 启动 Bootstrap
→ 旧 Updater 退出
→ Bootstrap 替换 updater/SoftwareServicePlatform.Updater.exe
→ 删除 .updater-self
→ 重启业务软件
```

普通版本不需要包含 `.updater-self/`。
