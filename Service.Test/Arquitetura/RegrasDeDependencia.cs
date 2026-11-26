namespace Service.Test.Arquitetura;

public sealed class RegrasDeDependencia
{
    public const string PrefixoDaSolution = "Flashcard.";
    public const string PrefixoFoundation = "Lumia.Foundation.";
    public const string Core = "Core";
    public const string EFRepository = "EFRepository";
    public const string Auth = "Auth";

    private static readonly string[] TerceirosDaPresentation =
        ["Markdig", "HtmlSanitizer", "NLog"];

    private static readonly string[] PacotesProibidosNosTestes =
        ["Moq", "FluentAssertions", "Respawn", "Playwright", "Microsoft.Playwright"];

    private static readonly string[] Producao =
        ["Domain", "Application", "Persistence", "Service", "Presentation"];

    public IReadOnlyList<string> Validar(IReadOnlyCollection<ProjetoCsproj> projetos)
    {
        var erros = new List<string>();

        var porNome = projetos.ToDictionary(
            p => Normalizar(p.Nome),
            StringComparer.OrdinalIgnoreCase);

        // R1: projetos obrigatórios
        foreach (var nome in Producao.Where(n => !porNome.ContainsKey(n)))
        {
            erros.Add($"[R1] Falta o projeto Flashcard.{nome}.");
        }

        foreach (var nome in Producao
                     .Select(n => n + ".Test")
                     .Append("Test.Suporte")
                     .Where(n => !porNome.ContainsKey(n)))
        {
            erros.Add($"[R1] Falta o projeto Flashcard.{nome}.");
        }

        // R2: referências permitidas entre projetos de produção
        var permitidas = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["Domain"] = [],
            ["Application"] = ["Domain"],
            ["Persistence"] = ["Application", "Domain"],
            ["Service"] = ["Application", "Persistence"],
            ["Presentation"] = [],
        };

        foreach (var (nome, permitidos) in permitidas)
        {
            if (porNome.TryGetValue(nome, out var p))
            {
                foreach (var r in p.ReferenciasDeProjeto
                             .Select(Normalizar)
                             .Where(r => !permitidos.Contains(r, StringComparer.OrdinalIgnoreCase)))
                {
                    erros.Add(
                        $"[R2] Flashcard.{nome} não pode referenciar o projeto 'Flashcard.{r}' " +
                        $"(permitido: {string.Join(", ", permitidos.Select(x => "Flashcard." + x))}).");
                }
            }
        }

        // R3: produção não pode referenciar testes/suporte
        foreach (var p in projetos.Where(p => Producao.Contains(Normalizar(p.Nome), StringComparer.OrdinalIgnoreCase)))
        {
            foreach (var r in p.ReferenciasDeProjeto
                         .Select(Normalizar)
                         .Where(r => r.EndsWith(".Test", StringComparison.OrdinalIgnoreCase) || r == "Test.Suporte"))
            {
                erros.Add($"[R3] Flashcard.{p.Nome} não pode referenciar projeto de teste ou suporte: Flashcard.{r}.");
            }
        }

        // R4/R5: pacotes permitidos em Domain/Application
        PacotesSomente("Domain", p => p == PrefixoFoundation + Core, "R4", porNome, erros);
        PacotesSomente("Application", p => p == PrefixoFoundation + Core, "R5", porNome, erros);

        // R6: SDK e framework dos projetos de domínio
        foreach (var n in new[] { "Domain", "Application", "Persistence" })
        {
            if (porNome.TryGetValue(n, out var p))
            {
                if (!string.Equals(p.Sdk, "Microsoft.NET.Sdk", StringComparison.OrdinalIgnoreCase))
                {
                    erros.Add($"[R6] Flashcard.{n} deve usar Microsoft.NET.Sdk.");
                }

                if (p.ReferenciasDeFramework.Contains("Microsoft.AspNetCore.App", StringComparer.OrdinalIgnoreCase))
                {
                    erros.Add($"[R6] Flashcard.{n} não pode referenciar Microsoft.AspNetCore.App.");
                }
            }
        }

