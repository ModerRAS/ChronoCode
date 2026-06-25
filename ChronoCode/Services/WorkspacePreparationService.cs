using ChronoCode.Data;
using ChronoCode.Models;
using ChronoCode.Models.DTOs;
using ChronoCode.Models.Workflow;
using Microsoft.EntityFrameworkCore;

namespace ChronoCode.Services;

public interface IWorkspacePreparationService
{
    Task<WorkspacePreparationResult> PrepareAsync(ScheduledTask task, Guid executionId, CancellationToken cancellationToken = default);
}

public sealed record WorkspacePreparationResult(string WorkspacePath, string BranchName);

public sealed class WorkspacePreparationService : IWorkspacePreparationService
{
    private readonly IGitService _gitService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<WorkspacePreparationService> _logger;

    private string WorkspaceBasePath => _configuration["TaskRunner:WorkspaceBasePath"] ?? "/workspaces";

    public WorkspacePreparationService(IGitService gitService, IConfiguration configuration, ILogger<WorkspacePreparationService> logger)
    {
        _gitService = gitService;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<WorkspacePreparationResult> PrepareAsync(ScheduledTask task, Guid executionId, CancellationToken cancellationToken = default)
    {
        var workspacePath = Path.Combine(WorkspaceBasePath, task.Id.ToString(), DateTime.UtcNow.ToString("yyyyMMddHHmmss"));
        var branchName = task.BranchStrategy == BranchStrategy.New
            ? $"chronocode/{DateTime.UtcNow:yyyyMMddHHmmss}"
            : "chronocode/main";

        _logger.LogInformation("Preparing workspace {WorkspacePath} for task {TaskId}", workspacePath, task.Id);
        await _gitService.CloneRepositoryAsync(task.RepositoryUrl, workspacePath);
        await _gitService.CreateBranchAsync(workspacePath, branchName, task.BaseBranch);
        await _gitService.CheckoutBranchAsync(workspacePath, branchName);

        return new WorkspacePreparationResult(workspacePath, branchName);
    }
}
