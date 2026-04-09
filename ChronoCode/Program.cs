using ChronoCode.Data;
using ChronoCode.Middleware;
using ChronoCode.Services;
using ChronoCode.Validators;
using FluentValidation;
using Hangfire;
using Hangfire.MemoryStorage;
using Hangfire.Dashboard;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
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
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<ITaskRepository, EfTaskRepository>();
builder.Services.AddScoped<IExecutionRepository, EfExecutionRepository>();
builder.Services.AddSingleton<IOpencodeServerManager, OpencodeServerManager>();
builder.Services.AddSingleton<IOpencodeClient, OpencodeClient>();
builder.Services.AddSingleton<IGitService, GitService>();
builder.Services.AddScoped<ITaskRunner, TaskRunner>();
builder.Services.AddScoped<ISchedulerService, HangfireSchedulerService>();
builder.Services.AddScoped<ScheduledTaskJob>();

builder.Services.AddValidatorsFromAssemblyContaining<CreateTaskDtoValidator>();

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

// Ensure database is created
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ChronoDbContext>();
    await db.Database.EnsureCreatedAsync();
}

app.UseCors();
app.UseRouting();

app.UseExceptionHandling();

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

app.MapGet("/health", () => Results.Ok(new { Status = "Healthy", Timestamp = DateTime.UtcNow }));

app.MapFallbackToFile("index.html");

app.Run();

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