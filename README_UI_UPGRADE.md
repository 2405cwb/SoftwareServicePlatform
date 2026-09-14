# 软件服务管理平台：角色化工作台 + 专业工单 UI 升级覆盖包

基准：GitHub `2405cwb/SoftwareServicePlatform` 的 `main` 最新代码（2026-09-14 核对）。

## 这次完成的内容

### 1. 角色化工作台

- Admin：全局管理统计 + 自己待办 + 未分配 + SLA + 下载
- Support：售后全局统计 + 自己待办 + 未分配 + SLA
- Developer：只看自己的待处理、紧急、风险和本月解决
- Sales：客户运营工作台（当前按 `Customer.SalesOwner == User.DisplayName` 匹配）
- Customer：公司级服务首页、我的软件、进行中问题、最近工单

所有角色登录后统一先进入 `/dashboard`。

### 2. Dashboard 跳转

- 客户卡片 -> 客户管理
- 软件卡片 -> 软件管理
- 待处理 / 我的待办 / 未分配 -> 工单页并自动筛选
- 本月工单 -> 工单页本月筛选
- SLA 黄色 / 红色列表 -> 直接打开对应工单
- 软件 / 客户 TOP5 -> 工单页按软件 / 客户筛选
- 下载 KPI -> 下载记录页，支持本月筛选
- 工单状态饼图 -> 对应状态工单

### 3. 个人待办

顶部和左侧“工单管理”增加个人未处理数量角标。

- Admin / Support：统计分配给自己的 Pending + Processing
- Developer：统计分配给自己的 Pending + Processing
- Customer：统计自己公司未结束工单

每 60 秒自动刷新，并在浏览器重新获得焦点时立即刷新。

### 4. 客户公司名称

Customer 登录后顶部显示：

`用户姓名 · 客户公司名称`

公司名称直接通过现有 `User.CustomerId -> Customer.Name` 查询，不改数据库。

### 5. 客户不再决定工单优先级

客户提交页面不再显示 Low / Normal / High / Urgent。

为了避免只靠“前端隐藏”，新增 EF Core `TicketIntakeSaveChangesInterceptor`：

- 所有 `Source = Portal` 的新增工单
- 保存前强制 `Priority = Unclassified`
- 清空 SLA 快照

因此即使客户手工伪造 HTTP 请求传 `Urgent` 也无效。

### 6. 售后分诊

Admin / Support 在工单工作台右侧可以：

- 确定 Low / Normal / High / Urgent
- 指定 Support / Developer 处理人
- 分诊时才应用当前 SLA 快照

SLA 截止时间仍然按工单 `CreatedAt` 计算，所以不能靠晚分诊拖延 SLA。

### 7. 售后代客户建单

Admin / Support 可以直接录入微信、电话、邮件、现场、内部反馈：

客户 -> 软件 -> 来源 -> 优先级 -> 问题描述 -> 可选处理人。

### 8. 专业工单工作台

新版 `/tickets`：

- 左侧：工单 Inbox
- 中间：专业沟通时间线
- 右侧：客户、软件、来源、负责人、SLA、分诊
- 公开回复 / 内部备注明确区分
- 客户自助提交、售后代录都支持首批附件；回复附件继续挂在对应消息下
- 内部备注 Customer 后端接口本来就不可见，继续保持
- 回复附件挂在对应消息下面
- 保留解决、关闭、重新打开
- Dashboard 的 `ticketId/status/priority/customerId/softwareId/scope/period` 参数都可以落地

### 9. 前端去重和结构整理

新增：

- `utils/session.ts`：用户 / 角色统一处理
- `utils/format.ts`：时间、时长、状态、优先级、来源统一格式化
- `hooks/useWorkProfile.ts`：个人待办自动刷新
- `components/dashboard/*`：KPI、状态 Badge、工单列表复用组件
- `styles/upgrade.css`：统一 UI 层，并顺带优化客户、软件、版本、用户、SLA 等旧页面的视觉

原来的超大 Dashboard 被重写为更薄的角色入口，不再继续把所有逻辑堆在一个文件。

## 数据库

**本覆盖包不新增数据库表、不新增字段，不需要 Migration。**

不要执行 `dotnet ef migrations add ...`。

## 覆盖方式

推荐先创建 Git 分支：

```bash
git checkout main
git pull
git checkout -b feature/role-dashboard-ui-refactor
```

然后把 ZIP 解压后的：

- `backend/`
- `forntend/`

直接复制到你现有 `SoftwareServicePlatform` 仓库根目录，选择“替换/覆盖同名文件”。

或者在 PowerShell 中运行：

```powershell
.\apply_upgrade.ps1 -ProjectRoot "D:\你的路径\SoftwareServicePlatform"
```

## 编译

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

## 重点测试

1. Customer 登录：顶部出现公司名称。
2. Customer Dashboard：点击“提交新问题”，表单中没有优先级。
3. Customer 创建后：工单显示“待分诊”，SLA 暂未应用。
4. Support 登录：Dashboard 能看到未分配数量和自己的待办数量。
5. Support 工单页：右侧选择优先级 + 处理人，完成分诊。
6. 分诊后：SLA 快照生效；Developer 能看到分配给自己的工单。
7. Developer 顶部 / 左侧能看到个人未处理数量，点击直接进入筛选结果。
8. 公开回复：Customer 可见。
9. 内部备注：Customer 不可见。
10. SLA 黄色 / 红色卡片点击：直接打开对应工单。
11. Dashboard KPI / TOP5 / 状态图点击跳转正常。
12. 下载“本月下载”点击后，下载记录自动只查本月。

## 一个暂时保留的业务限制

`Customer.SalesOwner` 当前数据库中仍然只是姓名字符串，并没有关联 `User.Id`。

因此 Sales 工作台暂时采用：

`Customer.SalesOwner == 当前销售人员 DisplayName`

如果以后要做正式的销售客户归属、转交历史、多销售协作，再单独做数据库关系更合理。这次没有为了一个 Dashboard 强行改表。
