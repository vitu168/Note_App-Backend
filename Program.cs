using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Supabase;
using System;
using System.Diagnostics;

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

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => 
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Note API v1");
    });

    app.Lifetime.ApplicationStarted.Register(() =>
    {
        var url = "https://localhost:5001/swagger";
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    });
}
var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
app.Urls.Add($"http://*:{port}");

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();