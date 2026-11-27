using System.Data.Common;

namespace Persistence.Test.Suporte;

[CollectionDefinition(ColecaoPostgreSql.Nome)]
public sealed class ColecaoPostgreSqlDefinition : ICollectionFixture<PostgreSqlFixture>;

[Collection(ColecaoPostgreSql.Nome)]
public sealed class PostgreSqlFixtureTests(PostgreSqlFixture fixture)
{
    [Fact]
    public async Task ExecutarScriptAsync_WhenSelectSimples_RetornaExitCodeZero()
    {
        var resultado = await fixture.ExecutarScriptAsync("SELECT 1;");
        Assert.Equal(0, resultado.ExitCode);
    }

    [Fact]
    public async Task ExecutarScriptAsync_WhenPedeAVersao_RetornaAVersaoMaiorDeImagemDoBanco()
    {
        var resultado = await fixture.ExecutarScriptAsync("SELECT version();");
        Assert.Equal(0, resultado.ExitCode);
        Assert.Contains($"PostgreSQL {ImagemDoBanco.VersaoMaior}", resultado.Stdout);
    }

    [Fact]
    public void ObterConnectionString_WhenNomeValido_TrocaSomenteOBanco()
    {
        var original = new DbConnectionStringBuilder { ConnectionString = fixture.ConnectionString };
        var alterada = new DbConnectionStringBuilder { ConnectionString = fixture.ObterConnectionString("banco_teste") };
        Assert.Equal(original["Host"], alterada["Host"]);
        Assert.Equal(original["Port"], alterada["Port"]);
        Assert.Equal(original["Username"], alterada["Username"]);
        Assert.Equal("banco_teste", alterada["Database"]);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Banco")]
    [InlineData("banco teste")]
    [InlineData("banco;teste")]
    public void ObterConnectionString_WhenNomeInvalido_LancaArgumentException(string nome)
    {
        Assert.Throws<ArgumentException>(() => fixture.ObterConnectionString(nome));
    }

    [Fact]
    public void ConnectionString_WhenFixtureIniciada_NaoVazaSenhaFixaNoRepositorio()
    {
        var senha = new DbConnectionStringBuilder { ConnectionString = fixture.ConnectionString }["Password"]?.ToString();
        Assert.NotNull(senha);
        Assert.DoesNotContain(senha, new[] { "postgres", "password", "test" }, StringComparer.OrdinalIgnoreCase);
    }
}
