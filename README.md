# BugReport

Unity 客户端上报 + .NET 10 服务端接收/查询的 Bug/Crash 报告服务。

- **客户端**：Unity UPM 包（OpenUPM 子目录发布）
- **服务端**：ASP.NET Core Minimal API，Native AOT，唯一产物为 Docker 镜像（GHCR）
- **不含**：Dashboard 前端、IM 通知集成

设计文档见仓库内原始需求与 `docs/`。

## 仓库结构

```
BugReport/
├── client/com.setsuodu.bugreport/   # Unity UPM 包
├── server/src/BugReport.Server.Api/ # .NET 10 API（AOT）
├── shared/openapi.yaml             # 协议唯一事实来源
├── deploy/docker-compose.example.yml
└── .github/workflows/              # CI + 按 tag 发布
```

## 快速开始（服务端本地）

```bash
# 需要 .NET 10 SDK + PostgreSQL
export ConnectionStrings__Postgres="Host=localhost;Port=5432;Database=bugreport;Username=...;Password=..."
export Auth__IngestApiKey=dev-ingest-key
export Auth__AdminApiKey=dev-admin-key

cd server
dotnet run --project src/BugReport.Server.Api
# → http://localhost:8080/health
```

或使用示例 Compose（替换镜像名与密钥）：

```bash
# 先构建镜像或改 image 为本地 build
docker compose -f deploy/docker-compose.example.yml up
```

## API 摘要

| 方法 | 路径 | 鉴权 |
|------|------|------|
| POST | `/api/v1/ingest/reports` | `X-Api-Key`（项目 Key） |
| POST | `/api/v1/ingest/attachments/init` | 同上 |
| POST | `/api/v1/ingest/attachments/complete` | 同上 |
| GET  | `/api/v1/reports` | `X-Admin-Api-Key` |
| GET  | `/api/v1/reports/{id}` | 同上 |
| PATCH| `/api/v1/reports/{id}/status` | 同上 |

完整契约：`shared/openapi.yaml`。

## 发布 Tag

| Tag | 产物 |
|-----|------|
| `client-vX.Y.Z` | OpenUPM 客户端包 |
| `server-vX.Y.Z` | GHCR 镜像 `bugreport-server:X.Y.Z` |
| `vX.Y.Z` | 客户端 + 服务端联合发布 |

## 客户端接入

1. 通过 OpenUPM / git URL 安装 `com.setsuodu.bugreport`
2. 场景中添加 `BugReporter`（菜单：Tools → BugReport → Create BugReporter GameObject）
3. 配置 Server Base Url、Project Id、Ingest Api Key

异常与 `Error`/`Exception` 日志会自动入本地队列并周期性上报。

## 技术要点（服务端）

- Minimal API + `CreateSlimBuilder` + `JsonSerializerContext`（AOT）
- PostgreSQL + 原生 ADO.NET（Npgsql）+ DbUp 嵌入式迁移
- 附件：S3 兼容对象存储（MinIO/OSS），接口预留
- Docker：多阶段 AOT publish → `runtime-deps` chiseled 镜像

## 待办（设计文档）

- GHCR `:latest` 策略
- Admin API Key 轮换 / 多租户
- 附件存储选 MinIO 或云 OSS
- 多副本时将 DbUp 拆为独立迁移 Job
