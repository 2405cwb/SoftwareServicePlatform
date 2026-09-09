# SoftwareServicePlatform

软件服务管理平台，用于统一管理公司客户、软件、软件版本、安装包、客户软件绑定、用户权限以及后续工单和 Bug 处理流程。

项目目标是逐步解决公司目前软件交付和售后过程中存在的版本分发混乱、客户使用版本不统一、问题反馈依赖微信、售后问题直接找开发、处理过程无法追踪等问题。

当前项目已经完成基础全栈框架以及客户、软件、版本、用户、权限、客户软件绑定和软件下载等核心基础模块，下一阶段将进入 **工单系统 Ticket** 开发。

---

# 一、项目背景

目前公司软件在实际交付和售后过程中，主要存在以下问题：

* 软件通过微信等多个渠道发送
* 客户手中的软件版本不统一
* 软件升级记录不完整
* 无法快速确认客户当前使用的软件版本
* 客户出现问题后直接联系销售、售后或开发人员
* 问题处理过程缺少统一记录
* Bug 与软件版本之间缺少关联
* 修复完成后无法清楚追踪客户是否已经升级
* 难以统计售后、开发和软件发布工作量

因此开发本平台，希望逐步形成下面的完整业务闭环：

```text
客户
 ↓
客户软件绑定
 ↓
软件
 ↓
软件版本
 ↓
安装包 / Release
 ↓
客户下载 / 升级
 ↓
问题反馈
 ↓
Ticket 工单
 ↓
Bug
 ↓
修复版本
 ↓
通知客户升级
 ↓
问题关闭
```

---

# 二、技术栈

## 前端

* React
* TypeScript
* Vite
* React Router
* HTML
* CSS

## 后端

* ASP.NET Core Web API
* .NET 10
* Entity Framework Core 10
* ASP.NET Core Identity PasswordHasher
* JWT Authentication
* MemoryCache
* Npgsql

## 数据库

* PostgreSQL

## 开发工具

* Visual Studio 2022
* Visual Studio Code
* DBeaver
* Git
* GitHub

---

# 三、当前已完成功能

## 1. 客户管理

已经完成客户基础管理功能：

* 客户列表
* 新增客户
* 编辑客户
* 删除客户
* 客户启用 / 停用
* 客户基本资料维护
* 客户联系人信息
* PostgreSQL 数据持久化
* 前后端 API 联调

主要数据包括：

```text
客户名称
客户编码
客户类型
行业
省市地区
联系人
联系电话
邮箱
销售负责人
售后负责人
状态
备注
创建时间
更新时间
```

---

## 2. 软件管理

已经完成软件基础管理：

* 软件列表
* 新增软件
* 编辑软件
* 软件编码
* 软件名称
* 软件状态
* 软件基本信息维护

软件与版本建立一对多关系：

```text
Software
   ↓
SoftwareVersion
```

---

## 3. 软件版本管理

已经完成软件版本基础管理：

* 软件版本列表
* 创建版本
* 编辑版本
* 版本号管理
* Release / Beta / Dev 等发布类型基础支持
* 版本与所属软件关联
* 发布时间管理
* 发布状态管理

基本关系：

```text
Software
   ↓
SoftwareVersion
```

---

## 4. 软件安装包管理

已经实现软件安装包上传和下载的基础流程。

支持：

* 软件版本上传安装包
* 服务端统一保存安装包
* 客户下载软件安装包
* HTTP Range 下载支持
* 短期下载 Ticket
* 下载权限再次校验

下载流程：

```text
客户登录
 ↓
选择软件版本
 ↓
POST /api/download/version/{versionId}/ticket
 ↓
服务器生成短期下载 Ticket
 ↓
GET /api/download/file?ticket=xxx
 ↓
服务器重新检查客户权限
 ↓
下载安装包
```

下载 Ticket 当前使用 MemoryCache 保存，并设置较短有效期。

这样避免直接向前端暴露服务器真实文件路径。

---

# 四、客户软件绑定

已经实现：

```text
Customer
    ↓
CustomerSoftware
    ↓
Software
```

用于表示：

> 哪个客户拥有哪些软件。

主要支持：

* 给客户绑定软件
* 查询客户已经绑定的软件
* 防止同一客户重复绑定相同软件
* 客户只能访问自己被授权的软件

