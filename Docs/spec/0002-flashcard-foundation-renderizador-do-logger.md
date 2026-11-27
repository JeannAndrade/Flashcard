# Especificação técnica: Renderizadores de `TraceId` e `OwnerId` no Logger (LumiaFoundation)

- Data: 2026-11-26
- Status: aprovada para implementação (2026-11-26)
- ADR relacionado: 0020 (observabilidade; itens "renderizador no Logger" e "tag `OwnerId` na `Activity`")
- Repositórios afetados: **LumiaFoundation** (`Logger`). O Flashcard **não** é alterado nesta spec; ele consome a nova versão na spec 0004 (host do `Service`) e na 0005 (host da `Presentation`).

> Este documento é uma especificação para outro dev implementar. Não contém código de implementação: descreve o que fazer em cada classe, a ordem, os testes e a documentação.
> Todo código, comentário, commit e documentação em português. Nomes de tipos e membros seguem o padrão do repositório.

---

## 1. Resumo

Os logs do Flashcard precisam carregar o **`TraceId`** (correlação, mostrado também na tela de erro) e o **`OwnerId`** (dono do recurso) sem que o `ILoggerManager` mude. A solução do ADR 0020 é acrescentar ao `Lumia.Foundation.Logger` dois **renderizadores de layout do NLog** que leem esses valores da `System.Diagnostics.Activity` corrente. O `TraceId` vem da própria `Activity`; o `OwnerId` vem de uma **tag** da `Activity` da requisição, que um middleware do host (spec 0004) preenche depois da autenticação. Esta spec cobre só a biblioteca: renderizadores, constante da tag, testes, documentação e nova versão do pacote.

## 2. Decisões aprovadas

| Item | Decisão | Origem |
| --- | --- | --- |
| D1 | Correlação pelo `TraceId` W3C nativo da `Activity`; `ErrorDetails` não muda | ADR 0020 |
| D2 | `OwnerId` entra no log por uma **tag** da `Activity` da requisição, lida por um renderizador do Logger; o `ILoggerManager` **não** ganha escopo nem parâmetro novo | ADR 0020, aprovado pelo usuário em 2026-11-23 |
| D3 | Mudança **aditiva** na `LumiaFoundation`: nenhuma assinatura pública existente é alterada ou removida | regras do projeto |
| D4 | Nenhuma dependência nova: usa o NLog que o `Logger` já referencia e `System.Diagnostics` do runtime. A contingência `NLog.DiagnosticSource` **não** é usada | ADR 0020 |
| D5 | Os renderizadores são registrados pelo `nlog.config` dos hosts (`<extensions>`), e não por código no `ConfigureLoggerService` | proposta do arquiteto, ver D6 |
| D6 | Nomes dos renderizadores no `nlog.config`: `${lumia-traceid}` e `${lumia-ownerid}` (a Foundation ainda não tem convenção de nomes para renderizadores; estes passam a ser o padrão) | aprovada pelo usuário |
| D7 | Nome da tag da `Activity`: `lumia.owner_id`, exposto numa constante pública do Logger | aprovada pelo usuário |
| D8 | Nova versão do pacote `Lumia.Foundation.Logger`: **0.6.0** (de 0.5.1; adição de tipos públicos, sem quebra) | aprovada pelo usuário |

## 3. Verificações já feitas (não repetir)

Já confirmadas pelo mantenedor no código (2026-11-23):

- `ILoggerManager` usa o NLog, aceita `params object[]`, e **não** tem escopo nem propriedades nomeadas próprias.
- `ConfigureLoggerService` registra só o `ILoggerManager`; **não** chama `AddNLog()` (logs do framework não vão ao NLog).
- A versão publicada em uso no Flashcard é `Lumia.Foundation.Logger` 0.5.1.

**Não verificado por mim** (o código do `Logger` não estava disponível): framework de destino (`TargetFramework`), versão do pacote NLog, nomes e namespaces dos tipos existentes, existência de um projeto de teste do `Logger`. Por isso o Passo 0 é um inventário.

## 4. Atenção antes de começar

