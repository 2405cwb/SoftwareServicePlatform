# SoftwareServicePlatform

> 软件服务管理平台 —— 面向软件交付、版本发布、客户端升级、客户服务与工单协同的一体化管理平台。

SoftwareServicePlatform 用于统一管理公司客户、软件产品、软件版本、安装包、客户端自动更新、下载记录、工单、SLA 和通知流程，逐步替代依赖微信、网盘、FTP 和人工记录的软件交付与售后方式。

当前项目已形成从 **客户授权 → 软件发布 → 客户下载 / 自动升级 → 问题反馈 → 工单处理 → SLA → 实时通知** 的基础业务闭环。

> 当前状态：持续开发 / 内部试用阶段  
> 更新日期：2026-09-21

---

## 1. 项目背景

传统的软件交付和售后流程中，常见问题包括：

- 软件通过微信、网盘、FTP 等多个渠道发送
- 客户使用的软件版本不统一
- 很难确认某个客户当前使用哪个版本
- 安装包和更新包缺少统一管理
- 客户升级过程不可追踪
- 售后问题主要依赖微信沟通
- 问题处理过程缺少统一记录
- 开发、售后、销售之间的信息容易断层
- 无法准确统计下载、升级、工单、响应时间等数据

SoftwareServicePlatform 希望将这些流程统一到一个平台中。

```text
客户
  ↓
客户软件授权
  ↓
软件 / 软件版本
  ↓
版本发布
  ↓
安装包下载 / 客户端自动更新
  ↓
下载与升级记录
  ↓
客户问题反馈
  ↓
Ticket 工单
  ↓
分诊 / 指派 / 处理
  ↓
SLA 跟踪
  ↓
站内通知 / 外部通知
  ↓
问题关闭
```

---

## 2. 技术栈

### 前端

- React 19
- TypeScript
- Vite
- React Router
- SignalR Client
- ECharts
- Lucide React

### 后端

- .NET 10
- ASP.NET Core Web API
- Entity Framework Core 10
- ASP.NET Core JWT Authentication
- ASP.NET Core SignalR
- ASP.NET Core Identity PasswordHasher
- Npgsql

### 数据库

- PostgreSQL

### Windows 自动更新器

- .NET 10 Windows
- WinForms
- Self-contained Publish
- Single File
- UAC `requireAdministrator`
- SHA256 文件校验
- 文件级增量更新
- 自动备份与失败回滚

### 开发工具

- Visual Studio 2022
- Visual Studio Code
- DBeaver
- Git
- GitHub

---

## 3. 系统架构

```text
┌─────────────────────────────┐
│        React Frontend       │
│  Dashboard / Customer / ... │
└──────────────┬──────────────┘
               │ HTTP / SignalR
               ↓
┌─────────────────────────────┐
│      ASP.NET Core API       │
│ Auth / Ticket / Update /... │
└──────────────┬──────────────┘
               │ EF Core
               ↓
┌─────────────────────────────┐
│         PostgreSQL          │
└─────────────────────────────┘

                 ↑
                 │ X-Update-Token
                 │
┌─────────────────────────────┐
│ Windows Application Client  │
│           +                 │
│ SoftwareServicePlatform     │
│          Updater            │
└─────────────────────────────┘
```

---

## 4. 当前核心功能

### 4.1 客户管理

支持：

- 客户新增、编辑、删除
- 客户启用 / 停用
- 客户编码
- 客户类型
- 行业
- 省市信息
- 联系人
- 联系电话
- 邮箱
- 销售负责人
- 售后负责人
- 备注
- 客户与软件授权关系

---

### 4.2 软件管理

支持：

- 软件新增、编辑、删除
- 软件编码
- 软件名称
- 软件状态
- 软件与多个版本关联
- 软件与多个客户授权关联

关系：

```text
Software
  ├─ SoftwareVersion
  └─ CustomerSoftware
```

---

### 4.3 软件版本管理

支持：

- 创建软件版本
- 编辑版本信息
- Release / Beta / Dev 等版本类型
- 发布状态管理
- 发布说明
- 安装包上传
- 安装包下载
- 发布客户范围控制
- 指定客户发布
- 全部客户发布

同一软件可以维护多个历史版本。

---

