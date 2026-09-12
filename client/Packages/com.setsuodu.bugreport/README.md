# BugReport Unity Client

Unity 客户端 Bug/Crash 报告服务 UPM 包。用于自动捕获或手动上报游戏运行时的异常、日志与附件。

## Summary

* **功能**：游戏运行时异常捕获、Bug 手动上报、日志与截图等附件分片上传。
* **环境**：支持 Unity 全平台，轻量级无重度依赖。
* **路径**：UPM 完整包位于仓库 `client/Packages/com.setsuodu.bugreport`。

## GetStarted

### 1. 安装包 (OpenUPM)

```bash
openupm add com.setsuodu.bugreport
```

### 2. 初始化接入

在游戏启动入口初始化客户端配置：

```csharp
using Setsuodu.BugReport;

public class BugReportInitializer
{
    public static void Init()
    {
        BugReportClient.Initialize(new BugReportConfig
        {
            Endpoint = "https://your-bugreport-server.com", // 服务端地址
            ApiKey = "YOUR_PROJECT_INGEST_KEY"              // 客户端上报鉴权 Key (Header: X-Api-Key)
        });
    }
}
```

## API

客户端内部基于以下服务端核心接口进行网络交互与数据上报：

* **`POST /v1/ingest/reports`**：客户端提交 Bug/Crash 报告（支持自动捕获与手动提交）。
* **`POST /v1/ingest/attachments/init`**：初始化附件分片上传，获取预签名 URL。
* **`POST /v1/ingest/attachments/complete`**：完成分片上传，返回最终附件 URL。
