using System.Xml.Linq;

namespace Service.Test.Arquitetura;

public sealed class LeitorDeCsproj
{
    public ProjetoCsproj Ler(string caminhoDoCsproj)
    {
        var documento = XDocument.Load(caminhoDoCsproj);
        return Ler(Path.GetFileNameWithoutExtension(caminhoDoCsproj), documento);
    }

    public ProjetoCsproj Ler(string nome, XDocument documento)
    {
        var projeto = documento.Root
            ?? throw new InvalidOperationException("O documento não contém o elemento Project.");

        var referenciasDeProjeto = projeto.Descendants()
            .Where(e => e.Name.LocalName == "ProjectReference")
            .Select(e => (string?)e.Attribute("Include")?.Value)
            .Where(valor => valor is not null)
            .Select(valor => Path.GetFileNameWithoutExtension(valor!.Replace('\\', '/')))
            .ToArray();

        var pacotes = projeto.Descendants()
            .Where(e => e.Name.LocalName == "PackageReference")
            .Select(e => (string?)e.Attribute("Include")?.Value)
            .Where(valor => valor is not null)
            .Cast<string>()
            .ToArray();

        var frameworks = projeto.Descendants()
            .Where(e => e.Name.LocalName == "FrameworkReference")
            .Select(e => (string?)e.Attribute("Include")?.Value)
            .Where(valor => valor is not null)
            .Cast<string>()
            .ToArray();

        return new ProjetoCsproj(
            nome,
            (string?)projeto.Attribute("Sdk") ?? string.Empty,
            referenciasDeProjeto,
            pacotes,
            frameworks);
    }
}
