using ShrekParty.Rehearsal;

// Боты-гости для репетиции: dotnet run --project tools/ShrekParty.Rehearsal -- --url http://localhost:5000 --bots 9
var url = new Uri(Arg("--url") ?? "http://localhost:5000");
var count = int.Parse(Arg("--bots") ?? "9");
var minDelay = int.Parse(Arg("--min-delay") ?? "1000");
var maxDelay = int.Parse(Arg("--max-delay") ?? "6000");

using var stop = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    stop.Cancel();
};

var log = TextWriter.Synchronized(Console.Out);
var options = new BotOptions(url, minDelay, maxDelay);
var bots = BotNames.Take(count).Select(login => new Bot(login, options, new BotTransport(), log, new Random())).ToList();
log.WriteLine($"{bots.Count} ботов ждут регистрацию на {url}. Ctrl+C — выход.");

await Task.WhenAll(bots.Select(async bot =>
{
    try
    {
        await bot.RunAsync(stop.Token);
    }
    catch (Exception e)
    {
        log.WriteLine($"[{bot.Login}] упал: {e.Message}");
    }
}));

string? Arg(string name)
{
    var i = Array.IndexOf(args, name);
    return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
}
