# ADR 0006 — Banco de dados: PostgreSQL

- **Status:** Aceita
- **Data:** 2026-11-15

## Contexto

A ideia inicial era MongoDB. O domínio, porém, é pequeno e estruturado: usuário → pastas → cartões, relação N:N (um `Cartao` pertence a uma ou mais `Pasta`s e nunca a zero), consultas como "cartões vencidos da pasta X" e "contagem de cartões por pasta". O pacote `Auth` da Foundation baseia-se em ASP.NET Core Identity com EF Core (relacional), e o `EFRepository` já suporta PostgreSQL.

## Decisão

- **PostgreSQL** como único banco, em contêiner no Docker Compose.
- **Dois `DbContext`s** no mesmo banco: um de Identity e um de domínio, com migrations em pastas separadas, uma `IDesignTimeDbContextFactory` para cada e registro por `ConfigurePostgreSqlDatabase<T>` com `DbConnectionHelper`. O domínio não tem tabela de usuário: guarda apenas `OwnerId`.
- Credenciais só por configuração, variáveis de ambiente ou secret manager (ver ADR 0009).
- A relação `Cartao` ↔ `Pasta` é N:N por tabela de associação, e a invariante "um cartão tem ao menos uma pasta" é garantida na Application e testada. O detalhe da modelagem sai na rodada de modelagem.
- Migrations aplicadas por *migration bundle*, não pela API em tempo de execução.

## Consequências

- Reaproveita `Auth` e `EFRepository` como estão; transações simples; um único banco para operar e fazer backup.
- Não há necessidade de suporte a MongoDB na Foundation por causa deste projeto.
- Sem chave estrangeira entre `OwnerId` e a tabela de usuários de Identity, a remoção de dados de um usuário excluído é responsabilidade da aplicação.
- Toda coluna `string` com `HasMaxLength` e índices compostos começando por `OwnerId`, conforme o guia da Foundation.
- Testes de persistência rodam contra PostgreSQL real em contêiner, pois o provedor InMemory não aplica restrições, chaves estrangeiras nem índices únicos.

## Alternativas consideradas

- **Identity relacional + domínio em MongoDB:** dois bancos, pacote novo na Foundation e transações dependentes de *replica set*.
- **MongoDB para tudo:** exige reescrever o `IdentityContext`; mudança grande na Foundation.
- **MySQL/MariaDB:** suportado e usado no Vault, mas PostgreSQL foi preferido.
