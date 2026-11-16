# ADR 0014 — Autenticação nas ilhas interativas (circuito do Blazor)

- **Status:** Aceita (detalhes validados no spike inicial)
- **Data:** 2026-11-15

## Contexto

A autenticação de cliente da Foundation guarda os tokens da API em sessão (`SessionTokenStore`, que usa `IHttpContextAccessor` e `ISession`). Dentro de um circuito do Blazor Server (ADR 0007) não há `HttpContext`/`ISession` confiável, e os handlers do `HttpClientFactory` rodam em outro escopo de DI. Além disso, o refresh token **rotaciona**: duas cópias dos tokens (sessão e circuito) invalidam uma à outra e deslogam o usuário. É preciso uma **fonte única de verdade** que sirva ao SSR e ao circuito.

## Decisão

Nova implementação de `ITokenStore` no pacote **`Lumia.Foundation.AspNetCore.Web`** (mudança aditiva), com tokens em **store no servidor indexado por uma claim `sid`**:

- No login, gera-se um identificador de sessão (`sid`) gravado como **claim do cookie** de autenticação.
- Os tokens (acesso e refresh) ficam em **cache em memória no servidor**, indexados pelo `sid`.
- O mesmo store atende o **SSR** (o `sid` vem do `HttpContext`) e o **circuito** (o `sid` vem do estado de autenticação, que carrega o mesmo cookie). Um accessor ligado ao circuito (por um `CircuitHandler`) permite que o handler do `HttpClient` encontre o `sid` no escopo correto.
- O **refresh é serializado por `sid`** (lock), evitando duas renovações simultâneas.
- **Falha de refresh no circuito** não passa pelo exception handler HTTP: o store do `sid` é limpo e a interface força navegação com recarga para o Login.
- O validador do cookie, hoje atrelado à sessão, passa a consultar o novo store.
- O `SessionTokenStore` permanece para hosts só com Razor Pages (como o Vault).

## Consequências

- Fonte única dos tokens, sem `ISession` no circuito; tokens continuam fora do navegador.
- Mudança aditiva no contrato público da Foundation: sinalizar o impacto (nenhum para o Vault, que mantém o `SessionTokenStore`) e **atualizar o guia de uso**.
- Cache em memória: reiniciar o contêiner desloga os usuários (hoje equivalente à sessão em memória); escalar para várias instâncias exigirá cache distribuído.
- O primeiro trabalho técnico do Flashcard é um **spike de autenticação em circuito**, com testes (refresh concorrente, expiração, logout e isolamento por `sid`) antes de qualquer tela.

## Alternativas consideradas

- **Copiar os tokens da sessão para o circuito (serviço escopado):** duas fontes de verdade; o refresh no circuito não chega à sessão e a próxima navegação SSR desloga o usuário.
- **Ilhas chamando endpoints do próprio Presentation (sem tokens no circuito):** isola o problema, mas duplica código e acrescenta uma camada sem necessidade.
