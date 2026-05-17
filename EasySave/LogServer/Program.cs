using EasyLog;
using System.Text.Json;

// Minimal ASP.NET Core log server that receives log entries from EasySave clients
// and writes them to a central "central-logs" directory.
// Exposes two endpoints: POST /logs to receive entries and GET /health for liveness checks.
var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();


// // Single semaphore shared across all incoming requests to prevent concurrent writes
// // from corrupting the log file when multiple clients post entries simultaneously.
var writeLock = new SemaphoreSlim(1, 1);

// POST /logs
// // Deserialises the request body as a LogEntry and appends it to the daily log file.
// // Accepts an optional "format" query parameter ("JSON" or "XML"); defaults to JSON.
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

        // Acquire the write lock so only one request writes at a time.
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

// GET /health
// // Returns a simple JSON object with the server status and current UTC time.
// // Used by Docker health checks or monitoring tools to confirm the server is up.
app.MapGet("/health", () => Results.Ok(new { status = "ok", time = DateTime.UtcNow }));

app.Run();