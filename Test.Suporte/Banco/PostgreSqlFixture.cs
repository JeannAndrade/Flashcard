using System.Data.Common;
using DotNet.Testcontainers.Containers;
using Testcontainers.PostgreSql;
using Xunit;

namespace Test.Suporte.Banco;

public sealed class PostgreSqlFixture : IAsyncLifetime
{
    private PostgreSqlContainer? _container;

    public string ConnectionString => _container?.GetConnectionString()
        ?? throw new InvalidOperationException("A fixture do PostgreSQL ainda não foi iniciada.");

    public async Task InitializeAsync()
    {
        _container = new PostgreSqlBuilder(ImagemDoBanco.Completa)
            .Build();

        try
        {
            await _container.StartAsync();
        }
        catch (Exception excecao)
        {
            await _container.DisposeAsync();
            _container = null;
            throw new InvalidOperationException(
                "Não foi possível iniciar o PostgreSQL de teste. Verifique se o Docker está em execução e acessível.",
                excecao);
        }
    }

    public string ObterConnectionString(string nomeDoBanco)
    {
        if (string.IsNullOrEmpty(nomeDoBanco)
            || nomeDoBanco.Any(caractere => !(char.IsLower(caractere) || char.IsDigit(caractere) || caractere == '_')))
        {
            throw new ArgumentException("O nome do banco deve conter apenas letras minúsculas, dígitos e sublinhado.", nameof(nomeDoBanco));
        }

        var construtor = new DbConnectionStringBuilder { ConnectionString = ConnectionString };
        construtor["Database"] = nomeDoBanco;
        return construtor.ConnectionString;
    }

    public async Task<ExecResult> ExecutarScriptAsync(string sql, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(sql);
        var container = _container ?? throw new InvalidOperationException("A fixture do PostgreSQL ainda não foi iniciada.");
        return await container.ExecScriptAsync(sql, cancellationToken);
    }

    public async Task DisposeAsync()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync();
            _container = null;
        }
    }
}
