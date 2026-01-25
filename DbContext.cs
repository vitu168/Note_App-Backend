using Microsoft.EntityFrameworkCore;
using NoteApi.Models;

namespace NoteApi
{
    public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
    {
        public DbSet<UserProfile> UserProfiles { get; set; } 
        public DbSet<Noteinfo> Noteinfos { get; set; } 
    }
}
