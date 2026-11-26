# Especificação técnica: Esqueleto da solution e teste de arquitetura (Flashcard)

- Data: 2026-11-24
- Status: aprovada para implementação (2026-11-24)
- ADRs relacionados: 0002, 0008, 0012, 0015, 0019, 0020, 0022 (fase 1)
- Repositórios afetados: **Flashcard** (todos os projetos). A `LumiaFoundation` **não** é alterada nesta spec.

> Este documento é uma especificação para outro dev implementar. Não contém código de implementação: descreve o que fazer em cada classe, a ordem, os testes e a documentação.
> Todo código, comentário, commit e documentação em português. Nomes de tipos e membros seguem o padrão do repositório.

---

## 1. Resumo

A solution `Flashcard` já foi criada e versionada. Esta spec garante que ela tenha **os 5 projetos de produção, os 5 projetos de teste e o projeto de suporte** com as **referências permitidas pelo ADR 0002**, e cria o **teste de arquitetura** que falha quando alguém violar essas regras. Não há regra de negócio, banco, autenticação nem tela nesta spec. É a primeira de uma série da fase 1 (ver seção 8).

## 2. Decisões aprovadas

| Item | Decisão | Origem |
|---|---|---|
| D1 | Projetos de produção: `Flashcard.Domain`, `Flashcard.Application`, `Flashcard.Persistence`, `Flashcard.Service` (host da API) e `Flashcard.Presentation` (Blazor Web App) | ADR 0002 |
| D2 | Um projeto de teste por projeto de produção, com sufixo `.Test`, mais `Flashcard.Test.Suporte` (compartilhado, nunca referenciado por código de produção) | ADR 0002, 0019 |
| D3 | Referências entre projetos de produção: Domain → nenhuma; Application → Domain; Persistence → Application e Domain; Service → Application e Persistence; Presentation → **nenhum** projeto interno | ADR 0002, 0012 |
| D4 | Domain referencia só o pacote Core da Foundation; Application só o Core e o Domain (sem EF, sem ASP.NET, sem Markdig, sem Foundation de infraestrutura) | ADR 0002, 0012, 0015 |
| D5 | Presentation usa pacotes da Foundation e, de terceiros, somente os aprovados em ADR (hoje: Markdig, HtmlSanitizer e NLog) | ADR 0002, 0015, 0020 |
| D6 | Presentation nunca referencia `EFRepository` nem `Auth` (o Web nunca acessa o banco; só a Api) | ADR 0002, 0008 |
| D7 | O teste de arquitetura vive em `Flashcard.Service.Test`, lê os arquivos `.csproj` (sem biblioteca nova) e roda com `dotnet test` | ADR 0019 |
| D8 | Test projects não usam Moq, FluentAssertions, Respawn nem Playwright | ADR 0019 |
| D9 | Convenção de nomes dos testes: classe `<Tipo>Tests`; método `<Membro>_When<Condicao>_<Resultado>` | ADR 0019 |
| D10 | O `.editorconfig` existente vale para todos os projetos novos; não alterá-lo | usuário |
| D11 | Nenhuma dependência nova de terceiros além das já aprovadas (xUnit é a base de teste) | regras do projeto |
| D14 | Layout de pastas como no Vault: a solution fica na raiz e cada projeto numa subpasta **no mesmo nível dela**, com o nome da camada sem o prefixo da solution (`Domain`, `Application`, `Persistence`, `Service`, `Presentation`) e o projeto de teste ao lado, com sufixo `.Test` (`Domain.Test`, ..., `Presentation.Test`). Sem pasta `src/` | usuário |
| D16 | `Flashcard.Presentation` usa **somente interatividade Server** (circuito). O projeto `Presentation.Client` (WebAssembly) gerado pelo template é **removido**; não há projeto WebAssembly na solution | usuário (opção A, 2026-11-26), ADR 0007 e 0014 |
| D15 | Specs numeradas em sequência própria do Flashcard (`docs/specs/NNNN-flashcard-<assunto>.md`) | usuário |
| D12 | `Flashcard.Presentation.Test` **não** referencia `Flashcard.Test.Suporte` (ver seção 4, item 4) | aprovada pelo usuário |
| D13 | `Flashcard.Presentation` não referencia o pacote `Auth` da Foundation (D6), confirmado pelo usuário (o `Vault.Presentation` não a referencia) | aprovada pelo usuário |

