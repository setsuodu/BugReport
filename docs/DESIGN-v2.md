[← 返回文档中心](README.md) | [客户端部署](client-deployment.md) | [服务器部署](server-deployment.md)

# BugReport 服务 设计文档 v2

基于原始需求文档的四大模块拆分,结合以下改版要求重新设计:
1. 服务端用 .NET 10,唯一产物是 Docker image(GHCR),不发 nupkg
2. Dashboard 是独立项目,本仓库只暴露 API,不涉及 Dashboard 代码
3. CI 支持:单独发客户端 / 单独发服务器 / 大版本联合发布
4. 服务端要 AOT Friendly

**范围声明**:本设计不包含 Dashboard 前端、不包含企业微信/钉钉/Slack 等 IM 通知集成(v1 明确不做)。

---

## 1. 仓库策略:Monorepo

理由:CI 要支持"客户端/服务端单独发 + 大版本联合发",这个需求本质上要求版本联动被 CI 感知,monorepo 下用 tag 前缀区分即可实现,multirepo 需要跨仓库触发,链路更脆弱。OpenUPM 支持子目录路径提交,不强制要求 `package.json` 在仓库根目录,所以不构成阻碍。

---

## 2. 仓库目录结构

```
BugReport/
├── .github/workflows/
│   ├── ci.yml                    # PR/push 触发,按 paths 过滤跑对应测试
│   ├── release-client.yml        # tag: client-vX.Y.Z
│   ├── release-server.yml        # tag: server-vX.Y.Z
│   └── release-all.yml           # tag: vX.Y.Z (大版本联合发布)
├── client/
│   └── com.setsuodu.bugreport/            # Unity UPM 包(OpenUPM 提交时填此子目录路径)
│       ├── package.json
│       ├── Runtime/
│       │   ├── BugReporter.cs
│       │   ├── Trace/
│       │   ├── Handlers/                 # v1 只做 C# 层(Application.logMessageReceived + UnhandledException),Lua/Java 不做
│       │   ├── Queue/                    # 本地队列 + 持久化 + 断网重试
│       │   ├── Models/                   # 与服务端 DTO 手动保持一致
│       │   └── Transport/ReportSender.cs
│       ├── Editor/
│       ├── Samples~/
│       └── Tests/
├── server/
│   ├── BugReport.Server.slnx
│   └── src/
│       └── BugReport.Server.Api/         # 唯一产物:Docker image,不是类库
│           ├── BugReport.Server.Api.csproj
│           ├── Program.cs
│           ├── Endpoints/
│           │   ├── IngestEndpoints.cs    # 客户端上报用
│           │   └── QueryEndpoints.cs     # Dashboard 查询用
│           ├── Storage/
│           │   ├── IReportStore.cs
│           │   └── PostgresReportStore.cs
│           ├── Infrastructure/Db/Scripts/    # DbUp 迁移脚本,编号命名,EmbeddedResource 打进程序集
│           │   └── 001_init.sql
│           ├── Models/                   # 与客户端字段手动同步
│           ├── Json/AppJsonSerializerContext.cs
│           ├── Auth/ApiKeyAuthHandler.cs
│           ├── Dockerfile
│           └── appsettings.json
├── deploy/
│   └── docker-compose.example.yml        # 给接入方直接抄的示例
├── shared/
│   └── openapi.yaml                      # 协议唯一事实来源(见下方"协议同步"说明)
└── docs/
    └── REQUIREMENT.md
```

**协议同步说明**:去掉 nupkg 后,客户端和服务端不再共享一个 Contract 项目,DTO 字段靠人工在两边保持一致。为避免漂移,建议:
- `shared/openapi.yaml` 作为字段/接口的唯一事实来源,改协议先改这个文件
- CI 里可以加一个轻量契约测试(用真实请求打 mock 服务,校验 JSON 结构与 openapi.yaml 一致),不是必须项,但建议列入待办

---

## 3. 服务端技术选型(AOT Friendly)