## 5. 客户软件授权

通过：

```text
CustomerSoftware
```

建立客户和软件之间的授权关系。

```text
Customer
    ↓
CustomerSoftware
    ↓
Software
```

数据库对：

```text
CustomerId + SoftwareId
```

建立唯一约束，防止重复绑定。

客户只能访问自己已授权的软件。

---

## 6. 客户门户

Customer 用户登录后可进入：

```text
/my-software
```

查看：

```text
当前客户
  ↓
已授权软件
  ↓
可用正式版本
  ↓
安装包
```

客户身份由 JWT 中的 CustomerId 和服务器数据库共同校验，不能通过修改前端参数访问其他客户数据。

---

## 7. 用户与权限系统

当前角色：

| Role | 说明 |
| --- | --- |
| Admin | 系统管理员 |
| Support | 售后人员 |
| Developer | 开发人员 |
| Sales | 销售人员 |
| Customer | 客户用户 |

系统同时进行：

```text
前端路由权限
+
后端 Authorize / Roles 权限
+
数据库业务权限校验
```

主要权限示例：

```text
Admin
├─ 客户管理
├─ 软件管理
├─ 版本管理
├─ 客户端更新
├─ 下载 / 更新记录
├─ 工单管理
├─ 用户管理
├─ SLA 设置
└─ 通知策略

Support
├─ 客户管理
├─ 软件管理
├─ 版本管理
├─ 下载 / 更新记录
└─ 工单管理

Developer
├─ 软件管理
├─ 版本管理
├─ 客户端更新
└─ 工单管理

Sales
├─ 客户管理
└─ 工作台

Customer
├─ 我的软件
├─ 工单管理
└─ 工作台
```

---

## 8. 登录认证

系统使用 JWT Authentication。

基本流程：

```text
用户名 + 密码
  ↓
POST /api/auth/login
  ↓
PasswordHasher 验证
  ↓
用户 / 客户状态检查
  ↓
签发 JWT
  ↓
前端 sessionStorage 保存
  ↓
apiFetch 自动附加 Authorization
```

JWT 中包含用户身份、角色以及 CustomerId 等业务信息。

---

## 9. 客户端自动更新

客户端自动更新是当前平台的重要功能之一。

后台入口：

```text
/client-updates
```

### 9.1 更新授权

每一个：

```text
CustomerSoftware
```

都可以生成独立的 UpdateToken。

请求通过：

```http
X-Update-Token: ssp_upd_xxx
```

进行认证。

数据库不会保存明文 Token，只保存：

```text
TokenHash
TokenPrefix
IsEnabled
CreatedAt
UpdatedAt
LastUsedAt
```

支持：

- 生成 Token
- 重置 Token
- 启用 Token
- 撤销 Token
- 查看最近使用时间

Token 数据已经正式纳入 EF Core Entity + Migration 管理。

---

### 9.2 文件级增量更新

平台的“增量更新”不是传统二进制 Patch，而是：

> 上传目标版本完整目录，服务端生成目标版本文件清单，客户端根据 SHA256 比较后只下载发生变化的文件。

流程：

```text
上传目标版本 ZIP
  ↓
服务端安全解压
  ↓
扫描目标文件
  ↓
计算每个文件 SHA256
  ↓
生成 manifest.json
  ↓
客户端检查版本
  ↓
比较本地 SHA256
  ↓
只下载变化 / 缺失文件
  ↓
备份旧文件
  ↓
替换文件
  ↓
再次校验 SHA256
  ↓
写入 version.txt
  ↓
重新启动主程序
```

例如：

```text
目标版本：1309 个文件
本地真正变化：14 个文件

最终只下载 14 个文件
```

---

### 9.3 更新包控制文件

支持：

```text
.update-ignore.txt
.update-delete.txt
```

`.update-ignore.txt` 用于指定不参与更新的文件。

`.update-delete.txt` 用于声明新版本中需要删除的旧文件。

默认受保护路径包括：

```text
updater/**
.update-temp/**
.update-backup/**
version.txt
.update-ignore.txt
.update-delete.txt
```

当前 Updater 不通过普通增量更新覆盖自身。

---

### 9.4 ZIP 安全保护

服务端对更新 ZIP 做安全检查，包括：

