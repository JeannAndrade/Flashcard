# ADR 0018 — Padrão de erros e validação

- **Status:** Aceita
- **Data:** 2026-11-23

## Contexto

O Flashcard herda o padrão de erros do Vault (ADR 0010, ADR 0008 e o guia da Foundation). Os fatos abaixo vêm do documento de integração do `Vault.Presentation` e valem para a versão atual da Foundation:

- A API registra `AddExceptionHandlers()`, que traz o `DomainExceptionHandler` e o `UnhandledExceptionHandler`, nessa ordem. A resposta de erro é `ErrorDetails`.
- Mapeamento atual: `EntityNotFoundException` → **404**; `EntityInUseException` e `CommandValidationException` → **422** (e não 409, como o quadro do guia supunha). Exceção não mapeada → 500 com `ErrorDetails`, registrada como falha de servidor.
- `DbUpdateException` não tem tratamento específico: vira 500.
- `BaseRepositoryManager.SaveAsync` é público e **não é `virtual`**.
- `ApiException` recebe a mensagem do `ErrorDetails` em **qualquer** status; se o corpo não for reconhecível, a mensagem é técnica (baseada no status).
- A Presentation do Vault valida localmente (DataAnnotations e `ModelState`), mostra mensagem amigável fixa na maioria das falhas e a `ApiException.Message` em alguns 422. Não desserializa erros por campo. Existe `/Error` genérica e não existem páginas por status.
- O `ApiUnauthorizedExceptionHandler` (Foundation, `AspNetCore.Web`) trata o 401 no pipeline HTTP.
- O Vault usa `AddExceptionHandlers()` do pacote `Lumia.Foundation.AspNetCore` (legado), e o guia recomenda `AspNetCore.Api` para projetos novos. A equivalência de comportamento entre os dois é conferida na criação do esqueleto.

O Flashcard acrescenta o que o Vault não tinha: limites configuráveis (ADRs 0015 e 0016), regras do Domain com mensagem ao usuário, violações de constraint do banco com probabilidade real e ilhas interativas (ADR 0007).

## Decisão

1. **Herdar o padrão do Vault:**
   - exceções do `Core` lançadas sem mencionar HTTP;
   - `CommandValidator.Validate` antes de qualquer acesso a dados;
   - DataAnnotations nos modelos, `[NotEmptyGuid]` e `IValidatableObject` para regras que cruzam campos;
   - log só nos handlers globais, nunca na Application (ADR 0010);
   - dono alheio se comporta como inexistente (404, ADR 0008);
   - **mensagens em português**, texto idêntico para a mesma situação, sem detalhes internos.
2. **Contrato HTTP (o atual da Foundation, sem alterá-lo):**

   | Exceção | Resposta | Uso no Flashcard |
   | --- | --- | --- |
   | `EntityNotFoundException` | 404 | cartão, pasta ou imagem inexistente ou de outro dono; pasta de outro dono na lista do cartão |
   | `CommandValidationException` | 422 | validação de modelo, limites, regras do Domain, nome de pasta repetido |
   | `EntityInUseException` | 422 | excluir pasta que ainda tem cartões |
   | não mapeada (inclui `DbUpdateException` não traduzida) | 500 | falha de servidor, registrada; o corpo não leva detalhes técnicos |

   Consequência: "pasta em uso" e "nome repetido" chegam como 422. A Presentation os distingue pela mensagem, não pelo status.
3. **Limites configuráveis (D1-A):**
   - A Application declara uma interface de limites (texto do cartão e imagem), implementada no Service a partir do `appsettings`. A Application continua sem ASP.NET Core (ADR 0012).
   - O comando valida esses limites logo depois do `CommandValidator`, antes de acessar dados, e lança `CommandValidationException` com mensagem em português que diz o limite.
   - As anotações carregam só os **tetos fixos** (5000, 60). Os limites operacionais (padrão 2000 e 1 MB) não viram anotação.
   - Configuração acima do teto falha na inicialização (ADR 0015, item 8, e ADR 0016, item 4).
