# ADR 0003 — Contrato entre Presentation e Service

- **Status:** Aceita
- **Data:** 2026-11-15

## Contexto

O front consome a API e nunca acessa o banco. É preciso um contrato claro, sem acoplar os dois projetos em tempo de compilação além do necessário.

## Decisão

- Cada lado mantém **os seus próprios DTOs**: a `Application` do Service tem os modelos de entrada e saída; a `Presentation` tem os modelos de consumo.
- O consumo é feito por **`HttpClient` tipado da Foundation**: `AddLumiaApiClient` e um cliente de recurso por agregado (`AddApiResourceClient<I...Api, ...Api>`), usando `IApiConnection`. Respostas malsucedidas geram `ApiException`.
- Os contratos genéricos `PagedResponse<T>` e `ErrorDetails` vêm de `Lumia.Foundation.Abstractions` e são os únicos compartilhados.
- A API publica OpenAPI/Scalar em desenvolvimento.

## Consequências

- Zero acoplamento de compilação entre Presentation e Service.
- Risco de divergência silenciosa entre os DTOs. Mitigação: **testes de contrato**, que serializam o modelo da API e o desserializam no modelo do cliente, entram na estratégia de testes.
- Duplicação controlada de classes simples.

## Alternativas consideradas

- **Projeto `Contracts` compartilhado:** simples e tipado, mas acopla os dois lados em compilação.
- **Cliente gerado a partir do OpenAPI (NSwag/Kiota):** desacopla de verdade, mas adiciona dependência e etapa de geração.