## 3. Verificações já feitas (não repetir)

- As regras de dependência acima vêm dos ADRs 0002 e 0012 (que seguem o layout-alvo do Vault de 2026-11-13). Não foram conferidas contra o estado atual do repositório Flashcard, que **não pôde ser lido** durante a decisão. Por isso o Passo 0 é um inventário.
- Os nomes exatos dos pacotes da Foundation (`Lumia.Foundation.*` ou `LumiaFoundation.*`) aparecem escritos das duas formas nos documentos do Vault. **Copiar o nome real do `.csproj` do Vault**; esta spec usa `Lumia.Foundation.*` por convenção de leitura, e o prefixo fica numa constante única no teste (Passo 2).
- Versões mínimas da Foundation (guia atualizado): `Auth` 0.3.1 e `EFRepository` 0.14.1. Esta spec não sobe versões de pacote; só confere que as referências existentes respeitam o mínimo, quando existirem.

## 4. Atenção antes de começar

1. **Não recriar o que já existe.** A solution já foi criada. Qualquer projeto já presente fica onde está (mesma pasta, mesmo nome). Esta spec só cria o que falta e só corrige referências.
2. **Não criar `Directory.Build.props`, `Directory.Packages.props` nem alterar o `.editorconfig`** por conta própria. Se já existirem, seguir o que está lá. Se o dev achar que falta algum, **parar e propor** (é decisão de arquitetura).
3. **Projeto de teste vazio:** `dotnet test` num projeto sem testes termina com sucesso e aviso. É esperado nos Passos 1 a 2 para os projetos que ainda não têm teste.
4. **Por que `Presentation.Test` não referencia o Suporte (D12):** o `Flashcard.Test.Suporte` conterá a fábrica da API e o `FakeRepositoryManager` (ADR 0019), portanto referenciará Service, Persistence e Application. Se o `Presentation.Test` o referenciasse, os testes da Presentation passariam a enxergar tipos da Api, o que contradiz o desacoplamento Web/Api. Se algum auxiliar for necessário só ali (por exemplo, relógio fixo), duplicar o mínimo dentro do `Presentation.Test`.
5. **Violações encontradas no Passo 3 não se corrigem por atalho** (relaxar a regra, adicionar exceção). Registrar a violação e a causa, e **perguntar** antes de mudar o teste.
6. **Template Blazor:** `dotnet new blazor` com interatividade WebAssembly ou Auto gera o projeto extra `Presentation.Client`. O projeto deste repositório **não pode** tê-lo (D16). Se um dia for preciso recriar a Presentation, usar `dotnet new blazor --interactivity Server` (conferir as opções com `dotnet new blazor --help`) e confirmar que **nenhum projeto `.Client` foi gerado**.
7. **Teste lê arquivos do disco.** Ele precisa achar a raiz da solution a partir de qualquer pasta de execução (Visual Studio Code, terminal, bundle de testes). Ver o Passo 2.

## 5. Ordem de implementação

Cada passo termina com build e testes verdes antes do seguinte (exceto onde o critério diz outra coisa).

| # | Passo | Onde |
|---|---|---|
| 0 | Inventário do estado atual | Flashcard (sem alterações) |
| 1 | Projetos de teste e `Flashcard.Test.Suporte` | Flashcard |
| 2 | Motor das regras e testes sintéticos (devem passar) | `Flashcard.Service.Test` |
| 2B | Corrigir a Presentation: remover o `Presentation.Client` (D16) | Flashcard (`Presentation`) |
| 3 | Teste contra a solution real; corrigir referências até ficar verde | Flashcard |
| 4 | READMEs por projeto e README da solution | Flashcard |
| 5 | Fechamento e conferência dos ADRs no repositório | Flashcard (docs) |

---

## Passo 0: Inventário do estado atual

**Não alterar nada.** Produzir uma tabela (colar na descrição do PR) com, para cada `.csproj` existente na solution:

- nome e pasta;
- `Sdk` do projeto (`Microsoft.NET.Sdk` ou `Microsoft.NET.Sdk.Web`);
- `TargetFramework`;
- `ProjectReference`s;
- `PackageReference`s (nome e versão);
- `FrameworkReference`s.

Conferir também: extensão da solution (`.sln` ou `.slnx`), existência de `.editorconfig`, `Directory.Build.props` e `Directory.Packages.props`, e se a pasta `docs/adr` tem os ADRs 0001 a 0022 e o `README.md` com o índice.

