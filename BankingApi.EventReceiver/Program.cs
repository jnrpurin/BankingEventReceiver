using BankingApi.EventReceiver;
using BankingApi.EventReceiver.Infra;
using BankingApi.EventReceiver.Interfaces;
using BankingApi.EventReceiver.Models;
using BankingApi.EventReceiver.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateDefaultBuilder(args);

builder.ConfigureServices((context, services) =>
{
    services.AddDbContext<BankingApiDbContext>(options =>
    {
        var connectionString = Environment.GetEnvironmentVariable("SQL_CONNECTION_STRING")
            ?? "Server=sqlserver,1433;Database=BankingApiTest;User Id=sa;Password=YourPassword123!;TrustServerCertificate=true;Encrypt=false;";
        options.UseSqlServer(connectionString);
    }, ServiceLifetime.Scoped);

    services.AddScoped<ITransactionProcessor, TransactionProcessor>();
    services.AddSingleton<IServiceBusReceiver, ServiceBusReceiver>();
    services.AddHostedService<MessageWorkerService>();

    services.AddLogging(builder =>
    {
        builder.AddConsole();
        builder.AddSimpleConsole(options =>
        {
            options.IncludeScopes = true;
            options.SingleLine = true;
            options.TimestampFormat = "yyyy-MM-dd HH:mm:ss ";
        });
        
        var logLevel = Environment.GetEnvironmentVariable("LOGGING_LEVEL");
        if (Enum.TryParse<LogLevel>(logLevel, out var level))
        {
            builder.SetMinimumLevel(level);
        }
        else
        {
            builder.SetMinimumLevel(LogLevel.Information);
        }
    });
});

var host = builder.Build();

using (var scope = host.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<BankingApiDbContext>();
    var dbLogger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    try
    {
        dbLogger.LogInformation("Ensuring database exists and applying migrations...");
        await dbContext.Database.MigrateAsync();
        dbLogger.LogInformation("Database ready");

        if (!await dbContext.BankAccounts.AnyAsync())
        {
            dbLogger.LogInformation("Seeding test bank accounts...");
            dbContext.BankAccounts.AddRange(
                new BankAccount { Id = Guid.Parse("7d445724-24ec-4d52-aa7a-ff2bac9f191d"), Balance = 1000.00m },
                new BankAccount { Id = Guid.Parse("3bbaf4ca-5bfa-4922-a395-d755beac475f"), Balance = 500.00m },
                new BankAccount { Id = Guid.Parse("f8e1a4b2-9c3d-4e5f-8a7b-1d2e3f4a5b6c"), Balance = 2500.00m }
            );
            await dbContext.SaveChangesAsync();
            dbLogger.LogInformation("Test accounts created");
        }
    }
    catch (Exception ex)
    {
        dbLogger.LogError(ex, "Failed to initialize database");
        Environment.Exit(1);
    }
}

var logger = host.Services.GetRequiredService<ILogger<Program>>();

logger.LogInformation("Starting Banking Event Receiver Host...");

try
{
    await host.RunAsync();
}
catch (Exception ex)
{
    logger.LogCritical(ex, "Host terminated unexpectedly");
    Environment.Exit(1);
}

logger.LogInformation("Banking Event Receiver Host stopped");