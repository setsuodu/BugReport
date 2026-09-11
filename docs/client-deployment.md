# 客户端部署指南

[← 返回文档中心](../README.md)

Unity 端以 UPM 包形式提供，路径为 `client/Packages/com.setsuodu.bugreport`。

---

## 1. 安装包

### 方式一：Git URL（推荐开发期）

在 Unity 的 **Package Manager** → **Add package from git URL**：

```
https://github.com/setsuodu/BugReport.git?path=client/Packages/com.setsuodu.bugreport
```

或指定版本：

```
https://github.com/setsuodu/BugReport.git?path=client/Packages/com.setsuodu.bugreport#client-v1.0.0
```

### 方式二：OpenUPM（正式发布后）

```
openupm add com.setsuodu.bugreport
```

包最低 Unity 版本：`2021.3`。

---

## 2. 场景接入

1. 菜单：**Tools → BugReport → Create BugReporter GameObject**
2. 选中生成的 `BugReporter` 对象，在 Inspector 配置：

| 字段 | 说明 | 示例 |
|------|------|------|
| Server Base Url | 服务端根地址（含端口） | `http://localhost:12080` 或生产域名 |
| Project Id | 项目标识，用于多项目隔离 | `my-game` |
| Ingest Api Key | 与服务端 `Auth__IngestApiKey` 一致 | `change-me-ingest` |
| Capture Unhandled | 是否捕获未处理异常 | 建议开启 |
| Flush Interval Seconds | 上报轮询间隔（秒） | 默认 `3` |
| Debug Http | 是否打印 HTTP 调试日志 | 开发时开启 |
| Clear Queue On Awake | 启动时清空本地残留队列 | 开发时可开 |

3. 确保场景中只有一个 `BugReporter`（会自动 `DontDestroyOnLoad`）。

---

## 3. 自动捕获范围

- `Application.logMessageReceived`：捕获 `Error` / `Exception` 日志
- 未处理异常：映射为 `Crash` 级别
- 同一帧内只保留一条（避免嵌套异常重复上报）
- 支持手动调用：

```csharp
BugReporter.Instance.Report("Error", "自定义消息", stackTrace: null, custom: null);
```

---

## 4. 本地队列与重试

- 报告先入本地持久化队列
- 有网络时按间隔批量 Flush
- 发送成功后从队列删除；失败则保留，下次重试

---

## 5. 验证

1. 启动服务端（见 [服务器部署](server-deployment.md)）
2. 在 Sample 场景或自建场景中触发异常
3. 观察 Console 的 `[BugReport]` 日志
4. 用 Admin Key 调用 `GET /api/v1/reports` 确认入库

Sample 说明见包内 `Samples~/BasicSetup`。

---

## 6. 注意事项

- `package.json` 中 `changelogUrl` 仍指向旧路径，以实际仓库为准
- 客户端 DTO 与服务端手动对齐，协议变更请先改 `shared/openapi.yaml`
- 附件上传接口已预留，当前客户端版本以报告正文为主

[← 返回文档中心](../README.md)
