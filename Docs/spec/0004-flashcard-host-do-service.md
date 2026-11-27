# Especificação técnica: Host do `Service` (Flashcard)

- Data: 2026-11-27
- Status: rascunho (aguarda confirmação dos itens D9 a D13; ver seção 2)
- ADRs relacionados: 0006, 0009, 0010, 0013 (infraestrutura é a spec seguinte), 0017, 0018, 0019, 0020, 0021
- Repositórios afetados: **Flashcard** (`Service`, `Persistence`, `Test.Suporte`, `Service.Test`). A `LumiaFoundation` **não** é alterada (consome o `Logger` 0.6.0 da spec 0002).
- Depende das specs 0001 (esqueleto), 0002 (Logger 0.6.0 **publicado**) e 0003 (`Test.Suporte` com PostgreSQL).

> Este documento é uma especificação para outro dev implementar. Não contém código de implementação: descreve o que fazer em cada classe, a ordem, os testes e a documentação.
> Todo código, comentário, commit e documentação em português. Nomes de tipos e membros seguem o padrão do repositório.

---

## 1. Resumo

Monta o `Program.cs` do `Service` do Flashcard com os mesmos blocos do `Program.cs` do Vault (que serviu de referência), **aplicando o que os ADRs decidiram de diferente**: NLog ligado ao framework (`AddNLog()`), `nlog.config` corrigido com `TraceId` e `OwnerId`, pipeline sem HTTPS, sem HSTS e sem cabeçalhos encaminhados, tratadores de exceção, health checks (liveness e readiness com o banco), configuração obrigatória validada na inicialização e o middleware que grava a tag `lumia.owner_id` na `Activity` da requisição. Cria também a **Persistence mínima** (`DbContext` vazio e `AddPersistence`) e a **fábrica da API** para os testes de integração.

**Fora desta spec, de propósito:** autenticação e JWT (fase 2, spec própria, ADR 0014), o limitador de taxa (spec seguinte, 0005) e qualquer controller de negócio. O host desta spec expõe só os health checks (e a documentação OpenAPI em desenvolvimento).

## 2. Decisões aprovadas

| Item | Decisão | Origem |
|---|---|---|
| D1 | O `Service` é o host da API e a raiz de composição (Application + Persistence + Foundation) | ADR 0002, 0012 |
| D2 | `AddNLog()` nos hosts, removendo os provedores padrão; níveis controlados pelo NLog; `Microsoft.*` e `System.*` em `Warning` (exceto `Microsoft.Hosting.Lifetime`, em `Information`) | ADR 0020 |
| D3 | `nlog.config` do Vault com as quatro correções do ADR 0020: sem alvo de texto sem regra; sem propriedades duplicadas; `Information` em produção e `Debug` só no desenvolvimento; arquivo no projeto host | ADR 0020 |
| D4 | Cada linha de log leva `traceId` e `ownerId` (renderizadores `${lumia-traceid}` e `${lumia-ownerid}` da spec 0002); `ILoggerManager` não muda | ADR 0020, spec 0002 |
| D5 | Middleware após a autenticação grava a tag `lumia.owner_id` (constante do Logger) na `Activity` da requisição | ADR 0020, aprovado pelo usuário em 2026-11-23 |
| D6 | HTTP somente: sem `UseHttpsRedirection`, sem `UseHsts`, sem certificado, sem CSP de site, sem cabeçalhos adicionais | ADR 0021 |
| D7 | Cabeçalhos encaminhados **não** habilitados (nenhum `UseForwardedHeaders`) | ADR 0013, item 6 |
| D8 | Exceções: `AddExceptionHandlers()`, `AddValidationFilters()` e `SuppressModelStateInvalidFilter`, como no Vault; mapeamento 404 / 422 / 422 / 500 e `ErrorDetails` | ADR 0018 |
| D9 | Dependência necessária pelo ADR 0006: `Npgsql.EntityFrameworkCore.PostgreSQL`, só no `Persistence`, na versão `10.x` compatível com o EF Core que o `EFRepository` traz | **aguarda aprovação explícita** (dependência nova) |
| D10 | A API não tem `CORS`: o navegador nunca fala com a API (ADRs 0007 e 0016), só a Presentation, servidor a servidor. O Vault tem `ConfigureCors`/`UseCors`; o Flashcard **não** | **aguarda confirmação** |
| D11 | `UseStaticFiles` fora (a API não serve arquivos) e documentação OpenAPI (`MapOpenApiDevTools`) **somente em `Development`** | **aguarda confirmação** |
| D12 | Connection string com chave `ConnectionStrings:Flashcard`, lida do ambiente (`ConnectionStrings__Flashcard`); **nunca** no `appsettings`; ausente ou vazia falha na inicialização com mensagem que diz qual chave está errada | ADR 0009; **o nome da chave aguarda confirmação** |
| D13 | `public partial class Program` no `Service` para a fábrica de testes (`WebApplicationFactory`) | ADR 0019; **aguarda confirmação** |
| D14 | Fábrica da API (`FlashcardApiFactory`) fica no `Test.Suporte` (que passa a referenciar o `Service` e o pacote `Microsoft.AspNetCore.Mvc.Testing`, já aprovado) | ADR 0019, item 8 |
| D15 | Autenticação, `UseAuthentication`/`UseAuthorization` e JWT **não** entram aqui (fase 2); o middleware do dono fica no ponto onde a autenticação entrará | ADR 0022 |

