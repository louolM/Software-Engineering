using EasyLog;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

// ── POST /logs — reçoit une LogEntry et l'écrit dans le fichier centralisé ───
app.MapPost("/logs", async (HttpContext ctx) =>
{
    try
    {
        var entry = await JsonSerializer.DeserializeAsync<LogEntry>(
            ctx.Request.Body,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (entry == null)
            return Results.BadRequest("Invalid log entry.");

        // Format demandé en query param : ?format=XML (défaut JSON)
        var format = ctx.Request.Query["format"].ToString().ToUpper();
        format = format == "XML" ? "XML" : "JSON";

        var logger = new Logger(format, logDirectory: "central-logs");
        logger.Write(entry);

        return Results.Ok();
    }
    catch (Exception ex)
    {
        return Results.Problem(ex.Message);
    }
});

// ── GET /health — vérification que le serveur tourne ─────────────────────────
app.MapGet("/health", () => Results.Ok(new { status = "ok", time = DateTime.UtcNow }));

app.Run();