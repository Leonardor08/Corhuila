using Microsoft.AspNetCore.Mvc;
using RutaSur.Negocio;

namespace RutaSur.Web.Controllers;

/// <summary>RF2 — Detalle de una experiencia con sus salidas y cupos disponibles.</summary>
public class ExperienciasController : Controller
{
    private readonly ICatalogoServicio _catalogo;
    public ExperienciasController(ICatalogoServicio catalogo) => _catalogo = catalogo;

    public async Task<IActionResult> Detalle(int id) => View(await _catalogo.ObtenerExperienciaAsync(id));
}
