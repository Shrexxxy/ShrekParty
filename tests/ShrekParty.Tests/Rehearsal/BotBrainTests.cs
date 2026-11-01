using System.Text.Json;
using AwesomeAssertions;
using ShrekParty.Rehearsal;

namespace ShrekParty.Tests.Rehearsal;

public class BotBrainTests
{
    private static readonly Guid Me = Guid.NewGuid();

    [Fact]
    public void Mafia_task_retried_when_snapshot_unchanged()
    {
        // Первый запрос задания не прошёл (сбой сети, «слишком быстро») — снапшот тот же, бот пробует снова.
        var snapshot = Night(tasksDone: 2);
        var done = new HashSet<string>();

        BotBrain.Decide(snapshot, Me, new Random(1), done).Should().ContainSingle(a => a.Path.EndsWith("/task-done"));
        BotBrain.Decide(snapshot, Me, new Random(1), done).Should().ContainSingle(a => a.Path.EndsWith("/task-done"));
    }

    [Fact]
    public void Mafia_no_tasks_after_five()
    {
        BotBrain.Decide(Night(tasksDone: 5), Me, new Random(1), new HashSet<string>()).Should().BeEmpty();
    }

    [Fact]
    public void Pet_hatch_retried_until_hatched()
    {
        var egg = JsonSerializer.SerializeToElement(new { myPet = new { hatched = false } });
        var done = new HashSet<string>();

        BotBrain.Pet(egg, done).Should().ContainSingle();
        BotBrain.Pet(egg, done).Should().ContainSingle();
        BotBrain.Pet(JsonSerializer.SerializeToElement(new { myPet = new { hatched = true } }), done).Should().BeEmpty();
    }

    private static JsonElement Night(int tasksDone) => JsonSerializer.SerializeToElement(new
    {
        id = Guid.NewGuid().ToString(),
        type = "Mafia",
        details = new
        {
            phase = "Night",
            day = 1,
            iAmAlive = true,
            myRole = "Civilian",
            myTasksDone = tasksDone,
            players = new[]
            {
                new { userId = Me, login = "Я", alive = true },
                new { userId = Guid.NewGuid(), login = "Сосед", alive = true },
            },
        },
    });
}