- Zip Slip / Path Traversal 防护
- Windows 大小写路径冲突检测
- 重复文件路径检测
- 更新清单与删除清单冲突检查
- ZIP 文件数量限制
- 单文件解压大小限制
- ZIP 总解压大小限制
- 实际解压字节再次校验

当前默认限制：

```text
最大文件数量：20000
单文件最大解压大小：4 GB
总解压大小：20 GB
```

---

## 10. Windows Updater

仓库包含独立 Windows Updater：

```text
clients/
└─ SoftwareServicePlatform.Updater/
```

Updater 当前已经使用 WinForms 图形界面，而不是控制台黑窗口。

支持显示：

```text
当前版本
目标版本
正在检查本地文件
正在下载更新文件
正在安装更新
当前处理文件
文件数量
百分比
更新完成状态
```

核心能力包括：

- UAC 管理员权限
- WinForms 更新进度窗口
- DPI 缩放适配
- SHA256 校验
- 文件级增量下载
- 等待主程序退出
- 更新前文件备份
- 更新失败自动回滚
- 成功后写入 `version.txt`
- 成功后自动重启主程序
- 增量失败可回退完整安装包
- `updater.log` 错误日志
- 错误 MessageBox

推荐目录：

```text
AppRoot/
├─ MainApp.exe
├─ version.txt
└─ updater/
   ├─ SoftwareServicePlatform.Updater.exe
   ├─ updater.json
   └─ updater.log
```

`updater.json` 示例：

```json
{
  "serverUrl": "https://service.example.com",
  "softwareCode": "YourSoftwareCode",
  "updateToken": "YOUR_UPDATE_TOKEN",
  "versionFile": "version.txt",
  "mainExecutable": "YourSoftware.exe",
  "fallbackToFullInstaller": true,
  "restartAfterUpdate": true,
  "waitForProcessSeconds": 60
}
```

> 不要将真实 UpdateToken 提交到公开 Git 仓库。

---

## 11. 客户端接入示例

仓库提供客户端接入示例：

```text
examples/
├─ CSharpWinForms/
└─ Qt5/
```

用于展示已有桌面软件如何：

```text
启动
  ↓
读取 version.txt
  ↓
调用平台检查更新接口
  ↓
提示用户
  ↓
启动 Updater
  ↓
退出主程序
```

平台自动更新设计尽量与业务软件解耦，使 WinForms、Qt 等现有 Windows 桌面软件都能复用同一套 Updater。

---

## 12. 下载 / 更新记录

平台统一记录：

```text
ManualPackage
AutoIncremental
AutoFullPackage
```

即：

- 客户手工下载安装包
- 客户端自动增量更新
- 自动完整安装包更新

记录内容包括：

```text
客户
软件
来源
旧版本
目标版本
更新方式
文件数量
实际下载大小
状态
错误信息
时间
```

自动更新状态：

```text
Started
Success
Failed
```

更新记录采用“每次升级尝试一条记录”，不会因为一个版本下载多个文件而产生大量记录。

---

## 13. Ticket 工单系统

当前已经实现工单主流程。

支持：

- Customer 客户创建工单
- 内部人员代客户创建工单
- 工单编号
- 客户与软件关联
- 搜索和状态筛选
- 优先级
- 来源
- 分诊
- 指派处理人
- 公开回复
- 内部处理记录
- 附件
- 解决
- 关闭
- 重新打开
- 客户权限隔离

基本流程：

```text
客户提交问题
  ↓
Pending
  ↓
售后分诊
  ↓
设置优先级
  ↓
分配处理人
  ↓
Processing
  ↓
处理记录 / 附件
  ↓
Resolved
  ↓
Closed
```

Developer、Support、Admin 和 Customer 根据各自权限进入工单页面。

---

## 14. SLA

系统已经支持工单 SLA 配置。

管理员可以在：

```text
/sla-settings
```

维护不同优先级的：

```text
首次响应目标时间
解决目标时间
是否启用
```

创建工单时会保存 SLA 快照，因此以后管理员修改规则，不会影响已经创建的历史工单。

系统可判断：

```text
响应是否超时
解决是否超时
```

并由后台服务进行相关通知处理。

---

## 15. 通知系统

平台已经实现站内通知体系。

包括：

