# ADR 0012 — Portas de repositório na Application

- **Status:** Aceita
- **Data:** 2026-11-15

## Contexto

No Vault, o `IRepositoryManager` e as interfaces de repositório ficavam na Persistence, e a Application a referenciava (e, de forma transitiva, o EF Core). Isso inverte a regra de dependências do Clean Architecture. O Vault foi refatorado para o layout-alvo e é a referência do Flashcard.

## Decisão

- `IRepositoryManager` e as interfaces `I<Entidade>Repository` ficam na **Application**. Os métodos têm linguagem de domínio (`GetAsync(ownerId, id, trackChanges, cancellationToken)`), devolvem entidades ou listas já materializadas e **nunca `IQueryable`**. A porta declara `SaveAsync(CancellationToken)` por conta própria, sem herdar de pacote de EF.
- A **Persistence** implementa as portas: repositórios estendem `BaseRepository<T>`, e o `RepositoryManager` estende `BaseRepositoryManager`, criando repositórios sob demanda (`Lazy`) sobre o **mesmo** `DbContext`, de modo que um único `SaveAsync` confirma a unidade de trabalho. Os repositórios não são registrados no contêiner; apenas `IRepositoryManager`.
- Como `AddServicesFromAssembly` só registra interfaces que estão no mesmo assembly da implementação, a Persistence expõe **um único método de extensão de registro** (por exemplo, `AddPersistence`), chamado pelo `Service`.
- Dependências: `Application` → `Domain` e `Lumia.Foundation.Core`; nenhum pacote de EF Core, ASP.NET Core ou logger na Application.
- **Teste de arquitetura** (`DependenciasEntreCamadasTests`, no padrão do Vault) falha se alguma dependência for invertida. O teste vive no projeto de testes do `Service`, que enxerga todas as camadas.

## Consequências

- Dependências realmente apontando para dentro; Commands e Queries testáveis com fakes, sem EF.
- Um pouco mais de código de cola e um registro explícito da Persistence.
- Consultas paginadas devolvem `PagedList<T>` da Foundation, convertido com `Map(Model.FromDomain)`; a ordenação é determinística (com desempate por `Id`).

## Alternativas consideradas

- **Repetir o desenho original do Vault (portas na Persistence):** menos código, mas a Application depende do EF Core.
- **Extrair um contrato sem EF para um pacote da Foundation:** reuso limpo, porém altera contratos públicos e impacta o Vault; reavaliar quando o padrão estiver provado.
