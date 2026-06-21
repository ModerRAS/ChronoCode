import { z } from 'zod';

const CreateTaskSchema = z.object({
  name: z.string().min(1, 'Name is required').max(100),
  cron: z.string().min(1, 'Cron is required'),
  repository: z.string().url('Invalid repository URL'),
  base_branch: z.string().optional().default('main'),
  branch_strategy: z.enum(['new', 'reuse']).optional().default('new'),
  prompt: z.string().min(1, 'Prompt is required'),
  max_runtime_seconds: z.number().optional().default(600),
  max_file_changes: z.number().optional().default(50),
  require_plan_review: z.boolean().optional().default(true),
  is_enabled: z.boolean().optional().default(true),
});

const AIErrorSchema = z.object({
  code: z.string(),
  message: z.string(),
});

export const ActionableAIStructuredResponseSchema = z.object({
  action: z.enum(['create_task', 'update_task', 'delete_task', 'trigger_task']),
  task_id: z.string().uuid().nullable().optional(),
  task: CreateTaskSchema.nullable().optional(),
  error: AIErrorSchema.nullable().optional(),
});

export const InfoAIStructuredResponseSchema = z.object({
  action: z.literal(''),
  task_id: z.null().optional(),
  task: z.null(),
  error: AIErrorSchema,
});

export const AIStructuredResponseSchema = z.union([
  ActionableAIStructuredResponseSchema,
  InfoAIStructuredResponseSchema,
]);

export type CreateTaskInput = z.infer<typeof CreateTaskSchema>;
export type ActionableAIStructuredResponse = z.infer<typeof ActionableAIStructuredResponseSchema>;
export type InfoAIStructuredResponse = z.infer<typeof InfoAIStructuredResponseSchema>;
export type AIStructuredResponse = z.infer<typeof AIStructuredResponseSchema>;

export function parseAIResponse(text: string): AIStructuredResponse | null {
  try {
    const match = text.match(/```json\n([\s\S]*?)\n```/);
    const jsonStr = match ? match[1] : text;
    const parsed = JSON.parse(jsonStr);
    return AIStructuredResponseSchema.parse(parsed);
  } catch {
    return null;
  }
}

export function isActionableAIResponse(
  response: AIStructuredResponse | null | undefined,
): response is ActionableAIStructuredResponse {
  return !!response && response.action !== '';
}
