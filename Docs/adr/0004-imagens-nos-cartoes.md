# ADR 0004 — Imagens nos cartões

- **Status:** Aceita (armazenamento, validação e entrega no ADR 0016)
- **Data:** 2026-11-15

## Contexto

Frente e verso do `Cartao` usam Markdown e devem aceitar imagens. O uso inicial é pessoal e somente online, sem importação de cartões.

## Decisão

- Cada `Cartao` tem **no máximo duas imagens**: uma na frente e uma no verso.
- O limite é uma regra de domínio, validada na Application e refletida no modelo de dados.

## Consequências

- Escopo pequeno: sem galeria, sem gerenciamento de muitas imagens por cartão.
- Armazenamento, tamanho e tipos permitidos, validação do conteúdo e entrega foram decididos no [ADR 0016](0016-armazenamento-validacao-e-entrega-de-imagens.md). A forma de referenciar a imagem no Markdown foi decidida no [ADR 0015](0015-renderizacao-e-sanitizacao-de-markdown.md): esquema próprio `cartao-imagem:ID`, com no máximo uma imagem por lado.

## Alternativas consideradas

- **Imagens ilimitadas dentro do Markdown:** mais flexível, mas amplia a superfície de armazenamento, de validação e de abuso.
