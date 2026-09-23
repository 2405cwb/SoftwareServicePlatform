# SoftwareServicePlatform

> 软件服务管理平台 —— 面向软件交付、版本发布、客户端升级、客户服务与工单协同的一体化管理平台。

SoftwareServicePlatform 用于统一管理公司客户、软件产品、软件版本、安装包、客户端自动更新、更新设备授权、下载 / 升级记录、工单、SLA、通知、操作审计与生产部署流程，逐步替代依赖微信、网盘、FTP 和人工记录的软件交付与售后方式。

当前项目已经形成：

```text
客户授权
  ↓
软件 / 版本管理
  ↓
发布前检查
  ↓
版本发布
  ↓
更新设备授权
  ↓
安装包下载 / 客户端自动升级
  ↓
下载与升级记录
  ↓
问题反馈 / Ticket
  ↓
SLA / 通知
  ↓
运维与操作审计
  ↓
CI / 手动生产部署
```

> 当前状态：持续开发 / 内部试用阶段  
> README 更新日期：2026-09-23

---

## 1. 项目背景

传统软件交付和售后流程中常见：

- 软件通过微信、网盘、FTP 等多个渠道发送
- 客户使用的软件版本不统一
- 无法快速确认某个客户当前可用版本
- 安装包和更新包缺少统一管理
- 客户升级过程不可追踪
- 客户问题依赖聊天记录跟踪
- 开发、售后、销售之间信息容易断层
- 缺少下载、升级、工单、SLA、错误与审计数据

SoftwareServicePlatform 的目标是将这些流程统一到一个平台中。

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
- JWT Authentication
- ASP.NET Core SignalR
- ASP.NET Core Identity PasswordHasher
- Npgsql

### 数据库

- PostgreSQL 17
- EF Core Migration

### Windows 自动更新

- .NET 10 Windows
- WinForms
- UAC `requireAdministrator`
- SHA256 文件校验
- 文件级增量更新
- 更新前本地备份
- 更新失败本地回滚
- 图形化更新进度
- 设备级 UpdateToken
- Updater 两阶段自更新

### 客户端接入

- C# WinForms .NET Framework 4.8
- Qt 5.8
- `updater.bootstrap.json`
- LocalAppData 独立设备更新配置
- 一次性更新激活码

### 工程化

- Docker / Docker Compose
- Nginx
- GitHub Actions CI
- GitHub Actions 手动生产部署
- PostgreSQL + storage 生产备份 / 恢复脚本
- `/api/health` 健康检查
- Git 分支 + Pull Request 工作流

---

## 3. 系统架构

```text
┌──────────────────────────────┐
│        React Frontend        │
│ Dashboard / Customer / ...   │
└──────────────┬───────────────┘
               │ HTTP / SignalR
               ↓
┌──────────────────────────────┐
│       ASP.NET Core API       │
│ Auth / Release / Ticket /... │
└──────────────┬───────────────┘
               │ EF Core
               ↓
┌──────────────────────────────┐
│          PostgreSQL          │
└──────────────────────────────┘

                 ↑
                 │ X-Update-Token
                 │ 每个 ClientInstallation 独立 Token
                 │
┌──────────────────────────────┐
│ Windows Application Client   │
│              +               │
│ SoftwareServicePlatform      │
│ Updater / UpdaterBootstrap   │
└──────────────────────────────┘
```

更新授权关系：

```text
Customer
  ↓
CustomerSoftware
  ↓
一次性更新激活码
  ↓
ClientInstallation
  ↓
设备独立 UpdateToken
```

---

## 4. 客户管理

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

## 5. 软件与版本管理

### 软件管理

支持：

- 软件新增、编辑、删除
- 软件编码
- 软件名称
- 软件状态
- 软件与多个版本关联
- 软件与多个客户关联

关系：

```text
Software
├─ SoftwareVersion
└─ CustomerSoftware
```

### 版本管理

支持：

- 创建 / 编辑软件版本
- Release / Beta / Dev 类型
- Draft / Published 等发布状态
- 发布标题和发布说明
- 完整安装包上传
- 文件级更新 ZIP 上传
- 指定客户发布
- 全部客户发布
- 目标版本文件清单
- 下载与升级记录

