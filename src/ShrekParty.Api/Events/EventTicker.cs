namespace ShrekParty.Api.Events;

/// <summary>
/// Раз в секунду двигает таймеры текущего ивента (переходы фаз, авто-завершение).
/// Выключается настройкой <c>Events:TickerEnabled = false</c> — в тестах время двигают вручную.
/// </summary>
public class EventTicker(IServiceScopeFactory scopes, IConfiguration configuration, ILogger<EventTicker> logger) : BackgroundService
{
    private static readonly TimeSpan Period = TimeSpan.FromSeconds(1);

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!configuration.GetValue("Events:TickerEnabled", true))
        {
            return;
        }

        using var timer = new PeriodicTimer(Period);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = scopes.CreateScope();
                await scope.ServiceProvider.GetRequiredService<EventEngine>().TickAsync();
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                // Падение одного тика не должно останавливать вечеринку.
                logger.LogError(e, "Тик ивентов упал");
            }
        }
    }
}
