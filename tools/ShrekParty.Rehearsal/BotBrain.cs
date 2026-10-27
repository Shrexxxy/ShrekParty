using System.Text.Json;

namespace ShrekParty.Rehearsal;

/// <summary>Одно действие бота.</summary>
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
            _ => [],
        };
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
