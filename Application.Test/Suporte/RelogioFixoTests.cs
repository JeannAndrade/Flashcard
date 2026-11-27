using Test.Suporte.Tempo;

namespace Application.Test.Suporte;

public sealed class RelogioFixoTests
{
    private static readonly DateTime Instante = new(2026, 11, 26, 12, 30, 0, DateTimeKind.Utc);

    [Fact]
    public void Agora_WhenCriado_RetornaOValorInformado() => Assert.Equal(Instante, new RelogioFixo(Instante).GetDateTime());

    [Fact]
    public void Construtor_WhenKindNaoUtc_LancaArgumentException() => Assert.Throws<ArgumentException>(() => new RelogioFixo(DateTime.SpecifyKind(Instante, DateTimeKind.Local)));

    [Fact]
    public void Avancar_WhenIntervaloPositivo_DesloCaOInstante()
    {
        var relogio = new RelogioFixo(Instante);
        relogio.Avancar(TimeSpan.FromHours(2));
        Assert.Equal(Instante.AddHours(2), relogio.GetDateTime());
    }

    [Fact]
    public void Avancar_WhenIntervaloNegativo_LancaArgumentOutOfRangeException() => Assert.Throws<ArgumentOutOfRangeException>(() => new RelogioFixo(Instante).Avancar(TimeSpan.FromSeconds(-1)));
}
