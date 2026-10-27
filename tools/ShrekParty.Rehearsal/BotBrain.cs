using System.Text.Json;

namespace ShrekParty.Rehearsal;

/// <summary>Одно действие бота.</summary>
/// <param name="Description">Строка в лог; пустая — частое действие (тапы), в лог только при ошибке.</param>
/// <param name="Photo">Отправить сгенерированную картинку формой вместо JSON.</param>
public record BotAction(string Description, HttpMethod Method, string Path, object? Body = null, bool Photo = false);

/// <summary>
/// Что сделал бы гость, глядя на текущий ивент. Каждое решение — один раз (ключ в <c>done</c>).
/// </summary>
public static class BotBrain
{
    private const double BetChance = 0.6;
    private const double LikeChance = 0.5;
    private const int MaxBet = 10;
    private const double FalseStartChance = 0.1;
    private const double QuizPressChance = 0.8;

    /// <summary>
    /// Новые действия по снапшоту ивента.
    /// </summary>
    public static IEnumerable<BotAction> Decide(JsonElement activeEvent, Guid me, Random random, ISet<string> done)
    {
        var id = activeEvent.GetProperty("id").GetString()!;
        var details = activeEvent.GetProperty("details");
        if (details.ValueKind != JsonValueKind.Object)
        {
            return [];
        }

        return activeEvent.GetProperty("type").GetString() switch
        {
            "FightPoll" => Poll(id, details, random, done),
            "Tournament" => Tournament(id, details, me, random, done),
            "PhotoHunt" => PhotoHunt(id, details, me, random, done),
            "HideAndSeek" => Hide(id, details, random, done),
            "Reaction" => Reaction(id, details, random, done),
            "Tapper" => Tapper(id, details, random),
            "Days" => Days(id, details, random, done),
            "NeuroPhoto" => Neuro(id, details, random, done),
            "Quiz" => Quiz(id, details, me, random, done),
            _ => [],
        };
    }

    private static IEnumerable<BotAction> Quiz(string id, JsonElement details, Guid me, Random random, ISet<string> done)
    {
        var phase = details.GetProperty("phase").GetString();
        var myScore = details.GetProperty("scores").EnumerateArray()
            .Where(s => s.GetProperty("userId").GetGuid() == me)
            .Select(s => s.GetProperty("score").GetInt32())
            .FirstOrDefault();

        if (phase is "Choosing" or "Closed" && details.GetProperty("chooserId") is { ValueKind: JsonValueKind.String } chooser && chooser.GetGuid() == me)
        {
            var free = details.GetProperty("board").EnumerateArray()
                .SelectMany(t => t.GetProperty("cells").EnumerateArray().Select(c => (Theme: t.GetProperty("name").GetString(), Cell: c)))
                .Where(x => !x.Cell.GetProperty("played").GetBoolean())
                .ToList();
            if (free.Count > 0 && done.Add($"quiz-choose:{id}:{details.GetProperty("round").GetInt32()}:{free.Count}"))
            {
                var (theme, cell) = free[random.Next(free.Count)];
                yield return new BotAction($"выбираю «{theme}» за {cell.GetProperty("price").GetInt32()}", HttpMethod.Post, $"/api/quiz/{id}/choose",
                    new { questionId = cell.GetProperty("questionId").GetGuid() });
            }
        }

        if (phase == "Reading"
            && !details.GetProperty("wrong").EnumerateArray().Any(w => w.GetGuid() == me)
            && done.Add($"quiz-press:{id}:{details.GetProperty("question").GetProperty("id").GetString()}:{details.GetProperty("wrong").GetArrayLength()}")
            && random.NextDouble() < QuizPressChance)
        {
            yield return new BotAction("жму!", HttpMethod.Post, $"/api/quiz/{id}/press", new { clientTime = DateTimeOffset.UtcNow });
        }

        if (details.GetProperty("final") is not { ValueKind: JsonValueKind.Object } final || !final.GetProperty("iAmFinalist").GetBoolean())
        {
            yield break;
        }

        if (phase == "FinalBetting" && myScore > 0 && final.GetProperty("myBet").ValueKind == JsonValueKind.Null && done.Add($"quiz-bet:{id}"))
        {
            var amount = random.Next(1, myScore + 1);
            yield return new BotAction($"ставлю в финале {amount}", HttpMethod.Post, $"/api/quiz/{id}/bet", new { amount });
        }

        if (phase == "FinalQuestion" && final.GetProperty("myAnswer").ValueKind == JsonValueKind.Null && done.Add($"quiz-answer:{id}"))
        {
            yield return new BotAction("отвечаю в финале", HttpMethod.Post, $"/api/quiz/{id}/final-answer", new { text = "Шрек!" });
        }
    }

    private static IEnumerable<BotAction> Reaction(string id, JsonElement details, Random random, ISet<string> done)
    {
        var round = details.GetProperty("round").GetInt32();
        var phase = details.GetProperty("phase").GetString();
        if (details.GetProperty("myAttempt").ValueKind != JsonValueKind.Null || details.GetProperty("myFalseStart").GetBoolean())
        {
            yield break;
        }

        // Иногда бот, как живой, не выдерживает красного.
        if (phase == "Red" && done.Add($"reaction-red:{id}:{round}") && random.NextDouble() < FalseStartChance)
        {
            yield return new BotAction("фальстарт!", HttpMethod.Post, $"/api/reaction/{id}/false-start");
        }
        else if (phase == "Green" && done.Add($"reaction:{id}:{round}"))
        {
            var ms = random.Next(200, 601);
            yield return new BotAction($"реакция {ms} мс", HttpMethod.Post, $"/api/reaction/{id}/attempt", new { ms });
        }
    }

