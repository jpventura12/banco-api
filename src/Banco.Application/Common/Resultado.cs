namespace Banco.Application.Common;

public enum EstadoResultado
{
    Ok,

    NoEncontrado,

    Invalido
}

public sealed record Resultado<T>
{
    public required EstadoResultado Estado { get; init; }

    public T? Valor { get; init; }

    public string? Error { get; init; }

    public static Resultado<T> Ok(T valor) => new() { Estado = EstadoResultado.Ok, Valor = valor };

    public static Resultado<T> NoEncontrado(string error) => new() { Estado = EstadoResultado.NoEncontrado, Error = error };

    public static Resultado<T> Invalido(string error) => new() { Estado = EstadoResultado.Invalido, Error = error };
}
