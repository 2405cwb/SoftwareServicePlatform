# C# WinForms .NET Framework 4.8 接入

将 `AutoUpdateHelper.cs` 加入现有 WinForms 工程。

项目引用：

```text
System.Net.Http
System.Web.Extensions
```

例如在主窗体 `Shown` 事件中：

```csharp
private async void MainForm_Shown(
    object sender,
    EventArgs e)
{
    await AutoUpdateHelper.CheckOnStartupAsync(
        this,
        "https://service.company.com",
        "ROAD_PROCESS",
        "ssp_upd_xxxxxxxxx",
        Application.ProductVersion,
        Path.Combine(
            Application.StartupPath,
            "updater",
            "SoftwareServicePlatform.Updater.exe"),
        Application.StartupPath);
}
```

如果你的程序集版本不等于业务发布版本，
建议不要使用 `Application.ProductVersion`，
而是统一读取安装目录的：

```text
version.txt
```

保持平台版本、客户端版本和 Updater 使用的是同一个版本号来源。
