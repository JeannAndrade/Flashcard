namespace Service.Test.Arquitetura;

public class ArquiteturaDependenciasTests
{
    [Fact]
    public void Solution_WhenLocalizada_ContemOsProjetosEsperados()
    {
        var localizador = new LocalizadorDaSolution();
        var raiz = localizador.LocalizarRaiz();
        var nomes = localizador.ListarCsprojs(raiz)
            .Select(Path.GetFileNameWithoutExtension)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var esperados = new[]
        {
            "Domain", "Application", "Persistence", "Service", "Presentation",
            "Domain.Test", "Application.Test", "Persistence.Test", "Service.Test",
            "Presentation.Test", "Test.Suporte",
        };

        var faltantes = esperados.Where(nome => !nomes.Contains(nome)).ToArray();

        Assert.True(
            faltantes.Length == 0,
            $"Projetos esperados ausentes: {string.Join(", ", faltantes)}.");
    }

    [Fact]
    public void Solution_WhenAnalisada_NaoTemViolacoes()
    {
        var localizador = new LocalizadorDaSolution();
        var raiz = localizador.LocalizarRaiz();
        var leitor = new LeitorDeCsproj();
        var projetos = localizador.ListarCsprojs(raiz)
            .Select(caminho => leitor.Ler(caminho))
            .ToArray();
        var violacoes = new RegrasDeDependencia().Validar(projetos);

        Assert.True(
            violacoes.Count == 0,
            "Violações de arquitetura encontradas:\n" + string.Join("\n", violacoes));
    }
}
