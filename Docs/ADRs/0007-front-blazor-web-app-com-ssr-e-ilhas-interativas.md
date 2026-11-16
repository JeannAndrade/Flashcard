# ADR 0007 — Front: Blazor Web App com SSR estático e ilhas interativas

- **Status:** Aceita
- **Data:** 2026-11-15

## Contexto

O Vault usa Razor Pages. Para o Flashcard foi escolhido Blazor, por composição de componentes, interatividade pontual e aprendizado. O app é logado (SEO é irrelevante), de uso somente online, e os tokens da API devem ficar no servidor (o navegador nunca chama a API direto).

## Decisão

**Blazor Web App** com:

- **SSR estático por padrão** em todas as páginas.
- **Login e Registro como Razor Pages** no mesmo app, reaproveitando o fluxo de autenticação de cliente da Foundation (cookie + chamada à API).
- **Ilhas interativas (Interactive Server) desde o início**, aplicadas **por componente**, nunca globalmente. Ilhas iniciais:
  1. sessão de estudo (virar e avaliar sem recarregar a página);
  2. editor de `Cartao` com preview de Markdown;
  3. seleção das pastas do `Cartao`.

Fora de escopo: WebAssembly e modo Auto (descartados: tokens e CORS no navegador, e offline não é requisito).

## Consequências

- Interatividade só onde agrega; o resto fica leve e testável como SSR.
- O `SessionTokenStore` da Foundation não funciona dentro do circuito (sem `HttpContext`/`ISession`); a solução é o **ADR 0014**, e o primeiro trabalho técnico é um **spike de autenticação em circuito**, antes de qualquer tela.
- O `<lumia-pagination>` é TagHelper e não funciona em `.razor`: a paginação vira um componente Blazor, primeiro no projeto e depois extraído para a Foundation (mudança aditiva, com sinalização de impacto).
- Mais conceitos a dominar (render modes, pré-renderização que executa duas vezes, `HttpContext` nulo em componentes interativos) e dependência de conexão estável nas ilhas.
- Testes de componentes podem exigir uma biblioteca nova (por exemplo, bUnit): decisão e aprovação na rodada de testes.

## Alternativas consideradas

- **Razor Pages (como no Vault):** menor risco e reuso total da Foundation, mas sem componentes nem interatividade pontual.
- **Blazor com SSR puro, sem ilhas no início:** mais simples, mas posterga o aprendizado e a necessidade de resolver a autenticação no circuito.
- **WebAssembly/Auto:** pior encaixe com a autenticação por tokens no servidor.
