# C# WinForms .NET Framework 4.8 接入

本版本已经升级为“设备级激活 + 设备独立 UpdateToken”。

业务源码不再保存客户专属 `updateToken`，客户也不需要手工填写密钥。

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

## 2. 安装目录约定

```text
AppRoot/
├─ YourApplication.exe
├─ version.txt
└─ updater/
   ├─ SoftwareServicePlatform.Updater.exe
   └─ updater.bootstrap.json
```

设备 UpdateToken 不写到 Program Files 安装目录。首次激活后会写到：

```text
%LOCALAPPDATA%\SoftwareServicePlatform\UpdaterConfigs\<softwareCode>\updater.json
```

其中：

- `updater.bootstrap.json` 可以直接打进所有客户共用的完整安装包；
- `updater.json` 不要预先打包，它会在客户首次激活后自动生成到当前 Windows 用户的 LocalAppData；
- 这样即使程序安装在 `C:\Program Files`，普通用户也可以完成首次激活；
- `updater.json` 包含设备自己的 UpdateToken，不要提交 Git。

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

`serverUrl` 和 `softwareCode` 不是客户密码，也不是敏感密钥，可以随安装包发布。

## 4. 客户第一次使用

客户流程：

```text
登录软件服务平台
→ 我的软件
→ 获取激活码
→ 启动桌面软件
→ 输入一次性激活码
→ 激活成功
```

客户端会自动调用：

```text
POST /api/client-activation/activate
```

服务器随后为这一台安装实例创建独立 UpdateToken，并返回给客户端。

客户端自动生成：

```text
%LOCALAPPDATA%\SoftwareServicePlatform\UpdaterConfigs\<softwareCode>\updater.json
```

主程序启动 Updater 时会显式传入 `--config`，所以 Updater 仍然可以读取这份配置。

以后启动不再要求输入激活码。

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

## 6. 设备级 Token

新的授权关系：

```text
CustomerSoftware
    ↓
ClientInstallation A → UpdateToken A
ClientInstallation B → UpdateToken B
ClientInstallation C → UpdateToken C
```

客户在网页“设备管理”中停用 B 后：

```text
A 正常
B 无法继续检查/下载更新
C 正常
```

不会影响其他设备。

## 7. UAC

业务程序不要硬编码 `Verb = "runas"`。

`SoftwareServicePlatform.Updater.exe` 应由自身 manifest 声明管理员权限。业务程序使用 `UseShellExecute = true` 启动 Updater，让 Windows 正常处理 UAC。
