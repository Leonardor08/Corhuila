using System.Net.Mail;
using System.Security.Cryptography;
using RutaSur.Datos.Entidades;
using RutaSur.Datos.Repositorios;

namespace RutaSur.Negocio;

/// <summary>Abstracción del reloj para que las reglas que dependen de la fecha sean comprobables.</summary>
public interface IReloj { DateTime Ahora { get; } }
internal sealed class RelojSistema : IReloj { public DateTime Ahora => DateTime.Now; }

// ---------------------------------------------------------------------------
// CATÁLOGO (RF1, RF2)
// ---------------------------------------------------------------------------
public interface ICatalogoServicio
{
    Task<IReadOnlyList<DestinoResumen>> ListarDestinosAsync();
    Task<ExperienciaDetalle> ObtenerExperienciaAsync(int experienciaId);
    Task<SalidaParaReservar> ObtenerSalidaParaReservarAsync(int salidaId);
}

internal sealed class CatalogoServicio : ICatalogoServicio
{
    private readonly ICatalogoRepositorio _catalogo;
    private readonly IReloj _reloj;

    public CatalogoServicio(ICatalogoRepositorio catalogo, IReloj reloj)
    {
        _catalogo = catalogo;
        _reloj = reloj;
    }

    public async Task<IReadOnlyList<DestinoResumen>> ListarDestinosAsync()
    {
        var destinos = await _catalogo.ListarDestinosConExperienciasAsync();
        return destinos.Select(d => new DestinoResumen(d.Id, d.Nombre, d.Municipio, d.Descripcion,
            d.Experiencias.OrderBy(e => e.Nombre)
                .Select(e => new ExperienciaResumen(e.Id, e.Nombre, e.PrecioPorPersona, e.DuracionHoras)).ToList()))
            .ToList();
    }

    public async Task<ExperienciaDetalle> ObtenerExperienciaAsync(int experienciaId)
    {
        var e = await _catalogo.ObtenerExperienciaConSalidasAsync(experienciaId, _reloj.Ahora)
                ?? throw new NoEncontradoException("La experiencia no existe.");
        return new ExperienciaDetalle(e.Id, e.Nombre, e.Descripcion, e.PrecioPorPersona, e.DuracionHoras,
            e.Destino!.Nombre, e.Destino.Municipio,
            e.Salidas.Select(s => new SalidaDto(s.Id, s.Fecha, s.CupoTotal, s.CuposDisponibles)).ToList());
    }

    public async Task<SalidaParaReservar> ObtenerSalidaParaReservarAsync(int salidaId)
    {
        var s = await _catalogo.ObtenerSalidaAsync(salidaId)
                ?? throw new NoEncontradoException("La salida no existe.");
        return new SalidaParaReservar(s.Id, s.Experiencia!.Nombre, s.Experiencia.Destino!.Nombre, s.Fecha,
            s.Experiencia.PrecioPorPersona, s.CuposDisponibles);
    }
}

// ---------------------------------------------------------------------------
// RESERVAS (RF3, RF4, RF5) — aquí vive la regla "cero sobreventa"
// ---------------------------------------------------------------------------
public interface IReservaServicio
{
    Task<ReservaDto> CrearReservaAsync(SolicitudReserva solicitud);
    Task<ReservaDto> ConsultarAsync(string codigo, string documento);
    Task<ReservaDto> CancelarAsync(string codigo, string documento);
}

internal sealed class ReservaServicio : IReservaServicio
{
    public const int MaxPersonasPorReserva = 10;
    public static readonly TimeSpan AnticipacionMinimaCancelacion = TimeSpan.FromHours(24);

    private readonly ICatalogoRepositorio _catalogo;
    private readonly ISalidaRepositorio _salidas;
    private readonly IReservaRepositorio _reservas;
    private readonly IUnidadDeTrabajo _uow;
    private readonly IReloj _reloj;

    public ReservaServicio(ICatalogoRepositorio catalogo, ISalidaRepositorio salidas,
        IReservaRepositorio reservas, IUnidadDeTrabajo uow, IReloj reloj)
    {
        _catalogo = catalogo;
        _salidas = salidas;
        _reservas = reservas;
        _uow = uow;
        _reloj = reloj;
    }

    public async Task<ReservaDto> CrearReservaAsync(SolicitudReserva s)
    {
        Validar(s);

        var salida = await _catalogo.ObtenerSalidaAsync(s.SalidaId)
                     ?? throw new NoEncontradoException("La salida no existe.");
        if (salida.Fecha <= _reloj.Ahora)
            throw new ReglaNegocioException("La salida ya ocurrió; no admite reservas.");

        var reserva = await _uow.EjecutarEnTransaccionAsync(async () =>
        {
            // 1) Descuento atómico: si otra solicitud tomó los últimos cupos, esto devuelve false.
            if (!await _salidas.IntentarDescontarCuposAsync(s.SalidaId, s.Personas))
                throw new CuposInsuficientesException(s.Personas);

            // 2) Registro de la reserva dentro de la MISMA transacción.
            var r = new Reserva
            {
                Codigo = GenerarCodigo(),
                SalidaId = s.SalidaId,
                NombreTitular = s.NombreTitular.Trim(),
                Documento = s.Documento.Trim(),
                Correo = s.Correo.Trim().ToLowerInvariant(),
                Personas = s.Personas,
                Total = salida.Experiencia!.PrecioPorPersona * s.Personas,
                Estado = EstadoReserva.Confirmada,
                FechaCreacionUtc = DateTime.UtcNow
            };
            await _reservas.AgregarAsync(r);
            return r;
        });

        return Mapear(reserva, salida);
    }