---

## 6. 发布前检查

平台已经加入统一的 Release Preflight。

发布前会对版本进行检查，避免把明显错误的版本发布出去。

主要检查包括：

- 版本必须处于可发布状态
- Dev 版本不能按正式版本发布
- 目标版本号必须高于当前已发布版本
- 软件必须可用
- 完整安装包路径与文件状态
- 更新 Manifest 是否存在且与软件 / 版本匹配
- 至少存在一种可用发布包
- 发布目标客户数量
- 发布说明 / 标题缺失提醒
- 是否包含 Updater 自更新文件

前端可以主动执行发布前检查，真正发布接口也会经过后端统一检查。

---

## 7. 客户软件授权

公司级授权通过：

```text
CustomerSoftware
```

建立客户与软件的关系。

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

建立唯一约束，避免重复绑定。

`CustomerSoftware` 只表示“该客户可以使用该软件”。

设备更新权限由：

```text
ClientInstallation
```

单独管理。

因此同一客户使用同一款软件时，可以存在多台独立设备：

```text
CustomerSoftware
├─ ClientInstallation A
├─ ClientInstallation B
└─ ClientInstallation C
```

---

## 8. 更新设备授权

### 一次性更新激活码

客户可在“我的软件”中生成短期、一次性的更新激活码。

```text
网页生成更新激活码
  ↓
新设备第一次输入
  ↓
服务器校验 CustomerSoftware
  ↓
创建 ClientInstallation
  ↓
生成该设备独立 UpdateToken
```

更新激活码不是软件注册码，不控制业务软件能否正常启动。

### 设备级 UpdateToken

后续更新请求通过：

```http
X-Update-Token: ssp_upd_xxx
```

认证。

数据库不保存完整明文 Token，只保存 Hash 和必要的展示信息。

旧的公司级共享 `ClientUpdateCredentials` 数据模型已经从当前模型中移除，当前更新授权以 `ClientInstallation` 为核心。

### 设备数量限制

`CustomerSoftware.MaxDeviceCount` 用于限制同一个“客户 + 软件”允许保持启用状态的设备数量：

```text
0 = 不限制
>0 = 最大启用设备数
```

服务端会在生成 / 使用激活码时进行设备数量校验。

### 设备管理

管理员支持：

- 查看客户 / 软件设备数量
- 查看启用 / 停用设备
- 设置设备数量上限
- 修改设备名称
- 修改设备备注
- 停用某台设备更新权限
- 重新启用设备更新权限

停用某台设备后：

```text
业务软件正常使用           ✅
该设备在线自动更新         ❌
同客户其他设备             ✅
```

---

## 9. 客户门户

Customer 用户登录后可进入：

```text
/my-software
```

查看：

- 当前客户已授权的软件
- 可用正式版本
- 安装包与版本资料
- 更新激活码
- 更新设备列表

客户身份由 JWT 与服务器数据库共同校验，不能通过修改前端参数访问其他客户的数据。

---

## 10. 用户与权限

当前角色：

| Role | 说明 |
| --- | --- |
| Admin | 系统管理员 |
| Support | 售后人员 |
| Developer | 开发人员 |
| Sales | 销售人员 |
| Customer | 客户用户 |

权限由三层共同控制：

```text
前端路由权限
+
后端 Authorize / Roles
+
数据库业务权限校验
```

所有已登录用户都可以修改自己的密码。

管理员仍可以在用户管理中重置其他账号密码。

---

## 11. 登录认证与账户安全

系统使用 JWT Authentication。

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
前端保存会话
  ↓
后续请求自动附加 Authorization
```

修改自己的密码：

```text
POST /api/account/change-password
```

规则包括：

- 验证当前密码
- 新密码至少 8 位
- 新密码不能与当前密码相同
- 修改成功后重新登录

管理员重置密码：

```text
POST /api/users/{id}/reset-password
```

---

## 12. 文件级增量更新

平台的增量更新不是二进制 Patch，而是：

> 上传目标版本目录，服务端生成目标文件清单；客户端根据本地 SHA256 比较，只下载变化或缺失的文件。

流程：

```text
上传目标版本 ZIP
  ↓
