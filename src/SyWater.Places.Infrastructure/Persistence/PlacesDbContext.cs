using Microsoft.EntityFrameworkCore;
using SyWater.Places.Infrastructure.Persistence.Entities;

namespace SyWater.Places.Infrastructure.Persistence;


public sealed class PlacesDbContext(DbContextOptions<PlacesDbContext> options) : DbContext(options)
{
    public DbSet<PlaceEntity> Places => Set<PlaceEntity>();
    public DbSet<CountryEntity> Countries => Set<CountryEntity>();
    public DbSet<SubdivisionEntity> Subdivisions => Set<SubdivisionEntity>();
    public DbSet<CityEntity> Cities => Set<CityEntity>();
    public DbSet<PlaceActivityEntity> ActivityLog => Set<PlaceActivityEntity>();
    public DbSet<PlaceTariffEntity> PlaceTariffs => Set<PlaceTariffEntity>();
    public DbSet<TariffCatalogEntity> TariffCatalog => Set<TariffCatalogEntity>();
    public DbSet<TipEntity> Tips => Set<TipEntity>();
    public DbSet<TipFavoriteEntity> TipFavorites => Set<TipFavoriteEntity>();
}