using System.Xml.Linq;

namespace Service.Test.Arquitetura;

public class LeitorDeCsprojTests
{
    private static ProjetoCsproj Ler(string corpo) =>
        new LeitorDeCsproj().Ler("Teste", XDocument.Parse(corpo));

    [Fact]
    public void Ler_WhenProjectReferenceComBarraInvertida_ExtraiONome() =>
        Assert.Contains(
            "Domain",
            Ler("<Project><ItemGroup><ProjectReference Include=\"..\\Domain\\Domain.csproj\" /></ItemGroup></Project>")
                .ReferenciasDeProjeto);

    [Fact]
    public void Ler_WhenPackageReferenceComCondition_IncluiOPacote() =>
        Assert.Contains(
            "xunit",
            Ler("<Project><PackageReference Include=\"xunit\" Condition=\"false\" /></Project>")
                .Pacotes);

    [Fact]
    public void Ler_WhenFrameworkReference_IncluiOFrameworkReference() =>
        Assert.Contains(
            "Microsoft.AspNetCore.App",
            Ler("<Project><FrameworkReference Include=\"Microsoft.AspNetCore.App\" /></Project>")
                .ReferenciasDeFramework);

    [Fact]
    public void Ler_WhenSdkWeb_RetornaOSdk() =>
        Assert.Equal(
            "Microsoft.NET.Sdk.Web",
            Ler("<Project Sdk=\"Microsoft.NET.Sdk.Web\" />").Sdk);

    [Fact]
    public void Ler_WhenPackageVersion_IgnoraOItem() =>
        Assert.Empty(
            Ler("<Project><PackageVersion Include=\"xunit\" Version=\"1\" /></Project>")
                .Pacotes);
}
