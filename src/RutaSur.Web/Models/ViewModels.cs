using System.ComponentModel.DataAnnotations;
using RutaSur.Negocio;

namespace RutaSur.Web.Models;

public class ReservaFormulario
{
    public int SalidaId { get; set; }

    [Required(ErrorMessage = "Ingrese el nombre del titular.")]
    [StringLength(120)]
    [Display(Name = "Nombre del titular")]
    public string NombreTitular { get; set; } = string.Empty;

    [Required(ErrorMessage = "Ingrese el documento.")]
    [RegularExpression(@"^\d{5,15}$", ErrorMessage = "Solo dígitos (5 a 15).")]
    [Display(Name = "Documento de identidad")]
    public string Documento { get; set; } = string.Empty;

    [Required(ErrorMessage = "Ingrese el correo.")]
    [EmailAddress(ErrorMessage = "Correo no válido.")]
    [Display(Name = "Correo electrónico")]
    public string Correo { get; set; } = string.Empty;

    [Range(1, 10, ErrorMessage = "Entre 1 y 10 personas por reserva.")]
    [Display(Name = "Número de personas")]
    public int Personas { get; set; } = 1;

    [DebeSerVerdadero(ErrorMessage = "Debe aceptar el tratamiento de datos personales.")]
    [Display(Name = "Acepto el tratamiento de mis datos personales (Ley 1581 de 2012)")]
    public bool AceptaTratamientoDatos { get; set; }
}

/// <summary>Valida que una casilla esté marcada (p. ej. autorización de datos, Ley 1581 de 2012).</summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class DebeSerVerdaderoAttribute : ValidationAttribute
{
    public override bool IsValid(object? value) => value is true;
}

public class ReservarViewModel
{
    public required SalidaParaReservar Salida { get; init; }
    public required ReservaFormulario Formulario { get; init; }
    public string? Error { get; init; }
}

public class ConsultaFormulario
{
    [Required(ErrorMessage = "Ingrese el código.")]
    [Display(Name = "Código de reserva")]
    public string Codigo { get; set; } = string.Empty;

    [Required(ErrorMessage = "Ingrese el documento.")]
    [Display(Name = "Documento del titular")]
    public string Documento { get; set; } = string.Empty;
}

public class NuevaSalidaFormulario
{
    [Display(Name = "Experiencia")]
    public int ExperienciaId { get; set; }

    [Required]
    [DataType(DataType.DateTime)]
    [Display(Name = "Fecha y hora")]
    public DateTime Fecha { get; set; } = DateTime.Today.AddDays(7).AddHours(7);

    [Range(1, 60, ErrorMessage = "Entre 1 y 60 cupos.")]
    [Display(Name = "Cupo total")]
    public int CupoTotal { get; set; } = 15;
}

public class OperacionViewModel
{
    public required IReadOnlyList<SalidaOperacionDto> Salidas { get; init; }
    public required IReadOnlyList<DestinoResumen> Destinos { get; init; }
    public NuevaSalidaFormulario NuevaSalida { get; init; } = new();
    public string? Mensaje { get; init; }
}

public class ReservasSalidaViewModel
{
    public required SalidaParaReservar Salida { get; init; }
    public required IReadOnlyList<ReservaOperacionDto> Reservas { get; init; }
}