**Critério de pronto:** tabela anexada. Divergências em relação à seção 2 listadas, **sem corrigir ainda**.

---

## Passo 1: Projetos de teste e `Flashcard.Test.Suporte`

Criar apenas os que faltam, seguindo o layout do D14: cada projeto de teste numa subpasta na raiz, ao lado da pasta do projeto de produção correspondente (`Domain.Test` ao lado de `Domain`, e assim por diante). O projeto compartilhado fica na pasta `Test.Suporte`. O **arquivo `.csproj` tem o mesmo nome da pasta**, como no Vault (confirmado pelo usuário): `Presentation/Presentation.csproj`, `Presentation.Test/Presentation.Test.csproj`, `Test.Suporte/Test.Suporte.csproj`. O nome do assembly e o namespace raiz continuam `Flashcard.<Camada>` (por exemplo `Flashcard.Presentation`), definidos por `AssemblyName` e `RootNamespace` **como nos projetos existentes** (conferir no inventário do Passo 0 e repetir o mesmo padrão). Os nomes `Flashcard.Domain.Test` etc. usados nesta spec são os nomes lógicos (assembly). Se o inventário divergir do esperado, **perguntar** antes de criar.

| Projeto | Tipo | Referencia (projetos) |
|---|---|---|
| `Flashcard.Domain.Test` | xUnit | `Flashcard.Domain`, `Flashcard.Test.Suporte` |
| `Flashcard.Application.Test` | xUnit | `Flashcard.Application`, `Flashcard.Domain`, `Flashcard.Test.Suporte` |
| `Flashcard.Persistence.Test` | xUnit | `Flashcard.Persistence`, `Flashcard.Application`, `Flashcard.Domain`, `Flashcard.Test.Suporte` |
| `Flashcard.Service.Test` | xUnit | `Flashcard.Service`, `Flashcard.Persistence`, `Flashcard.Application`, `Flashcard.Domain`, `Flashcard.Test.Suporte` |
| `Flashcard.Presentation.Test` | xUnit | `Flashcard.Presentation` (e **nenhum** outro; D12) |
| `Flashcard.Test.Suporte` | biblioteca de classes, `IsPackable=false` | nenhuma por enquanto |

Regras:

- Pacotes dos projetos de teste: o conjunto-padrão de xUnit do Vault (`Microsoft.NET.Test.Sdk`, `xunit`, `xunit.runner.visualstudio`, e o `coverlet.collector` se o Vault o usar). **Usar as mesmas versões do Vault** e anotá-las no README do projeto. Não adicionar `Testcontainers.PostgreSql`, `Microsoft.AspNetCore.Mvc.Testing` nem `bunit` agora (entram nas specs que os usam).
- `Flashcard.Test.Suporte` fica **vazio** (nenhuma classe). Ele é preenchido na spec da fixture do PostgreSQL.
- Adicionar todos à solution.
- Usar o mesmo `TargetFramework` dos projetos de produção.
- Não criar o arquivo `UnitTest1.cs` do template (apagar).

**Critério de pronto:** `dotnet build` verde na solution; os 6 projetos novos listados na solution; `dotnet test` verde (com avisos de "nenhum teste encontrado").

---

## Passo 2: Motor das regras e testes sintéticos

Pasta nova: `Service.Test/Arquitetura/` (na raiz da solution, ao lado de `Service`). Nenhum pacote novo.

### 2.1 `ProjetoCsproj` (registro imutável)

Campos: `Nome` (nome do arquivo sem extensão), `Sdk`, `ReferenciasDeProjeto` (nomes, sem caminho nem extensão), `Pacotes` (nomes), `ReferenciasDeFramework` (nomes).

### 2.2 `LeitorDeCsproj`