- Notification 数据模型
- 未读数量
- 通知列表
- 单条已读
- 全部已读
- SignalR 实时推送
- SignalR 自动重连
- 重连后未读数同步
- 通知点击跳转目标页面

前端右上角提供通知中心。

---

## 16. 通知策略与外部通知

管理员可以在：

```text
/notification-settings
```

维护通知策略。

当前通知架构已抽象为：

```text
业务事件
  ↓
NotificationEventService
  ↓
NotificationPolicy
  ↓
RecipientResolver
  ↓
站内通知
+
ExternalNotificationService
```

后端当前已经接入外部通知 Sender 架构，并支持钉钉配置。

设计上可继续扩展：

```text
DingTalk
WeCom
Feishu
Email
...
```

业务 Controller 不需要直接依赖具体外部平台。

---

## 17. Dashboard 工作台

系统提供：

```text
/dashboard
```

五类角色均有工作台入口。

工作台用于汇总：

- 客户
- 软件
- 版本
- 下载 / 更新
- 工单
- 待办
- SLA
- 当前用户相关业务数据

不同角色读取不同范围的数据。

---

## 18. 平台品牌配置

平台标题和公司名称通过后端配置提供。

例如：

```json
"Platform": {
  "Title": "软件服务管理平台",
  "CompanyName": "XX科技有限公司"
}
```

前端会动态应用到：

- 登录页面
- 后台 Header
- 浏览器标题
- 公司名称显示

可以在不修改 React 源码的情况下进行不同公司的基础品牌部署。

---

## 19. 项目结构

```text
SoftwareServicePlatform/
│
├─ backend/
│  └─ SoftwareServicePlatform.Api/
│     ├─ Controllers/
│     ├─ Data/
│     ├─ Dtos/
│     ├─ Hubs/
│     ├─ Migrations/
│     ├─ Models/
│     ├─ Services/
│     │  ├─ ClientUpdates/
│     │  ├─ ExternalNotifications/
│     │  └─ NotificationPolicies/
│     ├─ Program.cs
│     └─ appsettings.json
│
├─ forntend/
│  ├─ src/
│  │  ├─ components/
│  │  ├─ hooks/
│  │  ├─ layouts/
│  │  ├─ pages/
│  │  ├─ services/
│  │  ├─ styles/
│  │  ├─ utils/
│  │  └─ App.tsx
│  ├─ package.json
│  └─ vite.config.ts
│
├─ clients/
│  └─ SoftwareServicePlatform.Updater/
│
├─ examples/
│  ├─ CSharpWinForms/
│  └─ Qt5/
│
├─ docs/
├─ .gitignore
└─ README.md
```

> 前端目录因为项目早期历史原因仍命名为 `forntend`，暂时保持不变。

---

## 20. 本地开发

### 环境要求

```text
.NET SDK 10
Node.js 24+
npm
PostgreSQL
Git
```

推荐：

```text
Visual Studio 2022
Visual Studio Code
DBeaver
```

### 克隆项目

```bash
git clone https://github.com/2405cwb/SoftwareServicePlatform.git
cd SoftwareServicePlatform
```

---

## 21. 启动后端

```bash
cd backend/SoftwareServicePlatform.Api
dotnet restore
dotnet run
```

当前开发配置通常使用：

```text
https://localhost:7104
http://localhost:5108
```

---

## 22. PostgreSQL 配置

推荐将开发机数据库配置放入：

```text
appsettings.Development.json
```