服务端安全解压
  ↓
扫描目标文件
  ↓
计算 SHA256
  ↓
生成 Manifest
  ↓
客户端检查更新
  ↓
比较本地文件
  ↓
只下载变化 / 缺失文件
  ↓
备份旧文件
  ↓
替换
  ↓
再次校验
  ↓
更新 version.txt
  ↓
重启业务程序
```

例如：

```text
目标版本：1309 个文件
实际变化：14 个文件

→ 客户端只下载真正需要更新的文件
```

---

## 13. 更新包控制与安全

支持：

```text
.update-ignore.txt
.update-delete.txt
```

其中：

- `.update-ignore.txt`：排除不参与更新的文件
- `.update-delete.txt`：声明目标版本需要删除的旧文件

服务端 ZIP 安全检查包括：

- Zip Slip / Path Traversal 防护
- Windows 大小写路径冲突
- 重复文件路径
- 更新 / 删除清单冲突
- 文件数量限制
- 单文件解压大小限制
- 总解压大小限制
- 实际解压字节校验

当前默认限制：

```text
最大文件数量：20000
单文件最大解压大小：4 GB
总解压大小：20 GB
```

---

## 14. Windows Updater

仓库包含：

```text
clients/
├─ SoftwareServicePlatform.Updater/
└─ SoftwareServicePlatform.UpdaterBootstrap/
```

Updater 主要能力：

- WinForms 图形进度窗口
- UAC 管理员权限
- 当前版本 / 目标版本显示
- 文件数量和百分比
- SHA256 校验
- 文件级增量下载
- 等待主程序退出
- 更新前本地备份
- 更新失败本地回滚
- 写入 `version.txt`
- 自动重启主程序
- 增量失败时可回退完整安装包
- `updater.log`

### 推荐安装结构

```text
AppRoot/
├─ MainApp.exe
├─ version.txt
└─ updater/
   ├─ SoftwareServicePlatform.Updater.exe
   ├─ SoftwareServicePlatform.UpdaterBootstrap.exe
   └─ updater.bootstrap.json
```

新安装包不需要预置包含真实 Token 的 `updater.json`。

真正包含设备 UpdateToken 的配置保存到：

```text
%LOCALAPPDATA%\SoftwareServicePlatform\UpdaterConfigs\<softwareCode>\updater.json
```

---

## 15. Updater 自更新

正在运行的 `SoftwareServicePlatform.Updater.exe` 无法安全直接覆盖自己，因此项目加入：

```text
SoftwareServicePlatform.UpdaterBootstrap.exe
```

当某个版本需要更新 Updater 本身时，在更新 ZIP 中加入：

```text
.updater-self/
└─ SoftwareServicePlatform.Updater.exe
```

流程：

```text
旧 Updater 完成业务文件更新
  ↓
启动 UpdaterBootstrap
  ↓
旧 Updater 退出
  ↓
Bootstrap 替换 updater/SoftwareServicePlatform.Updater.exe
  ↓
删除 .updater-self
  ↓
重新启动业务软件
```

普通版本不需要包含 `.updater-self/`。

---

## 16. 客户端接入示例

仓库提供：

```text
examples/
├─ CSharpWinForms/
└─ Qt5/
```

基本流程：

```text
业务软件启动
  ↓
读取 updater.bootstrap.json
  ↓
检查 LocalAppData 是否已有设备 updater.json
  ├─ 有 → 检查更新
  └─ 无
      ↓
   输入一次性更新激活码
      ↓
   获取设备独立 UpdateToken
      ↓
   保存本机配置
      ↓
   检查更新
