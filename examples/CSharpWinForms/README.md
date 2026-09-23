# C# WinForms .NET Framework 4.8 接入

本版本已经升级为：

```text
设备更新授权
+
设备独立 UpdateToken
+
自动更新
```

业务源码不再保存客户专属 `updateToken`，客户也不需要手工填写长期更新密钥。

这里的“更新授权”只决定当前设备能否使用平台自动更新，不影响业务软件正常启动和使用。

---

## 1. 加入文件

将以下文件加入现有 WinForms 工程：

```text
AutoUpdateHelper.cs
ActivationCodeDialog.cs
```

项目引用：

```text
System.Net.Http
System.Web.Extensions
System.Windows.Forms
System.Drawing
```

---

## 2. 安装目录约定

```text
AppRoot/
├─ YourApplication.exe
├─ version.txt
└─ updater/
   ├─ SoftwareServicePlatform.Updater.exe
   └─ updater.bootstrap.json
```

新安装包不要预置真实：

```text
updater.json
```

设备完成更新授权后，真正包含设备 UpdateToken 的配置会写到：

```text
%LOCALAPPDATA%\SoftwareServicePlatform\UpdaterConfigs\<softwareCode>\updater.json
```

这样即使软件安装在：

```text
C:\Program Files\...
```

普通用户也可以保存更新配置。

---

## 3. 配置 updater.bootstrap.json

复制：

```text
updater.bootstrap.json.example
```

改名为：

```text
updater/updater.bootstrap.json
```

示例：

```json
{
  "serverUrl": "https://ssp.cwb2405.cn",
  "softwareCode": "ROAD_PROCESS",
  "versionFile": "version.txt",
  "mainExecutable": "YourApplication.exe",
  "fallbackToFullInstaller": true,
  "restartAfterUpdate": true,
  "waitForProcessSeconds": 60
}
```

其中：

```text
serverUrl
softwareCode
```

不是客户密码，也不是敏感密钥，可以随安装包发布。

`softwareCode` 必须与平台中的 `Software.Code` 一致。

---

## 4. 第一次更新授权

客户在平台：

```text
我的软件
→ 获取更新激活码
```

然后启动桌面软件。

如果本机还没有设备级更新配置，会显示：

```text
设备更新授权

请输入一次性更新激活码
```

流程：

```text
输入一次性更新激活码
  ↓
POST /api/client-activation/activate
  ↓
服务器验证客户 + 软件授权
  ↓
创建 ClientInstallation
  ↓
为当前设备生成独立 UpdateToken
  ↓
保存 updater.json 到 LocalAppData
  ↓
继续检查更新
```

以后启动不再要求输入更新激活码。

更新激活码是一次性的，只用于给一台新设备开通自动更新能力。

---

## 5. MainForm_Shown

推荐版本号统一读取安装目录下的 `version.txt`：

```csharp
private string GetCurrentVersion()
{
    string versionFile =
        Path.Combine(
            Application.StartupPath,
            "version.txt");

    if (!File.Exists(versionFile))
    {
        return "0.0.0";
    }

    return File.ReadAllText(versionFile).Trim();
}
```

主窗口 `Shown`：

```csharp
private async void MainForm_Shown(
    object sender,
    EventArgs e)
{
    await AutoUpdateHelper.CheckOnStartupAsync(
        this,
        GetCurrentVersion(),
        Path.Combine(
            Application.StartupPath,
            "updater",
            "SoftwareServicePlatform.Updater.exe"),
        Application.StartupPath);
}
```

---

## 6. 设备级 UpdateToken

新的更新授权关系：

```text
CustomerSoftware
    ↓
ClientInstallation A → UpdateToken A
ClientInstallation B → UpdateToken B
ClientInstallation C → UpdateToken C
```

管理员或客户停用 B 的更新权限后：

```text
A 正常
B 无法继续检查 / 下载更新
C 正常
```

只影响 B。

业务软件本身仍然可以正常启动和使用。

---

## 7. 断网行为

自动更新是辅助能力。

已经完成更新授权的设备临时断网：

```text
业务软件正常启动
→ 更新检查失败 / 跳过
→ 不影响正常使用
```

第一次更新授权时没有网络：

```text
更新授权无法完成
→ 提示网络错误
→ 不应把业务软件锁死
```

---

## 8. Updater 配置传递

设备配置位于：

```text
%LOCALAPPDATA%\SoftwareServicePlatform\UpdaterConfigs\<softwareCode>\updater.json
```

主程序启动 Updater 时会显式传入：

```text
--config "<LocalAppData中的updater.json>"
```

所以 Updater 不要求 `updater.json` 位于自身 EXE 同目录。

---

## 9. UAC

业务程序不要硬编码：

```text
Verb = "runas"
```

`SoftwareServicePlatform.Updater.exe` 应由自身 manifest 声明管理员权限。

业务程序使用：

```text
UseShellExecute = true
```

启动 Updater，让 Windows 正常处理 UAC。

---

## 10. 安全注意事项

不要把真实的：

```text
UpdateToken
updater.json
```

提交到 Git 或直接打进通用安装包。

安装包只需要携带：

```text
SoftwareServicePlatform.Updater.exe
updater.bootstrap.json
```
