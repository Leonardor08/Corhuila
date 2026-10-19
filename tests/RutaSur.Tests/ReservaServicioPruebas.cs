using Microsoft.Extensions.DependencyInjection;
using RutaSur.Negocio;

namespace RutaSur.Tests;

/// <summary>
/// Pruebas de la capa de Negocio contra una base SQLite real (archivo temporal),
/// atravesando las tres capas salvo la presentación.
/// </summary>
public sealed class ReservaServicioPruebas : IAsyncLifetime
{
    private readonly string _archivo = Path.Combine(Path.GetTempPath(), $"rutasur-{Guid.NewGuid():N}.db");
    private ServiceProvider _proveedor = null!;

    public async Task InitializeAsync()
    {
        var servicios = new ServiceCollection();
        servicios.AgregarRutaSur($"Data Source={_archivo};Default Timeout=30;Pooling=False");
        _proveedor = servicios.BuildServiceProvider();
        await _proveedor.InicializarRutaSurAsync();
    }

    public async Task DisposeAsync()
    {
        await _proveedor.DisposeAsync();
        foreach (var f in new[] { _archivo, _archivo + "-wal", _archivo + "-shm" })
            if (File.Exists(f)) File.Delete(f);
    }

    private T Servicio<T>(IServiceScope scope) where T : notnull => scope.ServiceProvider.GetRequiredService<T>();

    private async Task<int> ProgramarSalidaAsync(int cupo)
    {
        using var scope = _proveedor.CreateScope();
        var destinos = await Servicio<ICatalogoServicio>(scope).ListarDestinosAsync();
        var experienciaId = destinos.First().Experiencias.First().Id;
        return await Servicio<IOperacionServicio>(scope)
            .ProgramarSalidaAsync(new NuevaSalida(experienciaId, DateTime.Now.AddDays(5), cupo));
    }

    private async Task<int> CuposDisponiblesAsync(int salidaId)
    {
        using var scope = _proveedor.CreateScope();
        return (await Servicio<ICatalogoServicio>(scope).ObtenerSalidaParaReservarAsync(salidaId)).CuposDisponibles;
    }

    private static SolicitudReserva Solicitud(int salidaId, int personas, string doc = "1075123456") =>
        new(salidaId, "Ana Pérez", doc, "ana@correo.com", personas);

    [Fact]
    public async Task Reservar_descuenta_cupos_y_calcula_total()
    {
        var salidaId = await ProgramarSalidaAsync(cupo: 10);
        using var scope = _proveedor.CreateScope();

        var reserva = await Servicio<IReservaServicio>(scope).CrearReservaAsync(Solicitud(salidaId, 3));

        Assert.Equal("Confirmada", reserva.Estado);
        Assert.StartsWith("RS-", reserva.Codigo);
        Assert.Equal(7, await CuposDisponiblesAsync(salidaId));
        var precio = (await Servicio<ICatalogoServicio>(scope).ObtenerSalidaParaReservarAsync(salidaId)).PrecioPorPersona;
        Assert.Equal(precio * 3, reserva.Total);
    }

    [Fact]
    public async Task Reservar_mas_personas_que_cupos_es_rechazado_y_no_altera_cupos()
    {
        var salidaId = await ProgramarSalidaAsync(cupo: 2);
        using var scope = _proveedor.CreateScope();

        await Assert.ThrowsAsync<CuposInsuficientesException>(() =>
            Servicio<IReservaServicio>(scope).CrearReservaAsync(Solicitud(salidaId, 3)));

        Assert.Equal(2, await CuposDisponiblesAsync(salidaId));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(11)]
    public async Task Personas_fuera_de_rango_es_rechazado(int personas)
    {
        var salidaId = await ProgramarSalidaAsync(cupo: 20);
        using var scope = _proveedor.CreateScope();
        await Assert.ThrowsAsync<ReglaNegocioException>(() =>
            Servicio<IReservaServicio>(scope).CrearReservaAsync(Solicitud(salidaId, personas)));
    }

    [Fact]
    public async Task Cancelar_libera_cupos_y_no_permite_cancelar_dos_veces()
    {
        var salidaId = await ProgramarSalidaAsync(cupo: 5);
        string codigo;
        using (var scope = _proveedor.CreateScope())
            codigo = (await Servicio<IReservaServicio>(scope).CrearReservaAsync(Solicitud(salidaId, 4))).Codigo;
        Assert.Equal(1, await CuposDisponiblesAsync(salidaId));

        using (var scope = _proveedor.CreateScope())
        {
            var cancelada = await Servicio<IReservaServicio>(scope).CancelarAsync(codigo, "1075123456");
            Assert.Equal("Cancelada", cancelada.Estado);
        }
        Assert.Equal(5, await CuposDisponiblesAsync(salidaId));

        using (var scope = _proveedor.CreateScope())
            await Assert.ThrowsAsync<ReglaNegocioException>(() =>
                Servicio<IReservaServicio>(scope).CancelarAsync(codigo, "1075123456"));
    }

    [Fact]
    public async Task Consultar_con_documento_equivocado_no_revela_la_reserva()
    {
        var salidaId = await ProgramarSalidaAsync(cupo: 5);
        using var scope = _proveedor.CreateScope();
        var r = await Servicio<IReservaServicio>(scope).CrearReservaAsync(Solicitud(salidaId, 1));

        await Assert.ThrowsAsync<NoEncontradoException>(() =>
            Servicio<IReservaServicio>(scope).ConsultarAsync(r.Codigo, "999999"));
    }

    /// <summary>
    /// Escenario de calidad "cero sobreventa": 50 turistas intentan reservar a la vez
    /// una salida con 10 cupos. Deben confirmarse exactamente 10 y quedar 0 cupos.
    /// </summary>
    [Fact]
    public async Task Cero_sobreventa_con_50_solicitudes_concurrentes()
    {
        const int cupo = 10, solicitudes = 50;
        var salidaId = await ProgramarSalidaAsync(cupo);

        var tareas = Enumerable.Range(0, solicitudes).Select(i => Task.Run(async () =>
        {
            using var scope = _proveedor.CreateScope(); // un DbContext por "usuario", como en la web
            try
            {
                await Servicio<IReservaServicio>(scope).CrearReservaAsync(Solicitud(salidaId, 1, $"10{i:D6}"));
                return true;
            }
            catch (CuposInsuficientesException) { return false; }
        }));
        var resultados = await Task.WhenAll(tareas);

        Assert.Equal(cupo, resultados.Count(ok => ok));
        Assert.Equal(0, await CuposDisponiblesAsync(salidaId));
        using var s = _proveedor.CreateScope();
        var confirmadas = (await Servicio<IOperacionServicio>(s).ListarReservasDeSalidaAsync(salidaId))
            .Sum(r => r.Personas);
        Assert.Equal(cupo, confirmadas);
    }
}