## 3. Verificações já feitas (não repetir)

Vindas do `Program.cs`, do `nlog.config` do Vault e do `Service.csproj` do Flashcard enviados pelo mantenedor em 2026-11-27:

- O Vault já usa os namespaces `LumiaFoundation.AspNetCore.Api.*` (`Commons.Extensions`, `ExceptionHandlers`), ou seja, **o pacote `AspNetCore.Api`**, e não o legado. O ADR 0018 dizia o contrário; a equivalência é considerada resolvida e o ADR é corrigido no Passo 8.
- `IDateTimeProvider` e `UtcDateTimeProvider` estão em `LumiaFoundation.Core.TimeService` (relevante para o `RelogioFixo` da spec 0003).
- Os namespaces da Foundation são `LumiaFoundation.*` (sem ponto), enquanto os **ids dos pacotes** são `Lumia.Foundation.*`. O **nome do assembly** do Logger (usado em `<extensions>` do `nlog.config`) provavelmente é `LumiaFoundation.Logger`, e **não** `Lumia.Foundation.Logger`: o Passo 0 confirma.
- O `Program.cs` do Vault carrega o `nlog.config` por `LoggerManager.LoadConfigurationFromFile(...)` e chama `ConfigureLoggerService()`; **não** chama `AddNLog()`.
- O `Program.cs` do Vault tem `UseHsts()`, `UseForwardedHeaders(All)`, `UseStaticFiles()`, `ConfigureCors`/`UseCors` e `MapOpenApiDevTools()` sem condição de ambiente. Os quatro primeiros **não** valem no Flashcard (D6, D7, D10, D11).
- O `nlog.config` do Vault: alvo de texto `logfile` declarado e sem regra; `includeEventProperties="true"` junto do atributo `properties` (`${all-event-properties}`); regra `Debug` fixa para o arquivo; sem filtro por categoria.
- O `Service.csproj` do Flashcard já referencia `Lumia.Foundation.AspNetCore.Api` 0.32.1 (isso resolveu o alerta `NU1903` do `Microsoft.OpenApi`, confirmado pelo mantenedor), o `System.IdentityModel.Tokens.Jwt` 8.23.0 e os projetos `Application` e `Persistence`, e tem a `InterceptorsNamespaces` do gerador do OpenAPI. **Não** referencia o pacote do Logger diretamente.

**Não verificado por mim** (o código da Foundation não estava disponível): assinaturas exatas de `ConfigureHealthChecks`, `MapLumiaHealthChecks` (rotas e política de liveness e readiness), `ConfigureOpenApi`, `AddExceptionHandlers`, o tipo `ErrorDetails` e seus campos, como o `GetCurrentUserId` lê o identificador do usuário (qual claim) e como o `EFRepository` oferece a verificação de saúde do banco. O Passo 0 levanta tudo.

## 4. Atenção antes de começar

