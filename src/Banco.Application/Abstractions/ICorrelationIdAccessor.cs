namespace Banco.Application.Abstractions;

public interface ICorrelationIdAccessor
{
    string? CorrelationId { get; set; }
}

public sealed class CorrelationIdAccessor : ICorrelationIdAccessor
{
    private static readonly AsyncLocal<string?> Actual = new();

    public string? CorrelationId
    {
        get => Actual.Value;
        set => Actual.Value = value;
    }
}
