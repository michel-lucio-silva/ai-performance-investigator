using System.Text;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddHttpClient();
var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/api/health", () => Results.Ok(new {
    status = "healthy",
    project = Environment.GetEnvironmentVariable("PROJECT_NAME")
}));

app.MapPost("/api/analyze", async (
    AnalysisRequest request,
    IHttpClientFactory factory) =>
{
    if (string.IsNullOrWhiteSpace(request.Text))
        return Results.BadRequest(new { error = "Informe um texto." });

    var key = Environment.GetEnvironmentVariable("OPENAI_API_KEY");

    if (string.IsNullOrWhiteSpace(key))
        return Results.Ok(new {
            mode = "demo",
            response = "Demonstracao: " +
                request.Text[..Math.Min(request.Text.Length, 250)]
        });

    var prompt = Environment.GetEnvironmentVariable("AI_PROMPT")
        ?? "Analise o texto e apresente uma resposta util.";

    var payload = new {
        model = "gpt-4.1-mini",
        messages = new object[] {
            new { role = "system", content = prompt },
            new { role = "user", content = request.Text }
        }
    };

    var client = factory.CreateClient();
    client.DefaultRequestHeaders.Authorization =
        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", key);

    using var response = await client.PostAsync(
        "https://api.openai.com/v1/chat/completions",
        new StringContent(JsonSerializer.Serialize(payload),
            Encoding.UTF8, "application/json"));

    if (!response.IsSuccessStatusCode)
        return Results.Problem(
            "Falha na API de IA. Verifique chave, modelo e creditos.",
            statusCode: 502);

    using var doc = JsonDocument.Parse(
        await response.Content.ReadAsStringAsync());

    var answer = doc.RootElement.GetProperty("choices")[0]
        .GetProperty("message").GetProperty("content").GetString();

    return Results.Ok(new { mode = "openai", response = answer });
});

app.Run();

record AnalysisRequest(string Text);
