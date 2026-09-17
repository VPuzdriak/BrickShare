using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace BrickShare.Catalog.IntegrationTests.Rebrickable;

/// <summary>
/// Rebrickable, as far as the client can tell. Test infrastructure, so it is written outright
/// rather than driven by a test of its own — it has no behaviour we would ever have to defend.
/// </summary>
public sealed class RebrickableStub : IAsyncDisposable
{
    private readonly WebApplication _app;
    private int _failuresRemaining;

    private RebrickableStub()
    {
        WebApplicationBuilder builder = WebApplication.CreateSlimBuilder();

        // Port 0 means "whatever is free". Two test classes running in parallel must not fight
        // over a hard-coded port, and CI is where that fight would happen.
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();

        _app = builder.Build();

        _app.MapGet("/api/v3/lego/sets/{setNumber}/", (string setNumber, HttpRequest request) =>
        {
            Requests++;
            LastAuthorization = request.Headers.Authorization.ToString();

            if (_failuresRemaining > 0)
            {
                _failuresRemaining--;
                return Results.StatusCode(StatusCodes.Status500InternalServerError);
            }

            return Sets.TryGetValue(setNumber, out object? payload)
                ? Results.Json(payload)
                : Results.NotFound();
        });

        _app.MapGet("/api/v3/lego/themes/{themeId:int}/", (int themeId, HttpRequest request) =>
        {
            Requests++;
            LastAuthorization = request.Headers.Authorization.ToString();

            return Themes.TryGetValue(themeId, out object? payload)
                ? Results.Json(payload)
                : Results.NotFound();
        });
    }

    public Dictionary<string, object> Sets { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<int, object> Themes { get; } = new();

    public int Requests { get; private set; }

    public string? LastAuthorization { get; private set; }

    public string BaseAddress => $"{_app.Urls.First()}/api/v3/";

    public void FailNextRequests(int count) => _failuresRemaining = count;

    /// <summary>
    /// One stub serves every test in the database collection, so each test has to start from the
    /// same empty desk. Called by <see cref="DatabaseTest"/>, beside the database reset.
    /// </summary>
    public void Reset()
    {
        Sets.Clear();
        Themes.Clear();
        Requests = 0;
        LastAuthorization = null;
        _failuresRemaining = 0;
    }

    /// <summary>
    /// Constructing and starting are one step, so there is no such thing as a stub that exists
    /// but is not listening — and so a test can hold it in a single `await using`.
    /// </summary>
    public static async Task<RebrickableStub> StartAsync()
    {
        var stub = new RebrickableStub();
        await stub._app.StartAsync();

        return stub;
    }

    public async ValueTask DisposeAsync()
    {
        await _app.StopAsync();
        await _app.DisposeAsync();
    }
}
