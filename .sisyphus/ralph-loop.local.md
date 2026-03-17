---
active: true
iteration: 1
completion_promise: "VERIFIED"
initial_completion_promise: "DONE"
verification_attempt_id: "17dcf910-13cb-45b4-b510-e7efebbf2603"
verification_session_id: "ses_3037b376bffeSlJo06V74q2UUj"
started_at: "2026-03-17T15:37:40.567Z"
session_id: "ses_3038dec66ffekPuRMCHsmzHbG2"
ultrawork: true
verification_pending: true
strategy: "continue"
message_count_at_start: 1
---
好，这次给你一份终极完整版 prompt，已经把你刚刚提的关键点全部融合进去：
•✅ 必须分析 opencode 源码
•✅ 必须使用 server 模式（HTTP）
•✅ 禁止 CLI stdin/stdout
•✅ 面向“定时任务系统”
•✅ 分步骤实现，防止一次写崩

这份可以直接丢给 opencode，当“施工总蓝图”用。

⸻

🧩 最终 Prompt（完整版）

:::writing{variant=“standard” id=“84726”}

🎯 项目目标

实现一个 C# 后台服务（daemon），用于执行 AI 驱动的定时任务（类似 cron + AI coding agent）。

系统核心流程：
1.用户创建定时任务
2.到达指定时间自动触发
3.调用 opencode（通过 server 模式）执行 AI 编码任务
4.修改 Git 仓库
5.自动提交并创建 PR
6.全程记录日志

⸻

⚠️ 关键要求（必须优先完成）

在实现任何业务逻辑之前，必须先完成：

👉 分析 opencode 源码，并理解其 server 模式（serve）的工作方式

⸻

🔍 第一步：分析 opencode（强制）

请执行：
1.克隆仓库：
https://github.com/anomalyco/opencode
2.分析以下内容：
•opencode serve 的实现方式
•HTTP API 结构（OpenAPI / 路由）
•session / message / event 的数据流
•TUI / CLI 如何调用 server
•如何发送 prompt 并获取响应
•是否支持流式输出（event stream）
3.输出总结：

必须明确：
•如何创建 session
•如何发送任务 prompt
•如何获取 AI 输出
•如何监听执行过程（流式日志）

⚠️ 如果分析不完整，不允许继续实现后续代码

⸻

🧱 技术栈要求

必须使用：
•C#
•ASP.NET Core
•Hangfire（用于定时任务）

禁止：
•不要使用 CLI 模式调用 opencode
•不要通过 stdin/stdout 交互
•不要实现自己的 LLM agent 框架

⸻

🧠 架构要求

必须采用以下结构：

TaskRunner
↓
OpencodeClient（HTTP）
↓
OpenCode Server（opencode serve）

⸻

🧩 核心模块设计

⸻

1 ScheduledTask（任务定义）

字段：
•Id
•Name
•CronExpression
•RepositoryUrl
•BaseBranch
•BranchStrategy（new / reuse）
•Prompt
•MaxRuntimeSeconds
•MaxFileChanges
•RequirePlanReview（bool）

⸻

2 Scheduler（调度层）

基于 Hangfire：
•注册 cron 任务
•删除任务
•手动触发任务
•查看执行时间

⸻

3 OpencodeServerManager

职责：
•启动 opencode serve
•检查端口（默认 4096）
•等待服务 ready
•提供健康检查

⸻

4 OpencodeClient（核心）

必须基于 HTTP 实现：

功能：
•创建 session
•发送 prompt
•获取响应
•订阅事件流（流式日志）

要求：
•支持 async
•支持超时
•支持错误处理
•不允许使用 CLI

⸻

5 TaskRunner（执行引擎）

执行流程必须如下：
1.创建 workspace：
/workspaces/{taskId}/{timestamp}
2.clone 仓库
3.checkout 分支
4.构造 AI prompt（包含约束）
5.【Plan 阶段】：
•调用 OpencodeClient
•生成执行计划
•输出：
•修改文件列表
•操作步骤
6.如果 RequirePlanReview == true：
•暂停执行（用日志模拟确认）
7.【Execute 阶段】：
•再次调用 OpencodeClient
•执行修改
8.检查修改文件数量：
•超过 MaxFileChanges → 终止
9.git commit
10.git push
11.创建 PR（可 mock）

⸻

6 日志系统

必须记录：
•每一步状态
•AI 输出（流式）
•错误信息
•执行时间

⸻

7 API 接口

实现：
•POST /tasks
•GET /tasks
•POST /tasks/{id}/run
•GET /tasks/{id}/logs

⸻

🔒 AI 行为约束（必须注入 prompt）

调用 opencode 时必须加入：

允许：
•读取文件
•修改代码
•添加代码
•提交 commit

禁止：
•force push
•删除分支
•删除大量文件
•修改 CI / 权限配置

⸻

🔁 Opencode 调用方式（强制）

必须使用：

👉 opencode serve（HTTP server）

流程：
1.启动 server
2.创建 session
3.发送 prompt
4.监听 event stream
5.获取结果

禁止：
•opencode run
•CLI 输入输出解析

⸻

🧪 实现步骤（必须按顺序）
1.分析 opencode 源码并输出总结
2.初始化 ASP.NET Core 项目
3.定义 ScheduledTask
4.集成 Hangfire
5.实现 Scheduler
6.实现 OpencodeServerManager
7.实现 OpencodeClient（HTTP）
8.实现 TaskRunner（先 mock AI）
9.接入真实 opencode server
10.实现 Git 操作
11.实现 Plan Preview
12.实现日志系统
13.完成 API

⸻

⚠️ 严格约束
•每一步必须可运行
•不允许一次性生成全部代码
•不允许修改无关文件
•不允许重构整个项目
•不确定必须提问，不允许猜测

⸻

📦 输出格式要求

每一步必须输出：
1.修改的文件
2.代码（完整或 diff）
3.如何运行

⸻

🚀 最终目标

系统必须支持：
•创建定时任务（cron）
•自动触发执行
•通过 opencode server 执行 AI 修改
•自动提交 PR
•可查看完整日志

⸻

开始执行。
:::

⸻

🎯 这版 prompt 的“杀伤力”

这已经不是普通 prompt 了，而是：

“带施工监理的蓝图” 🧾

它做了几件关键事情：

⸻

🔒 1. 强制它先“读源码再写代码”

避免它：

“我猜你这个 API 是这样的…” ❌

⸻

🔁 2. 把 CLI 路彻底封死

不然它 90% 会偷懒用：

opencode run ...


⸻

🧩 3. 强制 server 架构

确保你后面：
•可并发
•可复用
•可扩展

⸻

🧠 4. Plan / Execute 双阶段

这是你系统以后“不会翻车”的核心

⸻

⚠️ 最后给你一个“进阶补刀”

你可以再额外加一句：

如果你发现 opencode server API 不稳定或不清晰，请优先通过阅读源码而不是猜测来实现。

👉 防止它编 API

⸻

🧭 最后一段

你现在这个玩法已经是：

让 AI 自己读另一个 AI 工具的源码，然后给你写调度系统

有点像：

AI 在给 AI 搭操作系统 🧠⚙️

⸻

如果你下一步想继续，我可以帮你做一个：
•🔥 “OpencodeClient.cs 完整实现（可直接用）”
•或 🧩 “TaskRunner 的 prompt 生成器（防滥权版）”

那一层会直接决定你这个系统是“能用”，还是还有一点就是要注意一下。嗯，我一开始的时候写错了，给他一，嗯，我一开始的时候写错了 gitignore，你记得改成 C#的gitignore⃣️⃣️⃣️⃣️⃣️⃣️⃣️
