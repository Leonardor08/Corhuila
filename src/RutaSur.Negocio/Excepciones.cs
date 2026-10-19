namespace RutaSur.Negocio;

/// <summary>Violación de una regla de negocio. La presentación la muestra al usuario.</summary>
public class ReglaNegocioException : Exception
{
    public ReglaNegocioException(string mensaje) : base(mensaje) { }
}

/// <summary>No hay cupos suficientes en la salida (regla "cero sobreventa").</summary>
public sealed class CuposInsuficientesException : ReglaNegocioException
{
    public CuposInsuficientesException(int solicitados)
        : base($"No hay cupos suficientes para {solicitados} persona(s) en esta salida. Elija otra fecha o menos personas.") { }
}

public sealed class NoEncontradoException : ReglaNegocioException
{
    public NoEncontradoException(string mensaje) : base(mensaje) { }
}
