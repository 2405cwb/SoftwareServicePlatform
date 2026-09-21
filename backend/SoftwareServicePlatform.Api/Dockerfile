# =========================================================
# 第一阶段：编译
# =========================================================
# SDK 镜像里面有 dotnet restore / build / publish 等开发工具
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /src

# 先只复制 csproj
# 这样 NuGet restore 可以利用 Docker 缓存
COPY ["SoftwareServicePlatform.Api.csproj", "./"]

RUN dotnet restore "SoftwareServicePlatform.Api.csproj"

# 再复制后端全部源码
COPY . .

# 发布 Release 版本
RUN dotnet publish "SoftwareServicePlatform.Api.csproj" \
    -c Release \
    -o /app/publish \
    /p:UseAppHost=false


# =========================================================
# 第二阶段：真正运行
# =========================================================
# 运行环境不需要完整 SDK，只需要 ASP.NET Core Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final

WORKDIR /app

# ASP.NET Core 容器内部监听 8080
EXPOSE 8080

ENV ASPNETCORE_URLS=http://+:8080

# 从编译阶段复制发布结果
COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "SoftwareServicePlatform.Api.dll"]