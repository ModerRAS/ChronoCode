using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ChronoCode.Services;

public static class DatabaseConfiguration
{
    public const string LocalConfigFileName = "appsettings.Local.json";
    public const string PostgreSqlProvider = "postgresql";
    public const string SqliteProvider = "sqlite";
    public const string DefaultSqlitePath = "storage/chronocode.db";
    private const string SetupModeSqlitePath = "storage/.chronocode-setup.db";

    public static string? NormalizeProvider(string? provider)
    {
        if (string.IsNullOrWhiteSpace(provider))
        {
            return null;
        }

        return provider.Trim().ToLowerInvariant() switch
        {
            "postgres" or "postgresql" or "pgsql" => PostgreSqlProvider,
            "sqlite" => SqliteProvider,
            _ => null
        };
    }

    public static bool IsConfigured(IConfiguration configuration, IHostEnvironment environment)
    {
        return CreateRuntimeState(configuration).Initialized;
    }

    public static bool IsConfigured(DatabaseRuntimeState state)
    {
        return state.Initialized;
    }

    public static DatabaseRuntimeState CreateRuntimeState(IConfiguration configuration)
    {
        var provider = NormalizeProvider(configuration["Database:Provider"]);
        var connectionString = provider switch
        {
            PostgreSqlProvider => configuration.GetConnectionString("DefaultConnection"),
            SqliteProvider => configuration.GetConnectionString("SqliteConnection"),
            _ => null
        };

        return new DatabaseRuntimeState(provider, connectionString);
    }

    public static void Configure(DbContextOptionsBuilder options, DatabaseRuntimeState state, IHostEnvironment environment)
    {
        if (!state.Initialized || state.Provider == null || string.IsNullOrWhiteSpace(state.ConnectionString))
        {
            Configure(options, SqliteProvider, BuildSqliteConnectionString(SetupModeSqlitePath, environment.ContentRootPath));
            return;
        }

        Configure(options, state.Provider, state.ConnectionString);
    }

    public static void Configure(DbContextOptionsBuilder options, string provider, string connectionString)
    {
        var normalizedProvider = NormalizeProvider(provider);
        switch (normalizedProvider)
        {
            case PostgreSqlProvider:
                options.UseNpgsql(connectionString);
                break;
            case SqliteProvider:
                options.UseSqlite(connectionString);
                break;
            default:
                throw new InvalidOperationException($"Unsupported database provider '{provider}'.");
        }
    }

    public static string BuildPostgreSqlConnectionString(string host, int port, string database, string username, string? password)
    {
        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = host,
            Port = port,
            Database = database,
            Username = username,
            Password = password ?? string.Empty
        };

        return builder.ConnectionString;
    }

    public static string BuildSqliteConnectionString(string sqlitePath, string contentRootPath)
    {
        var relativeOrAbsolutePath = string.IsNullOrWhiteSpace(sqlitePath) ? DefaultSqlitePath : sqlitePath.Trim();
        var absolutePath = Path.IsPathRooted(relativeOrAbsolutePath)
            ? relativeOrAbsolutePath
            : Path.GetFullPath(Path.Combine(contentRootPath, relativeOrAbsolutePath));

        var directoryPath = Path.GetDirectoryName(absolutePath);
        if (!string.IsNullOrWhiteSpace(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        return new SqliteConnectionStringBuilder
        {
            DataSource = absolutePath
        }.ToString();
    }
}
