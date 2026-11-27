# Especificação técnica: `Test.Suporte` com PostgreSQL em contêiner (Flashcard)

- Data: 2026-11-26
- Status: aprovada para implementação (2026-11-26)
- ADRs relacionados: 0006, 0019 (itens 2, 3 e 8), 0022 (fase 1)
- Repositórios afetados: **Flashcard** (`Test.Suporte`, `Persistence.Test`, `Application.Test`). A `LumiaFoundation` **não** é alterada.
- Depende da spec 0001 (os projetos de teste e o `Test.Suporte` já existem, vazios).

> Este documento é uma especificação para outro dev implementar. Não contém código de implementação: descreve o que fazer em cada classe, a ordem, os testes e a documentação.
> Todo código, comentário, commit e documentação em português. Nomes de tipos e membros seguem o padrão do repositório.

---

## 1. Resumo

O ADR 0019 manda testar a persistência contra um **PostgreSQL real em contêiner**, subido pelo próprio `dotnet test` (Testcontainers), e criar o esquema pelas migrations. Esta spec entrega a base disso no `Test.Suporte`: uma **fixture** que sobe um PostgreSQL por execução de um projeto de teste, uma **constante única** com a imagem do banco e um **relógio fixo** para os testes de regra com data. Não há `DbContext`, entidade nem migration ainda: a fixture é validada por um teste de fumaça que roda `SELECT` dentro do contêiner e confere a versão maior do banco.

Ficam para depois, de propósito: a **fábrica da API** (`WebApplicationFactory`), que precisa do `Program` do `Service` (spec 0004), e o **`FakeRepositoryManager`**, que precisa das portas de repositório da Application (fase 3, primeira fatia vertical).

## 2. Decisões aprovadas

| Item | Decisão | Origem |
|---|---|---|
| D1 | PostgreSQL real em contêiner com `Testcontainers.PostgreSql`; sem InMemory nem SQLite em teste de persistência | ADR 0019 |
| D2 | Um contêiner **por execução de cada projeto de teste**, compartilhado pelos testes do projeto por uma fixture de coleção; isolamento entre testes **por `ownerId`**, não por limpeza do banco | ADR 0019 |
| D3 | O esquema é criado pelas migrations (nas specs futuras), nunca por `EnsureCreated` | ADR 0019 |
| D4 | O `Test.Suporte` nunca é referenciado por código de produção nem pela `Presentation.Test` (regras R3, R8, R9 e R12 do teste de arquitetura) | spec 0001 |
| D5 | Imagem do banco: **`postgres:17`** (Debian, sem sufixo `-alpine`). A versão maior vive numa constante única do `Test.Suporte`, e o compose (spec 0006) usa a mesma | aprovada pelo usuário: imagem Debian sem sufixo (2026-11-26) e **versão maior 17**, mantida a que o usuário já usa (2026-11-26, substitui a 19 proposta antes) |
| D6 | Falta de Docker **falha** o teste com mensagem clara em português; nunca é ignorada silenciosamente | regras do projeto |
| D7 | Relógio fixo implementa o `IDateTimeProvider` da Foundation | ADR 0019 (item 4) |
| D8 | Dependência nova: `Testcontainers.PostgreSql`, **somente** no `Test.Suporte`. Já aprovada no ADR 0019 | ADR 0019 |

## 3. Verificações já feitas (não repetir)

