# BugReport 需求概要（设计文档引用）

本仓库实现 BugReport 服务：Unity 客户端上报 + .NET 服务端接收/查询。

详见仓库根目录设计文档与本目录后续补充。

范围：
- 客户端：C# 层异常/日志捕获、本地队列、断网重试、上报
- 服务端：Ingest API + Query API、PostgreSQL、S3 兼容附件存储、AOT 发布 Docker 镜像
- 不含 Dashboard 前端、不含 IM 通知集成
