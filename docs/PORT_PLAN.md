# LongLongGames 宿主机端口规划

容器内仍用标准端口；**仅宿主机映射**按本表，避免与经典服务及兄弟业务冲突。

## 分段

| 分区 | 段 | 说明 |
|------|-----|------|
| 平台 MP / Dashboard | 11000–11999 | 全公司一份 |
| 可复用组件 | 12000–12999 | BugReport / Mail / … |
| 游戏实例 | 13000+ | 每游戏一块 100 端口 |

## 平台

| 服务 | Host | Container |
|------|------|-----------|
| mp-gateway | 11080 | 80 |
| mp-postgres | 11032 | 5432 |
| mp-redis | 11079 | 6379 |
| GameDashboard | 11090 | 80 |

## 组件

| 服务 | Host | Container |
|------|------|-----------|
| bugreport-server | 12080 | 8080 |
| bugreport-postgres | 12032 | 5432 |
| bugreport-minio S3 | 12090 | 9000 |
| bugreport-minio Console | 12091 | 9001 |
| mail-server | 12180 | 8080 |
| mail-postgres | 12132 | 5432 |

## 游戏（G = 1,2,3…）

| 角色 | 公式 | match3 (G=1) |
|------|------|----------------|
| Gateway | 13000 + G×100 + 80 | 13180 |
| Postgres | 13000 + G×100 + 32 | 13132 |
| Redis | 13000 + G×100 + 79 | 13179 |

内部 user/core/leaderboard 不映射宿主机，只走本游戏 gateway。
