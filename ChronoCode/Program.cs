using ChronoCode.Data;
using ChronoCode.Middleware;
using ChronoCode.Services;
using ChronoCode.Validators;
using FluentValidation;
using Hangfire;
using Hangfire.MemoryStorage;
using Hangfire.Dashboard;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.AddHangfire(config => config
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UseMemoryStorage());

builder.Services.AddHangfireServer(options => options.ServerName = "ChronoCode");

builder.Services.AddDbContext<ChronoDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<ITaskRepository, EfTaskRepository>();
builder.Services.AddScoped<IExecutionRepository, EfExecutionRepository>();
builder.Services.AddSingleton<IOpencodeServerManager, OpencodeServerManager>();
builder.Services.AddSingleton<IOpencodeClient, OpencodeClient>();
builder.Services.AddSingleton<IGitService, GitService>();
builder.Services.AddSingleton<ITaskRunner, TaskRunner>();
builder.Services.AddSingleton<ISchedulerService, HangfireSchedulerService>();
builder.Services.AddSingleton<ScheduledTaskJob>();

builder.Services.AddValidatorsFromAssemblyContaining<CreateTaskDtoValidator>();

var app = builder.Build();

app.UseRouting();

app.UseExceptionHandling();

app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = new[] { new HangfireAuthFilter() }
});

app.MapControllers();

app.MapGet("/health", () => Results.Ok(new { Status = "Healthy", Timestamp = DateTime.UtcNow }));

app.Run();

public class HangfireAuthFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        return true;
    }
}