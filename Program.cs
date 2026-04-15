using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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
    AutoConnectRealtime = true  
};
 var supabaseClient = new Client(supabaseUrl, supabaseKey, options);

builder.Services.AddSingleton(supabaseClient);

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

app.UseAuthorization();
app.MapControllers();

app.Run();