4. **Regras do Domain violadas** (texto acima do teto, mais de uma imagem por lado, imagem inválida): o Domain lança `CommandValidationException` com mensagem em português, como o `EncerramentoValidador` do Vault. O Domain depende só do `Core`.
5. **Erros do banco (D2-B):**
   - A tradução fica na **Persistence** do Flashcard, que conhece o PostgreSQL. Ela identifica a violação pelo **nome da constraint** (nomeado no mapeamento, ADR 0017) e traduz:
     - índice único `(OwnerId, Nome)` de `Pastas` → `CommandValidationException`, com a **mesma mensagem** da checagem prévia;
     - FK `Restrict` de `CartaoPasta` para `Pasta` → `EntityInUseException`, com a mesma mensagem da checagem prévia.
   - Violação de qualquer outra constraint é **relançada sem alteração** (vira 500 e é registrada). Nada é engolido.
   - **Mecanismo (decidido pelo usuário):** tornar o `BaseRepositoryManager.SaveAsync(CancellationToken)` **`virtual`** na Foundation (`EFRepository`). O `RepositoryManager` do Flashcard o sobrescreve, chama a base dentro do tratamento e traduz a violação. É uma mudança **aditiva**: o comportamento do método não muda e quem herda a classe (como o Vault) continua compilando e funcionando sem alteração. O contrato público muda só no sentido de permitir extensão, e isso sai numa **nova versão do pacote `EFRepository`**, que o Flashcard passa a exigir como mínima (a numeração é decisão do usuário).
   - A tradução em si (as regras acima, por constraint) continua **só no Flashcard**. Subir a tradução para a Foundation, trocando a exceção que o Vault recebe hoje, continua sendo um passo à parte (item 8 do guia), com sinalização do impacto no Vault.
6. **Presentation:**
   - **Validação local** com DataAnnotations nos modelos da Presentation (como no Vault), mais a regra dinâmica dos limites vindos do endpoint do ADR 0015. A API continua sendo a autoridade.
   - **Sem erros por campo vindos da API na v1 (D3-A).** Erros que só a API detecta aparecem como mensagem geral no topo do formulário. Reavalia-se se o uso real mostrar atrito.
   - **404, 422 e 429:** mostram a `ApiException.Message` (são mensagens escritas para o cliente; o 429 vem do limitador, ADR 0013). O 409 recebe o mesmo tratamento, caso algum dia apareça.
   - **Demais status** (incluindo 5xx): mensagem amigável fixa, sem detalhe técnico.
   - **401:** nas requisições de SSR, o `ApiUnauthorizedExceptionHandler`; nas ilhas ele não é alcançado (a exceção ocorre no circuito), então a ilha limpa o store do `sid` e força navegação com recarga para o Login (ADR 0014).
   - **Cada ilha** fica dentro de um `ErrorBoundary`, para a falha de uma não derrubar as demais nem a sessão de estudo.
   - **Páginas globais** de exceção e de 404 (o Vault não tem página de status).
7. **Corpo de erro:** `ErrorDetails` como está. O identificador de correlação para o usuário informar um erro 500 é o `TraceId` do contexto de rastreio nativo, mostrado na tela de erro da Presentation (ADR 0020), sem mudar o `ErrorDetails`.
8. **Fora do pipeline de exceções:** o 429 do limite de taxa está definido no ADR 0013 (`ErrorDetails` em português e `Retry-After`). Os erros 413 e 415 do upload são definidos no spike de imagens, com o mesmo formato e mensagens em português.

## Consequências

