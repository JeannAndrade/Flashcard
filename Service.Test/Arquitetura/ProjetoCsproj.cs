namespace Service.Test.Arquitetura;

public sealed record ProjetoCsproj(
    string Nome,
    string Sdk,
    IReadOnlyCollection<string> ReferenciasDeProjeto,
    IReadOnlyCollection<string> Pacotes,
    IReadOnlyCollection<string> ReferenciasDeFramework);