| 项 | 选型 | 原因 |
|---|---|---|
| Web 框架 | ASP.NET Core Minimal API | MVC/Controllers 不兼容 Native AOT,Minimal API 是官方推荐且支持最完整的路径 |
| Host | `WebApplication.CreateSlimBuilder()` | 默认关闭非必要特性,更贴近 AOT 场景 |
| JSON | `System.Text.Json` + `JsonSerializerContext` 源生成器 | 避免反射序列化,AOT 下必需 |
| OpenAPI | `Microsoft.AspNetCore.OpenApi`(.NET 10 内置) | 不用 Swashbuckle(反射重度依赖,AOT 不友好) |
| 数据访问 | PostgreSQL + 原生 ADO.NET(`NpgsqlDataSource` 单例 + `NpgsqlCommand` 手写参数化 SQL) | Npgsql 8.0 起官方声明完全兼容 Native AOT/trimming;EF Core 反射重不适合 AOT;Dapper 实测在 publish 阶段会被裁剪出错,弃用 |
| 数据库迁移 | DbUp(`dbup-postgresql`)+ `WithScriptsEmbeddedInAssembly` | 已在同类 AOT 服务实测跑通,没有 trim 警告;脚本按编号命名(`001_init.sql`…),`EmbeddedResource` 打进程序集,启动时 `PerformUpgrade()` |
| 附件存储 | S3 兼容对象存储(MinIO/OSS),走接口抽象 | 容器化后本地磁盘不可靠,必须外部存储 |
| 鉴权 | Ingest 用 project API Key(Header);Query(给 Dashboard 用)用另一套 admin API Key | v1 先用静态 Key 够用,预留 JWT 扩展点,不在 v1 做 |

csproj 关键项(已实测跑通的组合):
```xml
<PropertyGroup>
  <TargetFramework>net10.0</TargetFramework>
  <Nullable>enable</Nullable>
  <ImplicitUsings>enable</ImplicitUsings>
  <InvariantGlobalization>true</InvariantGlobalization>
  <PublishAot>true</PublishAot>
  <TieredPGO>false</TieredPGO>
  <EventSourceSupport>false</EventSourceSupport>
  <HttpActivityPropagationSupport>false</HttpActivityPropagationSupport>
  <UseSystemResourceKeys>true</UseSystemResourceKeys>
</PropertyGroup>

<ItemGroup>
  <PackageReference Include="Npgsql" Version="10.0.3" />
  <PackageReference Include="dbup-postgresql" Version="7.0.1" />
</ItemGroup>

<ItemGroup>
  <EmbeddedResource Include="Infrastructure/Db/Scripts/*.sql" />
</ItemGroup>
```
`PublishAot=true` 会在 build/IDE 阶段就把 IL2026/IL3050 这类"需要反射/动态代码"的警告打出来,CI 里建议把这些警告当 error 处理,新加三方依赖(比如 MinIO SDK)时能第一时间拦截不兼容的库。

启动流程(`Program.cs` 骨架):
```csharp
var builder = WebApplication.CreateSlimBuilder(args);

builder.Services.ConfigureHttpJsonOptions(opts =>
{
    opts.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonContext.Default);
});

var connStr = builder.Configuration.GetConnectionString("Postgres")
    ?? throw new InvalidOperationException("缺少 ConnectionStrings__Postgres");

builder.Services.AddSingleton(new NpgsqlDataSourceBuilder(connStr).Build());

var app = builder.Build();

// DbUp 迁移:v1 单副本部署,直接在启动路径里跑;
// 一旦要横向扩容,这段要拆成独立的一次性 job(见下方待办),避免多副本同时抢跑迁移
{
    var upgrader = DeployChanges.To
        .PostgresqlDatabase(connStr)
        .WithScriptsEmbeddedInAssembly(typeof(Program).Assembly)
        .WithTransaction()
        .LogToConsole()
        .Build();
    var result = upgrader.PerformUpgrade();
    if (!result.Successful)
        throw new Exception("DB migration failed: " + result.Error);
}

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
// ... ingest / query endpoints
```

---

## 4. API 面(只包含这个服务自己的接口,不涉及 Dashboard UI)

两类路由分开,鉴权方式不同:

