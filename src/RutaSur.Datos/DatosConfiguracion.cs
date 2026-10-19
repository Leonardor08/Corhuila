using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RutaSur.Datos.Entidades;
using RutaSur.Datos.Repositorios;

namespace RutaSur.Datos;

public static class DatosConfiguracion
{
    /// <summary>Registra el DbContext (SQLite) y los repositorios de la capa de Datos.</summary>
    public static IServiceCollection AgregarCapaDatos(this IServiceCollection services, string cadenaConexion)
    {
        services.AddDbContext<RutaSurDbContext>(o => o.UseSqlite(cadenaConexion));
        services.AddScoped<ICatalogoRepositorio, CatalogoRepositorio>();
        services.AddScoped<ISalidaRepositorio, SalidaRepositorio>();
        services.AddScoped<IReservaRepositorio, ReservaRepositorio>();
        services.AddScoped<IUnidadDeTrabajo, UnidadDeTrabajo>();
        return services;
    }

    /// <summary>Crea la base de datos si no existe y carga datos de ejemplo.</summary>
    public static async Task InicializarBaseDatosAsync(IServiceProvider proveedor)
    {
        using var scope = proveedor.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RutaSurDbContext>();
        await db.Database.EnsureCreatedAsync();
        // WAL mejora la concurrencia lectura/escritura en SQLite.
        await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;");
        if (await db.Destinos.AnyAsync()) return;
        db.Destinos.AddRange(DatosSemilla.Crear(DateTime.Today));
        await db.SaveChangesAsync();
    }
}

internal static class DatosSemilla
{
    public static List<Destino> Crear(DateTime hoy)
    {
        List<Salida> Salidas(int cupo, params int[] dias) =>
            dias.Select(d => new Salida { Fecha = hoy.AddDays(d).AddHours(7), CupoTotal = cupo, CuposDisponibles = cupo }).ToList();

        return new List<Destino>
        {
            new()
            {
                Nombre = "Parque Arqueológico de San Agustín", Municipio = "San Agustín",
                Descripcion = "Patrimonio de la Humanidad (UNESCO): estatuaria megalítica, Mesitas A, B y C y la Fuente de Lavapatas.",
                Experiencias =
                {
                    new() { Nombre = "Ruta arqueológica guiada", DuracionHoras = 6, PrecioPorPersona = 95000m,
                        Descripcion = "Recorrido por las Mesitas, el Bosque de las Estatuas y Lavapatas con guía local.",
                        Salidas = Salidas(20, 1, 2, 3, 5, 7, 9, 12) },
                    new() { Nombre = "Estrecho del Magdalena y La Chaquira", DuracionHoras = 8, PrecioPorPersona = 140000m,
                        Descripcion = "Recorrido en jeep por el estrecho del río Magdalena y el mirador de La Chaquira.",
                        Salidas = Salidas(12, 2, 4, 6, 10) }
                }
            },
            new()
            {
                Nombre = "Alto de los Ídolos y Alto de las Piedras", Municipio = "Isnos",
                Descripcion = "Tumbas monumentales y estatuas policromadas; cerca del Salto de Bordones.",
                Experiencias =
                {
                    new() { Nombre = "Isnos arqueológico y Salto de Bordones", DuracionHoras = 7, PrecioPorPersona = 120000m,
                        Descripcion = "Alto de los Ídolos, Alto de las Piedras y mirador del Salto de Bordones.",
                        Salidas = Salidas(15, 1, 3, 6, 8, 11) }
                }
            },
            new()
            {
                Nombre = "Desierto de la Tatacoa", Municipio = "Villavieja",
                Descripcion = "Bosque seco tropical con zonas ocre (Cuzco) y gris (Los Hoyos); cielo ideal para la astronomía.",
                Experiencias =
                {
                    new() { Nombre = "Atardecer y noche de estrellas", DuracionHoras = 5, PrecioPorPersona = 110000m,
                        Descripcion = "Laberintos del Cuzco al atardecer y observación astronómica en el observatorio.",
                        Salidas = Salidas(10, 1, 2, 4, 5, 8, 13) },
                    new() { Nombre = "Caminata Los Hoyos y piscina natural", DuracionHoras = 4, PrecioPorPersona = 70000m,
                        Descripcion = "Sendero por el desierto gris con baño en la piscina de Los Hoyos.",
                        Salidas = Salidas(25, 3, 7, 14) }
                }
            }
        };
    }
}