```

普通网络错误或更新服务器暂时不可用，不应阻止业务软件正常启动。

---

## 17. 下载与更新记录

平台统一记录：

```text
ManualPackage
AutoIncremental
AutoFullPackage
```

记录包括：

- 客户
- 软件
- 来源
- 旧版本
- 目标版本
- 更新方式
- 文件数量
- 下载大小
- 状态
- 错误信息
- 时间

自动更新状态：

```text
Started
Success
Failed
```

---

## 18. Ticket 工单系统

当前已经实现主要工单流程：

- Customer 创建工单
- 内部人员代客户创建
- 工单编号
- 客户与软件关联
- 搜索和筛选
- 优先级
- 来源
- 分诊
- 指派处理人
- 公开回复
- 内部处理记录
- 附件
- Resolved / Closed / Reopen
- 客户数据隔离

典型流程：

```text
Pending
  ↓
分诊
  ↓
设置优先级
  ↓
分配处理人
  ↓
Processing
  ↓
Resolved
  ↓
Closed
```

---

## 19. SLA

管理员可维护不同优先级的：

- 首次响应目标时间
- 解决目标时间
- 是否启用

创建工单时保存 SLA 快照，后续调整规则不会影响历史工单。

系统可判断：

```text
响应是否超时
解决是否超时
```

---

## 20. 通知系统

当前支持：

- Notification 数据模型
- 未读数量
- 通知列表
- 单条已读
- 全部已读
- SignalR 实时推送
- 自动重连
- 重连后同步
- 点击通知跳转目标页面

通知架构：

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

当前已经具备钉钉外部通知基础能力，并可继续扩展：

```text
WeCom
Feishu
Email
...
```

---

## 21. 运维与操作审计

管理员可以访问：

```text
/operations
```

运维中心用于查看：

- 最近 24 小时操作数量
- API 错误数量
- 登录失败数量
- 客户端更新失败数量
- 最近系统事件
- 最近操作审计
- 最近更新失败记录

后端通过：

```text
AuditLogMiddleware
SystemEventMiddleware
```

记录操作与异常信息。

主要数据表：

```text
AuditLogs
SystemEventLogs
```

审计中不记录请求 Body，也不把 QueryString 作为审计内容保存，避免无意记录敏感参数。

---

## 22. 健康检查

健康检查接口：

```text
GET /api/health
```

正常返回类似：

```json
{
  "status": "ok",
  "database": true,
  "utc": "..."
}
```

如果数据库无法连接，接口返回 `503 Service Unavailable`。

该接口同时被生产部署流程用于部署后检查。

---

## 23. 项目结构

```text
SoftwareServicePlatform/
│
├─ .github/
│  └─ workflows/
│     ├─ ci.yml
│     └─ deploy.yml
│
├─ backend/
│  └─ SoftwareServicePlatform.Api/
│     ├─ Controllers/
│     ├─ Data/
│     ├─ Dtos/
│     ├─ Hubs/
│     ├─ Middleware/
│     ├─ Migrations/
│     ├─ Models/
│     ├─ Services/
│     │  ├─ ClientUpdates/
│     │  ├─ ExternalNotifications/
│     │  ├─ NotificationPolicies/
│     │  └─ Releases/
│     └─ Program.cs
│
├─ forntend/
│  ├─ src/
│  ├─ package.json
│  └─ vite.config.ts
│
├─ clients/
│  ├─ SoftwareServicePlatform.Updater/
│  └─ SoftwareServicePlatform.UpdaterBootstrap/
│
├─ examples/
│  ├─ CSharpWinForms/
│  └─ Qt5/
│
├─ scripts/
│  ├─ backup-production.sh
│  ├─ restore-production.sh
│  ├─ cleanup-legacy-update-credential.ps1
│  └─ create-hardening-migration.ps1
│
├─ compose.yaml
├─ compose.prod.yaml
└─ README.md
```

> 前端目录因项目早期历史原因仍命名为 `forntend`，暂时保持不变。

---

## 24. 本地开发

### 环境要求

```text
.NET SDK 10
Node.js 24+
npm
PostgreSQL
Git
```

推荐工具：

```text
Visual Studio 2022
Visual Studio Code
DBeaver
Docker Desktop
```

克隆：

```bash
git clone https://github.com/2405cwb/SoftwareServicePlatform.git
cd SoftwareServicePlatform
```

---

## 25. 本地直接启动

### 后端

```bash
cd backend/SoftwareServicePlatform.Api
dotnet restore
dotnet run
```

开发地址通常为：

```text
https://localhost:7104
http://localhost:5108
```

### 前端

```bash
cd forntend
npm install
npm run dev
```

Vite 默认：

```text
http://localhost:5173
```

开发环境 `/api` 代理到本地 API。

---

## 26. 本地 Docker

项目根目录：

```bash
docker compose up -d --build
```

当前 Docker 开发端口：

```text
前端：
http://localhost:18080