数据库中对：

```text
CustomerId + SoftwareId
```

建立唯一约束。

---

# 五、客户门户

已经增加 Customer 客户角色专用页面：

```text
/my-software
```

客户登录后只能查看：

```text
自己所属客户
    ↓
已经绑定的软件
    ↓
该软件当前可用的正式版本
    ↓
下载安装包
```

客户端不能通过修改 CustomerId 查询其他客户的软件。

CustomerId 由当前登录 JWT 身份确定。

---

# 六、用户系统

已经建立统一 User 用户模型。

主要字段：

```text
Id

Username

DisplayName

PasswordHash

Role

Email

Phone

IsEnabled

CustomerId

LastLoginAt

CreatedAt

UpdatedAt
```

Customer 类型用户通过：

```text
CustomerId
```

绑定所属客户。

内部员工：

```text
CustomerId = null
```

---

# 七、登录认证

已经完成 JWT 登录认证。

登录流程：

```text
用户名 + 密码
 ↓
POST /api/auth/login
 ↓
查询 User
 ↓
检查用户状态
 ↓
PasswordHasher 验证密码
 ↓
检查 Customer 状态
 ↓
生成 JWT
 ↓
返回前端
 ↓
sessionStorage 保存 Token
```

JWT 当前包含：

```text
UserId
Username
Role
DisplayName
CustomerId
```

前端 API 请求通过统一的：

```text
apiFetch
```

自动加入：

```http
Authorization: Bearer JWT
```

---

# 八、角色权限 RBAC

当前系统定义角色：

```text
Admin
Support
Developer
Sales
Customer
```

中文含义：

| Role      | 含义    |
| --------- | ----- |
| Admin     | 系统管理员 |
| Support   | 售后人员  |
| Developer | 开发人员  |
| Sales     | 销售人员  |
| Customer  | 客户用户  |

当前页面权限大致为：

### Admin

可以访问：

```text
客户管理
软件管理
版本管理
用户管理
```

### Support

可以访问：

```text
客户管理
软件管理
版本管理
```

### Sales

可以访问：

```text
客户管理
```

### Developer

可以访问：

```text
软件管理
版本管理
```

### Customer

可以访问：

```text
我的软件
```

前端通过：

```text
RequireAuth
RequireRole
HomeRedirect
```

控制页面访问和默认首页。

需要注意：

> 前端权限主要用于界面和用户体验，真正的权限控制仍然由 ASP.NET Core 后端 `[Authorize]` 和 `[Authorize(Roles = "...")]` 决定。

---

# 九、用户管理

管理员已经可以通过：

```text
/users
```

进入用户管理页面。

当前功能包括：

* 用户列表
* 用户搜索
* 创建内部用户
* 编辑用户信息
* 修改用户角色
* 用户启用 / 停用
* Customer 用户所属客户绑定
* Customer 用户重新绑定客户
* 管理员重置用户密码
* 防止普通用户进入用户管理
* 防止管理员误停用自己的账号
* 防止当前管理员把自己的 Admin 角色修改掉

密码不会以明文形式存储。

数据库仅保存：

```text
PasswordHash
```

管理员也无法查看用户原始密码，只能执行：

```text
重置密码
```

---

# 十、平台品牌配置

为了让同一套软件能够方便部署给不同公司使用，目前已经增加平台品牌配置。

后端配置：

```json
"Platform": {
  "Title": "软件服务管理平台",
  "CompanyName": "公司名称"
}
```

后端提供公共接口：

```text
GET /api/platform/info
```

返回：

```json
{
  "title": "软件服务管理平台",
  "companyName": "公司名称"
}
```

前端会动态读取配置。

目前已经应用到：

```text
登录页面标题
登录页面公司名称
后台顶部标题
后台顶部公司名称
浏览器标签页标题
```

因此以后给不同公司部署时，可以直接修改：

```text
appsettings.json
```

而不需要重新修改 React 页面代码。

例如：

```json
"Platform": {
  "Title": "道路检测软件服务平台",
  "CompanyName": "XX科技有限公司"
}
```

页面即可显示对应公司品牌。

后续可以继续扩展：

```text
Logo
Copyright
SupportPhone
SupportEmail
公司网站
登录页说明文字
```