示例：

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=software_service_platform;Username=postgres;Password=YOUR_PASSWORD"
  }
}
```

同时配置 JWT：

```json
{
  "Jwt": {
    "Issuer": "SoftwareServicePlatform",
    "Audience": "SoftwareServicePlatform",
    "Key": "YOUR_LONG_RANDOM_SECRET"
  }
}
```

> 数据库密码、JWT Key、UpdateToken、Webhook 等敏感配置不要提交到公开仓库。

---

## 23. EF Core Migration

进入：

```bash
cd backend/SoftwareServicePlatform.Api
```

更新数据库：

```bash
dotnet ef database update
```

Visual Studio Package Manager Console 也可以使用：

```powershell
Update-Database
```

数据库结构统一通过 EF Core Migration 管理。

---

## 24. 启动前端

```bash
cd forntend
npm install
npm run dev
```

默认访问：

```text
http://localhost:5173
```

开发环境 Vite 将：

```text
/api
```

代理到：

```text
http://localhost:5108
```

---

## 25. 构建检查

### 后端

```bash
dotnet build backend/SoftwareServicePlatform.Api/SoftwareServicePlatform.Api.csproj
```

### 前端

```bash
cd forntend
npm run build
```

### Updater

```bash
dotnet build clients/SoftwareServicePlatform.Updater/SoftwareServicePlatform.Updater.csproj
```

发布 Windows x64 单文件版本：

```bash
dotnet publish clients/SoftwareServicePlatform.Updater/SoftwareServicePlatform.Updater.csproj \
  -c Release \
  -r win-x64 \
  --self-contained true \
  -p:PublishSingleFile=true
```

---

## 26. Git 开发规范

主分支：

```text
main
```

每个功能、修复或重构单独创建分支。

例如：

```text
feature/client-auto-update
feature/ticket-management
feature/notification-policy

fix/update-download
fix/login-auth

refactor/download-service
```

标准流程：

```bash
git switch main
git pull

git switch -c feature/xxx

# 开发...

git status
git add .
git commit -m "feat: xxx"

git push -u origin feature/xxx
```

然后通过：

```text
Pull Request
  ↓
Review
  ↓
Merge main
```

合并后：

```bash
git switch main
git pull
git branch -d feature/xxx
```

常用 Commit 类型：

```text
feat      新功能
fix       Bug 修复
refactor  重构
style     样式
docs      文档
chore     工程 / 配置
```

---

## 27. 当前完成度

| 模块 | 状态 |
| --- | --- |
| React + TypeScript 前端 | ✅ |
| ASP.NET Core Web API | ✅ |
| PostgreSQL / EF Core | ✅ |
| JWT 登录认证 | ✅ |
| RBAC 权限 | ✅ |
| 客户管理 | ✅ |
| 软件管理 | ✅ |
| 软件版本管理 | ✅ |
| 客户软件绑定 | ✅ |
| 客户门户 | ✅ |
| 安装包上传 / 下载 | ✅ |
| 下载记录 | ✅ |
| 客户端自动更新 | ✅ |
| 文件级增量更新 | ✅ |
| UpdateToken 权限 | ✅ |
| 更新记录上报 | ✅ |
| WinForms GUI Updater | ✅ |
| SHA256 校验 | ✅ |
| 更新失败回滚 | ✅ |
| ZIP 安全限制 | ✅ |
| Ticket 工单 | ✅ |
| 工单附件 / 回复 | ✅ |
| SLA | ✅ |
| Dashboard | ✅ |
| SignalR 实时通知 | ✅ |
| 通知策略 | ✅ |
| 钉钉通知基础接入 | ✅ |
| 正式生产部署 | ⏳ |
| 更完整的操作审计 | ⏳ |
| Updater 自更新 | ⏳ |

---

## 28. 当前不做 / 后续优化

当前版本刻意没有把所有需求一次做满。

后续可根据实际业务继续扩展：

- Updater 自更新
- HTTPS 正式域名与反向代理部署
- 对象存储 / 文件服务器
- 操作审计日志
- Bug 独立管理模块
- Bug 与修复版本关联
- 更多外部通知渠道
- 软件更新灰度策略增强
- 客户端在线状态
- 更细粒度统计报表
- 数据备份与恢复策略
- 自动化测试
- CI/CD

---

## 29. 项目目标

项目最终希望把原来的：

```text
微信发软件
+
人工确认版本
+
客户直接找开发
+
聊天记录跟踪问题
```

逐步替换为：

```text
统一客户
  ↓
统一软件
  ↓
统一版本
  ↓
统一发布
  ↓
统一下载 / 自动更新
  ↓
统一工单
  ↓
统一通知
  ↓
完整记录
  ↓
可追踪
  ↓
可统计
```

SoftwareServicePlatform 不只是一个软件下载页面，而是面向软件交付、升级和售后服务全生命周期的管理平台。

---

## License

当前项目主要用于内部业务开发与技术实践。  
如需对外开源或商业发布，请根据实际情况补充正式 License。
