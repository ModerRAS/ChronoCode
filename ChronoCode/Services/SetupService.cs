using System.Text.Json;
using System.Text.Json.Nodes;
using ChronoCode.Data;
using ChronoCode.Models.DTOs;
using Microsoft.EntityFrameworkCore;

namespace ChronoCode.Services;

public interface ISetupService
{
    SetupStatusDto GetStatus();
    Task<SetupStatusDto> InitializeAsync(InitializeSetupDto request, CancellationToken cancellationToken = default);
}

public class SetupService : ISetupService
{
    private readonly IConfiguration _configuration;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<SetupService> _logger;

    public SetupService(IConfiguration configuration, IHostEnvironment environment, ILogger<SetupService> logger)
    {
        _configuration = configuration;
        _environment = environment;
        _logger = logger;
    }

    public SetupStatusDto GetStatus()
    {
        return new SetupStatusDto
        {
            Initialized = DatabaseConfiguration.IsConfigured(_configuration, _environment),
            DatabaseProvider = DatabaseConfiguration.NormalizeProvider(_configuration["Database:Provider"]),
            ConfigFilePath = Path.Combine(_environment.ContentRootPath, DatabaseConfiguration.LocalConfigFileName),
            DefaultSqlitePath = DatabaseConfiguration.DefaultSqlitePath
        };
    }

    public async Task<SetupStatusDto> InitializeAsync(InitializeSetupDto request, CancellationToken cancellationToken = default)
    {
        if (GetStatus().Initialized)
        {
            throw new InvalidOperationException("ChronoCode is already initialized.");
        }

        var provider = DatabaseConfiguration.NormalizeProvider(request.DatabaseProvider)
            ?? throw new ArgumentException("Unsupported database provider. Use 'postgresql' or 'sqlite'.");

        var connectionString = provider switch
        {
            DatabaseConfiguration.PostgreSqlProvider => BuildPostgreSqlConnectionString(request),
            DatabaseConfiguration.SqliteProvider => DatabaseConfiguration.BuildSqliteConnectionString(request.SqlitePath ?? DatabaseConfiguration.DefaultSqlitePath, _environment.ContentRootPath),
            _ => throw new ArgumentException($"Unsupported database provider '{request.DatabaseProvider}'.")
        };

        await WriteLocalConfigAsync(provider, connectionString, cancellationToken);
        await InitializeDatabaseAsync(provider, connectionString, cancellationToken);

        _logger.LogInformation("ChronoCode setup completed with provider {DatabaseProvider}", provider);

        return new SetupStatusDto
        {
            Initialized = true,
            DatabaseProvider = provider,
            ConfigFilePath = Path.Combine(_environment.ContentRootPath, DatabaseConfiguration.LocalConfigFileName),
            DefaultSqlitePath = DatabaseConfiguration.DefaultSqlitePath
        };
    }

    private string BuildPostgreSqlConnectionString(InitializeSetupDto request)
    {
        if (!string.IsNullOrWhiteSpace(request.ConnectionString))
        {
            return request.ConnectionString.Trim();
        }

        if (string.IsNullOrWhiteSpace(request.PostgresHost)
            || string.IsNullOrWhiteSpace(request.PostgresDatabase)
            || string.IsNullOrWhiteSpace(request.PostgresUsername))
        {
            throw new ArgumentException("PostgreSQL host, database, and username are required.");
        }

        return DatabaseConfiguration.BuildPostgreSqlConnectionString(
            request.PostgresHost.Trim(),
            request.PostgresPort.GetValueOrDefault(5432),
            request.PostgresDatabase.Trim(),
            request.PostgresUsername.Trim(),
            request.PostgresPassword);
    }

    private async Task WriteLocalConfigAsync(string provider, string connectionString, CancellationToken cancellationToken)
    {
        var configPath = Path.Combine(_environment.ContentRootPath, DatabaseConfiguration.LocalConfigFileName);
        JsonObject root;

        if (File.Exists(configPath))
        {
            root = JsonNode.Parse(await File.ReadAllTextAsync(configPath, cancellationToken)) as JsonObject ?? new JsonObject();
        }
        else
        {
            root = new JsonObject();
        }

        var database = root["Database"] as JsonObject ?? new JsonObject();
        database["Provider"] = provider;
        root["Database"] = database;

        var connectionStrings = root["ConnectionStrings"] as JsonObject ?? new JsonObject();
        if (provider == DatabaseConfiguration.PostgreSqlProvider)
        {
            connectionStrings["DefaultConnection"] = connectionString;
            connectionStrings["SqliteConnection"] ??= string.Empty;
        }
        else
        {
            connectionStrings["SqliteConnection"] = connectionString;
            connectionStrings["DefaultConnection"] ??= string.Empty;
        }

        root["ConnectionStrings"] = connectionStrings;

        var json = root.ToJsonString(new JsonSerializerOptions
        {
            WriteIndented = true
        });

        await File.WriteAllTextAsync(configPath, json, cancellationToken);
    }

    private async Task InitializeDatabaseAsync(string provider, string connectionString, CancellationToken cancellationToken)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ChronoDbContext>();
        DatabaseConfiguration.Configure(optionsBuilder, provider, connectionString);

        await using var dbContext = new ChronoDbContext(optionsBuilder.Options);
        if (provider == DatabaseConfiguration.SqliteProvider)
        {
            await dbContext.Database.EnsureCreatedAsync(cancellationToken);
            return;
        }

        await dbContext.Database.MigrateAsync(cancellationToken);
    }
}
