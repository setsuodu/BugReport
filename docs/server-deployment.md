# 服务器部署指南

[← 返回文档中心](../README.md)

服务端为 .NET 10 Minimal API + Native AOT，唯一正式产物是 Docker 镜像。

---

## 1. 环境要求

- .NET 10 SDK（本地开发）
- PostgreSQL 16+
- （可选）MinIO / 任意 S3 兼容存储（附件）
- Docker + Docker Compose（推荐生产）

---

## 2. 本地快速启动

```bash
# 需要先准备 PostgreSQL
export ConnectionStrings__Postgres="Host=localhost;Port=5432;Database=bugreport;Username=bugreport;Password=bugreport"
export Auth__IngestApiKey=dev-ingest-key
export Auth__AdminApiKey=dev-admin-key

cd server
dotnet run --project src/BugReport.Server.Api
# → http://localhost:8080/health
```

环境变量也可通过 `appsettings.Development.json` 或 `.env` 提供（Compose 会自动加载）。

---

## 3. Docker Compose（推荐）

使用仓库提供的示例：

```bash
# 修改镜像名与密钥后
docker compose -f deploy/docker-compose.example.yml up -d
```

示例文件关键服务：

| 服务 | 宿主机端口 | 容器端口 | 说明 |
|------|------------|----------|------|
| bugreport-server | 12080 | 8080 | API |
| db (Postgres) | 12032 | 5432 | 数据库 |
| minio | 12090 / 12091 | 9000 / 9001 | 对象存储 + Console |

端口遵循公司组件区规划，详见 [端口规划](PORT_PLAN.md)。

### 关键环境变量

```yaml
environment:
  - ConnectionStrings__Postgres=Host=db;Port=5432;Database=bugreport;Username=bugreport;Password=bugreport
  - Storage__Endpoint=http://minio:9000
  - Storage__Bucket=bugreport-attachments
  - Storage__AccessKey=minioadmin
  - Storage__SecretKey=minioadmin
  - Storage__ForcePathStyle=true
  - Auth__IngestApiKey=change-me-ingest
  - Auth__AdminApiKey=change-me-admin
  - ASPNETCORE_URLS=http://+:8080
```

启动后验证：

```bash
curl http://localhost:12080/health
```

---

## 4. 镜像构建与发布

CI 在推送 `server-vX.Y.Z` tag 时自动构建并推送到 GHCR：

```
ghcr.io/<owner>/bugreport-server:X.Y.Z
```

本地手动构建：

```bash
docker build -f server/src/BugReport.Server.Api/Dockerfile -t bugreport-server:local .
```

Dockerfile 使用多阶段 AOT 发布 + `runtime-deps` chiseled 镜像，体积小、攻击面小。

---

## 5. 数据库迁移

- 使用 **DbUp**，迁移脚本作为 EmbeddedResource 打进程序集
- v1 在应用启动时自动执行 `PerformUpgrade()`
- **多副本部署时** 必须把迁移拆成独立一次性 Job，避免并发抢跑

---

## 6. 鉴权与安全建议

| Key | Header | 用途 |
|-----|--------|------|
| IngestApiKey | `X-Api-Key` | 客户端上报 |
| AdminApiKey | `X-Admin-Api-Key` | Dashboard 查询与状态更新 |

- 生产环境务必更换默认密钥
- 建议通过 Secret / 环境变量注入，不要写进镜像
- Admin Key 后续可扩展为可轮换 / 多租户

---

## 7. 附件存储

- 接口已预留：`/ingest/attachments/init` 与 `/complete`
- 当前实现依赖 S3 兼容存储（MinIO / OSS）
- Compose 示例内置 MinIO，生产可替换为云厂商 OSS

---

## 8. 健康检查与监控

- `GET /health` → `{ "status": "ok" }`
- 建议在 K8s / Compose 中配置 livenessProbe 指向此接口

---

## 9. 常见问题

**Q: 启动报 “缺少 ConnectionStrings__Postgres”**  
A: 确认环境变量或 appsettings 中已正确配置。

**Q: AOT 发布失败**  
A: 确保 Dockerfile 中安装了 `clang` 与 `zlib1g-dev`，且目标框架为 `net10.0`。

**Q: 多副本迁移冲突**  
A: 将 DbUp 逻辑移出启动路径，做成独立 Job。

更多设计细节见 [设计文档 v2](DESIGN-v2.md)。

[← 返回文档中心](../README.md)
