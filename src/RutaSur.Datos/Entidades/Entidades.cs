namespace RutaSur.Datos.Entidades;

/// <summary>Destino turístico del sur del Huila (San Agustín, Isnos, Tatacoa...).</summary>
public class Destino
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Municipio { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public List<Experiencia> Experiencias { get; set; } = new();
}

/// <summary>Experiencia o tour que se vende en un destino (p. ej. "Ruta arqueológica").</summary>
public class Experiencia
{
    public int Id { get; set; }
    public int DestinoId { get; set; }
    public Destino? Destino { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public decimal PrecioPorPersona { get; set; }
    public int DuracionHoras { get; set; }
    public List<Salida> Salidas { get; set; } = new();
}

/// <summary>Salida programada de una experiencia, con cupo limitado.</summary>
public class Salida
{
    public int Id { get; set; }
    public int ExperienciaId { get; set; }
    public Experiencia? Experiencia { get; set; }
    public DateTime Fecha { get; set; }
    public int CupoTotal { get; set; }
    public int CuposDisponibles { get; set; }
    public List<Reserva> Reservas { get; set; } = new();
}

public enum EstadoReserva
{
    Confirmada = 1,
    Cancelada = 2
}

/// <summary>Reserva de un turista sobre una salida.</summary>
public class Reserva
{
    public int Id { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public int SalidaId { get; set; }
    public Salida? Salida { get; set; }
    public string NombreTitular { get; set; } = string.Empty;
    public string Documento { get; set; } = string.Empty;
    public string Correo { get; set; } = string.Empty;
    public int Personas { get; set; }
    public decimal Total { get; set; }
    public EstadoReserva Estado { get; set; }
    public DateTime FechaCreacionUtc { get; set; }
}
