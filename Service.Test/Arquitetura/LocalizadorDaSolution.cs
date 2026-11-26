namespace Service.Test.Arquitetura;

public sealed class LocalizadorDaSolution
{
    public string LocalizarRaiz()
    {
        var pasta = new DirectoryInfo(AppContext.BaseDirectory);
        while (pasta is not null)
        {
            if (pasta.EnumerateFiles("*.sln").Any()
                || pasta.EnumerateFiles("*.slnx").Any())
            {
                return pasta.FullName;
            }
            pasta = pasta.Parent;
        }
        throw new InvalidOperationException($"Raiz da solution não encontrada a partir de {AppContext.BaseDirectory}");
    }

    public IReadOnlyList<string> ListarCsprojs(string raiz) =>
        Directory.EnumerateFiles(raiz, "*.csproj", SearchOption.AllDirectories)
            .Where(caminho => !caminho
                .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Any(diretorio => diretorio is "bin" or "obj"))
            .OrderBy(caminho => caminho)
            .ToArray();
}