---

# 十一、前端基础框架

目前前端已经完成：

* React + TypeScript 初始化
* Vite 开发环境
* Vite API Proxy
* React Router
* 登录页面
* 后台 MainLayout
* Header
* Sidebar
* 权限菜单
* 403 页面
* 自动角色首页跳转
* 通用 PageHeader
* 通用 SearchBar
* 通用 StatusBadge
* 通用 ConfirmDeleteButton
* RequireAuth
* RequireRole
* apiFetch

当前主要页面：

```text
LoginPage

CustomerPage

SoftwarePage

VersionPage

UserPage

MySoftwarePage

ForbiddenPage
```

---

# 十二、项目结构

当前项目主要结构：

```text
SoftwareServicePlatform
│
├─ forntend
│  ├─ src
│  │  ├─ components
│  │  │  ├─ ConfirmDeleteButton.tsx
│  │  │  ├─ HomeRedirect.tsx
│  │  │  ├─ PageHeader.tsx
│  │  │  ├─ RequireAuth.tsx
│  │  │  ├─ RequireRole.tsx
│  │  │  ├─ SearchBar.tsx
│  │  │  └─ StatusBadge.tsx
│  │  │
│  │  ├─ layouts
│  │  │  └─ MainLayout.tsx
│  │  │
│  │  ├─ pages
│  │  │  ├─ LoginPage.tsx
│  │  │  ├─ CustomerPage.tsx
│  │  │  ├─ SoftwarePage.tsx
│  │  │  ├─ VersionPage.tsx
│  │  │  ├─ UserPage.tsx
│  │  │  ├─ MySoftwarePage.tsx
│  │  │  └─ ForbiddenPage.tsx
│  │  │
│  │  ├─ services
│  │  │  └─ platform.ts
│  │  │
│  │  ├─ App.tsx
│  │  ├─ App.css
│  │  └─ main.tsx
│  │
│  ├─ package.json
│  └─ vite.config.ts
│
├─ backend
│  └─ SoftwareServicePlatform.Api
│     ├─ Controllers
│     ├─ Data
│     ├─ Dtos
│     ├─ Migrations
│     ├─ Models
│     ├─ Program.cs
│     └─ appsettings.json
│
├─ docs
│
├─ .gitignore
│
└─ README.md
```

> 注意：当前前端目录历史原因名称为 `forntend`，暂时保持不变。

---

# 十三、本地开发环境

## 环境要求

当前开发环境：

```text
.NET SDK 10

Node.js 24

npm

PostgreSQL

Git
```

推荐开发工具：

```text
Visual Studio 2022

Visual Studio Code

DBeaver
```

---

# 十四、克隆项目

```bash
git clone https://github.com/2405cwb/SoftwareServicePlatform.git
```

进入项目：

```bash
cd SoftwareServicePlatform
```

---

# 十五、后端启动

进入：

```bash
cd backend/SoftwareServicePlatform.Api
```

还原依赖：

```bash
dotnet restore
```

启动：

```bash
dotnet run
```

也可以直接通过 Visual Studio 2022 启动。

当前开发环境通常监听：

```text
https://localhost:7104

http://localhost:5108
```

---

# 十六、PostgreSQL 配置

本地开发环境建议创建：

```text
appsettings.Development.json
```

用于保存数据库连接字符串等本地配置。

示例：

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=software_service_platform;Username=postgres;Password=YOUR_PASSWORD"
  }
}
```

注意：

> `appsettings.Development.json` 可能包含数据库密码、JWT Secret 等敏感信息，不应提交到 Git 仓库。

---

# 十七、EF Core Migration

更新数据库：

```bash
dotnet ef database update
```

如果使用 Visual Studio Package Manager Console：

```powershell
Update-Database
```

当前数据库已经不再只有 Customers 表，还包含软件、版本、客户软件绑定和用户等相关业务表。

---

# 十八、前端启动

进入：

```bash
cd forntend
```

第一次运行安装依赖：

```bash
npm install
```

PowerShell 如果无法直接执行 npm：

```bash
npm.cmd install
```

启动：

```bash
npm run dev
```

或者：

```bash
npm.cmd run dev
```

浏览器访问：

```text
http://localhost:5173
```

---

# 十九、前后端通信

开发环境 Vite 已配置：

```text
/api
```

代理到：

```text
http://localhost:5108
```

例如 React：

```text
/api/auth/login
```

实际请求：

```text
http://localhost:5108/api/auth/login
```

因此前端代码不需要写死后端完整地址。

---

# 二十、系统基础数据流

例如软件列表：

```text
React
 ↓
