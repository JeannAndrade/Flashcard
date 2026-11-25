# ADR 0008 — Isolamento multi-tenant

- **Status:** Aceita
- **Data:** 2026-11-15

## Contexto

Todo recurso pertence a um usuário e vazamento entre usuários é inaceitável. O cadastro é aberto (ADR 0013), então o isolamento é requisito real. O Vault já adota o modelo abaixo.

## Decisão

- **`ownerId` explícito de ponta a ponta**: o controller o lê do usuário autenticado (`GetCurrentUserId`); todo Command/Query o recebe como parâmetro `Guid ownerId`; todo método de repositório o recebe e filtra por ele. **Nome único `ownerId`/`OwnerId`** em Commands, Queries, repositórios e entidades.
- **O dono nunca vem do corpo da requisição.** Nenhum `ModelForCreation`, `ModelForUpdate` ou parâmetro de consulta contém `OwnerId`; ele entra na entidade só por `ToDomain(ownerId)`. O ID de outro usuário se comporta como inexistente (`EntityNotFoundException`, 404).
- Validação de referências (por exemplo, as pastas de um `Cartao`) consulta com o mesmo `ownerId`.
- Índices compostos sempre começam por `OwnerId`.

Testes obrigatórios:
1. **Arquitetura/convenção:** toda interface de Command/Query e todo método de leitura/escrita de repositório recebe `Guid ownerId`, com lista explícita de exceções.
2. **Isolamento:** dados de dois `OwnerId` e prova de que o segundo nunca enxerga o primeiro, em cada operação, contra PostgreSQL real.
3. **"Forjar o dono":** teste de integração que envia um `OwnerId` de outro usuário no corpo e prova que é ignorado.

## Consequências

- Isolamento visível, explícito e auditável, sem "mágica" no pipeline.
- Esquecer o filtro em uma consulta nova seria um vazamento; por isso os testes acima são obrigatórios e bloqueiam a entrega.

## Alternativas consideradas

- **`ICurrentUser` ambiente + filtro global de consulta do EF:** esquecimento mais difícil, mas cria um segundo canal de identidade e mais "mágica". Rejeitada por ora; pode voltar como camada extra.
- **Row Level Security do PostgreSQL:** defesa em profundidade, porém complexa com pool de conexões e EF; exagero para o uso atual. Evolução futura.
