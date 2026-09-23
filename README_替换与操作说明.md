# SoftwareServicePlatform：设备级更新凭证 + 用户修改密码

本修改包基于当前 `main` 的现有结构设计，目标包含两部分：

1. 客户端更新凭证从“客户 + 软件共享一个 Token”升级为“每个安装实例一个独立 Token”；
2. 所有登录用户都可以修改自己的密码，同时保留管理员现有的“重置用户密码”功能。

---

## 一、先创建 Git 分支

在仓库根目录执行：

```bash
git switch main
git pull origin main
git switch -c feat/client-device-activation-account-security
```

本次属于独立功能，不建议直接在 `main` 修改。

---

## 二、直接替换 / 新增这些文件

压缩包已经按照仓库目录结构放好，解压后可按相同路径覆盖。

### 后端

替换：

```text
backend/SoftwareServicePlatform.Api/Models/CustomerSoftware.cs
backend/SoftwareServicePlatform.Api/Services/ClientUpdates/ClientUpdateCredentialStore.cs
```

新增：

```text
backend/SoftwareServicePlatform.Api/Models/ClientInstallation.cs
backend/SoftwareServicePlatform.Api/Models/ClientActivationCode.cs
backend/SoftwareServicePlatform.Api/Services/ClientUpdates/ClientActivationCodeService.cs
backend/SoftwareServicePlatform.Api/Controllers/ClientActivationController.cs
backend/SoftwareServicePlatform.Api/Controllers/AccountController.cs
backend/SoftwareServicePlatform.Api/Dtos/Auth/ChangePasswordRequest.cs
```

### React 前端

替换：

```text
forntend/src/App.tsx
forntend/src/pages/MySoftwarePage.tsx
```

新增：

```text
forntend/src/components/AccountShell.tsx
forntend/src/pages/ChangePasswordPage.tsx
```

### C# WinForms 接入示例

替换：

```text
examples/CSharpWinForms/AutoUpdateHelper.cs
examples/CSharpWinForms/README.md
```

新增：

```text
examples/CSharpWinForms/ActivationCodeDialog.cs
examples/CSharpWinForms/updater.bootstrap.json.example
```

---

## 三、数据库迁移必须执行

这次新增了两个持久化实体，对应数据库表名以 EF Migration 实际生成为准：

```text
ClientInstallation
ClientActivationCode
```

其中：

```text
CustomerSoftware
    ↓ 1:N
ClientInstallations
```

每一个 `ClientInstallation` 都有独立的 `TokenHash`。

在：

```text
backend/SoftwareServicePlatform.Api
```

执行：

```bash
dotnet ef migrations add AddClientDeviceActivation
dotnet build
```

本地开发数据库需要立即更新时再执行：

```bash
dotnet ef database update
```

你的线上程序启动时已经执行 `Database.MigrateAsync()`，所以正式部署时只要把新 Migration 一起提交并重新部署，容器启动会自动执行迁移。

生成 Migration 后，记得把 Migration 文件和 `AppDbContextModelSnapshot.cs` 一起提交 Git。

---

## 四、新的客户激活流程

新的正式流程是：

```text
管理员
    ↓
给客户绑定软件（CustomerSoftware）
    ↓
客户登录网页
    ↓
“我的软件”
    ↓
“获取激活码”
    ↓
获得一次性激活码（24小时有效）
    ↓
新安装的软件第一次启动
    ↓
客户输入一次性激活码
    ↓
POST /api/client-activation/activate
    ↓
服务器创建 ClientInstallation
    ↓
为这一台安装实例生成独立 UpdateToken
    ↓
客户端自动保存 updater.json
```

客户不再填写：

```text
serverUrl
softwareCode
updateToken
```

客户真正需要人工输入的只有“一次性激活码”。

---

## 五、为什么仍然保留旧版共享 Token

当前仓库已经存在一些按：

```text
客户 + 软件 = 一个共享 UpdateToken
```

部署的客户端。

这次没有直接删除旧表和旧接口，原因是避免更新后老客户突然全部无法检查更新。

`ClientUpdateCredentialStore` 现在查询顺序是：

```text
设备独立 Token
    ↓ 找不到
旧版共享 Token
```

因此：

- 新安装的软件：统一走设备级 Token；
- 已经部署出去的老软件：暂时还能继续使用原 Token；
- 等某个客户的旧设备全部重新激活以后，可以在原“客户端更新”页面把旧共享 Token 停用。

