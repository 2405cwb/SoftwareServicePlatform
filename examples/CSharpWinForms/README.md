# C# WinForms .NET Framework 4.8 接入

本示例已经改为：**业务源码不再保存 `serverUrl / softwareCode / updateToken`**，统一从 `updater/updater.json` 读取。

## 1. 加入文件

将 `AutoUpdateHelper.cs` 加入现有 WinForms 工程。

项目引用：

```text
System.Net.Http
System.Web.Extensions
```

## 2. 安装目录约定

```text
AppRoot/
├─ YourApplication.exe
├─ version.txt
└─ updater/
   ├─ SoftwareServicePlatform.Updater.exe
   └─ updater.json
```

把本目录的 `updater.json.example` 复制为：

```text
updater/updater.json
```

然后填写当前客户对应的 `serverUrl / softwareCode / updateToken`。

> `UpdateToken` 是“客户 + 软件”的低权限更新凭证，不要提交到 Git 仓库。

## 3. MainForm_Shown

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

这里已经不再出现 Token，也不再出现服务器地址和软件编码。

## 4. UAC

业务程序不要硬编码 `Verb = "runas"`。

`SoftwareServicePlatform.Updater.exe` 应由自身 manifest 声明管理员权限。C# 示例使用 `UseShellExecute = true` 启动 Updater，从而让 Windows 正常处理 Updater 自己的 UAC 声明。

Updater 启动成功后，业务程序正常退出，避免 EXE / DLL 被锁定。
