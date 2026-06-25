using ChronoCode.Data;
using ChronoCode.Middleware;
using ChronoCode.Services;
using ChronoCode.Services.Workflow;
using ChronoCode.Validators;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddJsonFile(DatabaseConfiguration.LocalConfigFileName, optional: true, reloadOnChange: true);

builder.Services.AddControllers()
    .AddFluentValidation(v => v.RegisterValidatorsFromAssemblyContaining<CreateTaskDtoValidator>())
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
    });

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

builder.Services.AddSingleton<DatabaseRuntimeState>(sp =>
    DatabaseConfiguration.CreateRuntimeState(sp.GetRequiredService<IConfiguration>()));

builder.Services.AddDbContext<ChronoDbContext>((sp, options) =>
    DatabaseConfiguration.Configure(options, sp.GetRequiredService<DatabaseRuntimeState>(), builder.Environment));

builder.Services.AddScoped<ITaskRepository, EfTaskRepository>();
builder.Services.AddScoped<IExecutionRepository, EfExecutionRepository>();
builder.Services.AddSingleton<ISetupService, SetupService>();
builder.Services.AddSingleton<ISettingsService, SettingsService>();
builder.Services.AddSingleton<IOpencodeServerManager, OpencodeServerManager>();
builder.Services.AddSingleton<IOpencodeClient, OpencodeClient>();
builder.Services.AddSingleton<IChatRuntimeService, ChatRuntimeService>();
builder.Services.AddSingleton<OpencodeRuntime>();
builder.Services.AddSingleton<PiRuntime>();
builder.Services.AddSingleton<IGitService, GitService>();
builder.Services.AddScoped<IWorkspacePreparationService, WorkspacePreparationService>();
builder.Services.AddScoped<IWorkflowRunService, WorkflowRunService>();
builder.Services.AddSingleton<IAgentRuntimeResolver, AgentRuntimeResolver>();
builder.Services.AddSingleton<ISchedulerService, AppSchedulerService>();
builder.Services.AddHostedService<SchedulerBackgroundService>();

builder.Services.AddHttpClient("Opencode", client =>
{
    client.Timeout = TimeSpan.FromMinutes(5);
});

builder.Services.AddHttpClient("OpencodeServer", client =>
{
    client.Timeout = TimeSpan.FromSeconds(35);
});

builder.Services.AddHttpClient("GitHub", client =>
{
    client.BaseAddress = new Uri("https://api.github.com");
    client.DefaultRequestHeaders.Add("User-Agent", "ChronoCode");
});

var app = builder.Build();

var setupInitialized = DatabaseConfiguration.IsConfigured(app.Configuration, app.Environment);
if (setupInitialized)
{
    await EnsureDatabaseAsync(app);
}

app.UseRouting();
app.UseCors();

app.UseExceptionHandling();

app.Use(async (context, next) =>
{
    var databaseRuntimeState = context.RequestServices.GetRequiredService<DatabaseRuntimeState>();
    if (DatabaseConfiguration.IsConfigured(databaseRuntimeState))
    {
        await next();
        return;
    }

    var path = context.Request.Path;
    var isSetupApi = path.StartsWithSegments("/api/setup");
    var isHealth = path.StartsWithSegments("/health");
    var isSetupPage = !path.StartsWithSegments("/api") && !Path.HasExtension(path);
    var isStaticAsset = !path.StartsWithSegments("/api") && Path.HasExtension(path);

    if (isSetupApi || isHealth || isSetupPage || isStaticAsset)
    {
        await next();
        return;
    }

    context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
    await context.Response.WriteAsJsonAsync(new
    {
        error = new
        {
            code = "SETUP_REQUIRED",
            message = "ChronoCode is not initialized yet. Open the setup page to choose and configure a database."
        }
    });
});

app.UseDefaultFiles();
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(
        Path.Combine(builder.Environment.ContentRootPath, "wwwroot")),
});

app.MapControllers();

app.MapGet("/health", (DatabaseRuntimeState databaseRuntimeState) => Results.Ok(new
{
    Status = "Healthy",
    Timestamp = DateTime.UtcNow,
    Initialized = DatabaseConfiguration.IsConfigured(databaseRuntimeState)
}));

app.MapFallback(async context =>
{
    if (context.Request.Path.StartsWithSegments("/api"))
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        return;
    }

    context.Response.ContentType = "text/html; charset=utf-8";
    await context.Response.SendFileAsync(
        Path.Combine(builder.Environment.ContentRootPath, "wwwroot", "index.html"));
});

app.Run();

static async Task EnsureDatabaseAsync(WebApplication app)
{
    if (app.Environment.IsEnvironment("Testing"))
    {
        return;
    }

    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<ChronoDbContext>();
    var runtimeState = scope.ServiceProvider.GetRequiredService<DatabaseRuntimeState>();
    var provider = runtimeState.Provider;
    var legacy = await WorkflowMigration.ReadLegacyTasksAsync(db);

    if (provider == DatabaseConfiguration.SqliteProvider)
    {
        await db.Database.EnsureCreatedAsync();
    }
    else
    {
        await db.Database.MigrateAsync();
    }

    try
    {
        await WorkflowMigration.ApplyBackfillAsync(db, legacy);
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogWarning(ex, "Workflow legacy backfill skipped");
    }
}
