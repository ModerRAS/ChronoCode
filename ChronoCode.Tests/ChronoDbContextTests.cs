using ChronoCode.Data;
using ChronoCode.Models;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ChronoCode.Tests;

public class ChronoDbContextTests
{
    [Fact]
    public void OnModelCreating_ConfiguresForeignKey_FromTaskExecutionToScheduledTask()
    {
        var options = new DbContextOptionsBuilder<ChronoDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        using var context = new ChronoDbContext(options);

        var entityType = context.Model.FindEntityType(typeof(TaskExecution));
        var foreignKey = Assert.Single(entityType!.GetForeignKeys());

        Assert.Equal(typeof(ScheduledTask), foreignKey.PrincipalEntityType.ClrType);
        Assert.Equal(nameof(TaskExecution.TaskId), Assert.Single(foreignKey.Properties).Name);
    }
}