1. **Não alterar o `ILoggerManager`** nem a sua implementação. Se o dev achar que precisa, **parar e perguntar**.
2. **Nenhum registro por código no `ConfigureLoggerService`** (D5). Se a lista de extensões do `nlog.config` não funcionar com a versão do NLog do pacote, **registrar a falha e perguntar** antes de trocar de abordagem.
3. **O renderizador não é "thread agnostic".** `Activity.Current` vive em `AsyncLocal`, que depende do fluxo de execução. Marcar o renderizador como independente de thread faria o NLog renderizá-lo numa thread de destino assíncrono, onde a `Activity` já não é a da requisição. **Não** aplicar o atributo de independência de thread.
4. **`OwnerId` só é aceito se for um `Guid`** (como valor ou como texto que converte em `Guid`). Qualquer outra coisa vira texto vazio. É proposital: impede injeção de quebra de linha ou de conteúdo arbitrário no log por meio da tag.
5. **A tag pode estar na `Activity` pai.** Quando o código loga dentro de uma `Activity` filha (por exemplo, uma chamada HTTP de saída), a tag foi colocada na `Activity` da requisição. O renderizador percorre a cadeia `Activity.Current` → `Parent` até achar a tag.
6. **Publicar o pacote antes** de o Flashcard o consumir (Passo 5). O mantenedor usa o NuGet e uma pasta local de pacotes; seguir o mesmo procedimento das versões anteriores.
7. **Testes com NLog usam configuração global** (`LogManager`). Os testes devem montar e desmontar a configuração (`LogManager.Shutdown()` no `Dispose`) e **não** podem rodar em paralelo com outros testes que alteram a configuração do NLog: colocar as classes de teste do registro numa mesma `Collection` do xUnit.

## 5. Ordem de implementação

Cada passo termina com build e testes verdes antes do seguinte.

| # | Passo | Onde |
| --- | --- | --- |
| 0 | Inventário do `Logger` | LumiaFoundation (sem alterações) |
| 1 | Constante da tag | `Logger` |
| 2 | Renderizador de `TraceId` | `Logger` |
| 3 | Renderizador de `OwnerId` | `Logger` |
| 4 | Testes de registro pelo `nlog.config` e de saída JSON | `Logger.Test` |
| 5 | Documentação, versão e publicação | `Logger` + raiz |
| 6 | Fechamento (ADR 0020) | Flashcard (docs) |

---

## Passo 0: Inventário do `Logger`

**Não alterar nada.** Preencher e colar na descrição do PR:

- pasta e nome do projeto (`LumiaFoundation.Logger`?), `TargetFramework`, versão atual do pacote, versão do pacote NLog referenciada;
- nomes e namespaces dos tipos públicos existentes (`ILoggerManager`, a implementação, `ConfigureLoggerService`);
- onde o `nlog.config` é lido e se o `Logger` tem algum ponto de configuração do NLog;
- existência de um projeto de teste do `Logger` e dos pacotes de teste que ele usa;
- convenção de namespaces e de pastas dos demais projetos da Foundation;
- se o `TargetFramework` **não** for `net10.0` e o pacote `System.Diagnostics.DiagnosticSource` for necessário para `Activity`, **parar e perguntar** (seria dependência nova).

**Critério de pronto:** inventário anexado e divergências em relação às seções 3 e 4 listadas, **sem corrigir ainda**.

---

## Passo 1: Constante da tag

Num tipo estático público e selado do `Logger` (namespace e pasta conforme o inventário), declarar:

- uma constante pública com o nome da tag do dono: valor `lumia.owner_id` (D7);
- comentário em português dizendo que o middleware do host grava a tag na `Activity` da requisição depois da autenticação e que o renderizador `lumia-ownerid` a lê.

Esta constante é o **único** lugar onde o nome da tag aparece como literal. O host (spec 0004) e os testes usam a constante.

**Critério de pronto:** a solution compila; a constante é pública e documentada.

---

## Passo 2: Renderizador de `TraceId`

Classe pública e selada, derivada do layout renderer base do NLog, registrada com o nome `lumia-traceid` (D6). **Sem** o atributo de independência de thread (seção 4, item 3).

Comportamento ao renderizar um evento de log:

- Sem `Activity.Current`: escreve **nada** (texto vazio).
- Com `Activity.Current` cujo formato de id **não** é W3C (`IdFormat` diferente de `W3C`): escreve nada.
- Com `Activity.Current` W3C: escreve o `TraceId` em hexadecimal minúsculo, 32 caracteres, sem separadores.
- Não lança exceção em nenhum caso; não consulta nada além da `Activity.Current`.

Nenhuma propriedade configurável.

**Critério de pronto:** compila; os testes do Passo 4 (classe `TraceIdLayoutRendererTests`) passam.

---

## Passo 3: Renderizador de `OwnerId`

Classe pública e selada, derivada do layout renderer base do NLog, registrada com o nome `lumia-ownerid` (D6). Sem independência de thread.

Comportamento:

