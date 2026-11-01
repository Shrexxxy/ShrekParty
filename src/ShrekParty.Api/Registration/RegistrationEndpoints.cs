using ShrekParty.Api.Auth;

namespace ShrekParty.Api.Registration;

/// <summary>
/// Маршруты регистрации-ловушки.
/// </summary>
public static class RegistrationEndpoints
{
    /// <summary>
    /// <c>/api/registration/*</c> (анонимно) и <c>/api/admin/registration/*</c>.
    /// </summary>
    public static IEndpointRouteBuilder MapRegistration(this IEndpointRouteBuilder app)
    {
        var guest = app.MapGroup("/api/registration");

        guest.MapGet("/status", (RegistrationService registration) => registration.GetStatusAsync());

        guest.MapGet("/results", (RegistrationService registration) => registration.GetResultsAsync())
            .RequireAuthorization();

        guest.MapPost("/start", async (StartRegistrationRequest request, RegistrationService registration) =>
        {
            var (outcome, attemptId) = await registration.StartAsync(request.Login, request.Password);
            return outcome == RegistrationOutcome.Ok
                ? Results.Ok(new StartRegistrationResponse(attemptId))
                : ToError(outcome);
        });

        guest.MapPost("/complete", async (CompleteRegistrationRequest request, RegistrationService registration, HttpContext http) =>
        {
            var (outcome, user) = await registration.CompleteAsync(request.AttemptId, request.NewPassword);
            if (outcome != RegistrationOutcome.Ok)
            {
                return ToError(outcome);
            }

            await AuthEndpoints.SignInAsync(http, user!);
            return Results.Ok(user!.ToMe());
        });

        var admin = app.MapGroup("/api/admin/registration").RequireAuthorization(AuthEndpoints.AdminPolicy);

        admin.MapPost("/open", async (RegistrationService registration) =>
            await registration.OpenAsync() ? Results.NoContent() : Results.Conflict());

        admin.MapPost("/close", async (RegistrationService registration) =>
            await registration.CloseAsync() ? Results.NoContent() : Results.Conflict());

        return app;
    }

    private static IResult ToError(RegistrationOutcome outcome) => outcome switch
    {
        RegistrationOutcome.NotOpen => Results.StatusCode(StatusCodes.Status403Forbidden),
        RegistrationOutcome.InvalidInput => Results.BadRequest(new { error = "invalid" }),
        RegistrationOutcome.LoginTaken => Results.Conflict(new { error = "login_taken" }),
        RegistrationOutcome.AttemptNotFound => Results.NotFound(),
        RegistrationOutcome.AlreadyCompleted => Results.Conflict(new { error = "completed" }),
        RegistrationOutcome.SamePassword => Results.BadRequest(new { error = "same" }),
        _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, null),
    };
}