1. **Ordem das chamadas importa.** O `nlog.config` é carregado **antes** de `AddNLog()`, e o `AddNLog()` vem **depois** de `ClearProviders()`. Inverter isso duplica ou perde linhas de log.
2. **Filtro de nível do framework.** O `Microsoft.Extensions.Logging` aplica seus próprios filtros (seção `Logging` do `appsettings`) **antes** de entregar ao NLog. Para o NLog ser a única autoridade de nível: remover a seção `Logging` do `appsettings.json` do template e chamar `SetMinimumLevel` com `Trace` (ver Passo 3).
3. **Nada de segredo em arquivo.** A senha do banco vem do ambiente. `appsettings*.json` não pode conter `Password=`. O teste do Passo 6 procura isso.
4. **`UseAuthentication` não é chamado nesta spec.** Chamar sem registrar autenticação derruba o host na inicialização. O comentário no `Program.cs` marca o ponto exato onde a fase 2 o insere, e o middleware do dono fica logo depois desse ponto.
5. **A tag do dono só aceita `Guid`** (spec 0002). O middleware nunca grava texto livre.
6. **O middleware não pode lançar exceção** nem alterar a resposta. Se não conseguir extrair o dono, não grava a tag e segue.
7. **Testes de log com NLog usam configuração global.** As classes de teste de log ficam todas numa mesma `Collection` do xUnit (sem execução em paralelo entre elas) e desmontam a configuração ao final.
8. **Dependência nova (D9)** só entra depois do aval do mantenedor. Se ainda não houver aval, **parar** no Passo 1.
9. **O Logger 0.6.0 precisa estar publicado** (spec 0002). Antes disso, o passo 1 falha na restauração do pacote.
10. **Nenhum projeto de produção referencia `Test.Suporte`** (regra R3 do teste de arquitetura). O `Test.Suporte` passa a referenciar o `Service`, e isso é permitido pelas regras.

## 5. Ordem de implementação

Cada passo termina com build e testes verdes antes do seguinte.

| # | Passo | Projeto |
|---|---|---|
| 0 | Inventário da Foundation e do estado atual | (sem alterações) |
| 1 | Pacotes e referências | `Service`, `Persistence`, `Test.Suporte` |
| 2 | Persistence mínima | `Persistence` |
| 3 | `Program.cs`, `appsettings` e `nlog.config` | `Service` |
| 4 | Middleware do dono | `Service` |
| 5 | Fábrica da API e controller de teste | `Test.Suporte`, `Service.Test` |
| 6 | Testes de integração do host | `Service.Test` |
| 7 | READMEs | `Service`, `Persistence`, `Test.Suporte`, raiz |
| 8 | Fechamento (ADRs 0018 e 0020, status) | docs |

---

## Passo 0: Inventário da Foundation e do estado atual

**Não alterar nada.** Responder e anexar ao PR:

1. **Logger 0.6.0** publicado e restaurável? (`dotnet restore` num projeto descartável com a referência.) Nome do **assembly** do Logger.
2. Assinatura e efeito de `ConfigureHealthChecks()` e de `MapLumiaHealthChecks()`: quais rotas expõe (liveness e readiness), se aceita configurar a rota, se devolve algo que permita marcar o endpoint como isento de limite de taxa (usado na spec 0005), e **como registrar a verificação do banco** (extensão do `EFRepository` com a tag de readiness, ou `AddDbContextCheck` equivalente). Se nenhuma extensão pronta existir, **parar e perguntar**.
3. Assinatura de `ConfigureOpenApi(string, string)`, `AddExceptionHandlers()`, `AddValidationFilters()`.
4. Tipo `ErrorDetails` (namespace, campos e nomes serializados em JSON), usado nos testes.
5. Como o `GetCurrentUserId` obtém o identificador do usuário (tipo de claim) e o que ele devolve se faltar a claim. O middleware do Passo 4 deve ler o dono **do mesmo jeito**.
6. Versão do **EF Core** resolvida no `Persistence` hoje (`dotnet list Persistence package --include-transitive`), para escolher a versão `10.x` correta do provedor Npgsql (D9).
7. Conteúdo atual de `Service/Program.cs`, `Service/appsettings*.json`, `Service/Properties/launchSettings.json` e `Persistence` (extensões, classes existentes).
8. Existência de um `.gitignore` que cubra `logs/`, `internal_logs/` e arquivos `.env`.

**Critério de pronto:** respostas anexadas; divergências em relação às seções 3 e 4 listadas, sem corrigir.

---

## Passo 1: Pacotes e referências

Só depois do aval do D9.

