using Microsoft.AspNetCore.Mvc;
using RutaSur.Negocio;
using RutaSur.Web.Models;

namespace RutaSur.Web.Controllers;

/// <summary>RF3 Reservar · RF4 Consultar · RF5 Cancelar.</summary>
public class ReservasController : Controller
{
    private readonly ICatalogoServicio _catalogo;
    private readonly IReservaServicio _reservas;

    public ReservasController(ICatalogoServicio catalogo, IReservaServicio reservas)
    {
        _catalogo = catalogo;
        _reservas = reservas;
    }

    [HttpGet]
    public async Task<IActionResult> Crear(int salidaId)
    {
        var salida = await _catalogo.ObtenerSalidaParaReservarAsync(salidaId);
        return View(new ReservarViewModel { Salida = salida, Formulario = new ReservaFormulario { SalidaId = salidaId } });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Crear([Bind(Prefix = "Formulario")] ReservaFormulario f)
    {
        var salida = await _catalogo.ObtenerSalidaParaReservarAsync(f.SalidaId);
        if (!ModelState.IsValid)
            return View(new ReservarViewModel { Salida = salida, Formulario = f });

        try
        {
            var reserva = await _reservas.CrearReservaAsync(
                new SolicitudReserva(f.SalidaId, f.NombreTitular, f.Documento, f.Correo, f.Personas));
            TempData["Documento"] = reserva.Documento;
            return RedirectToAction(nameof(Confirmacion), new { codigo = reserva.Codigo });
        }
        catch (ReglaNegocioException ex) when (ex is not NoEncontradoException)
        {
            // Cupos agotados u otra regla: se vuelve al formulario con datos frescos de la salida.
            var actualizada = await _catalogo.ObtenerSalidaParaReservarAsync(f.SalidaId);
            return View(new ReservarViewModel { Salida = actualizada, Formulario = f, Error = ex.Message });
        }
    }

    [HttpGet]
    public async Task<IActionResult> Confirmacion(string codigo)
    {
        if (TempData["Documento"] is not string documento)
            return RedirectToAction(nameof(Consultar));
        return View("Detalle", await _reservas.ConsultarAsync(codigo, documento));
    }

    [HttpGet]
    public IActionResult Consultar() => View(new ConsultaFormulario());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Consultar(ConsultaFormulario f)
    {
        if (!ModelState.IsValid) return View(f);
        try
        {
            return View("Detalle", await _reservas.ConsultarAsync(f.Codigo, f.Documento));
        }
        catch (NoEncontradoException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(f);
        }
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancelar(string codigo, string documento)
    {
        var reserva = await _reservas.CancelarAsync(codigo, documento);
        ViewData["Mensaje"] = "La reserva fue cancelada y los cupos quedaron liberados.";
        return View("Detalle", reserva);
    }
}
