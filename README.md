# BugReport 文档中心

Unity 客户端上报 + .NET 10 服务端接收/查询的 Bug/Crash 报告服务。

- **客户端**：Unity UPM 包（`client/Packages/com.setsuodu.bugreport`）
- **服务端**：ASP.NET Core Minimal API，Native AOT，产物为 Docker 镜像（GHCR）
- **不含**：Dashboard 前端、IM 通知集成

完整协议见仓库根目录 `shared/openapi.yaml`。

---

## 文档导航

| 文档 | 说明 |
|------|------|
| [主页介绍](README.md) | 项目概览、仓库结构、API 功能表、发布说明 |
| [客户端部署](docs/client-deployment.md) | Unity 包安装、配置、接入步骤 |
| [服务器部署](docs/server-deployment.md) | 本地运行、Docker Compose、环境变量、端口规划 |
| [设计文档 v2](docs/DESIGN-v2.md) | 详细设计（技术选型、AOT、CI/CD、待办） |

---

## 仓库结构

```
BugReport/
├── client/
│   └── Packages/com.setsuodu.bugreport/   # Unity UPM 包（实际路径）
├── server/src/BugReport.Server.Api/       # .NET 10 API（AOT）
├── shared/openapi.yaml                    # 协议唯一事实来源
├── deploy/docker-compose.example.yml      # 接入示例
├── docs/                                  # 本文档
└── .github/workflows/                     # CI + 按 tag 发布
```

> **注意**：README 旧版写的是 `client/com.setsuodu.bugreport/`，实际包位于 `client/Packages/com.setsuodu.bugreport/`。

---

## API 功能表

完整契约以 `shared/openapi.yaml` 为准。所有业务接口前缀为 `/api/v1`（健康检查除外）。

| 方法 | 路径 | 鉴权 | 功能说明 |
|------|------|------|----------|
| GET | `/health` | 无 | 健康检查，返回 `{ "status": "ok" }` |
| POST | `/api/v1/ingest/reports` | `X-Api-Key`（项目 Ingest Key） | 客户端提交 Bug/Crash 报告 |
| POST | `/api/v1/ingest/attachments/init` | 同上 | 初始化附件分片上传，返回预签名 URL |
| POST | `/api/v1/ingest/attachments/complete` | 同上 | 完成附件上传，返回最终 URL |
| GET | `/api/v1/reports` | `X-Admin-Api-Key` | 分页列表查询（支持 status / projectId 过滤） |
| GET | `/api/v1/reports/{id}` | 同上 | 获取单条报告详情 |
| PATCH | `/api/v1/reports/{id}/status` | 同上 | 更新状态（Open / Fixed / Closed） |

### 鉴权说明

- **Ingest（客户端上报）**：Header `X-Api-Key`
- **Admin（Dashboard 查询）**：Header `X-Admin-Api-Key`
- 两个 Key 在服务端配置中独立设置，互不通用。

---

## 发布 Tag 策略

| Tag | 触发工作流 | 产物 |
|-----|------------|------|
| 不打tag | 外部触发 | OpenUPM 客户端包 |
| `server-vX.Y.Z` | release-server | GHCR 镜像 `bugreport-server:X.Y.Z` |

---

## 快速链接

- [客户端部署指南](docs/client-deployment.md)
- [服务器部署指南](docs/server-deployment.md)
- [设计文档 v2](docs/DESIGN-v2.md)
