var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://localhost:5050");
var app = builder.Build();

app.MapGet("/api/hello", () => Results.Json(new { text = "Hello from XpGetter C# backend! wow" }));

app.Run();