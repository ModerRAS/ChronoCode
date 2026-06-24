namespace ChronoCode.Services;

public sealed class DatabaseRuntimeState
{
    public DatabaseRuntimeState(string? provider, string? connectionString)
    {
        Provider = DatabaseConfiguration.NormalizeProvider(provider);
        ConnectionString = string.IsNullOrWhiteSpace(connectionString) ? null : connectionString;
    }

    public string? Provider { get; private set; }
    public string? ConnectionString { get; private set; }
    public bool Initialized => !string.IsNullOrWhiteSpace(Provider) && !string.IsNullOrWhiteSpace(ConnectionString);

    public void SetConfigured(string provider, string connectionString)
    {
        Provider = DatabaseConfiguration.NormalizeProvider(provider)
            ?? throw new InvalidOperationException($"Unsupported database provider '{provider}'.");
        ConnectionString = connectionString;
    }
}
