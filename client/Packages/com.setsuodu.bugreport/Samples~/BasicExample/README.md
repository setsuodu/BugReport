# Basic Setup & Error Triggers

## 场景测试步骤

1. 通过 Package Manager 导入本 Sample（或直接打开包内 `Samples~/BasicSetup`）。
2. 新建空场景（或用现有场景）。
3. 菜单 **Tools → BugReport → Create BugReporter GameObject**，在 Inspector 中填写：
   - **Server Base Url**（例如 `http://localhost:8080`）
   - **Project Id**
   - **Ingest Api Key**
4. 再创建一个空物体，挂上 **ErrorTriggers** 组件。
5. 进入 Play Mode。

## 如何触发上报

| 快捷键 | Context Menu | 效果 |
|--------|--------------|------|
| **F1** | Trigger / Log Error | `Debug.LogError` → 被 `logMessageReceived` 捕获 |
| **F2** | Trigger / Throw Exception | 抛异常 + `LogException` + 手动 `Report` |
| **F3** | Trigger / NullReferenceException | 空引用异常 |
| **F4** | Trigger / Manual Report | 直接调用 `BugReporter.Report` |
| **F5** | Trigger / Nested Exception | 带 InnerException 的嵌套异常 |

Inspector 上右键 **ErrorTriggers** 组件还可触发：

- Division By Zero
- Index Out Of Range
- Log Exception
- **Flush Queue Now**（立刻尝试上传本地队列）

默认约每 15 秒 flush 一次；也可在触发后立刻用 Flush Queue Now。

确认服务端 `/api/v1/reports`（Admin Key）能看到新报告即表示端到端打通。
