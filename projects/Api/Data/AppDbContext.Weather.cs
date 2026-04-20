using Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Api.Data;

public partial class AppDbContext
{
    /// <summary>Rolling 50-tick weather forecast entries per city.</summary>
    public DbSet<CityWeatherForecast> CityWeatherForecasts => Set<CityWeatherForecast>();

    private static void ConfigureWeatherEntities(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CityWeatherForecast>(e =>
        {
            e.HasKey(f => new { f.CityId, f.Tick });
            e.HasIndex(f => new { f.CityId, f.Tick });
            e.HasOne(f => f.City)
             .WithMany()
             .HasForeignKey(f => f.CityId)
             .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