- O usuário decidiu **manter o PostgreSQL 17** (a versão que já usa), em vez do 19, lançado em 2026-11-19 ([nota de lançamento](https://postgresql.org/about/news/postgresql-19-released-3449)). A 17 é uma linha madura, e a troca por uma versão mais nova fica para um ADR próprio.
- O provedor `Npgsql.EntityFrameworkCore.PostgreSQL` listado no NuGet é o 10.0.3. **Eu não conferi a matriz de suporte de versões do PostgreSQL desse provedor para a 17**; as migrations e a fixture das próximas specs provam a compatibilidade na prática.
- **Não conferi o Docker Hub:** a imagem `postgres:17` existe há muito tempo, mas o Passo 0 confirma.
- O pacote `Testcontainers.PostgreSql` não precisa do `Npgsql` para esta spec: o teste de fumaça usa a execução de script dentro do contêiner.
- **Não verificado por mim:** o local do tipo `IDateTimeProvider` (provável: pacote Core da Foundation) e a versão do xUnit do repositório (v2 ou v3). O Passo 0 confere.

## 4. Atenção antes de começar

1. **xUnit v2 e v3 diferem.** Em v2, `IAsyncLifetime.InitializeAsync` e `DisposeAsync` retornam `Task`; em v3 retornam `ValueTask`. Usar a assinatura da versão do xUnit instalada nos projetos de teste (inventário da spec 0001).
2. **A definição da coleção fica no projeto de teste, não no `Test.Suporte`.** O xUnit só descobre `[CollectionDefinition]` no assembly dos testes. O `Test.Suporte` oferece a **classe da fixture** e uma constante com o nome da coleção; cada projeto de teste que a usa declara a sua própria classe de definição (pequena, sem lógica).
3. **Cada projeto de teste sobe o seu contêiner.** Rodar `dotnet test` na solution toda sobe um por projeto que usar a fixture (hoje só o `Persistence.Test`; depois, o `Service.Test`). Isso é esperado.
4. **Docker em WSL:** o Docker precisa estar acessível a partir do WSL (Docker Desktop com integração WSL ou Docker Engine instalado no WSL). Se o Testcontainers não achar o Docker, a mensagem do D6 deve dizer isso.
5. **Contêiner auxiliar do Testcontainers (Ryuk):** o Testcontainers sobe um contêiner auxiliar que limpa recursos órfãos. **Não desligar** (`TESTCONTAINERS_RYUK_DISABLED`) para contornar problema; se ele falhar, registrar o erro e perguntar.
6. **Nenhum segredo em texto puro no repositório.** A senha do banco de teste é **gerada aleatoriamente a cada execução** (o Testcontainers faz isso por padrão se não for informada; **não** definir senha fixa). Nada disso vai para arquivo de configuração.
7. **Não criar banco, usuário nem esquema à mão** na fixture. Ela só sobe o contêiner e entrega a connection string.

## 5. Ordem de implementação

Cada passo termina com build e testes verdes antes do seguinte.

| # | Passo | Projeto |
|---|---|---|
| 0 | Inventário e conferência da imagem | (sem alterações) |
| 1 | Pacote e constante da imagem | `Test.Suporte` |
| 2 | Fixture do PostgreSQL | `Test.Suporte` |
| 3 | Relógio fixo | `Test.Suporte` |
| 4 | Testes de fumaça | `Persistence.Test`, `Application.Test` |
| 5 | READMEs e fechamento | `Test.Suporte` + testes |

---

## Passo 0: Inventário e conferência da imagem

**Não alterar nada.**

1. Confirmar que o Docker responde: `docker info`.
2. Confirmar que a imagem existe: `docker pull postgres:17` e, em seguida, `docker run --rm postgres:17 postgres --version` (deve mostrar `postgres (PostgreSQL) 17.x`). Se a imagem não existir, **parar e perguntar**.
3. Anotar a versão do **xUnit** (pacote `xunit` ou `xunit.v3`) e do `Microsoft.NET.Test.Sdk` nos projetos de teste.
4. Localizar o `IDateTimeProvider` (pacote e namespace) e a sua forma: membros e se existe `UtcDateTimeProvider`. Anotar o nome exato do membro que devolve a data e hora atuais.
5. Conferir a versão estável mais recente de `Testcontainers.PostgreSql` compatível com `net10.0`.

**Critério de pronto:** respostas anexadas ao PR; divergências listadas, sem corrigir.

---

## Passo 1: Pacote e constante da imagem

**`Test.Suporte/Test.Suporte.csproj`**

- Adicionar `PackageReference` de `Testcontainers.PostgreSql` (versão do Passo 0, item 5). Nenhum outro pacote novo.
- Adicionar a referência ao pacote da Foundation onde mora o `IDateTimeProvider` (Passo 0, item 4), na mesma versão usada pela `Application`. **Não** referenciar projetos de produção.

**Classe `ImagemDoBanco`** (estática, pública, em `Test.Suporte/Banco/`):

- Constante com a imagem completa, valor `postgres:17`.
- Constante inteira com a versão maior, valor `17`.
- Comentário em português: o compose (spec 0006) deve usar a mesma imagem, e quem mudar a versão altera as duas coisas juntas.

**Critério de pronto:** a solution compila; o teste de arquitetura (spec 0001) continua verde (o `Test.Suporte` não referencia projeto de produção nem a Presentation).

---

## Passo 2: Fixture do PostgreSQL

**Classe `PostgreSqlFixture`** (pública, em `Test.Suporte/Banco/`), implementa o ciclo de vida assíncrono do xUnit (seção 4, item 1).

Comportamento:

- **Inicialização:** monta e inicia um contêiner PostgreSQL com a imagem de `ImagemDoBanco`, sem senha fixa, **sem volume** (os dados somem com o contêiner), com mapeamento de porta aleatória (padrão do Testcontainers). Espera o banco aceitar conexões (a espera padrão do pacote).
- **Falha ao iniciar:** captura a exceção do Testcontainers e a **relança** como `InvalidOperationException` em português que diz: "Não foi possível iniciar o PostgreSQL de teste. Verifique se o Docker está em execução e acessível." e **preserva a exceção original** como `InnerException`. Não engolir a exceção.
- **Propriedades:**
  - `ConnectionString`: a connection string do banco padrão do contêiner (entregue pelo próprio Testcontainers);
  - `ObterConnectionString(string nomeDoBanco)`: devolve a mesma connection string trocando o nome do banco. Usar `System.Data.Common.DbConnectionStringBuilder` (BCL; **não** usar o `Npgsql` aqui, para não criar dependência). Recusa nome vazio ou com caracteres fora de letras minúsculas, dígitos e sublinhado (lança `ArgumentException`); o banco não é criado aqui, a criação fica com o `Migrate` das specs futuras.
  - `ExecutarScriptAsync(string sql, CancellationToken cancellationToken)`: executa o SQL dentro do contêiner (o recurso de execução de script do contêiner) e devolve o resultado com `ExitCode`, `Stdout` e `Stderr`. Serve para o teste de fumaça e para inspeção.
- **Encerramento:** para e descarta o contêiner. Se o contêiner nunca iniciou, não lança exceção.

**Classe `ColecaoPostgreSql`** (estática, pública, no mesmo diretório): somente uma constante com o nome da coleção, `"PostgreSql"`. A definição da coleção (seção 4, item 2) **não** fica aqui.

**Critério de pronto:** a solution compila. O comportamento é provado no Passo 4.

---

## Passo 3: Relógio fixo

**Classe `RelogioFixo`** (pública, em `Test.Suporte/Tempo/`), implementa o `IDateTimeProvider` do Passo 0.

- Construtor recebe a data e hora UTC fixas. A propriedade devolvida pelo membro do `IDateTimeProvider` é **sempre** esse valor.
- Construtor recusa valor cuja `Kind` não seja `Utc` (`ArgumentException` em português).
- Método `Avancar(TimeSpan intervalo)` desloca o relógio para a frente (recusa intervalo negativo com `ArgumentOutOfRangeException`). Serve aos testes de fila de estudo (fase 6).
- Sem estado estático; cada teste cria o seu relógio.

**Critério de pronto:** compila; coberta pelos testes do Passo 4.

---

## Passo 4: Testes de fumaça

Nomes no padrão `<Membro>_When<Condicao>_<Resultado>` (ADR 0019).

### 4.1 `Persistence.Test`

Em `Persistence.Test/Suporte/`:

- **Definição da coleção** (classe sem lógica): associa a coleção `ColecaoPostgreSql.Nome` à `PostgreSqlFixture` pela interface de fixture de coleção do xUnit.
- **`PostgreSqlFixtureTests`** (marcada com a coleção):
  - `ExecutarScriptAsync_WhenSelectSimples_RetornaExitCodeZero`
  - `ExecutarScriptAsync_WhenPedeAVersao_RetornaAVersaoMaiorDeImagemDoBanco`: executa `SELECT version();` e confere que o `Stdout` contém `PostgreSQL 17` (montar o texto a partir da constante `VersaoMaior`, sem literal).
  - `ObterConnectionString_WhenNomeValido_TrocaSomenteOBanco`: o resultado tem o mesmo host, porta e usuário e o banco pedido.
  - `ObterConnectionString_WhenNomeInvalido_LancaArgumentException` (nome vazio, com maiúscula, com espaço e com ponto e vírgula).
  - `ConnectionString_WhenFixtureIniciada_NaoVazaSenhaFixaNoRepositorio`: confere que a senha da connection string **não** é igual a nenhuma string literal do repositório conhecida (por exemplo `postgres`, `password`, `test`); basta uma lista curta de valores proibidos.

### 4.2 `Application.Test`

Em `Application.Test/Suporte/`: **`RelogioFixoTests`**

- `Agora_WhenCriado_RetornaOValorInformado` (usar o membro real do `IDateTimeProvider`)
- `Construtor_WhenKindNaoUtc_LancaArgumentException`
- `Avancar_WhenIntervaloPositivo_DesloCaOInstante`
- `Avancar_WhenIntervaloNegativo_LancaArgumentOutOfRangeException`

### 4.3 Caso de falha do Docker (manual, não automatizado)

Parar o Docker e rodar `dotnet test Persistence.Test`: o resultado deve falhar com a mensagem do D6 e a `InnerException` original visível. Registrar o resultado no PR. **Não** criar teste automático que desligue o Docker.

**Critério de pronto:** `dotnet test` verde em `Persistence.Test` e em `Application.Test`, com o Docker em execução.

---

## Passo 5: READMEs e fechamento

- `Test.Suporte/README.md`: o que o projeto oferece (fixture, relógio fixo), que **nunca** é referenciado por produção nem pela `Presentation.Test`, como declarar a coleção num projeto de teste (seção 4, item 2), a regra de mudar a versão do banco junto com o compose, o que ainda não existe (fábrica da API, `FakeRepositoryManager`) e **como rodar** (`dotnet test Persistence.Test`, com o Docker em execução).
- `Persistence.Test/README.md`: acrescentar que exige o Docker em execução.
- Atualizar o README da raiz (seção "Como rodar os testes") dizendo que a partir daqui os testes de `Persistence.Test` exigem o Docker.
- Marcar o **Status** desta spec como "implementada (data)".
- Commits em português, um por passo.

---

## 6. Validação ponta a ponta

1. `docker info` responde.
2. `dotnet build` na solution: sem erros nem avisos novos.
3. `dotnet test Persistence.Test`: os 5 testes passam; um contêiner `postgres:17` aparece durante a execução (`docker ps`) e **desaparece** ao final (`docker ps -a` sem sobras).
4. `dotnet test Application.Test`: os 4 testes do relógio passam, **sem** precisar do Docker.
5. `dotnet test` na solution: tudo verde, incluindo o teste de arquitetura da spec 0001.
6. Parar o Docker e repetir o item 3: falha com a mensagem em português (Passo 4.3).
7. Conferir que nenhum arquivo do repositório contém senha de banco em texto puro (`grep -ri "password=" --include=*.cs --include=*.json --include=*.yml .`).

## 7. Riscos e pontos de atenção

- **Versão do banco fixada em 17.** A versão maior vive em **uma constante** (e no compose). Se um dia for preciso subir de versão, é troca de um valor, e os testes de migration das próximas specs rodam nessa fixture e acusam incompatibilidade cedo. A atualização deve ter ADR próprio.
- **Lentidão:** o primeiro `dotnet test` baixa a imagem; os seguintes reaproveitam. Cada projeto de teste que usa a fixture paga a subida de um contêiner (alguns segundos).
- **Layout de dados da imagem:** na 17 o diretório padrão de dados é o tradicional (`/var/lib/postgresql/data`). Quem subir para a 18 ou mais nova deve rever o ponto de montagem do volume no compose, porque a imagem oficial mudou esse layout a partir da 18 (conferir a página da imagem na época). Não afeta os testes (sem volume).
- **Testes paralelos:** classes na mesma coleção rodam em sequência. Como o isolamento é por `ownerId`, o custo é baixo, mas testes de DDL (migration do zero) devem usar **um banco próprio** (`ObterConnectionString`) para não interferir nos demais.
- **Sem integração contínua** (ADR 0019): ninguém roda isto sozinho; a disciplina de `dotnet test` antes de cada entrega vale.

## 8. Fora de escopo

- `DbContext`, entidades, mapeamento e migrations (fase 3 em diante).
- A fábrica da API (`WebApplicationFactory`, `Microsoft.AspNetCore.Mvc.Testing`) e o JWT de teste (spec 0004).
- O `FakeRepositoryManager` e qualquer fake da Application (fase 3).
- `bunit` e testes de componentes (fase da primeira ilha).
- Ajustes de desempenho do banco de teste (por exemplo, desligar `fsync`).
- O `docker compose` (spec 0006).
