namespace RutaSur.Negocio;

// DTOs: lo único que la capa de Presentación conoce. Ninguna entidad de EF cruza hacia arriba.

public record ExperienciaResumen(int Id, string Nombre, decimal PrecioPorPersona, int DuracionHoras);

public record DestinoResumen(int Id, string Nombre, string Municipio, string Descripcion,
    IReadOnlyList<ExperienciaResumen> Experiencias);

public record SalidaDto(int Id, DateTime Fecha, int CupoTotal, int CuposDisponibles)
{
    public bool Agotada => CuposDisponibles == 0;
    public int PorcentajeOcupacion => CupoTotal == 0 ? 0 : (int)Math.Round(100.0 * (CupoTotal - CuposDisponibles) / CupoTotal);
}

public record ExperienciaDetalle(int Id, string Nombre, string Descripcion, decimal PrecioPorPersona,
    int DuracionHoras, string Destino, string Municipio, IReadOnlyList<SalidaDto> Salidas);

public record SalidaParaReservar(int SalidaId, string Experiencia, string Destino, DateTime Fecha,
    decimal PrecioPorPersona, int CuposDisponibles);

public record SolicitudReserva(int SalidaId, string NombreTitular, string Documento, string Correo, int Personas);

public record ReservaDto(string Codigo, string Estado, string NombreTitular, string Documento, string Correo,
    int Personas, decimal Total, string Experiencia, string Destino, DateTime FechaSalida, DateTime FechaCreacionUtc);

public record SalidaOperacionDto(int SalidaId, string Experiencia, string Destino, DateTime Fecha,
    int CupoTotal, int CuposDisponibles)
{
    public int Vendidos => CupoTotal - CuposDisponibles;
}

public record ReservaOperacionDto(string Codigo, string NombreTitular, string Documento, int Personas,
    decimal Total, string Estado, DateTime FechaCreacionUtc);

public record NuevaSalida(int ExperienciaId, DateTime Fecha, int CupoTotal);