- **`Service/Service.csproj`:** acrescentar `PackageReference` de `Lumia.Foundation.Logger` na versão **0.6.0** (referência direta, para fixar o mínimo, mesmo que o pacote já venha transitivo). **Manter** o restante (`AspNetCore.Api` 0.32.1, `System.IdentityModel.Tokens.Jwt`, `InterceptorsNamespaces`, as duas `ProjectReference`).
- **`Persistence/Persistence.csproj`:** acrescentar `PackageReference` de `Npgsql.EntityFrameworkCore.PostgreSQL` na versão `10.x` escolhida no Passo 0, item 6. Nenhum outro pacote novo (o `Microsoft.EntityFrameworkCore.Design` entra na primeira migration, fase 2, e **não** aqui).
- **`Test.Suporte/Test.Suporte.csproj`:** acrescentar `PackageReference` de `Microsoft.AspNetCore.Mvc.Testing` (versão `10.x`, compatível com o ASP.NET Core do projeto) e `ProjectReference` para `Service`. **Não** referenciar a `Presentation` (R12).
- Conferir que o teste de arquitetura da spec 0001 continua verde.

**Critério de pronto:** `dotnet restore` e `dotnet build` verdes; teste de arquitetura verde.

---

## Passo 2: Persistence mínima

Em `Persistence/`, nas pastas e namespaces dos projetos existentes:

- **`FlashcardDbContext`:** classe que herda `DbContext`, **sem** `DbSet` e **sem** mapeamento. É o contexto de domínio (o do Identity é outro, fase 2, ADR 0006). Comentário em português dizendo que as entidades entram a partir da fase 3.
- **`AddPersistence(IServiceCollection, IConfiguration)`** (classe de extensão na pasta `Extensions`, como no Vault):
  - lê `ConnectionStrings:Flashcard`; ausente, nula, vazia ou só espaços → lança `InvalidOperationException` com a mensagem em português: "A connection string 'Flashcard' não foi configurada. Defina a variável de ambiente ConnectionStrings__Flashcard." (D12). Não loga a connection string nem parte dela;
  - registra o `FlashcardDbContext` com o provedor PostgreSQL e a connection string;
  - **não** liga `EnableSensitiveDataLogging` nem `EnableDetailedErrors` (ADR 0020);
  - registra a **verificação de saúde do banco** do jeito levantado no Passo 0, item 2, com a tag que o `MapLumiaHealthChecks` usa para a readiness.
- **Não** registrar `RepositoryManager`, repositórios nem `SaveAsync` virtual: isso é fase 3.

**Critério de pronto:** compila; os testes do Passo 6 (grupos de inicialização e de saúde) passam.

---

## Passo 3: `Program.cs`, `appsettings` e `nlog.config`

### 3.1 `Service/Program.cs` — blocos, na ordem

Usar as mesmas seções (`#region`) do `Program.cs` do Vault, com nomes em português, e **esta ordem de registro** de serviços:

1. `var configuration = builder.Configuration;` (o comentário do Vault sobre variáveis de ambiente continua válido; manter e adaptar para a connection string).
2. **Persistence:** `AddPersistence(configuration)`.
3. **Aplicação:** `AddServicesFromAssembly` apontando para o assembly da Application, com um tipo marcador **que exista na Application hoje**. Se a Application ainda não tem nenhum Command/Query, **não** chamar o método agora; deixar a região com um comentário "entra na fase 3" (não inventar tipo). Registrar `IDateTimeProvider` → `UtcDateTimeProvider` (escopo igual ao do Vault).
4. **Logs:**
   - Carregar o `nlog.config` com `LoggerManager.LoadConfigurationFromFile` apontando para `ContentRootPath/nlog.config`, como no Vault;
   - em `Development`, ajustar a **variável** `minLevel` do NLog para `Debug` (Passo 3.3);
   - `ConfigureLoggerService()`;
   - `builder.Logging.ClearProviders()`; `builder.Logging.SetMinimumLevel(LogLevel.Trace)`; `builder.Logging.AddNLog()` — nessa ordem (seção 4, itens 1 e 2).
5. **Saúde:** `ConfigureHealthChecks()`.
6. **Controllers:** `AddControllers()` e `AddValidationFilters()`.
7. **Exceções:** `Configure<ApiBehaviorOptions>` com `SuppressModelStateInvalidFilter = true` e `AddExceptionHandlers()`.
8. **OpenAPI:** `ConfigureOpenApi("Flashcard API", "v1")` e `AddOpenApi` com o transformador de documento (título "Flashcard API", versão "v1"), como no Vault.

