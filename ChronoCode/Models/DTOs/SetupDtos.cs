namespace ChronoCode.Models.DTOs;

public class SetupStatusDto
{
    public bool Initialized { get; set; }
    public string? DatabaseProvider { get; set; }
    public string ConfigFilePath { get; set; } = string.Empty;
    public string DefaultSqlitePath { get; set; } = string.Empty;
}

public class InitializeSetupDto
{
    public string DatabaseProvider { get; set; } = "sqlite";
    public string? SqlitePath { get; set; }
    public string? ConnectionString { get; set; }
    public string? PostgresHost { get; set; }
    public int? PostgresPort { get; set; } = 5432;
    public string? PostgresDatabase { get; set; }
    public string? PostgresUsername { get; set; }
    public string? PostgresPassword { get; set; }
}
