# SoftwareServicePlatform

软件服务管理平台，用于统一管理公司软件、客户、版本发布、问题反馈与后续工单流程。

项目目前处于持续开发阶段，现阶段已完成基础全栈框架搭建，以及客户管理模块的基础 CRUD 功能。

---

## 项目背景

公司软件在实际交付和售后过程中，可能存在以下问题：

* 软件版本分发不统一
* 客户使用版本不一致
* 软件更新记录不清晰
* 客户问题直接由开发人员处理，缺少统一记录
* 售后问题、Bug、软件版本之间缺少关联
* 难以统计客户使用情况和售后工作量

因此开发该平台，希望逐步建立完整的软件服务管理流程：

```text
客户
 ↓
软件
 ↓
软件版本
 ↓
客户使用版本
 ↓
问题 / 工单
 ↓
Bug
 ↓
修复版本
 ↓
客户升级
```

---

## 技术栈

### 前端

* React
* TypeScript
* Vite
* React Router
* HTML
* CSS

### 后端

* ASP.NET Core Web API
* .NET 10
* Entity Framework Core
* Npgsql

### 数据库

* PostgreSQL

### 开发工具

* Visual Studio 2026
* Visual Studio Code
* DBeaver
* Git / GitHub

---

## 当前已完成功能

### 客户管理

目前已经完成客户管理基础 CRUD：

* 客户列表查询
* 新增客户
* 编辑客户
* 删除客户
* PostgreSQL 数据持久化
* 前后端 API 联调

后端 API：

```text
GET     /api/customers
POST    /api/customers
PUT     /api/customers/{id}
DELETE  /api/customers/{id}
```

### 前端基础框架

目前已经完成：

* React + TypeScript 项目初始化
* Vite 开发环境
* 前后端代理配置
* React 页面组件拆分
* React Router 路由
* 后台管理基础布局
* 客户管理页面

---

## 计划功能

后续将逐步实现以下模块：

* 客户管理
* 软件管理
* 软件版本管理
* 客户软件授权 / 绑定
* 客户当前版本管理
* Release / Beta / Dev 版本管理
* 软件包上传与下载
* 软件版本更新检测
* 强制升级 / 版本黑名单
* 工单管理
* Bug 管理
* Bug 与修复版本关联
* 客户反馈记录
* 附件 / 日志上传
* 用户登录
* 用户权限管理
* 角色权限 RBAC
* 发布记录
* 操作日志
* 数据统计
* Docker 部署
* Nginx 部署

---

## 项目结构

```text
SoftwareServicePlatform
│
├─ frontend
│  ├─ src
│  │  ├─ pages
│  │  │  ├─ CustomerPage.tsx
│  │  │  └─ SoftwarePage.tsx
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
│     ├─ Models
│     ├─ Data
│     ├─ Migrations
│     ├─ Program.cs
│     └─ appsettings.json
│
├─ docs
│
├─ .gitignore
└─ README.md
```

---

# 本地开发环境

## 1. 环境要求

建议安装：

```text
.NET SDK 10
Node.js
npm
PostgreSQL
Git
```

数据库管理工具推荐：

```text
DBeaver
```

---

## 2. 克隆项目

```bash
git clone https://github.com/2405cwb/SoftwareServicePlatform.git
```

进入项目：

```bash
cd SoftwareServicePlatform
```

---

# 后端启动

进入后端：

```bash
cd backend/SoftwareServicePlatform.Api
```

还原依赖：

```bash
dotnet restore
```

---

## PostgreSQL 配置

创建数据库：

```text
software_service_platform
```

在后端目录创建：

```text
appsettings.Development.json
```

可参考：

```text
appsettings.Development.example.json
```

配置示例：

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=software_service_platform;Username=postgres;Password=YOUR_PASSWORD"
  }
}
```

注意：

> `appsettings.Development.json` 中可能包含本地数据库密码，因此该文件不会提交到 Git 仓库。

---

## EF Core Migration

如果数据库尚未初始化，可以执行：

```powershell
Update-Database
```

或者使用 .NET CLI：

```bash
dotnet ef database update
```

数据库初始化完成后，目前会创建：

```text
Customers
__EFMigrationsHistory
```

等数据表。

---

## 启动后端

可以直接使用 Visual Studio 启动。

或者：

```bash
dotnet run
```

当前本地开发 API 地址通常为：

```text
http://localhost:5108
```

测试接口：

```text
GET http://localhost:5108/api/test
```

客户接口：

```text
GET http://localhost:5108/api/customers
```

---

# 前端启动

进入：

```bash
cd frontend
```

安装依赖：

```bash
npm install
```

Windows PowerShell 如果因为执行策略无法使用 `npm`，可以使用：

```bash
npm.cmd install
```

启动 Vite：

```bash
npm run dev
```

或：

```bash
npm.cmd run dev
```

浏览器访问：

```text
http://localhost:5173
```

---

## 前后端通信

开发环境中 Vite 已配置 API Proxy。

前端请求：

```text
/api/customers
```

会被代理到：

```text
http://localhost:5108/api/customers
```

因此 React 代码中无需直接写完整后端地址。

---

# 数据流

以新增客户为例：

```text
React
 ↓
fetch POST /api/customers
 ↓
Vite Proxy
 ↓
ASP.NET Core
 ↓
CustomersController
 ↓
AppDbContext
 ↓
Entity Framework Core
 ↓
Npgsql
 ↓
PostgreSQL
```

查询数据时则反向返回：

```text
PostgreSQL
 ↓
EF Core
 ↓
ASP.NET Core API
 ↓
JSON
 ↓
React
 ↓
页面显示
```

---

# Git 开发规范

主分支：

```text
main
```

功能开发建议创建独立分支，例如：

```text
feature/software-management
feature/version-management
feature/ticket-management
```

创建分支：

```bash
git switch -c feature/software-management
```

提交示例：

```bash
git add .
git commit -m "feat: 完成软件管理基础功能"
```

常用提交类型：

```text
feat      新增功能
fix       修复问题
refactor  代码重构
style     样式调整
docs      文档修改
chore     工程或配置调整
```

---

# 当前开发状态

当前项目主要完成：

```text
基础工程搭建        ✅
React + TypeScript   ✅
ASP.NET Core API     ✅
PostgreSQL           ✅
EF Core              ✅
客户 CRUD            ✅
前后端联调           ✅
页面路由             ✅

软件管理             🚧
版本管理             ⏳
工单管理             ⏳
Bug管理              ⏳
权限系统              ⏳
软件发布             ⏳
正式部署             ⏳
```

---

## 项目说明

该项目目前主要用于：

* 实践完整的前后端开发流程
* 学习 React + TypeScript
* 深入掌握 ASP.NET Core Web API
* 学习 PostgreSQL 与 EF Core
* 建立规范的 Git 开发流程
* 最终形成可以实际使用的软件服务管理平台

项目正在持续完善中。
