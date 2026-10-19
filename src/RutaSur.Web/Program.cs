using System.Globalization;
using RutaSur.Negocio;
using RutaSur.Web.Infraestructura;

var builder = WebApplication.CreateBuilder(args);

// Presentación (MVC) + filtro que traduce excepciones de negocio a mensajes en pantalla.
builder.Services.AddControllersWithViews(o => o.Filters.Add<ReglaNegocioFiltro>());

// La Presentación solo conoce la capa de Negocio; esta registra a su vez la capa de Datos.
builder.Services.AgregarRutaSur(builder.Configuration.GetConnectionString("RutaSur")!);

var app = builder.Build();

var cultura = new CultureInfo("es-CO");
CultureInfo.DefaultThreadCurrentCulture = cultura;
CultureInfo.DefaultThreadCurrentUICulture = cultura;

await app.Services.InicializarRutaSurAsync();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}

app.UseStaticFiles();
app.UseRouting();

app.MapControllerRoute(name: "default", pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

public partial class Program { }