API：
http://localhost:5108

PostgreSQL：
localhost:5433
```

Docker 内部数据库地址仍然是：

```text
postgres:5432
```

注意：

> VS2022 / `dotnet run` 与 Docker API 是否看到同一套数据，取决于它们的 `DefaultConnection` 是否连接同一个 PostgreSQL。

停止：

```bash
docker compose down
```

不要随意执行：

```bash
docker compose down -v
```

`-v` 会删除 PostgreSQL Volume。

---

## 27. EF Core Migration

进入后端：

```bash
cd backend/SoftwareServicePlatform.Api
```

更新数据库：

```bash
dotnet ef database update
```

当前重要 Migration 包括：

```text
AddClientDeviceActivation
AddPlatformHardening
```

`AddPlatformHardening` 包含：

- 删除旧 `ClientUpdateCredentials`
- 添加 `CustomerSoftware.MaxDeviceCount`
- 添加 `ClientInstallation.Remark`
- 创建 `AuditLogs`
- 创建 `SystemEventLogs`

应用启动时也会执行尚未应用的 Migration。

---

## 28. 构建检查

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

### UpdaterBootstrap

```bash
dotnet build clients/SoftwareServicePlatform.UpdaterBootstrap/SoftwareServicePlatform.UpdaterBootstrap.csproj
```

---

## 29. Git 开发规范

主分支：

```text
main
```

独立功能 / 修复 / 重构 / 文档修改均使用独立分支，例如：

```text
feature/client-auto-update
feature/github-actions-ci
feature/github-actions-cd

fix/update-download
fix/frontend-port

refactor/download-service

docs/update-readme
```

推荐流程：

```bash
git switch main
git fetch origin
git pull --ff-only origin main

git switch -c feature/xxx

# 开发...

git status
git add .
git commit -m "feat: xxx"
git push -u origin feature/xxx
```

然后：

```text
Pull Request
  ↓
CI
  ↓
Review
  ↓
