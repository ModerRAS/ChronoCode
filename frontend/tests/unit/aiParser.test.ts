import { describe, expect, it } from 'vitest';
import {
  isActionableAIResponse,
  parseAIResponse,
} from '../../src/utils/aiParser';

describe('aiParser', () => {
  it('parses valid actionable JSON response', () => {
    const json = `\`\`\`json
{
  "action": "create_task",
  "task": {
    "name": "Test Task",
    "cron": "0 9 * * *",
    "repository": "https://github.com/test/repo",
    "prompt": "Test"
  }
}
\`\`\``;

    const result = parseAIResponse(json);

    expect(result).not.toBeNull();
    expect(result?.action).toBe('create_task');
    expect(result?.task?.name).toBe('Test Task');
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
});
