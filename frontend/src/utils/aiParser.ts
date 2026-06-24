import { z } from 'zod';

/**
 * A valid default workflow definition JSON: start -> prepare_workspace ->
 * agent(plan) -> commit_changes -> create_pull_request -> end. The agent node
 * uses the "pi" backend and exposes a minimal data contract.
 */
export const DEFAULT_WORKFLOW_DEFINITION_JSON = JSON.stringify(
  {
    version: 1,
    startNodeId: 'start',
    nodes: [
      { type: 'start', nodeId: 'start', name: 'Start', nextNodeId: 'prepare' },
      {
        type: 'prepare_workspace',
        nodeId: 'prepare',
        name: 'Prepare Workspace',
        nextNodeId: 'plan',
      },
      {
        type: 'agent',
        nodeId: 'plan',
        name: 'Plan',
        backend: 'pi',
        promptTemplate: 'Analyze the repository and produce an implementation plan.',
        dataContract: {
          fields: [{ name: 'summary', type: 'string', required: true }],
        },
        nextNodeId: 'commit',
      },
      {
        type: 'commit_changes',
        nodeId: 'commit',
        name: 'Commit Changes',
        commitMessageTemplate: 'AI: {{$.task.name}}',
        nextNodeId: 'pr',
      },
      {
        type: 'create_pull_request',
        nodeId: 'pr',
        name: 'Create Pull Request',
        titleTemplate: '{{$.task.name}}',
        bodyTemplate: '{{$.nodes.plan.output.summary}}',
        nextNodeId: 'end',
      },
      { type: 'end', nodeId: 'end', name: 'End' },
    ],
  },
  null,
  2,
);

const CreateTaskSchema = z.object({
  name: z.string().min(1, 'Name is required').max(100),
  cron: z.string().min(1, 'Cron is required'),
  repository: z.string().url('Invalid repository URL'),
  base_branch: z.string().optional().default('main'),
  branch_strategy: z.enum(['new', 'reuse']).optional().default('new'),
  max_runtime_seconds: z.number().optional().default(600),
  max_file_changes: z.number().optional().default(50),
  is_enabled: z.boolean().optional().default(true),
  workflow_definition_json: z
    .string()
    .optional()
    .default(() => DEFAULT_WORKFLOW_DEFINITION_JSON),
  default_inputs_json: z.string().nullable().optional(),
  runtime_backend: z.enum(['pi']).nullable().optional(),
  max_concurrent_runs: z.number().optional().default(1),
  node_failure_policy_json: z.string().optional().default('{}'),
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
export type ActionableAIStructuredResponse = z.infer<
  typeof ActionableAIStructuredResponseSchema
>;
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
