using Microsoft.EntityFrameworkCore;
using TimescaleProject.Models;

namespace TimescaleProject;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<FileResult> Results { get; set; }
    public DbSet<DataValue> Values { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<FileResult>().HasIndex(f => f.FileName).IsUnique();
    }
}
