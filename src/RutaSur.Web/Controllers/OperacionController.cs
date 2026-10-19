using Microsoft.AspNetCore.Mvc;
using RutaSur.Negocio;
using RutaSur.Web.Models;

namespace RutaSur.Web.Controllers;

/// <summary>RF6 Ocupación por salida · RF7 Reservas de una salida · RF8 Programar salidas (operador).</summary>
public class OperacionController : Controller
{
    private readonly IOperacionServicio _operacion;
    private readonly ICatalogoServicio _catalogo;

    public OperacionController(IOperacionServicio operacion, ICatalogoServicio catalogo)
    {
        _operacion = operacion;
        _catalogo = catalogo;
    }

    public async Task<IActionResult> Index() => View(await ConstruirAsync(new NuevaSalidaFormulario(), TempData["Mensaje"] as string));

    public async Task<IActionResult> Reservas(int id) => View(new ReservasSalidaViewModel
    {
        Salida = await _catalogo.ObtenerSalidaParaReservarAsync(id),
        Reservas = await _operacion.ListarReservasDeSalidaAsync(id)
    });

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ProgramarSalida([Bind(Prefix = "NuevaSalida")] NuevaSalidaFormulario f)
    {
        if (!ModelState.IsValid) return View("Index", await ConstruirAsync(f, null));
        try
        {
            await _operacion.ProgramarSalidaAsync(new NuevaSalida(f.ExperienciaId, f.Fecha, f.CupoTotal));
            TempData["Mensaje"] = "Salida programada correctamente.";
            return RedirectToAction(nameof(Index));
        }
        catch (ReglaNegocioException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View("Index", await ConstruirAsync(f, null));
        }
    }

    private async Task<OperacionViewModel> ConstruirAsync(NuevaSalidaFormulario f, string? mensaje) => new()
    {
        Salidas = await _operacion.ListarSalidasAsync(),
        Destinos = await _catalogo.ListarDestinosAsync(),
        NuevaSalida = f,
        Mensaje = mensaje
    };
}
