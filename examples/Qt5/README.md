# Qt 5.8 接入

把：

```text
AutoUpdateChecker.h
AutoUpdateChecker.cpp
```

加入 Qt 工程。

`.pro` 中确保：

```pro
QT += network
```

应用启动后调用，例如：

```cpp
QCoreApplication::setApplicationVersion("1.2.0");

AutoUpdateChecker *checker =
    new AutoUpdateChecker(this);

checker->checkForUpdates(
    this,
    "https://service.company.com",
    "ROAD_PROCESS",
    "ssp_upd_xxxxxxxxx",
    QCoreApplication::applicationVersion(),
    QCoreApplication::applicationDirPath()
        + "/updater/SoftwareServicePlatform.Updater.exe",
    QCoreApplication::applicationDirPath());
```

推荐不要把真实 Token 写进 Git 仓库源码。

更适合的方式是安装/交付时写入：

```text
updater/updater.json
```

主程序检查更新时，可以从自己的客户配置或 `updater.json` 读取
`serverUrl / softwareCode / updateToken`，不要把真实 Token 编译进公共源码。

当前示例为了展示最小接入方式，直接把这三个值作为参数传入
`checkForUpdates()`；实际项目中建议由配置文件加载。
