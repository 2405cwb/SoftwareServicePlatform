# Qt 5.8 客户端接入：设备激活 + 自动更新

本示例与 C# WinForms 示例使用同一套设备级授权逻辑：

```text
客户 + 软件授权（CustomerSoftware）
        ↓
一次性激活码
        ↓
当前安装实例（ClientInstallation）
        ↓
本设备独立 UpdateToken
```

客户不再手工填写 `UpdateToken`。

---

## 1. 加入 Qt 工程的文件

把下面 4 个源码文件加入现有 Qt 5.8 工程：

```text
AutoUpdateChecker.h
AutoUpdateChecker.cpp
ActivationCodeDialog.h
ActivationCodeDialog.cpp
```

`.pro` 中至少确保：

```pro
QT += widgets network

HEADERS += \
    AutoUpdateChecker.h \
    ActivationCodeDialog.h

SOURCES += \
    AutoUpdateChecker.cpp \
    ActivationCodeDialog.cpp

win32:LIBS += -lshell32
```

`shell32` 用于 Windows 下通过 Shell 启动独立 Updater，使 Updater 自身的管理员权限 manifest 可以正常触发 UAC。

---

## 2. 新安装包的目录结构

完整安装包仍然是所有客户共用的同一个 EXE / MSI，不需要给不同客户重新打包。

建议目录：

```text
AppRoot/
├─ YourQtApplication.exe
├─ version.txt
└─ updater/
   ├─ SoftwareServicePlatform.Updater.exe
   └─ updater.bootstrap.json
```

注意：**新安装包不要预置 `updater.json`。**

真正包含设备 UpdateToken 的 `updater.json` 会在首次激活成功后自动生成。

---

## 3. updater.bootstrap.json

把：

```text
updater.bootstrap.json.example
```

复制为：

```text
updater/updater.bootstrap.json
```

开发环境示例：

```json
{
  "serverUrl": "http://localhost:5108",
  "softwareCode": "ROAD_PROCESS",
  "versionFile": "version.txt",
  "mainExecutable": "YourQtApplication.exe",
  "fallbackToFullInstaller": true,
  "restartAfterUpdate": true,
  "waitForProcessSeconds": 60
}
```

其中：

```text
serverUrl
```

应该填写 API 根地址，不是 Vite 前端开发端口。

本地开发通常是：

```text
http://localhost:5108
```

正式环境 HTTPS 配置完成后建议使用：

```text
https://ssp.cwb2405.cn
```

`softwareCode` 必须与平台数据库中的 `Software.Code` 完全对应。

---

## 4. 首次激活流程

第一次启动时，如果本机还没有设备配置：

```text
启动 Qt 软件
    ↓
读取 updater.bootstrap.json
    ↓
弹出“软件首次激活”窗口
    ↓
客户输入一次性激活码
    ↓
POST /api/client-activation/activate
    ↓
服务器创建 ClientInstallation
    ↓
服务器返回本设备独立 UpdateToken
    ↓
客户端自动保存 updater.json
    ↓
继续检查更新
```

客户只需要接触一次性激活码，不会看到长期 UpdateToken。

---

## 5. updater.json 保存位置

新版 Qt 示例与 C# 示例保持一致，真正的设备配置保存到：

```text
%LOCALAPPDATA%\SoftwareServicePlatform\UpdaterConfigs\<softwareCode>\updater.json
```

例如：

```text
C:\Users\cwb\AppData\Local\SoftwareServicePlatform\UpdaterConfigs\XRDataProcess\updater.json
```

这样软件即使安装在：

```text
C:\Program Files\...
```

普通用户也能完成首次激活，不会因为安装目录无写权限失败。

---

## 6. 读取 version.txt

推荐所有业务软件统一从安装目录读取：

```cpp
QString readCurrentVersion()
{
    QFile file(
        QCoreApplication::applicationDirPath()
        + "/version.txt");

    if (!file.open(
            QIODevice::ReadOnly
            | QIODevice::Text))
    {
        return QStringLiteral("0.0.0");
    }

    return QString::fromUtf8(
        file.readAll())
        .trimmed();
}
```

---

## 7. 主窗口中调用

例如主窗口显示完成后：

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

业务源码中不需要出现：

```text
serverUrl
softwareCode
updateToken
```

其中前两个来自 `updater.bootstrap.json`，设备 Token 由首次激活自动生成。

---

## 8. 启动 Updater 时的变化

新版会显式传入：

```text
--config "C:\Users\...\AppData\Local\...\updater.json"
--app-root "C:\Program Files\YourApp"
--wait-pid 1234
```

因此独立 Updater 不要求 `updater.json` 必须放在自己的 EXE 同目录。

---

## 9. 旧客户端兼容

如果老版本目录里仍然存在：

```text
updater/updater.json
```

并且里面是原来的“客户 + 软件共享 Token”，新版 Qt 示例仍然会继续读取它。

读取顺序为：

```text
LocalAppData 设备级 updater.json
        ↓ 找不到
安装目录旧版 updater/updater.json
        ↓ 找不到
首次激活
```

因此可以逐步迁移，不要求已经部署出去的旧客户端当天全部重新激活。

---

## 10. 旧 updater.json.example

仓库中的 `updater.json.example` 仅保留用于说明旧版格式和迁移，不要复制进新的正式安装包。

新安装包只需要：

```text
SoftwareServicePlatform.Updater.exe
updater.bootstrap.json
```

---

## 11. Qt 5.8 兼容说明

本示例只使用 Qt 5.8 已具备的常用模块/API：

```text
QtCore
QtWidgets
QtNetwork
QJsonDocument / QJsonObject
QSaveFile
QStandardPaths
ShellExecuteW（Windows）
```

没有依赖 Qt 6 API。