    public async Task<ReservaDto> ConsultarAsync(string codigo, string documento)
    {
        var r = await ObtenerPropiaAsync(codigo, documento);
        return Mapear(r, r.Salida!);
    }

    public async Task<ReservaDto> CancelarAsync(string codigo, string documento)
    {
        var r = await ObtenerPropiaAsync(codigo, documento);
        if (r.Estado == EstadoReserva.Cancelada)
            throw new ReglaNegocioException("La reserva ya estaba cancelada.");
        if (r.Salida!.Fecha - _reloj.Ahora < AnticipacionMinimaCancelacion)
            throw new ReglaNegocioException("Solo se puede cancelar con al menos 24 horas de anticipación.");

        await _uow.EjecutarEnTransaccionAsync(async () =>
        {
            r.Estado = EstadoReserva.Cancelada;
            await _reservas.GuardarCambiosAsync();
            await _salidas.LiberarCuposAsync(r.SalidaId, r.Personas);
            return true;
        });
        return Mapear(r, r.Salida!);
    }

    private async Task<Reserva> ObtenerPropiaAsync(string codigo, string documento)
    {
        var r = await _reservas.ObtenerPorCodigoAsync((codigo ?? "").Trim().ToUpperInvariant());
        // Mismo mensaje si no existe o si el documento no coincide: no se revela qué códigos existen.
        if (r is null || !string.Equals(r.Documento, (documento ?? "").Trim(), StringComparison.Ordinal))
            throw new NoEncontradoException("No encontramos una reserva con ese código y documento.");
        return r;
    }

    private static void Validar(SolicitudReserva s)
    {
        if (s.Personas < 1 || s.Personas > MaxPersonasPorReserva)
            throw new ReglaNegocioException($"El número de personas debe estar entre 1 y {MaxPersonasPorReserva}.");
        if (string.IsNullOrWhiteSpace(s.NombreTitular))
            throw new ReglaNegocioException("El nombre del titular es obligatorio.");
        if (string.IsNullOrWhiteSpace(s.Documento) || !s.Documento.Trim().All(char.IsDigit) || s.Documento.Trim().Length is < 5 or > 15)
            throw new ReglaNegocioException("El documento debe tener entre 5 y 15 dígitos.");
        if (!MailAddress.TryCreate(s.Correo?.Trim(), out _))
            throw new ReglaNegocioException("El correo electrónico no es válido.");
    }

    private static string GenerarCodigo()
    {
        const string alfabeto = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // sin 0/O/1/I para evitar confusiones
        return "RS-" + string.Concat(Enumerable.Range(0, 6).Select(_ => alfabeto[RandomNumberGenerator.GetInt32(alfabeto.Length)]));
    }

    private static ReservaDto Mapear(Reserva r, Salida s) => new(r.Codigo, r.Estado.ToString(), r.NombreTitular,
        r.Documento, r.Correo, r.Personas, r.Total, s.Experiencia!.Nombre, s.Experiencia.Destino!.Nombre,
        s.Fecha, r.FechaCreacionUtc);
}

// ---------------------------------------------------------------------------
// OPERACIÓN (RF6, RF7) — panel del operador turístico
// ---------------------------------------------------------------------------
public interface IOperacionServicio
{
    Task<IReadOnlyList<SalidaOperacionDto>> ListarSalidasAsync();
    Task<IReadOnlyList<ReservaOperacionDto>> ListarReservasDeSalidaAsync(int salidaId);
    Task<int> ProgramarSalidaAsync(NuevaSalida nueva);
}

internal sealed class OperacionServicio : IOperacionServicio
{
    private readonly ICatalogoRepositorio _catalogo;
    private readonly IReservaRepositorio _reservas;
    private readonly IReloj _reloj;

    public OperacionServicio(ICatalogoRepositorio catalogo, IReservaRepositorio reservas, IReloj reloj)
    {
        _catalogo = catalogo;
        _reservas = reservas;
        _reloj = reloj;
    }

    public async Task<IReadOnlyList<SalidaOperacionDto>> ListarSalidasAsync()
    {
        var salidas = await _catalogo.ListarSalidasAsync(_reloj.Ahora.Date);
        return salidas.Select(s => new SalidaOperacionDto(s.Id, s.Experiencia!.Nombre, s.Experiencia.Destino!.Nombre,
            s.Fecha, s.CupoTotal, s.CuposDisponibles)).ToList();
    }

    public async Task<IReadOnlyList<ReservaOperacionDto>> ListarReservasDeSalidaAsync(int salidaId)
    {
        var reservas = await _reservas.ListarPorSalidaAsync(salidaId);
        return reservas.Select(r => new ReservaOperacionDto(r.Codigo, r.NombreTitular, r.Documento, r.Personas,
            r.Total, r.Estado.ToString(), r.FechaCreacionUtc)).ToList();
    }

    public async Task<int> ProgramarSalidaAsync(NuevaSalida n)
    {
        if (n.CupoTotal is < 1 or > 60)
            throw new ReglaNegocioException("El cupo de la salida debe estar entre 1 y 60 personas.");
        if (n.Fecha <= _reloj.Ahora)
            throw new ReglaNegocioException("La fecha de la salida debe ser futura.");
        if (await _catalogo.ObtenerExperienciaConSalidasAsync(n.ExperienciaId, _reloj.Ahora) is null)
            throw new NoEncontradoException("La experiencia no existe.");

        var salida = new Salida { ExperienciaId = n.ExperienciaId, Fecha = n.Fecha, CupoTotal = n.CupoTotal, CuposDisponibles = n.CupoTotal };
        await _catalogo.AgregarSalidaAsync(salida);
        return salida.Id;
    }
}