- **Uma mudança aditiva na Foundation:** `SaveAsync` passa a ser `virtual` no `BaseRepositoryManager`. Impacto no Web/Api: nenhum para o Vault (sem mudança de comportamento ou de assinatura). O guia de uso e o README do `EFRepository` precisam registrar o ponto de extensão, e o Flashcard só compila contra a nova versão do pacote. A mudança precisa ser publicada **antes** de a Persistence do Flashcard ser implementada.
- O `override` fica na Persistence, na única classe que conhece o PostgreSQL, e os casos de uso continuam vendo só `IRepositoryManager`.
- "Pasta em uso" e "nome repetido" têm o mesmo status (422), diferenciados pela mensagem. Se isso atrapalhar a Presentation, a saída é diferenciar na Foundation, em novo ADR.
- A mensagem da `ApiException` é técnica quando o corpo não é `ErrorDetails` (por exemplo, um 404 de proxy). Mostrar a mensagem em 404 e 422 pode, nesse caso, exibir texto técnico. Mitigação: teste com corpo não reconhecível e, se confirmado, a Presentation passa a usar a mensagem fixa nesses casos.
- A tradução do banco é código só do Flashcard, com mensagens duplicadas entre checagem prévia e tradução. As mensagens ficam em constantes compartilhadas para não divergirem.
- A validação duplicada na Presentation (anotações) tem risco de divergência com a API. O teste de contrato (ADR 0003) cobre os tetos fixos.
- Cada ilha com `ErrorBoundary` tem custo de marcação, mas evita derrubar o circuito.

## Como será testado

- **Application (xUnit):** tabela de casos de validação por comando (anotações, limite operacional, regras do Domain), cada uma com mensagem em português; teste das mensagens de "não encontrado" (sem texto em inglês).
- **Persistência (PostgreSQL real):** nome de pasta repetido e exclusão de pasta com cartão geram a exceção de domínio esperada; violação de outra constraint é relançada sem alteração; cancelamento não é engolido.
- **API:** mapeamento de cada exceção para o status e o corpo; a exceção não mapeada devolve 500 sem pilha nem detalhes internos.
- **Presentation:** tabela de casos da tradução de `ApiException` (404, 422, 5xx, 401) e teste com corpo não reconhecível; o tratamento de falha por ilha e o 401 no circuito entram no spike de autenticação (ADR 0014) e na estratégia de testes (bUnit ainda a decidir).
- **Contrato (ADR 0003):** os tetos fixos das anotações da Presentation batem com os da API.

## Alternativas consideradas

- **Erros por campo no `ErrorDetails` (Foundation):** mensagem junto do campo, mas muda contrato público (Vault e `Http.Client`) e a validação local já cobre os campos.
- **`ProblemDetails`:** padrão da indústria, mas rompe o padrão da Foundation.
- **Cada página trata a exceção do seu jeito:** funciona no Vault por serem poucas páginas, mas diverge mais nas ilhas.
- **Limite operacional por `IValidatableObject` buscando serviços:** uma etapa só, mas acopla o modelo à injeção e não sei se o `CommandValidator` expõe o provedor de serviços.
- **Limite fixo (2000) na anotação:** perde a configurabilidade pedida.
- **Tradução de erros do banco, mecanismo (escolhido: `SaveAsync` `virtual` na Foundation):**
  - **Implementação explícita de `IRepositoryManager.SaveAsync` no Flashcard:** não toca a Foundation e os casos de uso não a contornam, mas é uma solução específica do Flashcard e não deixa o ponto de extensão para outros projetos. Rejeitada pelo usuário em favor de tornar o método extensível.
  - **Decorador sobre `IRepositoryManager`:** isola a tradução, mas exige repassar todas as propriedades de repositório e mais um registro no contêiner.
  - **Ocultar o `SaveAsync` da base (`new`):** frágil, pois quem usa o tipo base contorna a tradução.
  - **Traduzir na própria Foundation:** beneficia todos os projetos, mas troca a exceção que o Vault recebe hoje.
- **Handler genérico na API para `DbUpdateException` (409):** mensagem genérica e a API passaria a conhecer EF.
