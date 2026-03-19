import { describe, it, expect } from 'vitest';
import { parseAIResponse, AIStructuredResponseSchema } from '../../src/utils/aiParser';

describe('aiParser', () => {
  it('parses valid JSON response', () => {
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
  });

  it('returns null for invalid JSON', () => {
    const result = parseAIResponse('not valid json');
    expect(result).toBeNull();
  });
});
