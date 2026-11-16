# ADR 0011 — Linguagem do domínio e idioma

- **Status:** Aceita
- **Data:** 2026-11-15

## Contexto

O Vault usa substantivos de domínio em português e verbos em inglês. Mensagens de exceção e validação chegam ao cliente e fazem parte do contrato público.

## Decisão

- **Substantivos do domínio em português:** `Pasta`, `Cartao` (sem acento em identificadores). Verbos dos casos de uso em inglês: `Create`, `Get`, `Update`, `Delete`, e termos de negócio como `Estudar`/`Avaliar` quando mais claros.
- **Um único idioma nas mensagens: português.** Mensagens de exceção e de DataAnnotations em português, com texto idêntico para a mesma situação em qualquer caso de uso (por exemplo, "Cartao não encontrado" em leitura, atualização e exclusão), respeitando o gênero.
- Código, comentários, commits e documentação em português; mensagens ao cliente sem detalhes internos (tabelas, SQL, caminhos).
- `Management` (e não `Managment`) para a pasta do gerenciador de repositórios.

## Consequências

- Consistência com o Vault e com o guia da Foundation.
- Ao copiar um caso de uso para outro agregado, revisar as mensagens é obrigatório; uma busca por textos em inglês nos casos de uso (por exemplo, "not found") ou um teste de mensagens evita regressão.

## Alternativas consideradas

- **Tudo em inglês:** padrão do ecossistema, mas diverge do Vault e da exigência de código e documentação em português.
- **`Card` em vez de `Cartao`:** curto, mas mistura idiomas no substantivo do domínio.
