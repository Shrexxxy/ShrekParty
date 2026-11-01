using System.Text.Json;

namespace ShrekParty.Rehearsal;

/// <summary>Одно действие бота.</summary>
/// <param name="Description">Строка в лог; пустая — частое действие (тапы), в лог только при ошибке.</param>
/// <param name="Photo">Отправить сгенерированную картинку формой вместо JSON.</param>
/// <param name="DelayMs">Своя пауза перед действием — сверх обычной «человеческой» (задание делается не мгновенно).</param>
public record BotAction(string Description, HttpMethod Method, string Path, object? Body = null, bool Photo = false, int DelayMs = 0);

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
    private const double AuctionBidChance = 0.4;
    private const int MafiaTasks = 5;
    private const int TaskMs = 1600;

    private static readonly int[] AuctionSteps = [5, 10, 25];

    /// <summary>
    /// Яйцо драконоослика есть и не вылуплено — вылупить (гость тапает яйцо, серверу важен только итог).
    /// Без ключа в done: не прошло — следующий снапшот попробует снова; повтор сервер не считает.
    /// </summary>
    public static IEnumerable<BotAction> Pet(JsonElement snapshot, ISet<string> done)
    {
        if (snapshot.TryGetProperty("myPet", out var pet) && pet.ValueKind == JsonValueKind.Object
            && !pet.GetProperty("hatched").GetBoolean())
        {
            yield return new BotAction("вылупил дракоосла", HttpMethod.Post, "/api/pets/hatch");
        }
    }

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
            "Tournament" => Tournament(id, details, me, random, done),
            "PhotoHunt" => PhotoHunt(id, details, me, random, done),
            "HideAndSeek" => Hide(id, details, random, done),
            "Reaction" => Reaction(id, details, random, done),
            "Tapper" => Tapper(id, details, random),
            "Days" => Days(id, details, random, done),
            "NeuroPhoto" => Neuro(id, details, random, done),
            "Quiz" => Quiz(id, details, me, random, done),
            "Auction" => Auction(id, details, me, random, done),
            "Mafia" => Mafia(id, details, me, random, done),
            "BossFight" => Boss(id, details, me, done),
            _ => [],
        };
    }

    /// <summary>
    /// Бой с боссом по-простому, но с умом: под объявленный удар — уклон, танк прикрывает, маг лечит и жжёт, боец бьёт мощно.
    /// Выбывший поддерживает раз за ход. Выбор — без ключа в done: пока сервер его не увидел, следующий снапшот попробует снова.
    /// </summary>
    private static IEnumerable<BotAction> Boss(string id, JsonElement details, Guid me, ISet<string> done)
    {
        if (details.GetProperty("phase").GetString() != "Choosing" || details.GetProperty("myId").ValueKind != JsonValueKind.String)
        {
            yield break;
        }

        var fighters = details.GetProperty("fighters").EnumerateArray().ToList();
        var self = fighters.Single(f => f.GetProperty("userId").GetGuid() == me);
        if (!self.GetProperty("alive").GetBoolean())
        {
            // Пока снапшот не догнал, iCheered ещё false — ключ хода не даёт жать повторно.
            if (!details.GetProperty("iCheered").GetBoolean() && done.Add($"boss-cheer:{id}:{details.GetProperty("turn").GetInt32()}"))
            {
                yield return new BotAction("поддерживаю с того света", HttpMethod.Post, $"/api/boss/{id}/cheer");
            }

            yield break;
        }

        if (details.GetProperty("myChoice").ValueKind == JsonValueKind.Object)
        {
            yield break;
        }

        var can = details.GetProperty("myOptions").EnumerateArray()
            .Where(o => o.GetProperty("unavailable").ValueKind == JsonValueKind.Null)
            .Select(o => o.GetProperty("action").GetString()!)
            .ToHashSet();
        if (can.Count == 0)
        {
            // Тыква — ход пропускается, жать нечего.
            yield break;
        }

        var targets = details.GetProperty("intent") is { ValueKind: JsonValueKind.Object } intent
            ? intent.GetProperty("targets").EnumerateArray().Select(t => t.GetGuid()).ToList()
            : [];
        var targeted = targets.Contains(me);
        var hurt = fighters
            .Where(f => f.GetProperty("alive").GetBoolean() && f.GetProperty("hp").GetInt32() * 2 < f.GetProperty("maxHp").GetInt32())
            .OrderBy(f => f.GetProperty("hp").GetInt32())
            .Select(f => f.GetProperty("userId").GetGuid())
            .FirstOrDefault();

        var (action, target) = true switch
        {
            _ when targeted && targets.Count < fighters.Count && can.Contains("Dodge") => ("Dodge", (Guid?)null),
            _ when can.Contains("Cover") && targets.Any(t => t != me) => ("Cover", null),
            _ when can.Contains("Heal") && hurt != Guid.Empty => ("Heal", hurt),
            _ when can.Contains("Fire") => ("Fire", null),
            _ when can.Contains("Power") && !targeted => ("Power", null),
            _ => ("Attack", null),
        };
        yield return new BotAction($"в бою: {action}", HttpMethod.Post, $"/api/boss/{id}/act", new { action, targetId = target });
    }

    private static IEnumerable<BotAction> Mafia(string id, JsonElement details, Guid me, Random random, ISet<string> done)
    {
        var phase = details.GetProperty("phase").GetString();
        var day = details.GetProperty("day").GetInt32();
        if (!details.GetProperty("iAmAlive").GetBoolean())
        {
            yield break;
        }

        var players = details.GetProperty("players").EnumerateArray().ToList();
        var others = players.Where(p => p.GetProperty("alive").GetBoolean() && p.GetProperty("userId").GetGuid() != me)
            .Select(p => (Id: p.GetProperty("userId").GetGuid(), Login: p.GetProperty("login").GetString()))
            .ToList();

        if (phase == "Speech" && details.GetProperty("speakerId") is { ValueKind: JsonValueKind.String } speaker && speaker.GetGuid() == me
            && done.Add($"mafia-speech:{id}:{day}"))
        {
            yield return new BotAction("закончил речь", HttpMethod.Post, $"/api/mafia/{id}/speech-done");
        }

        if (phase == "Voting" && !details.GetProperty("iVoted").GetBoolean() && others.Count > 0 && done.Add($"mafia-vote:{id}:{day}"))
        {
            var (targetId, login) = others[random.Next(others.Count)];
            yield return new BotAction($"голосую против {login}", HttpMethod.Post, $"/api/mafia/{id}/vote", new { targetId });
        }

        if (phase != "Night" || others.Count == 0)
        {
            yield break;
        }

        var role = details.GetProperty("myRole").GetString();
        var tasks = details.GetProperty("myTasksDone").GetInt32();

        // Задания — все живые (роли для вида). Сервер не берёт чаще раза в 1,5 с — бот «делает» задание 1,6 с.
        // Без ключа в done: не прошло — следующий снапшот попробует снова, лишнее сервер сам отрежет лимитом.
        if (tasks < MafiaTasks)
        {
            yield return new BotAction($"задание {tasks + 1}", HttpMethod.Post, $"/api/mafia/{id}/task-done", DelayMs: TaskMs);
        }

        if (role == "Civilian")
        {
            yield break;
        }

        var partners = details.GetProperty("partners").EnumerateArray().Select(p => p.GetGuid()).ToHashSet();
        var mine = details.GetProperty("myNightTarget") is { ValueKind: JsonValueKind.String } t ? t.GetGuid() : (Guid?)null;
        var candidates = role switch
        {
            "Mafia" => others.Where(o => !partners.Contains(o.Id)).ToList(),
            "Doctor" => others.Where(o => !(details.GetProperty("lastHeal") is { ValueKind: JsonValueKind.String } h && h.GetGuid() == o.Id)).ToList(),
            _ => others,
        };
        if (candidates.Count == 0)
        {
            yield break;
        }

        if (role == "Mafia")
        {
            // Чтобы не меняться жертвами по кругу: тот, у кого id больше, повторяет выбор напарника.
            var partnerTarget = details.GetProperty("nightActions").EnumerateArray().Select(a => a.GetProperty("targetId").GetGuid()).Cast<Guid?>().FirstOrDefault();
            var follower = partners.Any(p => p.CompareTo(me) < 0);
            var target = follower && partnerTarget is { } pt ? pt : mine ?? candidates[random.Next(candidates.Count)].Id;
            if (target != mine && done.Add($"mafia-night:{id}:{day}:{target}"))
            {
                yield return new BotAction("ночью выбираю жертву", HttpMethod.Post, $"/api/mafia/{id}/night", new { targetId = target });
            }

            yield break;
        }

        if (mine is null && done.Add($"mafia-night:{id}:{day}"))
        {
            yield return new BotAction(role == "Doctor" ? "лечу" : "проверяю", HttpMethod.Post, $"/api/mafia/{id}/night", new { targetId = candidates[random.Next(candidates.Count)].Id });
        }
    }

    private static IEnumerable<BotAction> Auction(string id, JsonElement details, Guid me, Random random, ISet<string> done)
    {
        if (details.GetProperty("phase").GetString() != "Bidding"
            || details.GetProperty("lot") is not { ValueKind: JsonValueKind.Object } lot
            || details.GetProperty("myBalance") is not { ValueKind: JsonValueKind.Number } balanceElement
            || details.GetProperty("leader") is { ValueKind: JsonValueKind.Object } leader && leader.GetProperty("userId").GetGuid() == me)
        {
            yield break;
        }

        var price = details.GetProperty("price") is { ValueKind: JsonValueKind.Number } p ? p.GetInt32() : (int?)null;
        // Решаем один раз на каждое состояние торгов: срок сдвигается с каждой ставкой и с новым выходом лота
        // (после отмены продажи он выходит снова). Стартовую берём всегда — чтобы лот не уходил без ставок.
        if (!done.Add($"auction:{id}:{lot.GetProperty("id").GetString()}:{details.GetProperty("deadline").GetString()}") || (price is not null && random.NextDouble() >= AuctionBidChance))
        {
            yield break;
        }

        var step = price is null ? 0 : AuctionSteps[random.Next(AuctionSteps.Length)];
        var amount = price is null ? lot.GetProperty("startPrice").GetInt32() : price.Value + step;
        if (amount <= balanceElement.GetInt32())
        {
            yield return new BotAction($"ставлю {amount} на «{lot.GetProperty("name").GetString()}»", HttpMethod.Post, $"/api/auction/{id}/bid", new { step, expected = price });
        }
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
