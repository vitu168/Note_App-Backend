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
// await supabaseClient.InitializeAsync();  // Remove to prevent startup failure

builder.Services.AddSingleton(supabaseClient);

// Optional: EF Core + Npgsql Setup (for direct SQL if not using Supabase client exclusively)
// builder.Services.AddDbContext<YourDbContext>(options =>
//     options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")))

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => 
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Note API v1");
    });

    // Open browser automatically
    app.Lifetime.ApplicationStarted.Register(() =>
    {
        var url = "https://localhost:5001/swagger";
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch
        {
            // Ignore if can't open browser
        }
    });
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();