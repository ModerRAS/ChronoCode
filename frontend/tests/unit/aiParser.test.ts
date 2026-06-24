import { describe, expect, it } from 'vitest';
import {
  isActionableAIResponse,
  parseAIResponse,
} from '../../src/utils/aiParser';

describe('aiParser', () => {
  const workflowDefinitionJson = JSON.stringify({
    version: 1,
    startNodeId: 'start',
    nodes: [],
  });

  it('parses create_task with workflow definition (markdown-fenced)', () => {
    const payload = {
      action: 'create_task',
      task: {
        name: 'Test Task',
        cron: '0 9 * * *',
        repository: 'https://github.com/test/repo',
        workflow_definition_json: workflowDefinitionJson,
        runtime_backend: 'pi',
        max_concurrent_runs: 1,
        node_failure_policy_json: '{}',
      },
    };
    const json = '```json\n' + JSON.stringify(payload, null, 2) + '\n```';

    const result = parseAIResponse(json);

    expect(result).not.toBeNull();
    expect(result?.action).toBe('create_task');
    expect(result?.task?.name).toBe('Test Task');
    expect(result?.task?.workflow_definition_json).toBe(workflowDefinitionJson);
    expect(isActionableAIResponse(result)).toBe(true);
  });

  it('parses create_task from raw JSON (no code fence)', () => {
    const payload = {
      action: 'create_task',
      task: {
        name: 'Raw Task',
        cron: '0 9 * * *',
        repository: 'https://github.com/test/repo',
        workflow_definition_json: workflowDefinitionJson,
      },
    };

    const result = parseAIResponse(JSON.stringify(payload));

    expect(result).not.toBeNull();
    expect(result?.action).toBe('create_task');
    expect(result?.task?.name).toBe('Raw Task');
    expect(isActionableAIResponse(result)).toBe(true);
  });

  it('parses create_task with default workflow when workflow_definition_json omitted', () => {
    const payload = {
      action: 'create_task',
      task: {
        name: 'Default WF Task',
        cron: '0 9 * * *',
        repository: 'https://github.com/test/repo',
      },
    };

    const result = parseAIResponse(JSON.stringify(payload));

    expect(result).not.toBeNull();
    expect(result?.action).toBe('create_task');
    expect(result?.task?.workflow_definition_json).toBeTruthy();
    const def = JSON.parse(result!.task!.workflow_definition_json);
    expect(def.version).toBe(1);
    expect(def.startNodeId).toBe('start');
    expect(def.nodes.length).toBeGreaterThan(0);
  });

  it('parses update_task with task_id', () => {
    const payload = {
      action: 'update_task',
      task_id: '123e4567-e89b-12d3-a456-426614174000',
      task: {
        name: 'Updated Task',
        cron: '0 10 * * *',
        repository: 'https://github.com/test/repo',
        workflow_definition_json: workflowDefinitionJson,
      },
    };

    const result = parseAIResponse(JSON.stringify(payload));

    expect(result).not.toBeNull();
    expect(result?.action).toBe('update_task');
    expect(result?.task_id).toBe('123e4567-e89b-12d3-a456-426614174000');
    expect(result?.task?.name).toBe('Updated Task');
    expect(isActionableAIResponse(result)).toBe(true);
  });

  it('parses delete_task with task_id', () => {
    const payload = {
      action: 'delete_task',
      task_id: '123e4567-e89b-12d3-a456-426614174000',
      task: null,
      error: null,
    };

    const result = parseAIResponse(JSON.stringify(payload));

    expect(result).not.toBeNull();
    expect(result?.action).toBe('delete_task');
    expect(result?.task_id).toBe('123e4567-e89b-12d3-a456-426614174000');
    expect(result?.task).toBeNull();
    expect(isActionableAIResponse(result)).toBe(true);
  });

  it('parses trigger_task with task_id', () => {
    const payload = {
      action: 'trigger_task',
      task_id: '123e4567-e89b-12d3-a456-426614174000',
      task: null,
      error: null,
    };

    const result = parseAIResponse(JSON.stringify(payload));

    expect(result).not.toBeNull();
    expect(result?.action).toBe('trigger_task');
    expect(result?.task_id).toBe('123e4567-e89b-12d3-a456-426614174000');
    expect(isActionableAIResponse(result)).toBe(true);
  });

  it('parses info responses as non-actionable', () => {
    const json = `\`\`\`json
{
  "action": "",
  "task": null,
  "error": {
    "code": "INFO",
    "message": "Here is some help"
  }
}
\`\`\``;

    const result = parseAIResponse(json);

    expect(result).not.toBeNull();
    expect(result?.action).toBe('');
    expect(result?.task).toBeNull();
    expect(result?.error?.code).toBe('INFO');
    expect(isActionableAIResponse(result)).toBe(false);
  });

  it('returns null for invalid JSON', () => {
    const result = parseAIResponse('not valid json');
    expect(result).toBeNull();
  });

  it('returns null for unknown action', () => {
    const payload = { action: 'unknown_action', task: null, error: null };
    const result = parseAIResponse(JSON.stringify(payload));
    expect(result).toBeNull();
  });
});
