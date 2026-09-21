# SoftwareServicePlatform Docker + 阿里云 ECS 部署操作说明

> 项目：SoftwareServicePlatform  
> 技术栈：React + Nginx + ASP.NET Core 10 + PostgreSQL 17 + Docker Compose  
> 部署环境：阿里云 ECS / Ubuntu 24.04 LTS / x86_64  
> 用途：记录从本地 Docker 化、生产配置、购买 ECS、安装 Docker、部署上线，到故障排查的完整过程。

> **安全说明**：本文所有密码、JWT Key、SSH 私钥均使用占位符。真实密码不要提交 Git、不要写入 README、不要发到工单或聊天记录中。

---

## 1. 最终部署架构

```text
互联网
   |
   | 80 / 443
   v
React + Nginx
   |
   | Docker 内部网络
   v
ASP.NET Core API :8080
   |
   | Docker 内部网络
   v
PostgreSQL :5432

宿主机：
/opt/software-service-platform/storage
    └─ 软件安装包 / 更新包 / 工单附件 / 版本附件
```

生产环境原则：

- 只有 Nginx 对公网开放。
- PostgreSQL 不对公网开放。
- ASP.NET Core API 不直接对公网开放。
- API 和 PostgreSQL 通过 Docker 内部网络通信。
- 数据库使用 Docker Volume 持久化。
- 软件安装包、更新包和附件保存到宿主机目录。
- ASP.NET Core DataProtection Key 使用 Docker Volume 持久化。
- 所有真实密码放在服务器 `.env` 中，不提交 Git。

---

# 2. 本地 Docker 化

## 2.1 后端 Dockerfile

位置：

```text
backend/SoftwareServicePlatform.Api/Dockerfile
```

内容：

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["SoftwareServicePlatform.Api.csproj", "./"]
RUN dotnet restore "SoftwareServicePlatform.Api.csproj"
COPY . .
RUN dotnet publish "SoftwareServicePlatform.Api.csproj" \
    -c Release \
    -o /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "SoftwareServicePlatform.Api.dll"]
```

后端 `.dockerignore`：

```text
bin/
obj/
.vs/
storage/
*.user
*.suo
*.log
```

---

## 2.2 前端 Dockerfile

位置：

```text
forntend/Dockerfile
```

> 当前项目目录实际拼写是 `forntend`，Compose 中必须按实际目录名写。

```dockerfile
FROM node:24-alpine AS build
WORKDIR /app
COPY package.json package-lock.json ./
RUN npm ci
COPY . .
RUN npm run build

