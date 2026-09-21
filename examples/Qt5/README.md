# Qt 5.8 接入

本示例已经改为：**业务源码不再保存 `serverUrl / softwareCode / updateToken`**，统一从 `updater/updater.json` 读取。

## 1. 加入文件

把：

```text
AutoUpdateChecker.h
AutoUpdateChecker.cpp
```

加入 Qt 工程。

`.pro` 中确保：

```pro
QT += network

win32:LIBS += -lshell32
```

`shell32` 用于 Windows 下通过 Shell 启动 Updater，使 Updater 自己的管理员权限 manifest 可以正常触发 UAC。

## 2. 安装目录约定

```text
AppRoot/
├─ YourQtApplication.exe
├─ version.txt
└─ updater/
   ├─ SoftwareServicePlatform.Updater.exe
   └─ updater.json
```

把本目录的 `updater.json.example` 复制为：

```text
updater/updater.json
```

填写当前客户对应的 `serverUrl / softwareCode / updateToken`。

> 不要把真实 UpdateToken 写进 C++ 源码，也不要提交到 Git 仓库。

## 3. 读取 version.txt

推荐统一读取安装目录的 `version.txt`：

```cpp
QString readCurrentVersion()
{
    QFile file(
        QCoreApplication::applicationDirPath()
        + "/version.txt");

    if (!file.open(QIODevice::ReadOnly | QIODevice::Text))
    {
        return QStringLiteral("0.0.0");
    }

    return QString::fromUtf8(file.readAll()).trimmed();
}
```

## 4. 调用

```cpp
AutoUpdateChecker *checker =
    new AutoUpdateChecker(this);

checker->checkForUpdates(
    this,
    readCurrentVersion(),
    QCoreApplication::applicationDirPath()
        + "/updater/SoftwareServicePlatform.Updater.exe",
    QCoreApplication::applicationDirPath());
```

调用处已经不再出现：

```text
serverUrl
softwareCode
updateToken
```

这些全部由 `AutoUpdateChecker` 从同一个 `updater.json` 读取。

## 5. UAC

Updater 自己应在 manifest 中声明管理员权限。

Qt 的 `QProcess::startDetached()` 在 Windows 上不适合直接启动一个要求提升权限的 EXE，因此示例在 Windows 下改用 `ShellExecuteW(..., "open", ...)`。代码本身不写 `runas`，是否需要管理员权限由 Updater 自己的 manifest 决定。
