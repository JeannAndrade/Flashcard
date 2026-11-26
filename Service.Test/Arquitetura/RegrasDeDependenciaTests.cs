namespace Service.Test.Arquitetura;

public class RegrasDeDependenciaTests
{
    private static ProjetoCsproj P(
        string nome,
        IEnumerable<string>? refs = null,
        IEnumerable<string>? pacotes = null,
        string sdk = "Microsoft.NET.Sdk",
        IEnumerable<string>? frameworks = null) =>
        new(
            nome,
            sdk,
            (refs ?? []).ToArray(),
            (pacotes ?? []).ToArray(),
            (frameworks ?? []).ToArray());

    private static List<ProjetoCsproj> Valida() =>
    [
        P("Domain", pacotes: ["Lumia.Foundation.Core"]),
        P("Application", ["Domain"], ["Lumia.Foundation.Core"]),
        P("Persistence", ["Application", "Domain"]),
        P("Service", ["Application", "Persistence"], sdk: "Microsoft.NET.Sdk.Web"),
        P("Presentation", pacotes: ["Markdig"], sdk: "Microsoft.NET.Sdk.Web"),
        P("Domain.Test", ["Domain", "Test.Suporte"], ["xunit"]),
        P("Application.Test", ["Application", "Domain", "Test.Suporte"], ["xunit"]),
        P("Persistence.Test", ["Persistence", "Application", "Domain", "Test.Suporte"], ["xunit"]),
        P("Service.Test", ["Service", "Persistence", "Application", "Domain", "Test.Suporte"], ["xunit"]),
        P("Presentation.Test", ["Presentation"], ["xunit"]),
        P("Test.Suporte"),
    ];

    private static void Tem(string regra, List<ProjetoCsproj> ps) =>
        Assert.Contains(
            new RegrasDeDependencia().Validar(ps),
            x => x.StartsWith("[" + regra + "]"));

    [Fact]
    public void Validar_WhenSolutionValida_RetornaListaVazia() =>
        Assert.Empty(new RegrasDeDependencia().Validar(Valida()));

    [Fact]
    public void Validar_WhenDomainReferenciaApplication_RetornaViolacaoR2()
    {
        var p = Valida();
        p[0] = P("Domain", ["Application"], ["Lumia.Foundation.Core"]);
        Tem("R2", p);
    }

    [Fact]
    public void Validar_WhenApplicationReferenciaPersistence_RetornaViolacaoR2()
    {
        var p = Valida();
        p[1] = P("Application", ["Domain", "Persistence"], ["Lumia.Foundation.Core"]);
        Tem("R2", p);
    }

    [Fact]
    public void Validar_WhenPresentationReferenciaProjetoInterno_RetornaViolacaoR2()
    {
        var p = Valida();
        p[4] = P("Presentation", ["Application"], sdk: "Microsoft.NET.Sdk.Web");
        Tem("R2", p);
    }

    [Fact]
    public void Validar_WhenProducaoReferenciaProjetoDeTeste_RetornaViolacaoR3()
    {
        var p = Valida();
        p[3] = P("Service", ["Application", "Persistence", "Domain.Test"], sdk: "Microsoft.NET.Sdk.Web");
        Tem("R3", p);
    }

    [Fact]
    public void Validar_WhenDomainUsaPacoteDeTerceiro_RetornaViolacaoR4()
    {
        var p = Valida();
        p[0] = P("Domain", pacotes: ["Newtonsoft.Json"]);
        Tem("R4", p);
    }

    [Fact]
    public void Validar_WhenApplicationUsaEntityFramework_RetornaViolacaoR5()
    {
        var p = Valida();
        p[1] = P("Application", ["Domain"], ["Microsoft.EntityFrameworkCore"]);
        Tem("R5", p);
    }

    [Fact]
    public void Validar_WhenApplicationUsaEFRepository_RetornaViolacaoR5()
    {
        var p = Valida();
        p[1] = P("Application", ["Domain"], ["Lumia.Foundation.EFRepository"]);
        Tem("R5", p);
    }

