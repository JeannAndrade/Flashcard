# ADR 0004 — Imagens nos cartões

- **Status:** Aceita (armazenamento e entrega ficam para um ADR próprio)
- **Data:** 2026-11-15

## Contexto

Frente e verso do `Cartao` usam Markdown e devem aceitar imagens. O uso inicial é pessoal e somente online, sem importação de cartões.

## Decisão

- Cada `Cartao` tem **no máximo duas imagens**: uma na frente e uma no verso.
- O limite é uma regra de domínio, validada na Application e refletida no modelo de dados.

## Consequências

- Escopo pequeno: sem galeria, sem gerenciamento de muitas imagens por cartão.
- Ficam para ADR posterior: onde armazenar (banco, volume em disco no contêiner ou outro), tamanho e tipos permitidos, validação do conteúdo (não confiar na extensão), entrega por endpoint autenticado e escopado por dono, e como a imagem é referenciada no Markdown.

## Alternativas consideradas

- **Imagens ilimitadas dentro do Markdown:** mais flexível, mas amplia a superfície de armazenamento, de validação e de abuso.