**Não incluir** (decididos nos ADRs 0013, 0021 e D10 a D15): `ConfigureIdentity`, `ConfigureIdentityServiceManager`, `ConfigureAppSettingsReader`, `ConfigureJWT`, `AddAuthentication`, `ConfigureCors`.

### 3.2 Pipeline (depois de `builder.Build()`), na ordem

1. `UseExceptionHandler(opt => { })`
2. *(ponto reservado para a autenticação, fase 2: `UseAuthentication`)* — comentário no código
3. Middleware do dono (Passo 4)
4. *(ponto reservado: `UseAuthorization`, fase 2, e o limitador de taxa, spec 0005)* — comentário
5. `MapControllers()`
6. `MapLumiaHealthChecks()`
7. `if (app.Environment.IsDevelopment())` → `MapOpenApiDevTools()`

**Não incluir:** `UseHsts`, `UseHttpsRedirection`, `UseForwardedHeaders`, `UseStaticFiles`, `UseCors`.

No final do arquivo, a declaração `public partial class Program` (D13).

### 3.3 `Service/nlog.config`

Substitui o arquivo atual (ou cria). Conteúdo, descrito em itens:

- **Raiz:** namespaces padrão do NLog, `autoReload="true"` (como no Vault), `internalLogLevel="Warn"` e `internalLogFile` dentro de `${basedir}/logs/internal/internallog.txt` (para o volume do compose cobrir também o log interno).
- **`<extensions>`:** uma entrada `add` com o **assembly do Logger** (Passo 0, item 1), para registrar `lumia-traceid` e `lumia-ownerid`.
- **Variável:** `minLevel`, valor padrão `Info`.
- **Alvos (apenas dois):**
  - `jsonFile`: arquivo `${basedir}/logs/current.json`; arquivamento `${basedir}/logs/archive/${shortdate}_log.{#}.json`; `archiveAboveSize` 10485760 (10 MB); `maxArchiveFiles` 30; `archiveNumbering` `Rolling`; `createDirs` `true`;
  - `console`: saída padrão.
  - **Ambos** com o mesmo `JsonLayout` com `includeEventProperties="true"` e **sem** o atributo `properties` com `${all-event-properties}`. Atributos do layout, nesta ordem: `time` (data e hora em **UTC**, ISO 8601), `level`, `logger` (`${logger}`), `message` (`${message}`), `traceId` (`${lumia-traceid}`), `ownerId` (`${lumia-ownerid}`), `exception` (`${exception:format=tostring}`).
  - **Sem** alvo de texto (`logfile`) declarado.
- **Regras, nesta ordem (ordem importa):**
  1. logger `Microsoft.Hosting.Lifetime`, nível mínimo `Info`, escreve nos dois alvos, **`final="true"`** (sem isso, os eventos `Warning` e acima seriam gravados duas vezes pela regra seguinte);
  2. loggers `Microsoft.*` e `System.*`, nível mínimo `Warning`, nos dois alvos, `final="true"`;
  3. logger `*`, nível mínimo vindo da variável `minLevel`, nos dois alvos.
- **Proibido:** nível `Debug` ou `Trace` escrito como literal em qualquer regra (o `Debug` só entra pela variável, em desenvolvimento).

Se a versão do NLog do pacote não aceitar a variável no atributo de nível mínimo da regra, **parar e perguntar** (não trocar de abordagem por conta própria).

### 3.4 `Service/appsettings.json` e `appsettings.Development.json`

- **Remover** a seção `Logging` do template (o NLog controla os níveis; seção 4, item 2).
- Manter `AllowedHosts`.
- **Nenhuma** connection string, senha ou segredo (seção 4, item 3). Em `Development`, a connection string vem de variável de ambiente ou de `dotnet user-secrets` do desenvolvedor.
- `launchSettings.json`: perfil `http` apenas, sem perfil `https` e sem `applicationUrl` com `https://` (ADR 0021).

### 3.5 `.gitignore`

Garantir que cubra `logs/`, `internal_logs/`, `.env` e `*.env` (e que **não** ignore um `.env.example`, que a spec 0008 cria).

**Critério de pronto:** o `Service` sobe (`dotnet run --project Service`) com `ConnectionStrings__Flashcard` definida num terminal e **falha com a mensagem do Passo 2** se não estiver definida; `GET` nas rotas de saúde responde; um arquivo `logs/current.json` aparece com linhas JSON válidas.

---

## Passo 4: Middleware do dono

Em `Service/Middlewares/` (criar a pasta se não existir):

