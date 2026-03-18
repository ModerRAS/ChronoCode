---
active: true
iteration: 1
completion_promise: "DONE"
initial_completion_promise: "DONE"
started_at: "2026-03-18T00:33:17.909Z"
session_id: "ses_301a4420bffevbqUx3P4pbniik"
ultrawork: true
strategy: "continue"
message_count_at_start: 5
---
用 Vue来写一个前端，然后也可以装一个 playwright 来测试。好，这一把我们直接给你一份**“能驱动 LLM 把前端完整做出来”的 Vue 开发总 prompt** 🎯
不是随便写页面，而是：

👉 让它按“工程级规范 + 可持续扩展”去做

我会把它写成一个分阶段施工蓝图，你可以直接丢给 opencode。

⸻

🧩 Vue 前端开发 Prompt（完整版）

:::writing{variant=“standard” id=“59381”}

🎯 项目目标

实现一个前端 Web UI，用于管理 AI 定时任务系统（类似 cron + AI agent）。

该 UI 需要支持：
	•	创建 / 编辑定时任务
	•	查看任务列表
	•	手动触发任务
	•	查看执行日志（支持流式输出）
	•	查看执行历史

⸻

🧱 技术栈（必须使用）
	•	Vue 3（Composition API）
	•	Vite
	•	TypeScript
	•	UI 框架：Naive UI（优先）或 Element Plus
	•	状态管理：Pinia
	•	路由：Vue Router
	•	HTTP：axios
	•	实时通信：WebSocket 或 SSE

⸻

⚠️ 全局约束
	•	必须使用 TypeScript
	•	必须模块化（禁止单文件堆逻辑）
	•	每个功能必须拆分组件
	•	API 层必须独立封装
	•	所有类型必须定义 interface
	•	不允许硬编码数据
	•	不允许一次性生成全部代码（必须分步骤实现）

⸻

🧠 UI 设计原则

这个系统不是“输入 prompt”，而是：

👉 定义一个可重复执行的 AI 行为

因此：
	•	用结构化表单代替自由输入
	•	用选项限制 AI 行为
	•	所有危险操作必须可控

⸻

📦 页面结构

必须实现以下页面：

⸻

1️⃣ Dashboard（可简化）

路径：/

内容：
	•	任务总数
	•	最近执行状态
	•	最近失败任务

⸻

2️⃣ Tasks（任务列表）

路径：/tasks

功能：
	•	展示任务列表
	•	显示：
	•	名称
	•	cron 表达式
	•	下次执行时间
	•	最近执行状态
	•	操作：
	•	手动运行
	•	编辑
	•	查看日志

⸻

3️⃣ Task Editor（核心页面）

路径：
	•	/tasks/new
	•	/tasks/:id/edit

必须包含：

基础信息
	•	任务名称
	•	描述

调度配置
	•	cron 表达式（提供简单选择 UI）

仓库配置
	•	Repository URL
	•	Base Branch
	•	Branch Strategy（新建 / 复用）

AI 行为
	•	类型（下拉）：
	•	修复问题
	•	重构代码
	•	自定义
	•	描述（文本）

安全限制
	•	最大运行时间
	•	最大修改文件数
	•	是否需要 Plan Preview

执行策略
	•	失败重试次数
	•	是否通知

⸻

4️⃣ Task Logs（日志）

路径：/tasks/:id/logs

必须支持：
	•	流式输出（WebSocket 或 SSE）
	•	分阶段显示：
	•	Plan
	•	Execute
	•	高亮 AI 输出

⸻

5️⃣ Runs（执行历史）

路径：/tasks/:id/runs

内容：
	•	每次执行记录
	•	状态（成功 / 失败）
	•	执行时间
	•	持续时间

⸻

🧩 组件结构要求

必须拆分：
	•	TaskCard.vue
	•	TaskForm.vue
	•	CronSelector.vue
	•	RepoSelector.vue
	•	AIConfigPanel.vue
	•	SafetyConfigPanel.vue
	•	LogViewer.vue

⸻

🔌 API 设计（必须封装）

创建：

/src/api/task.ts

必须实现：
	•	getTasks()
	•	createTask()
	•	updateTask()
	•	runTask()
	•	getLogs()
	•	getRuns()

⸻

🧠 状态管理（Pinia）

创建 store：

/src/stores/task.ts

包含：
	•	tasks
	•	currentTask
	•	loading 状态

⸻

🔁 日志流（重点）

必须实现：
	•	WebSocket 或 SSE 客户端
	•	实时追加日志
	•	自动滚动
	•	支持断线重连

⸻

🎨 UI 细节要求
	•	使用卡片布局
	•	状态用颜色区分（成功 / 失败 / 运行中）
	•	表单分区清晰
	•	禁止“一个大表单堆到底”

⸻

🧪 开发步骤（必须严格按顺序）

⸻

Step 1

初始化项目：
	•	Vite + Vue3 + TS
	•	安装依赖
	•	配置 router / pinia / naive UI

⸻

Step 2

实现基础布局：
	•	顶部导航
	•	侧边栏

⸻

Step 3

实现任务列表页面（/tasks）

（先用 mock 数据）

⸻

Step 4

实现 TaskCard 组件

⸻

Step 5

实现 Task Editor（基础版）

⸻

Step 6

拆分表单组件（AIConfig / Safety 等）

⸻

Step 7

接入真实 API

⸻

Step 8

实现日志页面（支持流式）

⸻

Step 9

实现执行历史页面

⸻

Step 10

优化 UX（loading / 错误提示）

⸻

⚠️ 行为约束
	•	不允许跳步骤
	•	每一步必须可运行
	•	不允许生成未使用代码
	•	不允许修改无关文件
	•	不确定必须提问

⸻

📦 输出要求

每一步必须输出：
	1.	新增/修改文件列表
	2.	完整代码
	3.	如何运行

⸻

🚀 最终目标

实现一个完整前端：
	•	可创建 AI 定时任务
	•	可管理任务
	•	可查看执行日志（实时）
	•	可查看执行历史

⸻

开始执行 Step 1。
:::

⸻

🎯 这份 prompt 的关键价值

它不是“让 AI 写页面”，而是：

🧠 1. 强制工程结构

避免出现：

App.vue 3000 行大杂烩

⸻

🔁 2. 强制分阶段

避免：

一次生成 → 崩 → 重来

⸻

🧩 3. 强制组件拆分

你的 UI 后面才能扩展：
	•	权限系统
	•	多仓库
	•	多 agent

⸻

⚡ 4. 日志流设计提前锁死

这个是最容易被 AI 写废的部分

⸻

🧭 小建议（很关键）

你可以在 prompt 最后再加一句：

如果 UI 设计不合理，请主动优化，而不是机械实现。

👉 不然它会死板照抄

⸻

🚀 下一步你可以做的

如果你想继续把这项目推到“很能打”的程度，我可以帮你：
	•	🎨 设计一版「任务编辑器 UI 草图」（更像 GitHub Actions）
	•	🧠 写一套「AI Prompt 生成策略」（防止乱改代码）
	•	🔥 或直接给你一套 后端 + 前端联调协议（DTO + 类型对齐）

你现在这个项目，已经开始有点像：

“个人 AI 自动化平台”雏形了 🧠⚙️

再往前一步，就是自己的 Zapier + Copilot + CI 😏