- Começa em `Activity.Current` e percorre a cadeia de `Parent` até a raiz, procurando o primeiro item de tag com o nome da constante do Passo 1.
- Se achar um valor que seja `Guid`, escreve-o no formato `D` (36 caracteres, com hífens, minúsculo).
- Se achar um `string` que converta em `Guid`, escreve o `Guid` normalizado (mesmo formato).
- Se não achar, ou se o valor for de outro tipo, nulo, vazio ou texto que não converte em `Guid`: escreve nada.
- Para na **primeira** tag encontrada, mesmo que o valor seja inválido (a `Activity` mais próxima da chamada prevalece).
- Não lança exceção em nenhum caso.

**Critério de pronto:** compila; os testes do Passo 4 (classe `OwnerIdLayoutRendererTests`) passam.

---

## Passo 4: Testes

Projeto de teste do `Logger`: o existente (conforme inventário) ou, se não houver, `LumiaFoundation.Logger.Test`, com xUnit na mesma versão dos outros projetos de teste da Foundation. Sem biblioteca nova (o `MemoryTarget` já vem no NLog). Nomes no padrão `<Membro>_When<Condicao>_<Resultado>`.

Para criar `Activity` nos testes é preciso um `ActivitySource` com um `ActivityListener` registrado (sem listener, `StartActivity` devolve nulo): montar um auxiliar de teste, privado do projeto, que registra o listener (`ShouldListenTo = true`, `Sample = AllData`) e o remove no `Dispose`. Usar `Activity.DefaultIdFormat = W3C` explicitamente nos testes.

### 4.1 `TraceIdLayoutRendererTests`

- `Render_WhenSemActivity_RetornaVazio`
- `Render_WhenActivityW3C_RetornaTraceIdEmHexaDe32Caracteres` (comparar com `activity.TraceId.ToHexString()`)
- `Render_WhenActivityHierarquica_RetornaVazio`
- `Render_WhenActivityFilha_RetornaOTraceIdDaRaiz`
- `Render_WhenLogDepoisDeAwaitEmOutraThread_PropagaOTraceId`

### 4.2 `OwnerIdLayoutRendererTests`

- `Render_WhenSemActivity_RetornaVazio`
- `Render_WhenTagNaActivityAtual_RetornaOGuid`
- `Render_WhenTagSoNaActivityPai_RetornaOGuidDoPai`
- `Render_WhenTagComStringDeGuid_RetornaOGuidNormalizado`
- `Render_WhenTagAusente_RetornaVazio`
- `Render_WhenTagComTextoQueNaoEhGuid_RetornaVazio` (incluir um valor com quebra de linha)
- `Render_WhenTagDeOutroTipo_RetornaVazio`
- `Render_WhenTagInvalidaNaFilhaEValidaNoPai_RetornaVazio` (a mais próxima prevalece)

### 4.3 `RegistroDosRenderizadoresTests` (classe de integração com o NLog; mesma `Collection`)

Cada teste monta a configuração do NLog a partir de um **XML em texto**, com `<extensions>` apontando para o assembly do Logger (nome do assembly conforme o inventário), um `MemoryTarget` e o logger de teste. Desmontar com `LogManager.Shutdown()`.

- `Config_WhenExtensionsApontaParaOLogger_RegistraOsDoisRenderizadores`: layout `${lumia-traceid}|${lumia-ownerid}|${message}`; dentro de uma `Activity` com a tag, a linha gravada é `<traceid>|<ownerid>|mensagem`.
- `Config_WhenSemActivity_RenderizaCamposVazios`: a linha gravada é `||mensagem` (a configuração não falha).
- `Config_WhenJsonLayout_GeraJsonValidoComTraceIdEOwnerId`: `JsonLayout` com atributos `traceId` e `ownerId`; interpretar a saída com `System.Text.Json` e conferir as duas propriedades.
- `Config_WhenLogViaILoggerManager_EscreveTraceIdEOwnerId`: cria o `ILoggerManager` do jeito que o inventário mostrou, loga uma mensagem dentro de uma `Activity` com a tag e confere a linha. **Se o inventário mostrar que não dá para criar o `ILoggerManager` sem tocar no código existente, escrever o teste com o NLog direto e registrar o motivo no PR** (não alterar o `ILoggerManager`).
- `Config_WhenAssemblyNaoCarregavel_FalhaNaConfiguracao`: caso negativo que prova que o teste de registro detecta a ausência da extensão (nome de assembly inexistente, esperado: exceção de configuração do NLog, com `LogManager.ThrowConfigExceptions = true` no teste).

### 4.4 Teste de contrato público (aditivo)

- `ILoggerManager_WhenComparadoComAVersaoAnterior_MantemOsMembros`: **só se** o repositório já tiver prática de teste de contrato; caso contrário, **não criar** e confirmar a ausência de mudança no `ILoggerManager` pela revisão do diff.

**Critério de pronto:** `dotnet test` verde no projeto de teste do `Logger`; nenhuma mudança em `ILoggerManager`.

