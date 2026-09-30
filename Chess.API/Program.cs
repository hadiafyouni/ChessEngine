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

// Swagger / OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "Chess Engine API",
        Version = "v1",
        Description = "REST + SignalR API for the Chess Engine"
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
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Chess Engine API v1");
        c.RoutePrefix = "swagger";
    });
}

app.UseHttpsRedirection();
app.UseCors();

app.MapControllers();
app.MapHub<EngineHub>("/hubs/engine").RequireCors("SignalR");

app.Run();