- **Classe `DonoNoLogMiddleware`** e um método de extensão `UseDonoNoLog` (nomes em português; o dev pode ajustar ao padrão da Foundation do Passo 0).
- **Comportamento:** para cada requisição, se `HttpContext.User` está autenticado e traz a claim do dono (a mesma lida pelo `GetCurrentUserId`, Passo 0 item 5) com valor que converte em `Guid`, grava `Activity.Current.SetTag(<constante do Logger>, guid)`. Em seguida chama o próximo middleware **sempre**.
- Sem `Activity.Current`, sem usuário autenticado, sem claim ou com valor inválido: **não grava nada** e segue. Nunca lança exceção, nunca altera a resposta, nunca registra o valor da claim em log.
- O nome da tag vem **somente** da constante do Logger (spec 0002), nunca de literal.

**Critério de pronto:** compila; testes do Passo 6 (grupo do middleware) passam.

---

## Passo 5: Fábrica da API e controller de teste

### 5.1 `Test.Suporte/Api/FlashcardApiFactory`

Classe pública que herda `WebApplicationFactory` do `Program` do `Service` (D13). Comportamento:

- Recebe, no construtor, a connection string a usar (vinda da `PostgreSqlFixture`) e **opcionalmente** um conjunto de ajustes de configuração. Aplica-os com configuração em memória, sem ler variável de ambiente do desenvolvedor.
- Ambiente `Testing` (nunca `Development` nem `Production`).
- Permite registrar serviços de teste (`ConfigureTestServices`) para os controllers de teste, o `ActivityListener` e o principal falso do Passo 6.
- Registra um **`ActivityListener`** (`ShouldListenTo` verdadeiro, `Sample` com `AllData`) e o remove ao descartar a fábrica, para existir `Activity` por requisição nos testes.
- Provê um **alvo de log em memória** do NLog (`MemoryTarget`) acoplado à configuração carregada, para os testes lerem as linhas escritas, e o remove ao descartar.
- Garante `LogManager.Shutdown()` ao descartar.

### 5.2 `Service.Test/Suporte/`

- **Definição da coleção** do PostgreSQL (a regra do xUnit está na spec 0003, seção 4, item 2) e uma coleção `Log` para os testes que alteram a configuração do NLog.
- **`ErrosDeTesteController`:** controller **só dos testes** (no assembly do `Service.Test`), registrado na fábrica por `AddApplicationPart`. Rotas sob `/teste/`:
  - uma para cada exceção: `EntityNotFoundException` (com mensagem conhecida), `EntityInUseException`, `CommandValidationException` e uma exceção genérica com mensagem identificável (por exemplo, contendo a palavra "segredo-interno");
  - uma que loga uma mensagem com `ILoggerManager` e outra com `ILogger<T>` (framework);
  - uma que devolve o esquema e o IP remoto vistos pela requisição;
  - uma que devolve o valor atual da tag do dono na `Activity` corrente.
- **Filtro de inicialização de teste** (`IStartupFilter`) que, por requisição, define `HttpContext.User` a partir de cabeçalhos de teste (por exemplo, um cabeçalho com o `Guid` do dono) **antes** do middleware do dono. Só existe nos testes.

**Critério de pronto:** compila; a fábrica sobe o host com a connection string da fixture.

---

## Passo 6: Testes de integração do host

Todos em `Service.Test`, nomes `<Membro>_When<Condicao>_<Resultado>`. Usam a `FlashcardApiFactory`. Os que dependem do banco usam a coleção do PostgreSQL (exigem Docker). Em todos os de log, a coleção `Log`.

### 6.1 `InicializacaoDoHostTests`

- `Host_WhenConnectionStringAusente_FalhaNaInicializacaoComMensagemClara` (a mensagem cita `ConnectionStrings__Flashcard` e **não** contém o texto de nenhuma connection string)
- `Host_WhenConnectionStringVazia_FalhaNaInicializacao`
- `Host_WhenConnectionStringValida_Inicia`
- `Configuracao_WhenArquivosAppsettings_NaoContemSenhaNemConnectionString` (varre `appsettings*.json` do `Service` por `Password=` e `ConnectionStrings`)

### 6.2 `PipelineSemHttpsTests`