---

## Passo 5: Documentação, versão e publicação

**Documentação** (`docs/readme.md` do `Logger` e README da raiz, conforme o padrão das outras libs):

- Seção "Renderizadores `${lumia-traceid}` e `${lumia-ownerid}`": para que servem, de onde lêem, o fato de o `OwnerId` vir de uma tag preenchida pelo host, e o que aparece quando não há `Activity` (vazio).
- Exemplo de `nlog.config` com `<extensions>` e um `JsonLayout` com os dois atributos, **sem** nenhum dado sensível no exemplo.
- Aviso: quem usa os renderizadores precisa declarar a extensão no `nlog.config`; sem isso o NLog não reconhece o nome.
- Histórico de versões: entrada `0.6.0` (aditiva, sem quebra) — o número final é do mantenedor (D8).
- Tabela do README da raiz: versão do pacote do `Logger`.

**Versão e publicação:**

- Subir a versão do pacote para a confirmada em D8.
- `dotnet test` na solution da Foundation inteira.
- Publicar (NuGet e/ou pasta local de pacotes, como nas versões anteriores) **antes** de o Flashcard consumir.
- No PR, sinalizar o impacto para Vault e Flashcard: **nenhum**; mudança só aditiva. O Vault só adota se quiser os campos nos seus logs.

**Critério de pronto:** testes verdes na solution da Foundation, documentação atualizada e pacote publicado.

---

## Passo 6: Fechamento (ADR 0020, no repositório Flashcard)

Em `Docs/adr/0020-observabilidade.md` (ou `docs/adr/`, conforme a pasta real do repositório):

- Na seção "Em aberto", marcar como **confirmado**: o renderizador do `Logger` existe (versão publicada) e funciona por `<extensions>` no `nlog.config`.
- Registrar as decisões D6, D7 e D8 nesta seção (nomes `lumia-traceid` e `lumia-ownerid`, tag `lumia.owner_id`, versão do pacote).
- **Não** alterar o status do ADR: restam itens em aberto (placeholders nomeados virando propriedades no JSON; `Activity` nas ações das ilhas; `AddNLog()` sem duplicar saída), tratados nas specs 0004 e 0005.

**Critério de pronto:** ADR atualizado e commitado.

---

## 6. Validação ponta a ponta

1. `dotnet test` no projeto de teste do `Logger`, depois na solution da Foundation: tudo verde.
2. Conferir que `git diff` mostra **apenas** arquivos novos (renderizadores, constante, testes, docs) e alteração de versão e de README: nenhum arquivo existente do `Logger` com mudança de código.
3. Num projeto descartável (console) que referencie o pacote novo: configurar um `nlog.config` com `<extensions>` e `JsonLayout`, abrir uma `Activity` com a tag `lumia.owner_id`, logar uma mensagem e conferir o JSON no arquivo ou console. Não commitar esse projeto.
4. Repetir o item 3 **sem** a linha de `<extensions>`: o NLog deve reclamar do renderizador desconhecido (comportamento esperado, e é o que o teste negativo cobre).

## 7. Riscos e pontos de atenção

- **`Activity.Current` pode ser nulo em produção.** O ASP.NET Core só cria a `Activity` da requisição se houver ouvinte ou se o log estiver habilitado. Isso é verificado no spike do host (spec 0004, e na Presentation, spec 0005), com o teste "o `TraceId` na tela de erro é o mesmo do log da API" do ADR 0019. Nesta spec, `Activity` ausente apenas gera campo vazio.
- **Custo por linha de log:** os renderizadores só leem propriedades e percorrem uma cadeia curta de `Activity`; sem alocação além do texto escrito.
- **Dependência do NLog:** se uma versão futura do NLog mudar o carregamento de extensões, o teste de registro (4.3) falha antes de chegar a produção.
- **Tag como superfície de entrada:** aceitar só `Guid` (seção 4, item 4) elimina injeção de conteúdo por esse caminho.
- **Mesma biblioteca, vários consumidores:** o Vault não adota nada automaticamente (aditivo), mas deve atualizar o guia da Foundation com a seção nova.

## 8. Fora de escopo

- O middleware que grava a tag `lumia.owner_id` na `Activity` da requisição (spec 0004).
- `AddNLog()`, o `nlog.config` do Flashcard e a correção das falhas do `nlog.config` do Vault (specs 0004 e 0005).
- `ActivityListener` e `ActivitySource` na `Presentation`, e a propagação do `traceparent` nas chamadas à API (spec 0005 e spike do ADR 0020).
- Qualquer mudança em `ILoggerManager`, em escopos de log ou em propriedades nomeadas.
- OpenTelemetry (fora da v1, ADR 0020).