FROM nginx:alpine AS final
RUN rm -f /etc/nginx/conf.d/default.conf
RUN rm -rf /usr/share/nginx/html/*
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist/ /usr/share/nginx/html/
EXPOSE 80
```

前端 `.dockerignore`：

```text
node_modules/
dist/
.git/
.vscode/
*.log
```

---

## 2.3 Nginx 配置

位置：

```text
forntend/nginx.conf
```

```nginx
server {
    listen 80;
    server_name _;

    root /usr/share/nginx/html;
    index index.html;

    location / {
        try_files $uri $uri/ /index.html;
    }

    location /api/ {
        proxy_pass http://api:8080;
        proxy_http_version 1.1;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }

    location /hubs/ {
        proxy_pass http://api:8080;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection "upgrade";
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }
}
```

作用：

```text
浏览器 /api/*
      ↓
Nginx
      ↓
api:8080/api/*

浏览器 /hubs/*
      ↓
Nginx
      ↓
api:8080/hubs/*
```

---

# 3. 本地开发 Compose

根目录文件：

```text
compose.yaml
```

本地开发环境主要用途：

- PostgreSQL 映射到 `localhost:5433`
- API 映射到 `localhost:5108`
- 前端映射到 `localhost:8080`
- 方便 DBeaver、浏览器、接口调试直接访问

核心结构：

```yaml
services:
  postgres:
    image: postgres:17
    container_name: software-service-postgres
    restart: unless-stopped
    environment:
      POSTGRES_USER: ${POSTGRES_USER}
      POSTGRES_PASSWORD: ${POSTGRES_PASSWORD}
      POSTGRES_DB: ${POSTGRES_DB}
    ports:
      - "127.0.0.1:5433:5432"
    volumes:
      - postgres-data:/var/lib/postgresql/data
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U $$POSTGRES_USER -d $$POSTGRES_DB"]
      interval: 5s
      timeout: 5s
      retries: 10

  api:
    build:
      context: ./backend/SoftwareServicePlatform.Api
      dockerfile: Dockerfile
    image: software-service-api:dev
    container_name: software-service-api
    restart: unless-stopped
    depends_on:
      postgres:
        condition: service_healthy
    environment:
      ASPNETCORE_ENVIRONMENT: Production
      ASPNETCORE_URLS: http://+:8080
      ConnectionStrings__DefaultConnection: "Host=postgres;Port=5432;Database=${POSTGRES_DB};Username=${POSTGRES_USER};Password=${POSTGRES_PASSWORD}"
      Jwt__Issuer: SoftwareServicePlatform
      Jwt__Audience: SoftwareServicePlatform.Client
      Jwt__Key: ${JWT_KEY}
      Jwt__ExpireMinutes: 120
      BootstrapAdmin__Enabled: ${BOOTSTRAP_ADMIN_ENABLED}
      BootstrapAdmin__Username: ${BOOTSTRAP_ADMIN_USERNAME}
      BootstrapAdmin__Password: ${BOOTSTRAP_ADMIN_PASSWORD}
      BootstrapAdmin__DisplayName: ${BOOTSTRAP_ADMIN_DISPLAY_NAME}
    ports:
      - "127.0.0.1:5108:8080"
    volumes:
      - ./docker-data/storage:/app/storage

  frontend:
    build:
      context: ./forntend
      dockerfile: Dockerfile
    image: software-service-frontend:dev
    container_name: software-service-frontend
    restart: unless-stopped
    depends_on:
      - api
    ports:
      - "127.0.0.1:8080:80"

volumes:
  postgres-data:
```

---

# 4. 本地 `.env`

根目录：

```text
.env
```

示例：

```env
POSTGRES_USER=ssp
POSTGRES_PASSWORD=本地开发数据库密码
POSTGRES_DB=software_service_platform

JWT_KEY=本地开发JWT密钥

BOOTSTRAP_ADMIN_ENABLED=true
BOOTSTRAP_ADMIN_USERNAME=admin
BOOTSTRAP_ADMIN_PASSWORD=本地管理员初始密码
BOOTSTRAP_ADMIN_DISPLAY_NAME=系统管理员

STORAGE_PATH=./docker-data/storage
```

确认 `.env` 已被 Git 忽略：

```bash
git check-ignore -v .env
```

根 `.gitignore` 增加：

```gitignore
# Docker runtime data
docker-data/
```

---

# 5. EF Core 自动 Migration

本地最初手工执行：

```powershell
dotnet ef database update --connection "Host=localhost;Port=5433;Database=software_service_platform;Username=ssp;Password=..."
```

服务器部署不适合每次手工执行，因此 `Program.cs` 增加：

```csharp
using (var scope = app.Services.CreateScope())
{
    var dbContext =
        scope.ServiceProvider
            .GetRequiredService<AppDbContext>();

    await dbContext.Database.MigrateAsync();
}
```

正确启动顺序：

```text
API 启动
   ↓
EF Core Migration
   ↓
创建数据库表
   ↓
NotificationPolicySeeder
   ↓
AdminAccountSeeder
   ↓
应用正式运行
```

---

# 6. 首次管理员初始化

全新数据库最开始 `Users` 表为 0 行，但注册接口只能创建 Customer 用户，因此没有管理员可登录。

增加：

```text
Services/AdminAccountSeeder.cs
```

通过以下配置控制：

```text
BootstrapAdmin__Enabled
BootstrapAdmin__Username
BootstrapAdmin__Password
BootstrapAdmin__DisplayName
```

第一次启动：

```env
BOOTSTRAP_ADMIN_ENABLED=true
```

自动创建：

```text
Username = admin
Role = Admin
CustomerId = null
IsEnabled = true
```

密码使用 `PasswordHasher<User>` 写入 Hash，不保存明文。

管理员创建并成功登录后改为：

```env
BOOTSTRAP_ADMIN_ENABLED=false
```

然后：

```bash
docker compose -f compose.prod.yaml --env-file .env \
  up -d --force-recreate api
```

> 修改 `.env` 中 `BOOTSTRAP_ADMIN_PASSWORD` 不会修改已经存在的 admin 密码，它只用于首次创建。

---

# 7. 本地遇到的坑及解决

## 7.1 PowerShell 执行 PostgreSQL SQL 时双引号丢失

原命令：

```powershell
docker compose exec postgres psql -U ssp -d software_service_platform -c 'SELECT COUNT(*) FROM "Users";'
```

报：

```text
ERROR: relation "users" does not exist
```

最终使用管道：

```powershell
'SELECT COUNT(*) FROM "Users";' |
docker compose exec -T postgres \
  psql -U ssp -d software_service_platform
```

---

## 7.2 Compose `environment` 缩进错误

报：

```text
services.api.depends_on.postgres additional properties 'environment' not allowed
```

错误结构：

```yaml
api:
  depends_on:
    postgres:
      condition: service_healthy
      environment:
```

正确：

```yaml
api:
  depends_on:
    postgres:
      condition: service_healthy

  environment:
    ...
```

---

## 7.3 Compose 重复粘贴导致 `build` 重复

报：

```text
mapping key "build" already defined
```

原因：`api:` 下重复粘贴了两套 `build/image/depends_on/environment`。

解决：不要继续局部补丁，直接整份覆盖正确的 `compose.yaml`。

---

## 7.4 BootstrapAdmin 放错服务

曾错误放入：

```yaml
postgres:
  environment:
```

正确应该在：

```yaml
api:
  environment:
```

否则 ASP.NET Core 读取不到。

---

## 7.5 API 没收到 BootstrapAdmin 配置

`docker compose config` 中最初看不到 BootstrapAdmin 环境变量，导致 Seeder 读取 `Enabled=false` 并直接跳过。

解决后必须先执行：

```bash
docker compose config
```

确认变量已经进入 `api.environment`，再 build。

---

## 7.6 前端一直显示 Welcome to nginx

解决：

```dockerfile
RUN rm -f /etc/nginx/conf.d/default.conf
RUN rm -rf /usr/share/nginx/html/*
```

然后：

```bash
docker compose build --no-cache frontend
docker compose up -d --force-recreate frontend
```

---

# 8. Git 管理过程

Docker 功能分支：

```bash
git switch -c feat/docker-deployment
```

开发 Docker 配置通过 PR #23 合并。

后续新增：

```text
.env.production.example
compose.prod.yaml
```

因为原 PR 已合并，新 commit 在合并后才 push，因此同步 main：

```bash
git fetch origin
git rebase origin/main
git push --force-with-lease
```

再通过 PR #24 合并生产部署配置。

最后：

```bash
git switch main
git pull origin main
```

原则：

```text
main
 ↓
feature/fix branch
 ↓
commit
 ↓
push
 ↓
Pull Request
 ↓
main
 ↓
服务器 git pull
```

不要直接在生产服务器修改项目源码。

---

# 9. 生产 Compose

根目录：

```text
compose.prod.yaml
```

生产环境和本地最大区别：

```text
开发：
localhost:8080 → frontend
localhost:5108 → API
localhost:5433 → PostgreSQL

生产：
公网 :80 → frontend
API 不暴露公网
PostgreSQL 不暴露公网
```

核心配置：

```yaml
services:
  postgres:
    image: postgres:17
    restart: unless-stopped
    environment:
      POSTGRES_USER: ${POSTGRES_USER}
      POSTGRES_PASSWORD: ${POSTGRES_PASSWORD}
      POSTGRES_DB: ${POSTGRES_DB}
    volumes:
      - postgres-data:/var/lib/postgresql/data
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U $$POSTGRES_USER -d $$POSTGRES_DB"]
      interval: 5s
      timeout: 5s
      retries: 10

  api:
    build:
      context: ./backend/SoftwareServicePlatform.Api
      dockerfile: Dockerfile
    image: software-service-api:prod
    restart: unless-stopped
    depends_on:
      postgres:
        condition: service_healthy
    environment:
      ASPNETCORE_ENVIRONMENT: Production
      ASPNETCORE_URLS: http://+:8080
      ConnectionStrings__DefaultConnection: "Host=postgres;Port=5432;Database=${POSTGRES_DB};Username=${POSTGRES_USER};Password=${POSTGRES_PASSWORD}"
      Jwt__Issuer: SoftwareServicePlatform
      Jwt__Audience: SoftwareServicePlatform.Client
      Jwt__Key: ${JWT_KEY}
      Jwt__ExpireMinutes: 120
      BootstrapAdmin__Enabled: ${BOOTSTRAP_ADMIN_ENABLED}
      BootstrapAdmin__Username: ${BOOTSTRAP_ADMIN_USERNAME}
      BootstrapAdmin__Password: ${BOOTSTRAP_ADMIN_PASSWORD}
      BootstrapAdmin__DisplayName: ${BOOTSTRAP_ADMIN_DISPLAY_NAME}
    volumes:
      - ${STORAGE_PATH}:/app/storage
      - data-protection:/root/.aspnet/DataProtection-Keys

  frontend:
    build:
      context: ./forntend
      dockerfile: Dockerfile
    image: software-service-frontend:prod
    restart: unless-stopped
    depends_on:
      - api
    ports:
      - "80:80"

volumes:
  postgres-data:
  data-protection:
```

---

# 10. `.env.production.example`

提交 Git 的模板只放占位符：

```env
POSTGRES_USER=ssp_app
POSTGRES_PASSWORD=CHANGE_ME_DATABASE_PASSWORD
POSTGRES_DB=software_service_platform

JWT_KEY=CHANGE_ME_TO_A_LONG_RANDOM_JWT_SECRET

BOOTSTRAP_ADMIN_ENABLED=true
BOOTSTRAP_ADMIN_USERNAME=admin
BOOTSTRAP_ADMIN_PASSWORD=CHANGE_ME_ADMIN_PASSWORD
BOOTSTRAP_ADMIN_DISPLAY_NAME=系统管理员

STORAGE_PATH=/opt/software-service-platform/storage
```

---

# 11. 阿里云 ECS 购买配置

实际部署选型：

```text
产品：阿里云 ECS
地域：华东1（杭州）
系统：Ubuntu 24.04 64位（LTS）
架构：x86_64
规格：ecs.e-c1m2.large
CPU：2 vCPU
内存：4 GiB
系统盘：约 60 GiB
公网 IPv4：开启
公网计费：按使用流量
峰值带宽：5 Mbps
```

不选抢占式实例，使用按量付费做初期测试。

---

# 12. ECS 安全组

新建：

```text
ssp-prod-sg
```

开放：

```text
22   SSH
80   HTTP
443  HTTPS
```

不开放：

```text
3389 RDP
5432 PostgreSQL
8080 ASP.NET Core
```

---

# 13. SSH 密钥

创建：

```text
ssp-prod-key
```

实际下载文件名是：

```text
_ssp-prod-key.pem
```

最初误用：

```bash
chmod 600 ~/.ssh/ssp-prod-key.pem
```

报 `No such file or directory`。

正确：

```bash
chmod 600 ~/.ssh/_ssp-prod-key.pem
```

登录：

```bash
ssh -i ~/.ssh/_ssp-prod-key.pem ecs-user@<服务器公网IP>
```

第一次提示主机指纹：

```text
Are you sure you want to continue connecting?
```

输入：

```text
yes
```

---

# 14. 服务器环境检查

```bash
cat /etc/os-release
uname -m
nproc
free -h
df -h /
git --version
docker --version
docker compose version
```

实际环境：

```text
Ubuntu 24.04.5 LTS
x86_64
2 CPU
约 3.5 GiB RAM
4 GiB Swap
根盘约 59G
Git 2.43
```

---

# 15. 一个真实误操作：Linux 命令执行在 Windows 上

退出 SSH 后在 Git Bash 执行：

```bash
sudo apt update
```

Windows 提示：

```text
已在此计算机上禁用 Sudo
```

判断方式：

Windows：

```text
cwb@DESKTOP-MNAHQU9 MINGW64 ~
```

服务器：

```text
ecs-user@iZ...:~$
```

执行 Linux 系统命令前先看提示符。

---

# 16. Ubuntu 安装 Docker

## 16.1 Docker 官方源失败

```bash
sudo curl -fsSL https://download.docker.com/linux/ubuntu/gpg \
  -o /etc/apt/keyrings/docker.asc
```

报：

```text
curl: (35) Recv failure: Connection reset by peer
```

改用阿里云 Docker CE 镜像源。

## 16.2 阿里云 Docker CE 源

```bash
sudo apt update
sudo apt install -y ca-certificates curl gnupg
sudo install -m 0755 -d /etc/apt/keyrings
```

```bash
curl -fsSL \
  http://mirrors.cloud.aliyuncs.com/docker-ce/linux/ubuntu/gpg \
  | sudo gpg --dearmor \
  -o /etc/apt/keyrings/docker.gpg
```

```bash
sudo chmod a+r /etc/apt/keyrings/docker.gpg
```

```bash
ARCH=$(dpkg --print-architecture)
DISTRO=$(. /etc/os-release && echo "$VERSION_CODENAME")

sudo tee /etc/apt/sources.list.d/docker.list > /dev/null <<EOF2
deb [arch=${ARCH} signed-by=/etc/apt/keyrings/docker.gpg] http://mirrors.cloud.aliyuncs.com/docker-ce/linux/ubuntu ${DISTRO} stable
EOF2
```

```bash
sudo apt update
```

```bash
sudo apt install -y \
  docker-ce \
  docker-ce-cli \
  containerd.io \
  docker-buildx-plugin \
  docker-compose-plugin
```

```bash
sudo systemctl enable --now docker
sudo systemctl status docker --no-pager
```

应看到：

```text
Active: active (running)
```

---

# 17. Docker 普通用户权限

```bash
sudo usermod -aG docker $USER
```

然后退出并重新 SSH 登录：

```bash
exit
```

重新登录后：

```bash
docker --version
docker compose version
docker ps
```

如果不需要 sudo 就成功。

---

# 18. Docker Hub 网络问题

第一次：

```bash
sudo docker run hello-world
```

出现 Docker Hub 超时：

```text
dial tcp ...:443: i/o timeout
```

后续重试成功：

```text
Hello from Docker!
```

说明 Docker Engine 正常，只是外网 Registry 访问不稳定。

---

# 19. Registry Mirror 情况

`docker info` 显示：

```text
Registry Mirrors:
 https://q3q5y8s4.mirror.aliyuncs.com/
```

而：

```bash
curl https://registry-1.docker.io/v2/
```

一直卡住。

但是：

```bash
docker pull nginx:alpine
```

成功。

说明：

```text
Docker Hub 直连        不稳定/不可用
阿里云 Mirror          部分镜像可用
```

---

# 20. PostgreSQL 镜像 `not found`

```bash
docker pull postgres:17
```

报：

```text
docker.io/library/postgres:17: not found
```

换成固定版本仍然失败。

这里不是代码问题，而是当前镜像加速源没有正确提供该镜像。

最终解决：**Windows Docker Desktop 拉取官方 linux/amd64 镜像，再上传服务器。**

---

# 21. Windows 手工转移 Docker 镜像

Windows：

```bash
docker pull --platform linux/amd64 postgres:17
```

```bash
docker save postgres:17 \
  | gzip \
  > ~/Downloads/postgres-17.tar.gz
```

上传：

```bash
scp -i ~/.ssh/_ssp-prod-key.pem \
  ~/Downloads/postgres-17.tar.gz \
  ecs-user@<服务器公网IP>:/tmp/
```

服务器：

```bash
gunzip -c /tmp/postgres-17.tar.gz | docker load
```

```bash
docker images postgres
rm /tmp/postgres-17.tar.gz
```

---

# 22. Node 镜像出现相同问题

第一次 build：

```text
node:24-alpine: not found
```

同时：

- `nginx:alpine` 可正常获取
- `.NET 10 SDK / Runtime` 从微软仓库可正常获取

Node 同样采用镜像转移：

```bash
docker pull --platform linux/amd64 node:24-alpine
```

```bash
docker save node:24-alpine \
  | gzip \
  > ~/Downloads/node-24-alpine.tar.gz
```

```bash
scp -i ~/.ssh/_ssp-prod-key.pem \
  ~/Downloads/node-24-alpine.tar.gz \
  ecs-user@<服务器公网IP>:/tmp/
```

服务器：

```bash
gunzip -c /tmp/node-24-alpine.tar.gz | docker load
rm /tmp/node-24-alpine.tar.gz
```

---

# 23. 服务器目录结构

```bash
sudo mkdir -p /opt/software-service-platform/app
sudo mkdir -p /opt/software-service-platform/storage
sudo chown -R $USER:$USER /opt/software-service-platform
```

最终：

```text
/opt/software-service-platform/
│
├── app/
│   ├── compose.prod.yaml
│   ├── .env
│   ├── backend/
│   └── forntend/
│
└── storage/
    ├── 软件安装包
    ├── 更新包
    └── 附件
```

克隆：

```bash
cd /opt/software-service-platform

git clone \
  https://github.com/2405cwb/SoftwareServicePlatform.git \
  app
```

检查：

```bash
cd app
git branch --show-current
git log --oneline -5
ls
```

---

# 24. 创建生产 `.env`

随机生成：

```bash
DB_PASSWORD=$(openssl rand -hex 24)
JWT_KEY=$(openssl rand -hex 32)
ADMIN_PASSWORD=$(openssl rand -hex 16)
```

管理员密码自行保存，不要发给别人。

```bash
umask 077
```

```bash
cat > .env <<EOF2
POSTGRES_USER=ssp_app
POSTGRES_PASSWORD=$DB_PASSWORD
POSTGRES_DB=software_service_platform

JWT_KEY=$JWT_KEY

BOOTSTRAP_ADMIN_ENABLED=true
BOOTSTRAP_ADMIN_USERNAME=admin
BOOTSTRAP_ADMIN_PASSWORD=$ADMIN_PASSWORD
BOOTSTRAP_ADMIN_DISPLAY_NAME=系统管理员

STORAGE_PATH=/opt/software-service-platform/storage
EOF2
```

检查：

```bash
ls -l .env
```

权限应类似：

```text
-rw-------
```

---

# 25. Compose 配置检查

```bash
docker compose \
  -f compose.prod.yaml \
  --env-file .env \
  config > /dev/null
```

无输出即通过。

如果报：

```text
The "STORAGE_PATH" variable is not set
invalid spec: :/app/storage
```

说明 `.env` 缺：

```env
STORAGE_PATH=/opt/software-service-platform/storage
```

---

# 26. 构建和启动

为了方便定位，第一次可以分开：

```bash
docker compose -f compose.prod.yaml --env-file .env build frontend
```

```bash
docker compose -f compose.prod.yaml --env-file .env build api
```

然后：

```bash
docker compose -f compose.prod.yaml --env-file .env up -d --no-build
```

或者一次完成：

```bash
docker compose -f compose.prod.yaml --env-file .env up -d --build
```

---

# 27. 检查运行状态

```bash
docker compose -f compose.prod.yaml --env-file .env ps
```

目标：

```text
postgres    Up ... (healthy)
api         Up ...
frontend    Up ...
```

API 日志：

```bash
docker compose -f compose.prod.yaml --env-file .env logs --tail=150 api
```

重点确认：

```text
首次管理员账号 admin 创建成功
Application started
Hosting environment: Production
Now listening on: http://[::]:8080
```

---

# 28. 服务器本机测试

Nginx：

```bash
curl -I http://localhost
```

期望：

```text
HTTP/1.1 200 OK
Server: nginx
```

API 反向代理：

```bash
curl http://localhost/api/platform/info
```

成功返回 JSON，说明：

```text
Nginx → ASP.NET Core → PostgreSQL
```

链路正常。

---

# 29. 公网访问及管理员登录

浏览器：

```text
http://<服务器公网IP>
```

账号：

```text
admin
```

密码：首次生成并写入 `.env` 的：

```text
BOOTSTRAP_ADMIN_PASSWORD
```

如果忘记，可在服务器自己查看：

```bash
grep BOOTSTRAP_ADMIN_PASSWORD .env
```

> 不要把该命令输出发给别人。

---

# 30. 关闭首次管理员初始化

登录成功后：

```bash
nano .env
```

改：

```env
BOOTSTRAP_ADMIN_ENABLED=true
```

为：

```env
BOOTSTRAP_ADMIN_ENABLED=false
```

Nano 保存：

```text
Ctrl + O
Enter
Ctrl + X
```

然后：

```bash
docker compose -f compose.prod.yaml --env-file .env \
  up -d --force-recreate api
```

已有 admin 不会被删除。

---

# 31. 常用运维命令

进入项目：

```bash
cd /opt/software-service-platform/app
```

查看状态：

```bash
docker compose -f compose.prod.yaml --env-file .env ps
```

API 日志：

```bash
docker compose -f compose.prod.yaml --env-file .env logs -f api
```

前端日志：

```bash
docker compose -f compose.prod.yaml --env-file .env logs -f frontend
```

PostgreSQL 日志：

```bash
docker compose -f compose.prod.yaml --env-file .env logs -f postgres
```

重启：

```bash
docker compose -f compose.prod.yaml --env-file .env restart
```

停止但保留 Volume：

```bash
docker compose -f compose.prod.yaml --env-file .env down
```

重新启动：

```bash
docker compose -f compose.prod.yaml --env-file .env up -d
```

---

# 32. 危险命令：不要随便 `down -v`

不要随意执行：

```bash
docker compose down -v
```

因为 `-v` 会删除 Compose 管理的 Volume。

PostgreSQL 数据位于：

```text
postgres-data
```

生产环境日常停止使用：

```bash
docker compose down
```

不要带 `-v`。

---

# 33. 以后服务器更新代码

```bash
cd /opt/software-service-platform/app
```

先：

```bash
git status
```

服务器原则上不直接修改源码。

更新：

```bash
git pull origin main
```

后端变化：

```bash
docker compose -f compose.prod.yaml --env-file .env up -d --build api
```

前端变化：

```bash
docker compose -f compose.prod.yaml --env-file .env up -d --build frontend
```

全部更新：

```bash
docker compose -f compose.prod.yaml --env-file .env up -d --build
```

EF Core Migration 会在 API 启动时自动检查和应用。

---

# 34. 当前已经完成

- [x] 阿里云 ECS 创建
- [x] Ubuntu 24.04 LTS
- [x] SSH Key 登录
- [x] Docker Engine
- [x] Docker Compose Plugin
- [x] PostgreSQL Docker 化
- [x] ASP.NET Core Docker 化
- [x] React Docker 化
- [x] Nginx 反向代理
- [x] SignalR `/hubs` 反向代理
- [x] PostgreSQL 持久化
- [x] storage 持久化
- [x] DataProtection Key 持久化
- [x] 自动 EF Core Migration
- [x] 首次管理员初始化
- [x] 初始化后关闭 Bootstrap
- [x] 公网 IP 可访问网页
- [x] API / PostgreSQL 不直接暴露公网

---

# 35. 目前仍需继续完善

## 35.1 HTTPS

当前还是：

```text
http://服务器IP
```

后续应升级：

```text
域名 → HTTPS → 443
```

## 35.2 域名 / ICP 备案

服务器位于中国大陆杭州。后续正式通过域名向公网提供服务时，需要结合实际业务处理备案。

## 35.3 PostgreSQL 自动备份

```text
持久化 ≠ 备份
```

建议后续：

```text
每日 pg_dump
保留 7~30 天
异地备份
```

## 35.4 storage 备份

当前：

```text
/opt/software-service-platform/storage
```

仍在 ECS 系统盘。以后安装包很多时建议迁移独立数据盘或 OSS。

## 35.5 Docker 镜像供应链

目前部分 Docker Hub 镜像通过阿里云 Mirror 获取失败，临时采用：

```text
Windows Docker Desktop
  ↓ docker save
scp
  ↓
ECS docker load
```

长期建议使用稳定 Registry / ACR / CI 构建发布流程。

---

# 36. 推荐后续 CI/CD 架构

当前：

```text
开发机
 ↓
GitHub
 ↓
服务器 git pull
 ↓
服务器 docker build
```

以后升级：

```text
开发机
 ↓
Pull Request
 ↓
main
 ↓
CI
 ↓
构建 Docker Image
 ↓
推送 Registry
 ↓
生产服务器 pull
 ↓
docker compose up -d
```

---

# 37. 故障快速对照表

| 现象 | 原因 | 解决 |
|---|---|---|
| `environment not allowed` | YAML 缩进错误 | `environment` 与 `depends_on` 同级 |
| `mapping key build already defined` | Compose 重复粘贴 | 整份覆盖正确版本 |
| 管理员未创建 | BootstrapAdmin 未传给 API | `docker compose config` 检查 |
| `relation "users" does not exist` | PowerShell 引号丢失 | 管道传 SQL |
| Welcome to nginx | 默认页面/缓存 | 清 html + `--no-cache` 重建 |
| `STORAGE_PATH not set` | `.env` 缺变量 | 增加 `STORAGE_PATH` |
| Docker GPG `Connection reset` | 官方源网络不稳定 | 阿里云 Docker CE 软件源 |
| `hello-world` timeout | Docker Hub 网络不稳定 | 重试 / Mirror |
| `postgres:17 not found` | Mirror 未正确提供 | Windows pull/save/scp/load |
| `node:24-alpine not found` | 同上 | 同样手工导入 |
| Windows 提示 sudo 禁用 | 当前不在服务器 | SSH 回 ECS |
| SSH Key 找不到 | 实际文件名不同 | `ls ~/.ssh` 确认 |

---

# 38. 本次部署最重要的经验

1. Docker 的核心不是“装进盒子”，而是把前端、后端、数据库、运行时、网络和启动顺序全部变成可重复部署的配置。
2. PostgreSQL 和 API 不应直接暴露公网。
3. Docker Volume 是持久化，不是备份。
4. `.env` 与源码必须分离。
5. Compose 有问题先 `docker compose config`。
6. 服务有问题先 `docker compose ps` + `docker compose logs`。
7. 中国大陆服务器出现 Docker Hub 网络问题很常见，`not found` 不一定表示镜像标签真的不存在。
8. 生产服务器不要直接开发代码，仍然走 Git 分支、PR、main、服务器 pull 的流程。

---

# 39. 最终状态

```text
Windows 本地开发
      ↓
GitHub main
      ↓
阿里云 ECS
      ↓
Ubuntu 24.04
      ↓
Docker Compose
      ├─ React + Nginx
      ├─ ASP.NET Core 10
      └─ PostgreSQL 17
      ↓
公网可访问
```

下一阶段重点：

```text
HTTPS
域名
备案
数据库备份
storage 备份
服务器安全加固
日志
监控
CI/CD
```

---

# 附录 A：常用命令速查

```bash
cd /opt/software-service-platform/app

# 校验 Compose
docker compose -f compose.prod.yaml --env-file .env config

# 启动
docker compose -f compose.prod.yaml --env-file .env up -d

# 构建并启动
docker compose -f compose.prod.yaml --env-file .env up -d --build

# 状态
docker compose -f compose.prod.yaml --env-file .env ps

# API 日志
docker compose -f compose.prod.yaml --env-file .env logs -f api

# 前端日志
docker compose -f compose.prod.yaml --env-file .env logs -f frontend

# PostgreSQL 日志
docker compose -f compose.prod.yaml --env-file .env logs -f postgres

# 停止
docker compose -f compose.prod.yaml --env-file .env down

# 重启
docker compose -f compose.prod.yaml --env-file .env restart

# 更新代码
git pull origin main
```

---

# 附录 B：SSH

Windows Git Bash：

```bash
ssh -i ~/.ssh/_ssp-prod-key.pem \
  ecs-user@<服务器公网IP>
```

---

# 附录 C：手工转移 Docker 镜像

Windows：

```bash
docker pull --platform linux/amd64 <镜像>

docker save <镜像> \
  | gzip \
  > ~/Downloads/image.tar.gz

scp -i ~/.ssh/_ssp-prod-key.pem \
  ~/Downloads/image.tar.gz \
  ecs-user@<服务器公网IP>:/tmp/
```

服务器：

```bash
gunzip -c /tmp/image.tar.gz | docker load
rm /tmp/image.tar.gz
```

---

**文档结束**