- `Get_WhenRequisicaoHttp_Retorna200SemRedirecionamentoESemHsts` (rota de saúde; sem `Location` e sem `Strict-Transport-Security`)
- `Get_WhenCabecalhosEncaminhadosEnviados_NaoAlteraEsquemaNemIp` (cabeçalhos `X-Forwarded-For` e `X-Forwarded-Proto` presentes; o esquema continua `http` e o IP não vira o enviado)
- `Options_WhenPreflightDeOutraOrigem_NaoRetornaCabecalhosDeCors`
- `Respostas_WhenQualquerRotaDoSite_NaoTemContentSecurityPolicy`

### 6.3 `TratamentoDeExcecoesTests`

- `Get_WhenEntityNotFound_Retorna404ComMensagemDoErro`
- `Get_WhenEntityInUse_Retorna422`
- `Get_WhenCommandValidation_Retorna422`
- `Get_WhenExcecaoNaoMapeada_Retorna500SemDetalhesInternos` (o corpo é `ErrorDetails`, **não** contém "segredo-interno" nem trecho de pilha como " at ")
- `Get_WhenErro_RespondeComJsonDoTipoErrorDetails`

### 6.4 `SaudeDoHostTests`

- `Liveness_WhenProcessoVivo_Retorna200`
- `Readiness_WhenBancoDisponivel_Retorna200` (Docker)
- `Readiness_WhenBancoIndisponivel_Retorna503` (connection string apontando para `127.0.0.1` em uma porta sem serviço, com tempo limite curto)
- `Saude_WhenResposta_ContemApenasOEstado` (corpo exatamente igual a `Healthy` ou `Unhealthy`, texto puro)

### 6.5 `LogEstruturadoTests`

- `Providers_WhenHostIniciado_NaoTemProvedorDeConsoleDoFramework` (os `ILoggerProvider` registrados não incluem o do console padrão; incluem o do NLog)
- `Log_WhenFrameworkLoga_ChegaAoNLogUmaUnicaVez`
- `Log_WhenILoggerManagerLoga_ChegaAoNLog`
- `Log_WhenPlaceholderNomeadoNaMensagem_AparecePropriedadeNoJson` (confirma o item "Em aberto 1" do ADR 0020; se **não** aparecer, registrar o resultado e perguntar)
- `Log_WhenRequisicaoComDono_LinhaTemTraceIdEOwnerId` (a linha escrita durante uma requisição com o cabeçalho de teste do dono traz o `TraceId` da `Activity` e o `Guid` do dono)
- `Log_WhenRequisicaoSemDono_OwnerIdVazio`
- `NlogConfig_WhenCarregado_TemSomenteOsAlvosJsonEConsole`
- `NlogConfig_WhenCarregado_NaoTemNivelDebugOuTraceLiteral`
- `NlogConfig_WhenCarregado_VariavelMinLevelPadraoEInfo`
- `NlogConfig_WhenRegraDoHostingLifetime_EhFinal`
- `NlogConfig_WhenAmbienteDevelopment_EscreveDebug` e `NlogConfig_WhenAmbienteTesting_NaoEscreveDebug`
- `Log_WhenArquivoJson_EhJsonValidoPorLinha` (cada linha do alvo de arquivo, quando lido do disco numa pasta temporária, interpreta como JSON)

### 6.6 `DonoNoLogMiddlewareTests`

- `Invoke_WhenUsuarioAutenticadoComDonoValido_GravaATagNaActivity`
- `Invoke_WhenSemUsuario_NaoGravaATag`
- `Invoke_WhenClaimComValorInvalido_NaoGravaATagENaoLanca` (inclui valor com quebra de linha)
- `Invoke_WhenSemActivity_SegueSemErro`
- `Invoke_WhenSempre_ChamaOProximoMiddleware`

Testes do ADR 0019 que **não** cabem nesta spec, porque dependem de fluxos que ainda não existem: "log sem token, senha e texto de cartão" (fase 2 e 5), isolamento entre donos, 401 de ponta a ponta e o limite de taxa (spec 0005).

**Critério de pronto:** `dotnet test Service.Test` verde com o Docker em execução; teste de arquitetura da spec 0001 verde.

---

## Passo 7: READMEs