    private static IEnumerable<BotAction> Tapper(string id, JsonElement details, Random random)
    {
        // Во время забега снапшот приходит каждую секунду — на каждый отвечаем пачкой.
        if (details.GetProperty("phase").GetString() == "Running")
        {
            yield return new BotAction("", HttpMethod.Post, $"/api/tapper/{id}/taps", new { count = random.Next(5, 16) });
        }
    }

    private static IEnumerable<BotAction> Days(string id, JsonElement details, Random random, ISet<string> done)
    {
        if (details.GetProperty("phase").GetString() != "Answering" || details.GetProperty("iAmDisqualified").GetBoolean()
            || details.GetProperty("myAnswer").ValueKind != JsonValueKind.Null || !done.Add($"days:{id}"))
        {
            yield break;
        }

        var days = random.Next(8000, 10_001);
        yield return new BotAction($"имениннику {days} дней", HttpMethod.Post, $"/api/days/{id}/answer", new { days });
    }

    private static IEnumerable<BotAction> Neuro(string id, JsonElement details, Random random, ISet<string> done)
    {
        var index = details.GetProperty("index").GetInt32();
        if (details.GetProperty("phase").GetString() != "Guessing" || details.GetProperty("myGuess").ValueKind != JsonValueKind.Null
            || !done.Add($"neuro:{id}:{index}"))
        {
            yield break;
        }

        var options = details.GetProperty("options").EnumerateArray().ToList();
        var pick = options[random.Next(options.Count)];
        yield return new BotAction($"на фото {pick.GetProperty("login").GetString()}", HttpMethod.Post, $"/api/neuro/{id}/guess",
            new { userId = pick.GetProperty("userId").GetGuid() });
    }

    private static IEnumerable<BotAction> Poll(string id, JsonElement details, Random random, ISet<string> done)
    {
        if (details.GetProperty("closed").GetBoolean() || details.GetProperty("myVote").ValueKind != JsonValueKind.Null || !done.Add($"poll:{id}"))
        {
            yield break;
        }

        var options = details.GetProperty("options").EnumerateArray().Select(o => o.GetProperty("name").GetString()!).ToList();
        var option = options[random.Next(options.Count)];
        yield return new BotAction($"голосую за «{option}»", HttpMethod.Post, $"/api/events/{id}/vote", new { option });
    }

    private static IEnumerable<BotAction> Tournament(string id, JsonElement details, Guid me, Random random, ISet<string> done)
    {
        foreach (var match in details.GetProperty("matches").EnumerateArray())
        {
            if (match.GetProperty("status").GetString() != "Pending" || match.GetProperty("isBye").GetBoolean()
                || match.GetProperty("myBet").ValueKind != JsonValueKind.Null
                || PlayerId(match, "playerA") is not { } a || PlayerId(match, "playerB") is not { } b || a == me || b == me)
            {
                continue;
            }

            var matchId = match.GetProperty("id").GetString()!;
            if (!done.Add($"bet:{matchId}") || random.NextDouble() >= BetChance)
            {
                continue;
            }

            var side = random.Next(2) == 0 ? "playerA" : "playerB";
            var amount = random.Next(1, MaxBet + 1);
            yield return new BotAction(
                $"ставлю {amount} на {match.GetProperty(side).GetProperty("login").GetString()}",
                HttpMethod.Post,
                $"/api/matches/{matchId}/bets",
                new { onPlayerId = side == "playerA" ? a : b, amount });
        }

        var challenge = details.GetProperty("challenge");
        if (challenge.GetProperty("offeredToMe").GetBoolean() && challenge.GetProperty("state").GetString() == "Offered" && done.Add($"challenge:{id}"))
        {
            var accept = random.Next(2) == 0;
            yield return new BotAction(accept ? "бросаю вызов имениннику!" : "забираю 100%", HttpMethod.Post, $"/api/tournaments/{id}/challenge", new { accept });
        }
    }

    private static IEnumerable<BotAction> PhotoHunt(string id, JsonElement details, Guid me, Random random, ISet<string> done)
    {
        var phase = details.GetProperty("phase").GetString();
        if (phase == "Shooting" && details.GetProperty("myPhotoUrl").ValueKind == JsonValueKind.Null && done.Add($"photo:{id}"))
        {
            yield return new BotAction("шлю фото", HttpMethod.Post, $"/api/photo/{id}", Photo: true);
        }

        if (phase != "Rating")
        {
            yield break;
        }

        foreach (var photo in details.GetProperty("photos").EnumerateArray())
        {
            var owner = photo.GetProperty("userId").GetGuid();
            if (owner == me || photo.GetProperty("likedByMe").GetBoolean() || !done.Add($"like:{id}:{owner}") || random.NextDouble() >= LikeChance)
            {
                continue;
            }

            yield return new BotAction($"лайкаю {photo.GetProperty("login").GetString()}", HttpMethod.Put, $"/api/photo/{id}/likes/{owner}");
        }
    }

    private static IEnumerable<BotAction> Hide(string id, JsonElement details, Random random, ISet<string> done)
    {
        var shots = details.GetProperty("shots").EnumerateArray().Select(s => s.GetProperty("id").GetString()!).ToList();
        if (details.GetProperty("phase").GetString() != "Voting" || shots.Count == 0
            || details.GetProperty("myVote").ValueKind != JsonValueKind.Null || !done.Add($"hide:{id}"))
        {
            yield break;
        }

        yield return new BotAction("голосую за смешное фото", HttpMethod.Post, $"/api/hide/{id}/vote", new { shotId = shots[random.Next(shots.Count)] });
    }

    private static Guid? PlayerId(JsonElement match, string side) =>
        match.GetProperty(side) is { ValueKind: JsonValueKind.Object } player ? player.GetProperty("id").GetGuid() : null;
}