```
POST /api/v1/ingest/reports              # 客户端上报,project API Key
POST /api/v1/ingest/attachments/init      # 附件分片上传-初始化
POST /api/v1/ingest/attachments/complete  # 附件分片上传-完成

GET  /api/v1/reports                      # 列表查询,admin API Key,供 Dashboard 调用
GET  /api/v1/reports/{id}                 # 详情
PATCH /api/v1/reports/{id}/status         # 状态流转(Open/Fixed/Closed)
```

不做:Webhook / IM 通知(企业微信/钉钉/Slack)。如果以后要做,建议做成"事件表 + Dashboard 自己轮询/订阅",而不是服务端直接对接各家 IM API,避免这个服务承担越来越多和核心职责无关的集成逻辑。

---

## 5. Docker / Compose

Dockerfile 采用多阶段构建,AOT 发布产物是原生可执行文件,不需要完整 aspnet 运行时镜像,直接用 `runtime-deps` 的 chiseled 版本(比普通 runtime-deps 更小,没有包管理器和多余系统工具,攻击面也更小)。build 阶段要装 `clang` + `zlib1g-dev`,这是 Linux 下 ILC(AOT 编译器)的必需依赖,漏了会导致 publish 直接失败。

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
RUN apt-get update && apt-get install -y clang zlib1g-dev && rm -rf /var/lib/apt/lists/*

COPY server/src/BugReport.Server.Api/BugReport.Server.Api.csproj BugReport.Server.Api/
RUN dotnet restore BugReport.Server.Api/BugReport.Server.Api.csproj

COPY server/src/BugReport.Server.Api/ BugReport.Server.Api/
WORKDIR /src/BugReport.Server.Api
RUN dotnet publish BugReport.Server.Api.csproj -c Release -r linux-x64 -o /app/publish -p:PublishAot=true

FROM mcr.microsoft.com/dotnet/runtime-deps:10.0-noble-chiseled
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["./BugReport.Server.Api"]
```

`deploy/docker-compose.example.yml` 给接入方"只改配置不改代码"用:

```yaml
services:
  bugreport-server:
    image: ghcr.io/<org>/bugreport-server:1.0.0
    environment:
      - ConnectionStrings__Postgres=Host=db;Database=bugreport;...
      - Storage__Endpoint=...
      - Storage__Bucket=...
      - Auth__IngestApiKey=...
      - Auth__AdminApiKey=...
    ports:
      - "8080:8080"
```

镜像只出 `linux/amd64`,不做多架构(arm64 不考虑)。

---

## 6. CI/CD 设计

Tag 策略:

| Tag 格式 | 触发 | 产物 |
|---|---|---|
| `client-vX.Y.Z` | `release-client.yml` | OpenUPM 包发布(子目录路径提交) |
| `server-vX.Y.Z` | `release-server.yml` | Docker image push 到 GHCR,tag 为 `X.Y.Z` |
| `vX.Y.Z` | `release-all.yml` | 客户端 + 服务端一起发,两边版本号对齐到 X.Y.Z |

PR/push 到主分支时跑 `ci.yml`,用 `paths` 过滤:改 `client/**` 只跑 Unity Test Runner,改 `server/**` 只跑 `dotnet test` + `dotnet publish -p:PublishAot=true`(校验 AOT 兼容性),两边都改则都跑。

---

## 7. 待确认 / 待办

- [x] Npgsql 原生 ADO.NET + DbUp 迁移的 AOT 兼容性——已在同类服务实测跑通,`dotnet publish -p:PublishAot=true` 无告警,直接照抄这套组合
- [ ] GHCR 镜像是否需要保留历史 tag 的滚动更新(比如 `:latest` 指向最新 release),还是只用不可变版本号 tag
- [ ] admin API Key 是静态配置还是要做成可轮换/多租户(多个 Dashboard 部署实例共用一个服务时)
- [ ] 附件对象存储选 MinIO 自建还是云厂商 OSS/S3,影响 `deploy/docker-compose.example.yml` 里要不要内置一个 MinIO 服务
- [ ] DbUp 迁移现在跑在启动路径里,v1 单副本没问题;一旦要多副本部署,要拆成独立的一次性迁移 job,避免多个副本同时抢跑迁移产生竞态