后续确认所有客户已经迁移后，再单独做一次“移除旧版共享凭证”的清理即可。

---

## 六、设备管理

客户“我的软件”页面新增：

```text
[获取激活码]
[设备管理]
```

设备管理可以看到：

```text
设备名称
安装实例 ID
激活时间
最近检查更新时间
Token 前缀
当前状态
```

客户停用某一台设备时：

```text
电脑 A → 正常
电脑 B → 停用
电脑 C → 正常
```

只影响 B，不会导致整家公司其他电脑的 Token 一起失效。

本版暂时没有增加“最大允许设备数”，即客户可以激活多台设备。如果后续合同需要限制 3 台、5 台、10 台，再给 `CustomerSoftware` 增加 `MaxDeviceCount` 即可。

---

## 七、WinForms 安装包需要增加什么

完整安装包仍然是通用的 EXE / MSI，不需要为每个客户重新打包。

每一款软件只需要在自己的安装目录：

```text
updater/
```

中放：

```text
SoftwareServicePlatform.Updater.exe
updater.bootstrap.json
```

`updater.bootstrap.json` 只包含非敏感信息，例如：

```json
{
  "serverUrl": "https://ssp.example.com",
  "softwareCode": "ROAD_PROCESS",
  "versionFile": "version.txt",
  "mainExecutable": "YourApplication.exe",
  "fallbackToFullInstaller": true,
  "restartAfterUpdate": true,
  "waitForProcessSeconds": 60
}
```

其中 `softwareCode` 每款软件固定，不同客户不需要修改。

设备激活后的真正 `updater.json` 会写到：

```text
%LOCALAPPDATA%\SoftwareServicePlatform\UpdaterConfigs\<softwareCode>\updater.json
```

这样即使软件安装在：

```text
C:\Program Files\...
```

普通用户也不会因为没有目录写权限而导致首次激活失败。

主程序启动 Updater 时会自动传：

```text
--config <LocalAppData中的updater.json>
```

---

## 八、修改密码

所有已登录账号：

```text
Admin
Support
Developer
Sales
Customer
```

都会看到“修改密码”入口。

修改密码时必须输入：

```text
当前密码
新密码
确认新密码
```

后端接口：

```text
POST /api/account/change-password
```

规则：

```text
新密码至少8位
必须验证当前密码
两次新密码必须一致
新密码不能与当前密码相同
```

修改成功后前端会主动退出登录，要求使用新密码重新登录。

---

## 九、管理员重置密码

你当前 `main` 中已经存在管理员重置密码能力，因此本包没有重复修改这一部分。

现有后端接口：

```text
POST /api/users/{id}/reset-password
```

现有“用户管理”页面也已经有“重置密码”按钮。

所以最终效果是：

```text
普通用户 / 开发 / 售后 / 销售 / 客户
→ 自己修改自己的密码

管理员
→ 自己也可以修改密码
→ 同时可以给其他账号重置密码
```

---

## 十、本地验证顺序

建议按这个顺序测试：

```text
1. 生成并执行 EF Migration
2. 启动后端
3. 启动前端
4. 用 Customer 登录
5. 我的软件 → 获取激活码
6. 我的软件 → 设备管理，此时应为空
7. 用 WinForms 示例输入激活码
8. 再刷新设备管理，应出现当前电脑
9. 检查更新，应正常更新 LastUsedAt
10. 停用这台设备
11. 客户端再次检查更新，应被拒绝
12. 重新获取一个激活码，重新激活
13. 用 Developer / Support 登录测试“修改密码”
14. 用 Admin 在用户管理中测试“重置密码”
```

---

## 十一、构建验证

后端：

```bash
cd backend/SoftwareServicePlatform.Api
dotnet build
```

前端：

```bash
cd forntend
npm run build
```

两个都通过以后再提交：

```bash
git status
git add .
git commit -m "feat: add device activation and account password management"
git push -u origin feat/client-device-activation-account-security
```

然后创建 PR 合并到 `main`。

---

## 十二、暂时不做的内容

本次先把核心链路做通，没有同时加入：

```text
设备数量上限
设备改名
管理员统一设备管理页
激活码生成频率限制
忘记密码 / 邮箱找回
强制首次登录修改管理员初始密码
修改密码后立即吊销其他设备上的 JWT
```

这些都可以在当前结构上继续扩展，不需要再推翻设备级 Token 设计。