    [Fact]
    public void Validar_WhenPersistenceUsaSdkWeb_RetornaViolacaoR6()
    {
        var p = Valida();
        p[2] = P("Persistence", ["Application", "Domain"], sdk: "Microsoft.NET.Sdk.Web");
        Tem("R6", p);
    }

    [Fact]
    public void Validar_WhenApplicationReferenciaAspNetCoreApp_RetornaViolacaoR6()
    {
        var p = Valida();
        p[1] = P(
            "Application",
            ["Domain"],
            ["Lumia.Foundation.Core"],
            frameworks: ["Microsoft.AspNetCore.App"]);
        Tem("R6", p);
    }

    [Fact]
    public void Validar_WhenPresentationUsaEFRepository_RetornaViolacaoR7()
    {
        var p = Valida();
        p[4] = P("Presentation", pacotes: ["Lumia.Foundation.EFRepository"], sdk: "Microsoft.NET.Sdk.Web");
        Tem("R7", p);
    }

    [Fact]
    public void Validar_WhenPresentationUsaAuth_RetornaViolacaoR7()
    {
        var p = Valida();
        p[4] = P("Presentation", pacotes: ["Lumia.Foundation.Auth"], sdk: "Microsoft.NET.Sdk.Web");
        Tem("R7", p);
    }

    [Fact]
    public void Validar_WhenPresentationUsaPacoteDeTerceiroNaoAprovado_RetornaViolacaoR7()
    {
        var p = Valida();
        p[4] = P("Presentation", pacotes: ["Moq"], sdk: "Microsoft.NET.Sdk.Web");
        Tem("R7", p);
    }

    [Fact]
    public void Validar_WhenPresentationUsaMarkdig_NaoRetornaViolacao() =>
        Assert.DoesNotContain(
            new RegrasDeDependencia().Validar(Valida()),
            x => x.StartsWith("[R7]"));

    [Fact]
    public void Validar_WhenPresentationTestReferenciaSuporte_RetornaViolacaoR8()
    {
        var p = Valida();
        p[9] = P("Presentation.Test", ["Presentation", "Test.Suporte"], ["xunit"]);
        Tem("R8", p);
    }

    [Fact]
    public void Validar_WhenServiceTestReferenciaPresentation_RetornaViolacaoR9()
    {
        var p = Valida();
        p[8] = P("Service.Test", ["Service", "Presentation"], ["xunit"]);
        Tem("R9", p);
    }

    [Fact]
    public void Validar_WhenProjetoDeTesteUsaMoq_RetornaViolacaoR10()
    {
        var p = Valida();
        p[5] = P("Domain.Test", ["Domain", "Test.Suporte"], ["Moq"]);
        Tem("R10", p);
    }

    [Fact]
    public void Validar_WhenProjetoDeProducaoUsaXunit_RetornaViolacaoR11()
    {
        var p = Valida();
        p[0] = P("Domain", pacotes: ["Lumia.Foundation.Core", "xunit"]);
        Tem("R11", p);
    }

    [Fact]
    public void Validar_WhenSuporteReferenciaPresentation_RetornaViolacaoR12()
    {
        var p = Valida();
        p[10] = P("Test.Suporte", ["Presentation"]);
        Tem("R12", p);
    }

    [Fact]
    public void Validar_WhenFaltaProjetoDeTeste_RetornaViolacaoR1()
    {
        var p = Valida();
        p.RemoveAt(5);
        Tem("R1", p);
    }

    [Fact]
    public void Validar_WhenHaVariasViolacoes_RetornaTodas()
    {
        var p = Valida();
        p[0] = P("Domain", ["Application"], ["Moq"]);
        var erros = new RegrasDeDependencia().Validar(p);
        Assert.Contains(erros, x => x.StartsWith("[R2]"));
        Assert.Contains(erros, x => x.StartsWith("[R4]"));
    }

    [Fact]
    public void Validar_WhenNomesComESemPrefixo_ComparaPeloNomeNormalizado()
    {
        var p = Valida();
        p[0] = P("Flashcard.Domain", pacotes: ["Lumia.Foundation.Core"]);
        Assert.DoesNotContain(
            new RegrasDeDependencia().Validar(p),
            x => x.StartsWith("[R1]"));
    }
}
