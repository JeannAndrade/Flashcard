namespace Test.Suporte.Banco;

public static class ImagemDoBanco
{
    // O compose (spec 0006) deve usar a mesma imagem; quem mudar a versão altera as duas coisas juntas.
    public const string Completa = "postgres:17";
    public const int VersaoMaior = 17;
}