        // R7: pacotes permitidos em Presentation
        if (porNome.TryGetValue("Presentation", out var presentation))
        {
            foreach (var p in presentation.Pacotes)
            {
                if (p.StartsWith(PrefixoFoundation, StringComparison.OrdinalIgnoreCase)
                    && (p.Equals(PrefixoFoundation + EFRepository, StringComparison.OrdinalIgnoreCase)
                        || p.Equals(PrefixoFoundation + Auth, StringComparison.OrdinalIgnoreCase)))
                {
                    erros.Add($"[R7] Flashcard.Presentation não pode usar o pacote {p}.");
                }
                else if (!p.StartsWith(PrefixoFoundation, StringComparison.OrdinalIgnoreCase)
                         && !TerceirosDaPresentation.Contains(p, StringComparer.OrdinalIgnoreCase)
                         && !p.StartsWith("NLog.", StringComparison.OrdinalIgnoreCase))
                {
                    erros.Add($"[R7] Flashcard.Presentation não pode usar o pacote de terceiro {p}.");
                }
            }
        }

        // R8: Presentation.Test só pode referenciar Presentation
        if (porNome.TryGetValue("Presentation.Test", out var pt))
        {
            foreach (var r in pt.ReferenciasDeProjeto
                         .Where(r => !r.Equals("Presentation", StringComparison.OrdinalIgnoreCase)))
            {
                erros.Add($"[R8] Flashcard.Presentation.Test só pode referenciar Flashcard.Presentation (encontrado: {r}).");
            }
        }

        // R9/R10: restrições em projetos de teste
        foreach (var p in projetos.Where(p => p.Nome.EndsWith(".Test", StringComparison.OrdinalIgnoreCase)))
        {
            foreach (var r in p.ReferenciasDeProjeto
                         .Select(Normalizar)
                         .Where(r => r == "Presentation"
                                     && !p.Nome.Equals("Presentation.Test", StringComparison.OrdinalIgnoreCase)))
            {
                erros.Add($"[R9] Flashcard.{p.Nome} não pode referenciar Flashcard.Presentation.");
            }

            foreach (var pacote in p.Pacotes.Where(x =>
                         PacotesProibidosNosTestes.Any(y =>
                             x.Equals(y, StringComparison.OrdinalIgnoreCase)
                             || (y == "Playwright"
                                 && x.StartsWith("Microsoft.Playwright", StringComparison.OrdinalIgnoreCase)))))
            {
                erros.Add($"[R10] Flashcard.{p.Nome} não pode usar o pacote de teste {pacote}.");
            }
        }

        // R11: produção não pode usar pacotes de teste
        foreach (var p in projetos.Where(p => Producao.Contains(Normalizar(p.Nome), StringComparer.OrdinalIgnoreCase)))
        {
            foreach (var pacote in p.Pacotes.Where(x =>
                         x.StartsWith("xunit", StringComparison.OrdinalIgnoreCase)
                         || x.Equals("Microsoft.NET.Test.Sdk", StringComparison.OrdinalIgnoreCase)
                         || x.StartsWith("Testcontainers", StringComparison.OrdinalIgnoreCase)
                         || x.StartsWith("bunit", StringComparison.OrdinalIgnoreCase)
                         || x.Equals("Microsoft.AspNetCore.Mvc.Testing", StringComparison.OrdinalIgnoreCase)))
            {
                erros.Add($"[R11] Flashcard.{p.Nome} não pode usar pacote de teste {pacote}.");
            }
        }

        // R12: Test.Suporte não pode referenciar Presentation
        if (porNome.TryGetValue("Test.Suporte", out var suporte)
            && suporte.ReferenciasDeProjeto
                .Select(Normalizar)
                .Contains("Presentation", StringComparer.OrdinalIgnoreCase))
        {
            erros.Add("[R12] Flashcard.Test.Suporte não pode referenciar Flashcard.Presentation.");
        }

        return erros;
    }

    private static void PacotesSomente(
        string nome,
        Func<string, bool> permitido,
        string regra,
        Dictionary<string, ProjetoCsproj> projetos,
        List<string> erros)
    {
        if (!projetos.TryGetValue(nome, out var p))
        {
            return;
        }

        foreach (var pacote in p.Pacotes.Where(x => !permitido(x)))
        {
            erros.Add($"[{regra}] Flashcard.{nome} não pode usar o pacote {pacote}.");
        }
    }

    private static string Normalizar(string nome) =>
        nome.StartsWith(PrefixoDaSolution, StringComparison.OrdinalIgnoreCase)
            ? nome[PrefixoDaSolution.Length..]
            : nome;
}
