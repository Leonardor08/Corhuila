using Microsoft.AspNetCore.Mvc;
using RutaSur.Negocio;

namespace RutaSur.Web.Controllers;

/// <summary>RF1 — Catálogo de destinos y experiencias.</summary>
public class HomeController : Controller
{
    private readonly ICatalogoServicio _catalogo;
    public HomeController(ICatalogoServicio catalogo) => _catalogo = catalogo;

    public async Task<IActionResult> Index() => View(await _catalogo.ListarDestinosAsync());

    public IActionResult Error() => View("~/Views/Shared/Aviso.cshtml", "Ocurrió un error inesperado. Intente de nuevo.");
}
