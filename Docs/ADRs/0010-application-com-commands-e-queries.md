# ADR 0010 — Application com Commands e Queries

- **Status:** Aceita
- **Data:** 2026-11-15

## Contexto

A camada Application do Vault organiza os casos de uso como Commands e Queries com interface própria, sem mediator. Esse padrão é conhecido, simples de testar e deve ser mantido no Flashcard.

## Decisão

- Cada operação tem uma **interface** e uma **classe concreta** (`ICreatePastaCommand`/`CreatePastaCommand`, `IGetPastaListQuery`/`GetPastaListQuery`), com o método `ExecuteAsync(..., Guid ownerId, ..., CancellationToken cancellationToken)`. `CancellationToken` é obrigatório, último parâmetro e propagado até o `SaveAsync`.
- Os **controllers recebem as interfaces por construtor** e chamam `ExecuteAsync` diretamente. **Sem MediatR e sem mediator interno.**
- Estrutura por agregado: `<Agregado>/Commands/<Caso>` e `<Agregado>/Queries/<Caso>`. Modelos: `<X>Model` (saída, `FromDomain`), `<X>ModelForCreation` (entrada, `ToDomain(ownerId)`) e `<X>ModelForUpdate` (entrada, `UpdateDomain`).
- Ordem de um Command: validar com `CommandValidator.Validate(...)` (erro do cliente), buscar com escopo de dono, aplicar a regra (preferencialmente em operação de negócio da entidade), persistir e devolver um modelo, nunca a entidade.
- Regras que cruzam campos usam `IValidatableObject`; identificadores obrigatórios usam `[NotEmptyGuid]`, porque `[Required]` não protege `Guid`.
- **Commands e Queries não capturam nem logam erro** (padrão do Vault, ADR 0024 dele, e do guia da Foundation): o registro de falhas fica nos exception handlers globais da API.
- **Registro por convenção** (`I<Classe>` → `<Classe>`) com `AddServicesFromAssembly`, chamado no `Service` sobre o assembly da Application.
- Teste de registro por assembly: todos os serviços da Application devem resolver no contêiner.

## Consequências

- Nenhuma dependência nova; totalmente reversível.
- Validação e regra ficam visíveis no próprio caso de uso.
- Novos casos de uso não exigem registro manual, desde que sigam a convenção de nomes e residam no mesmo assembly da interface.

## Alternativas consideradas

- **MediatR/mediator:** pipeline de behaviors, mas dependência nova (licença comercial nas versões recentes) e mais indireção.
- **Serviços de aplicação agrupados por agregado:** menos arquivos, mas classes que crescem e casos de uso menos isolados.