apiFetch
 ↓
Vite Proxy
 ↓
ASP.NET Core Controller
 ↓
AppDbContext
 ↓
Entity Framework Core
 ↓
Npgsql
 ↓
PostgreSQL
```

返回：

```text
PostgreSQL
 ↓
EF Core
 ↓
Controller
 ↓
JSON
 ↓
React
 ↓
页面显示
```

---

# 二十一、Git 开发规范

主分支：

```text
main
```

每一个独立功能、修复或重构，都应创建对应分支。

例如：

```text
feature/user-management

feature/platform-branding

feature/ticket-management

fix/login-auth

refactor/download-service
```

开始新功能：

```bash
git switch main

git pull

git switch -c feature/功能名称
```

开发过程中：

```bash
git status

git add .

git commit -m "feat: 功能说明"
```

推送：

```bash
git push -u origin feature/功能名称
```

然后：

```text
GitHub Pull Request
 ↓
Review
 ↓
Merge main
```

合并后：

```bash
git switch main

git pull

git branch -d feature/功能名称
```

常用 Commit 类型：

```text
feat      新增功能

fix       修复问题

refactor  重构

style     样式调整

docs      文档修改

chore     工程 / 配置调整
```

---

# 二十二、当前开发状态

截至目前：

```text
基础工程搭建                ✅

React + TypeScript          ✅

ASP.NET Core Web API        ✅

PostgreSQL                  ✅

EF Core                     ✅

前后端 API 联调             ✅

React Router                ✅

后台基础布局                ✅


客户管理                    ✅

软件管理                    ✅

软件版本管理                ✅

安装包上传                  ✅

安装包下载                  ✅

客户软件绑定                ✅

客户“我的软件”              ✅


JWT 登录认证                ✅

用户模型                    ✅

角色权限 RBAC               ✅

前端路由权限                ✅

后端 API 权限               ✅

用户管理                    ✅

用户启用 / 停用             ✅

客户账号绑定客户            ✅

密码重置                    ✅


平台标题配置                ✅

公司名称配置                ✅

登录页品牌配置              ✅


Ticket 工单系统             ⏳

Bug 管理                    ⏳

Bug 与版本关联              ⏳

客户问题处理记录            ⏳

附件 / 日志上传             ⏳

消息通知                    ⏳

操作日志                    ⏳

数据统计                    ⏳

正式部署                    ⏳
```

---

# 二十三、下一阶段开发计划

下一阶段重点开发：

## Ticket 工单系统

目标：

```text
客户提出问题
 ↓
创建 Ticket
 ↓
售后受理
 ↓
问题分类
 ↓
分配处理人员
 ↓
处理记录
 ↓
如果确认是 Bug
 ↓
创建 Bug
 ↓
关联修复版本
 ↓
客户升级
 ↓
关闭工单
```

Ticket 初步计划包含：

```text
TicketNo

Title

Description

Status

Priority

CustomerId

SoftwareId

CreatedByUserId

AssignedToUserId

CreatedAt

UpdatedAt

ResolvedAt
```

工单状态初步考虑：

```text
Pending

Processing

Resolved

Closed
```

优先级：

```text
Low

Normal

High

Urgent
```

---

# 二十四、项目最终目标

该项目不仅用于学习 React 和 ASP.NET Core，也计划逐步发展为公司实际可使用的软件服务管理平台。

最终希望实现：

```text
统一客户管理

统一软件管理

统一版本管理

统一安装包发布

统一客户下载入口

统一权限体系

统一问题反馈入口

统一工单处理流程

统一 Bug 跟踪

统一版本修复关联

统一客户升级记录

统一数据统计
```

从目前依赖微信和人工记录的软件交付、售后模式，逐步转变为：

```text
客户
 ↓
平台
 ↓
销售 / 售后 / 开发
 ↓
统一流程
 ↓
完整记录
 ↓
可追踪
 ↓
可统计
```

项目持续开发中。
