using Microsoft.Extensions.DependencyInjection;
using RutaSur.Datos;

namespace RutaSur.Negocio;

public static class NegocioConfiguracion
{
    /// <summary>
    /// Punto único de registro. La Presentación llama solo a este método, así nunca
    /// referencia a la capa de Datos (las dependencias fluyen en un solo sentido).
    /// </summary>
    public static IServiceCollection AgregarRutaSur(this IServiceCollection services, string cadenaConexion)
    {
        services.AgregarCapaDatos(cadenaConexion);
        services.AddSingleton<IReloj, RelojSistema>();
        services.AddScoped<ICatalogoServicio, CatalogoServicio>();
        services.AddScoped<IReservaServicio, ReservaServicio>();
        services.AddScoped<IOperacionServicio, OperacionServicio>();
        return services;
    }

    public static Task InicializarRutaSurAsync(this IServiceProvider proveedor)
        => DatosConfiguracion.InicializarBaseDatosAsync(proveedor);
}
