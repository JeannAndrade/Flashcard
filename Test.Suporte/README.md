# Test.Suporte

Projeto compartilhado pelos testes, com a fixture de PostgreSQL em contêiner e o relógio fixo. Nunca deve ser referenciado por código de produção nem por `Presentation.Test`.

Projetos de teste que precisarem do banco devem declarar em seu próprio assembly uma classe `[CollectionDefinition(ColecaoPostgreSql.Nome)]` que implemente `ICollectionFixture<PostgreSqlFixture>`. A imagem é `postgres:17`; uma mudança de versão deve atualizar a constante e o compose juntos.

Ainda não fazem parte deste projeto a fábrica da API e o `FakeRepositoryManager`.

Com o Docker em execução, rode `dotnet test Persistence.Test`.
