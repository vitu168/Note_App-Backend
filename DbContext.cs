using Microsoft.EntityFrameworkCore;
using NoteApi.Models;

namespace NoteApi
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<UserProfile> UserProfiles { get; set; } 
        public DbSet<NoteInfo> NoteInfos { get; set; } 
    }
}
