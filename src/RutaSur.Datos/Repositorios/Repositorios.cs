using Microsoft.EntityFrameworkCore;
using RutaSur.Datos.Entidades;

namespace RutaSur.Datos.Repositorios;

// ---------------------------------------------------------------------------
// Contratos de la capa de Datos. La capa de Negocio depende de estas interfaces
// (y de las entidades), nunca de EF Core directamente.
// ---------------------------------------------------------------------------

public interface ICatalogoRepositorio
{
    Task<List<Destino>> ListarDestinosConExperienciasAsync();
    Task<Experiencia?> ObtenerExperienciaConSalidasAsync(int experienciaId, DateTime desde);
    Task<Salida?> ObtenerSalidaAsync(int salidaId);
    Task<List<Salida>> ListarSalidasAsync(DateTime desde);
    Task AgregarSalidaAsync(Salida salida);
}

public interface ISalidaRepositorio
{
    /// <summary>
    /// Descuenta cupos de forma ATÓMICA con un UPDATE condicional
    /// (WHERE CuposDisponibles &gt;= n). Devuelve false si no había cupo suficiente.
    /// Es la pieza que garantiza "cero sobreventa" aun con solicitudes concurrentes.
    /// </summary>
    Task<bool> IntentarDescontarCuposAsync(int salidaId, int personas);
    Task LiberarCuposAsync(int salidaId, int personas);
}

public interface IReservaRepositorio
{
    Task AgregarAsync(Reserva reserva);
    Task<Reserva?> ObtenerPorCodigoAsync(string codigo);
    Task<List<Reserva>> ListarPorSalidaAsync(int salidaId);
    Task GuardarCambiosAsync();
}

public interface IUnidadDeTrabajo
{
    Task<T> EjecutarEnTransaccionAsync<T>(Func<Task<T>> operacion);
}

// ---------------------------------------------------------------------------
// Implementaciones con EF Core
// ---------------------------------------------------------------------------

internal sealed class CatalogoRepositorio : ICatalogoRepositorio
{
    private readonly RutaSurDbContext _db;
    public CatalogoRepositorio(RutaSurDbContext db) => _db = db;

    public Task<List<Destino>> ListarDestinosConExperienciasAsync() =>
        _db.Destinos.AsNoTracking()
            .Include(d => d.Experiencias)
            .OrderBy(d => d.Nombre)
            .ToListAsync();

    public Task<Experiencia?> ObtenerExperienciaConSalidasAsync(int experienciaId, DateTime desde) =>
        _db.Experiencias.AsNoTracking()
            .Include(e => e.Destino)
            .Include(e => e.Salidas.Where(s => s.Fecha >= desde).OrderBy(s => s.Fecha))
            .FirstOrDefaultAsync(e => e.Id == experienciaId);

    public Task<Salida?> ObtenerSalidaAsync(int salidaId) =>
        _db.Salidas.AsNoTracking()
            .Include(s => s.Experiencia).ThenInclude(e => e!.Destino)
            .FirstOrDefaultAsync(s => s.Id == salidaId);

    public Task<List<Salida>> ListarSalidasAsync(DateTime desde) =>
        _db.Salidas.AsNoTracking()
            .Include(s => s.Experiencia).ThenInclude(e => e!.Destino)
            .Where(s => s.Fecha >= desde)
            .OrderBy(s => s.Fecha)
            .ToListAsync();

    public async Task AgregarSalidaAsync(Salida salida)
    {
        _db.Salidas.Add(salida);
        await _db.SaveChangesAsync();
    }
}

internal sealed class SalidaRepositorio : ISalidaRepositorio
{
    private readonly RutaSurDbContext _db;
    public SalidaRepositorio(RutaSurDbContext db) => _db = db;

    public async Task<bool> IntentarDescontarCuposAsync(int salidaId, int personas)
    {
        // UPDATE Salidas SET CuposDisponibles = CuposDisponibles - @p
        // WHERE Id = @id AND CuposDisponibles >= @p
        var filas = await _db.Salidas
            .Where(s => s.Id == salidaId && s.CuposDisponibles >= personas)
            .ExecuteUpdateAsync(set => set.SetProperty(
                s => s.CuposDisponibles, s => s.CuposDisponibles - personas));
        return filas == 1;
    }

    public Task LiberarCuposAsync(int salidaId, int personas) =>
        _db.Salidas
            .Where(s => s.Id == salidaId)
            .ExecuteUpdateAsync(set => set.SetProperty(
                s => s.CuposDisponibles, s => s.CuposDisponibles + personas));
}

internal sealed class ReservaRepositorio : IReservaRepositorio
{
    private readonly RutaSurDbContext _db;
    public ReservaRepositorio(RutaSurDbContext db) => _db = db;

    public async Task AgregarAsync(Reserva reserva)
    {
        _db.Reservas.Add(reserva);
        await _db.SaveChangesAsync();
    }

    public Task<Reserva?> ObtenerPorCodigoAsync(string codigo) =>
        _db.Reservas
            .Include(r => r.Salida).ThenInclude(s => s!.Experiencia).ThenInclude(e => e!.Destino)
            .FirstOrDefaultAsync(r => r.Codigo == codigo);

    public Task<List<Reserva>> ListarPorSalidaAsync(int salidaId) =>
        _db.Reservas.AsNoTracking()
            .Where(r => r.SalidaId == salidaId)
            .OrderBy(r => r.FechaCreacionUtc)
            .ToListAsync();

    public Task GuardarCambiosAsync() => _db.SaveChangesAsync();
}

internal sealed class UnidadDeTrabajo : IUnidadDeTrabajo
{
    private readonly RutaSurDbContext _db;
    public UnidadDeTrabajo(RutaSurDbContext db) => _db = db;

    public async Task<T> EjecutarEnTransaccionAsync<T>(Func<Task<T>> operacion)
    {
        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            var resultado = await operacion();
            await tx.CommitAsync();
            return resultado;
        }
        catch
        {
            await tx.RollbackAsync();
            _db.ChangeTracker.Clear();
            throw;
        }
    }
}
