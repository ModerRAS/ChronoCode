# ChronoCode

AI驱动的定时任务调度框架 - 通过自然语言对话或传统表单方式创建和管理定时任务

当前支持两种执行后端：`opencode` 和 `pi`。其中 `pi` 通过 RPC 模式运行，支持保留会话、获取 `sessionId/sessionFile`，以及在任务执行中追加 `steer` / `follow_up` 消息。

## 架构

```
┌─────────────────────────────────────────────────────────────────┐
│                           用户                                   │
└────────────────────────────┬────────────────────────────────────┘
                             │
                             ↓
┌─────────────────────────────────────────────────────────────────┐
│                      Vue 3 前端                                  │
│   (用户界面 - AI对话 + 任务管理 + 执行监控)                        │
│                                                                  │
│  ┌──────────────┐    ┌──────────────┐    ┌──────────────┐       │
│  │  AI Chat     │    │  Task CRUD   │    │  执行历史/日志│       │
│  │  /chat       │    │  /tasks/*    │    │  /tasks/:id  │       │
│  └──────────────┘    └──────────────┘    └──────────────┘       │
└────────────────────────────┬────────────────────────────────────┘
                             │
         ┌───────────────────┼───────────────────┐
         ↓                   ↓                   ↓
┌──────────────────┐ ┌─────────────────┐ ┌──────────────────┐
│ opencode / pi RPC│ │ ChronoCode 后端 │ │ opencode / pi RPC │
│  (AI对话/执行)   │◄┤  (REST API)    │◄┤   (任务执行)      │
└──────────────────┘ └────────┬────────┘ └──────────────────┘
                               │
                               ↓
                    ┌─────────────────────┐
                    │  Hangfire 调度       │
                    │  (定时触发任务)      │
                    └─────────────────────┘
```

## 核心概念

### Task (任务)
用户配置的定时任务，包含：
- **Cron表达式**: 任务执行时间（如 `0 2 * * *` 表示每天凌晨2点）
- **Repository**: Git仓库地址
- **Prompt**: 给AI的指令
- **约束条件**: 最大运行时间、最大文件变更数等

### Execution (执行记录)
每次任务执行的记录，包含：
- 开始/结束时间
- 执行状态（Pending/Running/Completed/Failed/Cancelled）
- 分支名、Commit SHA、PR链接
- 变更文件数、错误信息

### AI Chat
通过自然语言与AI对话创建任务：
1. 用户输入 "每周一凌晨检查TODO并整理"
2. AI解析并输出结构化JSON
3. 前端确认后调用API创建任务

## 快速开始

### 前置要求
- .NET 10.0 SDK
- Node.js 18+
- PostgreSQL 14+
- 可选执行后端之一：`opencode serve` 或 `pi`

### 1. 克隆项目
```bash
git clone https://github.com/ModerRAS/ChronoCode.git
cd ChronoCode
```

### 2. 配置数据库和执行后端
在 `ChronoCode/appsettings.json` 中配置连接字符串和执行后端：
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=chronocode;Username=postgres;Password=postgres"
  },
  "AgentRuntime": {
    "Backend": "pi"
  },
  "Pi": {
    "Command": "pi",
    "ApproveProjectTrust": true,
    "SessionNamePrefix": "chronocode",
    "Thinking": "medium"
  }
}
```

<<<<<<< HEAD
### 3. 启动应用
=======
如果继续使用 `opencode`，将 `AgentRuntime:Backend` 保持为 `opencode`，并保留 `Opencode` 配置段。

### 3. 启动后端
>>>>>>> 35f25ce (Add pi RPC runtime backend)
```bash
cd ChronoCode
dotnet run
```

首次构建或后续 `dotnet build` / `dotnet test` 会自动执行前端打包，并将静态资源输出到 `ChronoCode/wwwroot`。

### 4. 前端开发模式（可选）
```bash
cd frontend
npm install
npm run dev
```

### 5. 访问
- 应用首页: http://localhost:5242
- Hangfire Dashboard: http://localhost:5242/hangfire
- 前端开发服务器: http://localhost:5173

## 技术栈

### 后端
- .NET 10.0 ASP.NET Core
- Hangfire (任务调度)
- Entity Framework Core + PostgreSQL
- LibGit2Sharp (Git操作)

### 前端
- Vue 3 + TypeScript
- Vite
- Ant Design Vue
- Zod (验证)

## API端点

| 方法 | 路径 | 说明 |
|------|------|------|
| GET | /api/tasks | 列出所有任务 |
| POST | /api/tasks | 创建任务 |
| GET | /api/tasks/{id} | 获取任务详情 |
| PUT | /api/tasks/{id} | 更新任务 |
| DELETE | /api/tasks/{id} | 删除任务 |
| POST | /api/tasks/{id}/run | 手动触发任务 |
| GET | /api/tasks/{id}/executions | 获取执行历史 |
| POST | /api/tasks/ai | AI创建任务接口 |
| GET | /api/tasks/executions/{id}/session | 获取执行绑定的 agent 会话 |
| POST | /api/tasks/executions/{id}/message | 向运行中的 agent 追加 prompt / steer / follow_up |
| GET | /api/tasks/server/status | 当前执行后端状态 |
| POST | /api/tasks/server/start | 启动当前执行后端 |
| POST | /api/tasks/server/stop | 停止当前执行后端 |

详见 [API文档](docs/api.md)

## 项目结构

```
ChronoCode/
├── ChronoCode/                    # .NET 后端
│   ├── Controllers/              # API控制器
│   ├── Models/                   # 数据模型
│   ├── Services/                 # 业务逻辑
│   ├── Data/                     # EF Core DbContext
│   ├── Middleware/               # 中间件
│   ├── Validators/               # FluentValidation
│   └── Program.cs                # 入口
├── frontend/                     # Vue 3 前端
│   ├── src/
│   │   ├── views/                # 页面组件
│   │   ├── components/           # 公共组件
│   │   ├── api/                  # API调用
│   │   ├── composables/           # Vue组合式函数
│   │   └── utils/                # 工具函数
│   └── tests/
│       ├── unit/                 # 单元测试
│       └── e2e/                  # E2E测试
└── docs/                         # 文档
    └── api.md
```

## License

MIT