- `Service/README.md`: propósito, como rodar localmente (definir `ConnectionStrings__Flashcard` no terminal com um **valor de exemplo sem senha real**, por exemplo `Host=localhost;Port=5432;Database=flashcard;Username=flashcard;Password=<defina>`), onde ficam os logs (`logs/current.json`) e o log interno, rotas de saúde, OpenAPI só em `Development`, a premissa de implantação (HTTP, sem proxy, rede interna; ADR 0021) e o que **não** está nesta fase (autenticação e limite de taxa).
- `Persistence/README.md`: o contexto vazio, a chave da connection string e que o provedor é o Npgsql.
- `Test.Suporte/README.md`: acrescentar a fábrica da API e o que ela entrega.
- README da raiz: seção "Como rodar localmente" com o `dotnet run` do `Service` e a variável de ambiente.

---

## Passo 8: Fechamento

- **ADR 0018:** corrigir o contexto: o Vault já usa o `AspNetCore.Api` (namespaces), e a equivalência com o pacote legado deixa de ser pendência.
- **ADR 0020:** na seção "Em aberto", registrar o resultado dos itens 1 (placeholders nomeados como propriedades) e 3 (`AddNLog()` sem duplicar); manter o item 2 (`Activity` nas ilhas) em aberto até a spec da Presentation.
- **ADR 0006:** registrar o provedor `Npgsql.EntityFrameworkCore.PostgreSQL` e a versão adotada (D9), se aprovado.
- Marcar o **Status** desta spec como "implementada (data)".
- Commits em português, um por passo.

---

## 6. Validação ponta a ponta

1. `dotnet build` na solution: sem erros nem avisos novos (inclusive sem `NU1903`).
2. `dotnet test` na solution, com o Docker em execução: tudo verde (arquitetura, `Persistence.Test`, `Application.Test`, `Service.Test`).
3. `ConnectionStrings__Flashcard='Host=localhost;Port=5432;Database=flashcard;Username=...;Password=...' dotnet run --project Service` com um PostgreSQL 17 local (ou o contêiner de `docker run`): sobe, escuta em HTTP, rotas de saúde respondem, `logs/current.json` e o console mostram o mesmo JSON, **uma linha por evento**.
4. Sem a variável: o processo termina na inicialização com a mensagem em português.
5. `curl -i` na rota de saúde: `200`, sem `Location`, sem `Strict-Transport-Security`, sem `Content-Security-Policy`.
6. Com o banco parado, a rota de readiness devolve `503` e a de liveness continua `200`.
7. `git grep -n -i "password=" -- '*.json' '*.cs'`: nenhum resultado fora de comentário de exemplo.
8. `dotnet list Service package --vulnerable --include-transitive`: sem pacote vulnerável.

## 7. Riscos e pontos de atenção

- **`Activity.Current` em produção.** O ASP.NET Core só cria a `Activity` da requisição se houver ouvinte ou se o log estiver habilitado. Os testes registram um `ActivityListener`; em produção, com o NLog ligado ao framework (`AddNLog()`), o log habilitado cria a `Activity`. Se o `traceId` aparecer vazio no log real, registrar e perguntar (a solução seria registrar um `ActivityListener` no host).
- **Nível mínimo do NLog por variável.** Depende de o NLog da versão do pacote aceitar a variável em `minlevel`. O teste de ambiente cobre isso.
- **`AddNLog()` junto de `ConfigureLoggerService()`:** duplicação já foi tratada por `ClearProviders`; o teste de provedores a garante.
- **Readiness depende do `EFRepository`.** Se o `EFRepository` 0.14.1 não oferecer a verificação, o Passo 0 já para o trabalho.
- **`DbContext` vazio.** Sem `DbSet`, o EF aceita; sem migration, o banco fica sem esquema. A readiness só prova a conexão, não o esquema (decisão F2-A).
- **Persistence referencia um provedor de terceiros.** É o único pacote novo desta spec, previsto no ADR 0006; a versão deve acompanhar o EF Core do `EFRepository`.
- **Sem autenticação nesta fase,** qualquer rota do `Service` seria pública. Como só há rotas de saúde e a API não é publicada fora da rede interna (ADR 0013), o risco é nulo hoje; **não** adicionar controller de negócio antes da fase 2.

## 8. Fora de escopo

- Autenticação, JWT, Identity, `UseAuthentication` e `UseAuthorization` (fase 2).
- Limitador de taxa, política por dono, upload e o teto global (spec 0005).
- Dockerfile e `docker compose` (spec 0008).
- Host da `Presentation` (spec 0006) e o limitador dela (spec 0007).
- `RepositoryManager`, repositórios e entidades (fase 3).
- Endpoint de limites de texto e de imagem (fases 4 e 7).
