using ClientManagement.Server.Entities;
using Microsoft.EntityFrameworkCore;

namespace ClientManagement.Server.Data;

public sealed class AppDbContext : DbContext
{
    public AppDbContext(
        DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }


    // =====================================================
    // TÁBLÁK
    // =====================================================

    public DbSet<DeviceEntity> Devices =>
        Set<DeviceEntity>();

    public DbSet<DiskEntity> Disks =>
        Set<DiskEntity>();

    public DbSet<AgentCommandEntity> AgentCommands =>
        Set<AgentCommandEntity>();


    // =====================================================
    // ADATBÁZIS MODELL KONFIGURÁCIÓ
    // =====================================================

    protected override void OnModelCreating(
        ModelBuilder modelBuilder)
    {
        // -------------------------------------------------
        // DEVICE
        // -------------------------------------------------

        modelBuilder.Entity<DeviceEntity>(
            entity =>
            {
                entity.HasKey(
                    device => device.Id);

                entity.HasIndex(
                        device => device.AgentId)
                    .IsUnique();

                entity.Property(
                        device => device.AgentId)
                    .IsRequired();

                entity.Property(
                        device => device.Hostname)
                    .IsRequired();

                entity.HasMany(
                        device => device.Disks)
                    .WithOne(
                        disk => disk.Device)
                    .HasForeignKey(
                        disk => disk.DeviceId)
                    .OnDelete(
                        DeleteBehavior.Cascade);
            });


        // -------------------------------------------------
        // DISK
        // -------------------------------------------------

        modelBuilder.Entity<DiskEntity>(
            entity =>
            {
                entity.HasKey(
                    disk => disk.Id);

                entity.Property(
                        disk => disk.Name)
                    .IsRequired();
            });


        // -------------------------------------------------
        // AGENT COMMAND
        // -------------------------------------------------

        modelBuilder.Entity<AgentCommandEntity>(
            entity =>
            {
                entity.HasKey(
                    command => command.Id);

                entity.Property(
                        command => command.AgentId)
                    .IsRequired();

                entity.Property(
                        command => command.CommandType)
                    .IsRequired();

                entity.Property(
                        command => command.Status)
                    .IsRequired();

                entity.HasIndex(
                    command => new
                    {
                        command.AgentId,
                        command.Status
                    });
            });
    }
}