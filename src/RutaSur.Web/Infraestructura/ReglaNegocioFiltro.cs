using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using RutaSur.Negocio;

namespace RutaSur.Web.Infraestructura;

/// <summary>
/// Convierte las excepciones de la capa de Negocio en una página amigable (404 o 422),
/// para que los controladores no se llenen de try/catch.
/// </summary>
public sealed class ReglaNegocioFiltro : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        if (context.Exception is not ReglaNegocioException ex) return;

        context.Result = new ViewResult
        {
            ViewName = "~/Views/Shared/Aviso.cshtml",
            StatusCode = ex is NoEncontradoException ? 404 : 422,
            ViewData = new Microsoft.AspNetCore.Mvc.ViewFeatures.ViewDataDictionary(
                new Microsoft.AspNetCore.Mvc.ModelBinding.EmptyModelMetadataProvider(), context.ModelState)
            {
                Model = ex.Message
            }
        };
        context.ExceptionHandled = true;
    }
}
