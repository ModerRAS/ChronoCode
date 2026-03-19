# ChronoCode API 文档

## 概述

ChronoCode 提供 RESTful API 用于管理定时任务。API 基于 JSON 格式进行请求和响应。

**Base URL**: `http://localhost:5000/api`

## 认证

当前版本无需认证。

## 错误响应格式

所有错误响应遵循统一格式：

```json
{
  "error": {
    "code": "ERROR_CODE",
    "message": "Human readable message",
    "details": ["field: error details"],
    "timestamp": "2026-03-19T10:00:00Z",
    "traceId": "uuid"
  }
}
```

### 错误码

| Code | HTTP Status | 说明 |
|------|-------------|------|
| VALIDATION_ERROR | 400 | 请求参数验证失败 |
| NOT_FOUND | 404 | 资源不存在 |
| UNAUTHORIZED | 401 | 未授权（预留） |
| INTERNAL_ERROR | 500 | 服务器内部错误 |

---

## 任务 API

### 创建任务

**POST** `/api/tasks`

**Request Body**:
```json
{
  "name": "Weekly TODO Check",
  "cronExpression": "0 2 * * 1",
  "repositoryUrl": "https://github.com/owner/repo",
  "baseBranch": "main",
  "branchStrategy": "New",
  "prompt": "检查项目中的TODO注释并整理",
  "maxRuntimeSeconds": 600,
  "maxFileChanges": 50,
  "requirePlanReview": true,
  "isEnabled": true
}
```

**Response** (201 Created):
```json
{
  "id": "uuid",
  "name": "Weekly TODO Check",
  "cronExpression": "0 2 * * 1",
  "repositoryUrl": "https://github.com/owner/repo",
  "baseBranch": "main",
  "branchStrategy": "New",
  "prompt": "检查项目中的TODO注释并整理",
  "maxRuntimeSeconds": 600,
  "maxFileChanges": 50,
  "requirePlanReview": true,
  "createdAt": "2026-03-19T10:00:00Z",
  "lastRunAt": null,
  "lastStatus": "Pending",
  "isEnabled": true,
  "lastError": null
}
```

---

### 列出所有任务

**GET** `/api/tasks`

**Response** (200 OK):
```json
[
  {
    "id": "uuid",
    "name": "Weekly TODO Check",
    "cronExpression": "0 2 * * 1",
    ...
  }
]
```

---

### 获取任务详情

**GET** `/api/tasks/{id}`

**Response** (200 OK):
```json
{
  "id": "uuid",
  "name": "Weekly TODO Check",
  ...
}
```

**Response** (404 Not Found):
```json
{
  "error": {
    "code": "NOT_FOUND",
    "message": "Task uuid not found"
  }
}
```

---

### 更新任务

**PUT** `/api/tasks/{id}`

**Request Body** (所有字段可选):
```json
{
  "name": "Updated Name",
  "cronExpression": "0 3 * * *",
  "isEnabled": false
}
```

**Response** (200 OK): 返回更新后的任务对象

---

### 删除任务

**DELETE** `/api/tasks/{id}`

**Response** (204 No Content): 空响应

**Response** (404 Not Found):
```json
{
  "error": {
    "code": "NOT_FOUND",
    "message": "Task uuid not found"
  }
}
```

---

### 手动触发任务

**POST** `/api/tasks/{id}/run`

**Response** (202 Accepted): 空响应

---

### 获取执行历史

**GET** `/api/tasks/{id}/executions`

**Query Parameters**:
- `limit` (optional, default: 10): 返回记录数量

**Response** (200 OK):
```json
[
  {
    "id": "uuid",
    "taskId": "uuid",
    "startedAt": "2026-03-19T10:00:00Z",
    "completedAt": "2026-03-19T10:05:00Z",
    "status": "Completed",
    "branchName": "chrono-2026-03-19-100000",
    "commitSha": "abc1234",
    "prUrl": "https://github.com/owner/repo/pull/123",
    "filesChanged": 5,
    "errorMessage": null
  }
]
```

---

### 获取执行日志

**GET** `/api/tasks/executions/{executionId}/logs`

**Response** (200 OK):
```json
[
  {
    "timestamp": "2026-03-19T10:00:00Z",
    "level": "Info",
    "message": "Task started",
    "details": null
  },
  {
    "timestamp": "2026-03-19T10:01:00Z",
    "level": "Info",
    "message": "Cloning repository",
    "details": "https://github.com/owner/repo"
  }
]
```

---

## AI API

### AI 创建/管理任务

**POST** `/api/tasks/ai`

接收 opencode 返回的结构化 JSON 响应并执行相应操作。

**Request Body**:
```json
{
  "action": "create_task",
  "taskId": null,
  "task": {
    "name": "Weekly TODO Check",
    "cron": "0 2 * * 1",
    "repository": "https://github.com/owner/repo",
    "base_branch": "main",
    "prompt": "检查项目中的TODO注释并整理",
    "max_runtime_seconds": 600,
    "max_file_changes": 50,
    "require_plan_review": true,
    "is_enabled": true
  }
}
```

**支持的 Action**:

| Action | 说明 | 必需字段 |
|--------|------|----------|
| create_task | 创建新任务 | task |
| update_task | 更新任务 | taskId + task |
| delete_task | 删除任务 | taskId |
| trigger_task | 触发任务执行 | taskId |

**Response** (201 Created for create_task):
```json
{
  "id": "uuid",
  "name": "Weekly TODO Check"
}
```

---

## 服务器管理 API

### 获取服务器状态

**GET** `/api/tasks/server/status`

**Response** (200 OK):
```json
{
  "running": true,
  "url": "http://127.0.0.1:4096"
}
```

---

### 启动服务器

**POST** `/api/tasks/server/start`

**Response** (200 OK):
```json
{
  "url": "http://127.0.0.1:4096"
}
```

---

### 停止服务器

**POST** `/api/tasks/server/stop`

**Response** (200 OK): 空响应

---

## 数据模型

### BranchStrategy (枚举)

| 值 | 说明 |
|----|------|
| New | 每次执行创建新分支 |
| Reuse | 复用同一分支 |

### TaskStatus (枚举)

| 值 | 说明 |
|----|------|
| Pending | 等待执行 |
| Running | 执行中 |
| Completed | 已完成 |
| Failed | 失败 |
| Cancelled | 已取消 |

---

## AI Structured Output 格式

opencode AI 应输出以下 JSON 格式：

```json
{
  "action": "create_task",
  "task_id": null,
  "task": {
    "name": "任务名称",
    "cron": "0 2 * * 1",
    "repository": "https://github.com/owner/repo",
    "base_branch": "main",
    "prompt": "具体要AI做的事情",
    "max_runtime_seconds": 600,
    "max_file_changes": 50,
    "require_plan_review": true,
    "is_enabled": true
  }
}
```
