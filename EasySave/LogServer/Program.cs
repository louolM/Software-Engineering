using EasyLog;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

// ── Verrou partagé pour toutes les requêtes ───────────────────────────────────
var writeLock = new SemaphoreSlim(1, 1);

// ── POST /logs ────────────────────────────────────────────────────────────────
app.MapPost("/logs", async (HttpContext ctx) =>
{
    try
    {
        var entry = await JsonSerializer.DeserializeAsync<LogEntry>(
            ctx.Request.Body,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (entry == null)
            return Results.BadRequest("Invalid log entry.");

        var format = ctx.Request.Query["format"].ToString().ToUpper();
        format = format == "XML" ? "XML" : "JSON";

        // ── Écriture séquentielle — une seule requête à la fois ──────────
        await writeLock.WaitAsync();
        try
        {
            var logger = new Logger(format, logDirectory: "central-logs");
            logger.Write(entry);
        }
        finally
        {
            writeLock.Release();
        }

        return Results.Ok();
    }
    catch (Exception ex)
    {
        return Results.Problem(ex.Message);
    }
});

// ── GET /health ───────────────────────────────────────────────────────────────
app.MapGet("/health", () => Results.Ok(new { status = "ok", time = DateTime.UtcNow }));

app.Run();