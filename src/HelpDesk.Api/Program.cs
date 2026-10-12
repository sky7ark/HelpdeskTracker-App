using System.Text.Json.Serialization;
using HelpDesk.Api.Data;
using HelpDesk.Api.Middleware;
using HelpDesk.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// ---------- Dependency injection: interfaces mapped to implementations ----------
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IDbConnectionFactory, SqlConnectionFactory>();
builder.Services.AddScoped<IIncidentRepository, SqlIncidentRepository>();
builder.Services.AddScoped<IIncidentService, IncidentService>();

builder.Services
    .AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

var app = builder.Build();

// ---------- HTTP pipeline ----------
app.UseExceptionHandler();
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}
app.UseHttpsRedirection();

app.UseDefaultFiles();   // serves wwwroot/index.html at "/"
app.UseStaticFiles();
app.MapControllers();

app.Run();
