using LumiaFoundation.Core.TimeService;

namespace Test.Suporte.Tempo;

public sealed class RelogioFixo : IDateTimeProvider
{
    private DateTime _agora;

    public RelogioFixo(DateTime agora)
    {
        if (agora.Kind != DateTimeKind.Utc)
            throw new ArgumentException("O instante do relógio deve estar em UTC.", nameof(agora));
        _agora = agora;
    }

    public DateTime GetDateTime() => _agora;

    public void Avancar(TimeSpan intervalo)
    {
        if (intervalo < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(intervalo), "O intervalo não pode ser negativo.");
        _agora = _agora.Add(intervalo);
    }
}
