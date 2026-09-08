using Locaccessum.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Locaccessum.Infrastructure.Persistence;

public class LocaccessumDbContext(DbContextOptions<LocaccessumDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Inventory> Inventories => Set<Inventory>();
    public DbSet<Membership> Memberships => Set<Membership>();
    public DbSet<Invitation> Invitations => Set<Invitation>();
    public DbSet<Equipment> Equipment => Set<Equipment>();
    public DbSet<Reservation> Reservations => Set<Reservation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(LocaccessumDbContext).Assembly);
    }
}