- `Ler(string caminhoDoCsproj)` e `Ler(string nome, XDocument documento)` (a segunda existe para os testes sintéticos).
- Lê com `System.Xml.Linq`. Considerar **todos** os itens, ignorando atributos `Condition`.
- `ProjectReference`: pegar o `Include`, trocar `\` por `/` e extrair o nome do arquivo sem extensão.
- `Sdk`: atributo `Sdk` do elemento raiz `Project`.
- Se existir `Directory.Build.props` na raiz da solution, **somar** os seus `PackageReference` e `ProjectReference` a todos os projetos (o método de leitura da solution faz isso, não o leitor de um arquivo isolado).
- `PackageVersion` (de `Directory.Packages.props`) **não** é referência e é ignorado.

### 2.3 `LocalizadorDaSolution`

- A partir de `AppContext.BaseDirectory`, sobe os diretórios até achar um arquivo `*.sln` ou `*.slnx`. Se não achar, o teste falha com a mensagem "Raiz da solution não encontrada a partir de <pasta>".
- Lista todos os `*.csproj` abaixo da raiz, **ignorando** pastas `bin` e `obj`.

### 2.4 `RegrasDeDependencia`

Método principal: `IReadOnlyList<string> Validar(IReadOnlyCollection<ProjetoCsproj> projetos)`. Devolve a lista de violações, vazia quando tudo está correto. Cada mensagem começa com o código da regra, em português, e diz **o projeto, o que está errado e o que é permitido**. Exemplo: `[R3] Flashcard.Application não pode referenciar o projeto 'Flashcard.Persistence' (permitido: Flashcard.Domain).`

**Nomes dos projetos:** o `ProjetoCsproj.Nome` é o nome do arquivo (`Presentation`, `Presentation.Test`, `Test.Suporte`), sem o prefixo `Flashcard.`. As regras abaixo usam os nomes lógicos com prefixo (`Flashcard.Presentation`). O motor **normaliza** os dois lados removendo o prefixo da constante `PrefixoDaSolution` (`Flashcard.`) quando presente, para comparar sempre `Presentation` com `Presentation`. Um teste sintético cobre isso (`Validar_WhenNomesComESemPrefixo_ComparaPeloNomeNormalizado`).

Constantes nomeadas e únicas (um só lugar para alterar): `PrefixoFoundation`, nome dos pacotes `Core`, `EFRepository` e `Auth`, lista de pacotes de terceiros aprovados na Presentation e lista de pacotes proibidos nos testes.

Regras (cada uma com código estável, para os testes citarem):

| Código | Regra |
|---|---|
| R1 | Existe um projeto de teste `<Nome>.Test` para cada projeto de produção, mais `Flashcard.Test.Suporte`, e existem os 5 projetos de produção |
| R2 | `ProjectReference` de produção apenas conforme D3 (Domain: nenhuma; Application: Domain; Persistence: Application e Domain; Service: Application e Persistence; Presentation: nenhuma) |
| R3 | Projeto de produção nunca referencia projeto `*.Test` nem `Flashcard.Test.Suporte` |
| R4 | `Flashcard.Domain`: pacotes permitidos somente o Core da Foundation |
| R5 | `Flashcard.Application`: pacotes permitidos somente o Core da Foundation |
| R6 | `Flashcard.Domain`, `Flashcard.Application` e `Flashcard.Persistence` usam o Sdk `Microsoft.NET.Sdk` (nunca `.Web`) e não têm `FrameworkReference` a `Microsoft.AspNetCore.App` |
| R7 | `Flashcard.Presentation`: pacotes da Foundation permitidos, **exceto** `EFRepository` e `Auth` (D6, D13); pacotes de terceiros somente da lista aprovada (Markdig, HtmlSanitizer, NLog e pacotes de nome iniciado por `NLog.`) |
| R8 | `Flashcard.Presentation.Test` referencia apenas `Flashcard.Presentation` (D12) |
| R9 | Projetos de teste não referenciam `Flashcard.Presentation` exceto o próprio `Flashcard.Presentation.Test`; e `Flashcard.Presentation.Test` não referencia nenhum outro projeto interno |
| R10 | Projetos de teste não usam pacotes proibidos: `Moq`, `FluentAssertions`, `Respawn` e `Playwright`/`Microsoft.Playwright` (D8) |
| R11 | Projetos de produção não usam pacotes de teste (`xunit*`, `Microsoft.NET.Test.Sdk`, `Testcontainers*`, `bunit*`, `Microsoft.AspNetCore.Mvc.Testing`) |
| R12 | `Flashcard.Test.Suporte` não referencia `Flashcard.Presentation` |
| R13 | A solution contém **exatamente** os 11 projetos esperados (5 de produção, 5 de teste e o Suporte). Qualquer outro `.csproj` (por exemplo, `Presentation.Client`) é violação, e a mensagem lista os projetos não previstos. *(Regra proposta junto com D16; aguarda confirmação do usuário. Pode ser removida sem afetar as demais.)* |

> Os pacotes de terceiros da Presentation e os proibidos nos testes são comparados **sem diferenciar maiúsculas** e por **nome exato**, exceto os marcados com prefixo (`NLog.`, `xunit`, `Testcontainers`, `bunit`).

### 2.5 Testes sintéticos (devem passar já neste passo)

Classe `RegrasDeDependenciaTests`. Cada teste monta `ProjetoCsproj` em memória, parte de um conjunto **válido** de 11 projetos e **estraga uma coisa só**:

- `Validar_WhenSolutionValida_RetornaListaVazia`
- `Validar_WhenDomainReferenciaApplication_RetornaViolacaoR2`
- `Validar_WhenApplicationReferenciaPersistence_RetornaViolacaoR2`
- `Validar_WhenPresentationReferenciaProjetoInterno_RetornaViolacaoR2`
- `Validar_WhenProducaoReferenciaProjetoDeTeste_RetornaViolacaoR3`
- `Validar_WhenDomainUsaPacoteDeTerceiro_RetornaViolacaoR4`
- `Validar_WhenApplicationUsaEntityFramework_RetornaViolacaoR5`
- `Validar_WhenApplicationUsaEFRepository_RetornaViolacaoR5`
- `Validar_WhenPersistenceUsaSdkWeb_RetornaViolacaoR6`
- `Validar_WhenApplicationReferenciaAspNetCoreApp_RetornaViolacaoR6`
- `Validar_WhenPresentationUsaEFRepository_RetornaViolacaoR7`
- `Validar_WhenPresentationUsaAuth_RetornaViolacaoR7`
- `Validar_WhenPresentationUsaPacoteDeTerceiroNaoAprovado_RetornaViolacaoR7`
- `Validar_WhenPresentationUsaMarkdig_NaoRetornaViolacao`
- `Validar_WhenPresentationTestReferenciaSuporte_RetornaViolacaoR8`
- `Validar_WhenServiceTestReferenciaPresentation_RetornaViolacaoR9`
- `Validar_WhenProjetoDeTesteUsaMoq_RetornaViolacaoR10`
- `Validar_WhenProjetoDeProducaoUsaXunit_RetornaViolacaoR11`
- `Validar_WhenSuporteReferenciaPresentation_RetornaViolacaoR12`
- `Validar_WhenFaltaProjetoDeTeste_RetornaViolacaoR1`
- `Validar_WhenHaVariasViolacoes_RetornaTodas` (confirma que o motor **não para na primeira**)
- `Validar_WhenNomesComESemPrefixo_ComparaPeloNomeNormalizado`
- `Validar_WhenHaProjetoNaoPrevisto_RetornaViolacaoR13` (incluir um projeto `Presentation.Client` no conjunto)
- `Validar_WhenPresentationReferenciaProjetoClient_RetornaViolacaoR2`

Classe `LeitorDeCsprojTests` (usa `XDocument` em memória, sem disco):

- `Ler_WhenProjectReferenceComBarraInvertida_ExtraiONome`
- `Ler_WhenPackageReferenceComCondition_IncluiOPacote`
- `Ler_WhenFrameworkReference_IncluiOFrameworkReference`
- `Ler_WhenSdkWeb_RetornaOSdk`
- `Ler_WhenPackageVersion_IgnoraOItem`

**Critério de pronto:** testes sintéticos verdes. A classe do teste de arquitetura real ainda **não** existe.

---

## Passo 2B: Corrigir a Presentation (remover o `Presentation.Client`)

Decisão D16: a Presentation usa só interatividade Server. O template Blazor gerou o `Presentation.Client` (SDK `Microsoft.NET.Sdk.BlazorWebAssembly`) e, no `Presentation.csproj`, a referência a ele e o pacote `Microsoft.AspNetCore.Components.WebAssembly.Server`. **Nada disso fica.** Fazer em um commit só (`Presentation: remove o projeto Client (WebAssembly)`).

1. **Antes de apagar, listar o conteúdo do `Presentation.Client`.** Os arquivos do exemplo do template (página `Counter` e semelhantes) são apagados. Se houver **qualquer arquivo escrito pelo mantenedor**, **parar e perguntar** para onde ele vai, antes de remover a pasta.
2. Mover para `Presentation` o que for aproveitável do Client (por exemplo, `_Imports.razor` com `using` úteis, se o `Presentation` ainda não os tiver), mantendo namespaces `Flashcard.Presentation.*`.
3. `dotnet sln` (ou o equivalente para `.slnx`): **remover** o `Presentation.Client` da solution e **apagar** a pasta `Presentation.Client`.
4. `Presentation/Presentation.csproj`:
   - remover a `ProjectReference` para `Presentation.Client`;
   - remover o `PackageReference` de `Microsoft.AspNetCore.Components.WebAssembly.Server`;
   - **manter** `TargetFramework`, `Nullable`, `ImplicitUsings` e `BlazorDisableThrowNavigationException`, que são do template do Server também.
5. `Presentation/Program.cs`:
   - trocar o registro de componentes de **WebAssembly** para **Server** (`AddInteractiveServerComponents` no lugar de `AddInteractiveWebAssemblyComponents`);
   - trocar o mapeamento de renderização para **Server** (`AddInteractiveServerRenderMode` no lugar de `AddInteractiveWebAssemblyRenderMode`);
   - remover o `AddAdditionalAssemblies(...)` que aponta para o assembly do Client e os `using` do Client.
6. Em `Components/**/*.razor`: trocar qualquer `@rendermode` Auto ou WebAssembly (`InteractiveAuto`, `InteractiveWebAssembly`) por `InteractiveServer`. Se `Routes.razor` ou `App.razor` apontarem para o assembly do Client, remover a referência.
7. Procurar por `Client` e `WebAssembly` em todo o `Presentation` (`grep -ri "webassembly\|\.Client"`): **nenhuma ocorrência** deve restar, exceto nomes que nada têm a ver (por exemplo, `HttpClient`).
8. Subir a Presentation (`dotnet run --project Presentation`) e abrir a página inicial; sem erro de renderização.

**Critério de pronto:** a solution tem 11 projetos; `dotnet build` verde; a Presentation sobe; não há SDK `BlazorWebAssembly` nem pacote `WebAssembly` em nenhum `.csproj`.

---

## Passo 3: Teste contra a solution real

Classe `ArquiteturaDependenciasTests`, em `Service.Test/Arquitetura/`:

- `Solution_WhenLocalizada_ContemOsProjetosEsperados`: usa o localizador e confere que os 11 projetos existem (mensagem lista os que faltam).
- `Solution_WhenAnalisada_NaoTemViolacoes`: lê todos os `.csproj`, aplica `RegrasDeDependencia.Validar` e falha com **todas** as violações na mensagem (uma por linha).

Depois de criar o teste:

1. Rodar. **Esperado no primeiro run:** verde, se o Passo 2B e as correções do inventário do Passo 0 já foram feitas; se ainda falhar, tratar cada violação como abaixo.
2. Para cada violação: corrigir **a referência no `.csproj`** (remover o que sobra, mover para o projeto certo). Se a correção exigir mudar código (por exemplo, a Application usa algo de EF), **parar e perguntar**, citando o arquivo e a regra.
3. Se uma regra estiver errada (por exemplo, um pacote Foundation legítimo que a lista não prevê), **não relaxar sozinho**: registrar o caso e pedir decisão.
4. **Teste de sabotagem (manual, não commitar):** adicionar uma `ProjectReference` de `Flashcard.Presentation` para `Flashcard.Application`, rodar o teste, ver a falha `[R2]` citando o projeto, e **desfazer**.

**Critério de pronto:** `dotnet test` verde na solution inteira (os projetos de teste vazios continuam só com avisos), e a sabotagem do item 4 foi vista falhar.

---

## Passo 4: READMEs

Um `README.md` na pasta de cada um dos 11 projetos (`Domain`, ..., `Presentation`, `Domain.Test`, ..., `Presentation.Test`, `Test.Suporte`), **em português**, curto, com as mesmas seções:

1. **Propósito** (uma ou duas frases; ver ADR 0002).
2. **Pode depender de** e **não pode depender de** (copiar da seção 2, D3 a D6).
3. **Como rodar os testes** (comando exato; para projetos de teste, informar se exige o Docker em execução. Por enquanto nenhum exige).
4. **ADRs relacionados** (links relativos para `docs/adr`).

README da solution (raiz), se já existir, apenas **acrescentar** (não reescrever) as seções "Estrutura dos projetos" (tabela da seção 2, D1 a D3) e "Como rodar os testes" (`dotnet test` na raiz; observação de que, a partir das specs seguintes, os testes de Persistence e de integração do Service exigirão o Docker). Se não existir, criar com essas duas seções e um link para `docs/adr/README.md`.

**Critério de pronto:** 11 READMEs + README da raiz; links relativos conferidos (nenhum quebrado).

---

## Passo 5: Fechamento

- Conferir que `docs/adr/` contém 0001 a 0022 e o `README.md` com o índice até 0022 (a ordem de implementação é o ADR 0022). Se faltar, copiar dos arquivos entregues.
- Criar `docs/specs/README.md` (se não existir) com a lista das specs e o link para esta (numeração sequencial própria do Flashcard, ver seção 8).
- Mudar o **Status** desta spec para "implementada (data)".
- Mensagens de commit em português, no estilo do Vault, uma por passo concluído (sugestão: `Esqueleto: projetos de teste e suporte`, `Esqueleto: motor das regras de dependência`, `Esqueleto: teste de arquitetura contra a solution`, `Esqueleto: READMEs por projeto`).

---

## 6. Validação ponta a ponta

1. `dotnet build` na solution: sem erros nem avisos novos.
2. `dotnet test` na solution: tudo verde.
3. `dotnet test Flashcard.Service.Test --filter "FullyQualifiedName~Arquitetura"`: os 29 testes (24 de regras, 5 do leitor) e os 2 do teste de arquitetura passam.
4. Sabotagem manual (Passo 3, item 4): a falha cita `[R2]` e o projeto; ao desfazer, volta a passar.
5. Rodar o teste a partir de outra pasta (`cd ..; dotnet test Flashcard/Service.Test/...`, ajustando o nome da pasta-raiz do clone): continua achando a solution.
6. Conferir que não existe `Presentation.Client` na solution nem pacote `WebAssembly` em nenhum `.csproj` (`grep -ri webassembly --include=*.csproj .`).
7. Conferir que nenhum `.csproj` de produção referencia pacote ou projeto de teste (`grep` por `.Test` e `xunit` nos `.csproj` de produção).
8. Se o repositório usa `dotnet format` ou analisadores, rodar da forma que o Vault roda e confirmar que não há diferenças.

## 7. Riscos e pontos de atenção

- **O teste lê o disco.** Em ambientes onde só os binários são copiados (sem as pastas de fonte), ele falha por não achar a solution. Hoje não há CI, então o risco é baixo. Se a CI entrar, o teste deve rodar no checkout completo.
- **A lista de pacotes aprovados é uma constante.** Cada novo pacote de terceiro na Presentation exige ADR e atualização da constante (propositalmente: é o ponto de controle).
- **Pacotes transitivos não são vistos.** O teste lê só as referências diretas. É suficiente para as regras do ADR 0002; análise de pacotes transitivos fica fora.
- **Regras podem ficar desatualizadas** quando uma spec futura exigir referência nova (por exemplo, Service precisando do Domain). A mudança deve ser feita na constante **junto com um ADR**, e não por exceção local.
- **`Flashcard.Test.Suporte` vazio** só ganha conteúdo nas próximas specs. Até lá, os projetos de teste o referenciam sem usá-lo.

## 8. Fora de escopo (próximas specs da fase 1)

Numeração sequencial própria das specs do Flashcard (`docs/specs/NNNN-flashcard-<assunto>.md`), independente da dos ADRs. Proposta de recorte:

| Spec | Assunto |
|---|---|
| 0001 | Esqueleto da solution e teste de arquitetura (esta) |
| 0002 | Foundation: renderizador do `Logger` (`TraceId` e `OwnerId`), nova versão do pacote |
| 0003 | `Flashcard.Test.Suporte`: fixture do PostgreSQL (Testcontainers), relógio fixo e fábrica da API |
| 0004 | Host do `Service`: NLog (`AddNLog()`), tratadores de exceção, health checks, limitador de taxa, configuração validada |
| 0005 | Host da `Presentation`: NLog, `/Error` e 404, health check mínimo, limitador, interruptor de cadastro |
| 0006 | `docker compose`: banco, service e presentation, com healthcheck e `depends_on` |

Também fora de escopo desta spec: banco de dados e `DbContext`, autenticação, qualquer entidade, tela ou endpoint de negócio, e alterações em pacotes da Foundation.
