using Scalar.AspNetCore;
using Chess.Engine.AI;
using Chess.API.Hubs;

var builder = WebApplication.CreateBuilder(args);

// ─── Services ──────────────────────────────────────────────

// Engine services
builder.Services.AddSingleton<TranspositionTable>();
builder.Services.AddTransient<Search>(sp =>
    new Search(sp.GetRequiredService<TranspositionTable>()));

// Controllers + SignalR
builder.Services.AddControllers();
builder.Services.AddSignalR();

// OpenAPI document (served at /openapi/v1.json, browsed via Scalar)
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, _, _) =>
    {
        document.Info.Title = "Chess Engine API";
        document.Info.Version = "v1";
        document.Info.Description = "REST + SignalR API for the Chess Engine";
        return Task.CompletedTask;
    });
});

// CORS – allow all origins for development
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });

    // Named policy for SignalR (requires credentials / specific origins)
    options.AddPolicy("SignalR", policy =>
    {
        policy.SetIsOriginAllowed(_ => true)
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

var app = builder.Build();

// ─── Middleware ─────────────────────────────────────────────

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(options => options.WithTitle("Chess Engine API"));
}

app.UseHttpsRedirection();
app.UseCors();

app.MapControllers();
app.MapHub<EngineHub>("/hubs/engine").RequireCors("SignalR");

app.Run();
