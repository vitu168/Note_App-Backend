using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NoteApi.Services;
using Supabase;
using System;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var supabaseUrl = builder.Configuration["Supabase:Url"];
var supabaseKey = builder.Configuration["Supabase:Key"];  
if (string.IsNullOrEmpty(supabaseUrl) || string.IsNullOrEmpty(supabaseKey))
{
    throw new ArgumentException("Supabase URL or Key is missing in configuration.");
}

var options = new SupabaseOptions
{
    AutoConnectRealtime = false
};
 var supabaseClient = new Client(supabaseUrl, supabaseKey, options);
await supabaseClient.InitializeAsync();

builder.Services.AddSingleton(supabaseClient);

builder.Services.AddHttpClient<FcmNotificationService>();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

app.UseCors();
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Note API v1");
    c.RoutePrefix = "swagger";
});

var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrEmpty(port))
{
    // Production: bind to 0.0.0.0 for Render
    app.Urls.Add($"http://0.0.0.0:{port}");
}
else
{
    // Development: bind to localhost
    app.Urls.Add("http://localhost:5000");
    app.Urls.Add("https://localhost:5001");
}
app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();

// Health check endpoint — used by Render and UptimeRobot to keep service alive
app.MapGet("/health", async (Client supabase) =>
{
    try
    {
        // Ping Supabase with a lightweight query to verify connection is alive
        await supabase.From<NoteApi.Models.UserProfile>().Limit(1).Get();
        return Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow });
    }
    catch (Exception ex)
    {
        return Results.Problem(detail: ex.Message, statusCode: 503, title: "unhealthy");
    }
}).ExcludeFromDescription();

app.UseAuthorization();
app.MapControllers();

app.Run();