Merge main
```

避免直接在 `main` 上进行功能开发和提交。

常见 Commit 类型：

```text
feat
fix
refactor
docs
style
chore
ci
```

---

## 30. GitHub Actions CI

当前仓库已经加入：

```text
.github/workflows/ci.yml
```

触发场景：

- push 到 `main`
- push 到 `feature/**`
- push 到 `fix/**`
- 针对 `main` 的 Pull Request

CI 自动执行：

```text
Backend Build
Frontend Build
Updater Build
UpdaterBootstrap Build
```

主要环境：

```text
.NET 10
Node.js 24
Ubuntu Runner
Windows Runner
```

建议将 CI 检查配置为 `main` 分支合并前的必过条件。

---

## 31. 生产部署

生产 Compose：

```text
compose.prod.yaml
```

生产结构：

```text
公网
  ↓
Frontend / Nginx :80
  ↓
API :8080（Docker 内部）
  ↓
PostgreSQL :5432（Docker 内部）
```

PostgreSQL 不直接暴露公网端口。

API 上传文件目录通过：

```text
STORAGE_PATH
```

挂载到：

```text
/app/storage
```

DataProtection Key 使用独立 Docker Volume 持久化。

---

## 32. GitHub Actions CD

当前仓库已经加入：

```text
.github/workflows/deploy.yml
```

当前生产部署采用：

```text
手动 Run workflow
```

而不是每次合并 `main` 后直接自动部署。

部署过程：

```text
SSH 到 ECS
  ↓
拉取最新 origin/main
  ↓
生产数据备份
  ↓
docker compose up -d --build
  ↓
查看容器状态
  ↓
GET /api/health
  ↓
成功 / 失败结果
```

GitHub Actions 使用 Repository Secrets，例如：

```text
ECS_HOST
ECS_USER
ECS_SSH_KEY
```

私钥和服务器敏感配置禁止提交到仓库。

---

## 33. 生产备份与恢复

### 备份

脚本：

```text
scripts/backup-production.sh
```

主要备份：

```text
PostgreSQL database.dump
storage.tar.gz
SHA256SUMS.txt
```

默认备份目录：

```text
/opt/software-service-platform/backups
```

生产部署前会执行备份。

### 恢复

脚本：

```text
scripts/restore-production.sh
```

恢复会覆盖：

- 当前 PostgreSQL 数据库
- 当前 `STORAGE_PATH`

脚本要求人工输入：

```text
RESTORE
```

确认后才真正执行。

> 数据库恢复属于高风险操作，正式生产环境必须确认备份完整后再执行。

---

## 34. 当前完成度

| 模块 | 状态 |
| --- | --- |
| React + TypeScript 前端 | ✅ |
| ASP.NET Core Web API | ✅ |
| PostgreSQL / EF Core | ✅ |
| JWT 登录认证 | ✅ |
| RBAC 权限 | ✅ |
| 用户自助修改密码 | ✅ |
| 管理员重置密码 | ✅ |
| 客户管理 | ✅ |
| 软件管理 | ✅ |
| 软件版本管理 | ✅ |
| 客户软件绑定 | ✅ |
| 客户门户 | ✅ |
| 安装包上传 / 下载 | ✅ |
| 文件级增量更新 | ✅ |
| 更新激活码 | ✅ |
| 设备级 UpdateToken | ✅ |
| 旧共享 UpdateToken 表清理 | ✅ |
| 设备数量上限 | ✅ |
| 设备名称 / 备注 | ✅ |
| 管理员设备授权管理 | ✅ |
| C# WinForms 接入示例 | ✅ |
| Qt 5.8 接入示例 | ✅ |
| 下载 / 更新记录 | ✅ |
| WinForms GUI Updater | ✅ |
| UpdaterBootstrap 自更新 | ✅ |
| SHA256 校验 | ✅ |
| 客户端更新失败回滚 | ✅ |
| ZIP 安全限制 | ✅ |
| 发布前检查 | ✅ |
| Ticket 工单 | ✅ |
| 工单附件 / 回复 | ✅ |
| SLA | ✅ |
| Dashboard | ✅ |
| SignalR 实时通知 | ✅ |
| 通知策略 | ✅ |
| 钉钉通知基础接入 | ✅ |
| 操作审计 | ✅ |
| 系统事件记录 | ✅ |
| 运维中心 | ✅ |
| `/api/health` | ✅ |
| Docker 本地环境 | ✅ |
| Docker 生产环境 | ✅ |
| PostgreSQL / storage 备份 | ✅ |
| 生产恢复脚本 | ✅ |
| GitHub Actions CI | ✅ |
| GitHub Actions 手动 CD | ✅ |
| CD 自动代码回滚 | ⏳ |
| HTTPS | ⏳ |
| 对象存储 | ⏳ |
| 日志自动清理 / 保留策略 | ⏳ |
| 完整自动化测试 | ⏳ |

---

## 35. 后续方向

当前优先级较高的工程化工作：

```text
CD 部署失败自动回滚
  ↓
日志保留与清理
  ↓
HTTPS
  ↓
对象存储
  ↓
自动化测试
```

业务层后续可继续扩展：

- Bug 独立管理
- Bug 与修复版本关联
- 灰度发布策略增强
- 客户端在线状态
- 更多外部通知渠道
- 更细粒度统计报表

---

## 36. 项目目标

项目希望把原来的：

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
统一设备更新授权
  ↓
统一下载 / 自动更新
  ↓
统一问题反馈
  ↓
统一工单
  ↓
统一通知
  ↓
统一审计与运维
  ↓
可追踪
  ↓
可统计
  ↓
可部署
```

SoftwareServicePlatform 不只是一个软件下载页面，而是面向软件交付、升级和售后服务全生命周期的管理平台。

---

## License

当前项目主要用于内部业务开发与技术实践。
