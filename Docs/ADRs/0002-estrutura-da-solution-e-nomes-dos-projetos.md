# ADR 0002 — Estrutura da solution e nomes dos projetos

- **Status:** Aceita
- **Data:** 2026-11-15

## Contexto

O Flashcard usa .NET 10 em uma única solution. O projeto de referência (Vault) já adota uma nomenclatura de camadas, e consistência entre projetos é objetivo de qualidade.

## Decisão

Solution única `Flashcard`, com os projetos:

| Projeto | Papel |
| --- | --- |
| `Domain` | Entidades, regras e cálculos puros do domínio |
| `Application` | Commands, Queries, modelos (DTOs de entrada/saída) e **portas** de repositório |
| `Persistence` | `DbContext`s, mapeamentos, migrations e implementação das portas |
| `Service` | **Host da API REST** (controllers) e raiz de composição |
| `Presentation` | **Projeto de front** (Blazor Web App); nunca acessa o banco, só a API por HTTP |

Dependências (sempre apontando para dentro):

- `Domain` → `Lumia.Foundation.Core`
- `Application` → `Domain` e `Lumia.Foundation.Core`
- `Persistence` → `Application`, `Domain`, `EFRepository` e `Auth`
- `Service` → `Application`, `Persistence` e pacotes da Foundation
- `Presentation` → somente pacotes da Foundation (nenhuma referência a Domain, Application ou Persistence)

**Nota de esclarecimento (2026-11-21, ADR 0015):** a restrição "somente pacotes da Foundation" para a `Presentation` trata de **projetos internos**. Pacotes de terceiros são permitidos nela quando aprovados em ADR (hoje: Markdig e HtmlSanitizer).

Projetos de teste usam o sufixo `.Test` (por exemplo, `Application.Test`, `Persistence.Test`, `Service.Test`) e xUnit.

Registrado no README de cada projeto: `Presentation` é o host do front e `Service` é o host da API, para evitar a leitura de "Service" como serviço de negócio.

## Consequências

- Paridade total com o Vault refatorado; quem conhece um projeto conhece o outro.
- O nome `Service` exige a nota de esclarecimento acima.
- Um teste de arquitetura impede inversões de dependência (ver ADR 0012).

## Alternativas consideradas

- **`Web` e `Api`:** mais autoexplicativo, mas diverge do Vault e exigiria renomear a referência.
- **Um único projeto com pastas:** simples, mas não impõe as fronteiras de dependência por construção.
