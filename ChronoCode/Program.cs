using ChronoCode.Data;
using ChronoCode.Middleware;
using ChronoCode.Services;
using ChronoCode.Validators;
using FluentValidation;
using FluentValidation.AspNetCore;
using Hangfire;
using Hangfire.MemoryStorage;
using Hangfire.Dashboard;
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

builder.Services.AddHangfire(config => config
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UseMemoryStorage());

builder.Services.AddHangfireServer(options => options.ServerName = "ChronoCode");

builder.Services.AddDbContext<ChronoDbContext>(options =>
    DatabaseConfiguration.Configure(options, builder.Configuration, builder.Environment));

builder.Services.AddScoped<ITaskRepository, EfTaskRepository>();
builder.Services.AddScoped<IExecutionRepository, EfExecutionRepository>();
builder.Services.AddSingleton<ISetupService, SetupService>();
builder.Services.AddSingleton<IOpencodeServerManager, OpencodeServerManager>();
builder.Services.AddSingleton<IOpencodeClient, OpencodeClient>();
builder.Services.AddSingleton<OpencodeRuntime>();
builder.Services.AddSingleton<PiRuntime>();
builder.Services.AddSingleton<IAgentRuntime, ConfiguredAgentRuntime>();
builder.Services.AddSingleton<IGitService, GitService>();
builder.Services.AddScoped<ITaskRunner, TaskRunner>();
builder.Services.AddScoped<ISchedulerService, HangfireSchedulerService>();
builder.Services.AddScoped<ScheduledTaskJob>();

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
    if (DatabaseConfiguration.IsConfigured(app.Configuration, app.Environment))
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

app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = new[] { new HangfireAuthFilter(app.Environment) }
});

app.MapControllers();

app.MapGet("/health", () => Results.Ok(new
{
    Status = "Healthy",
    Timestamp = DateTime.UtcNow,
    Initialized = DatabaseConfiguration.IsConfigured(app.Configuration, app.Environment)
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
    var provider = DatabaseConfiguration.NormalizeProvider(app.Configuration["Database:Provider"]);

    if (provider == DatabaseConfiguration.SqliteProvider)
    {
        await db.Database.EnsureCreatedAsync();
        return;
    }

    await db.Database.MigrateAsync();
}

public class HangfireAuthFilter : IDashboardAuthorizationFilter
{
    private readonly IHostEnvironment _environment;
    
    public HangfireAuthFilter(IHostEnvironment environment)
    {
        _environment = environment;
    }
    
    public bool Authorize(DashboardContext context)
    {
        if (_environment.IsDevelopment())
            return true;
            
        var httpContext = context.GetHttpContext();
        return httpContext?.User?.Identity?.IsAuthenticated ?? false;
    }
}
