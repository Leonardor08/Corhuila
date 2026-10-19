using Microsoft.EntityFrameworkCore;
using RutaSur.Datos.Entidades;

namespace RutaSur.Datos;

public class RutaSurDbContext : DbContext
{
    public RutaSurDbContext(DbContextOptions<RutaSurDbContext> options) : base(options) { }

    public DbSet<Destino> Destinos => Set<Destino>();
    public DbSet<Experiencia> Experiencias => Set<Experiencia>();
    public DbSet<Salida> Salidas => Set<Salida>();
    public DbSet<Reserva> Reservas => Set<Reserva>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Destino>(e =>
        {
            e.Property(x => x.Nombre).HasMaxLength(120).IsRequired();
            e.Property(x => x.Municipio).HasMaxLength(80).IsRequired();
        });

        b.Entity<Experiencia>(e =>
        {
            e.Property(x => x.Nombre).HasMaxLength(120).IsRequired();
            // SQLite no tiene decimal nativo: se guarda como TEXT con precisión exacta.
            e.Property(x => x.PrecioPorPersona).HasConversion<string>();
        });

        b.Entity<Salida>(e =>
        {
            // Defensa en profundidad: la base de datos tampoco admite cupos negativos.
            e.ToTable(t => t.HasCheckConstraint("CK_Salida_Cupos",
                "CuposDisponibles >= 0 AND CuposDisponibles <= CupoTotal"));
        });

        b.Entity<Reserva>(e =>
        {
            e.HasIndex(x => x.Codigo).IsUnique();
            e.Property(x => x.Codigo).HasMaxLength(12).IsRequired();
            e.Property(x => x.NombreTitular).HasMaxLength(120).IsRequired();
            e.Property(x => x.Documento).HasMaxLength(20).IsRequired();
            e.Property(x => x.Correo).HasMaxLength(120).IsRequired();
            e.Property(x => x.Total).HasConversion<string>();
        });
    }
}
