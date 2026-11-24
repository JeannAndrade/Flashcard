# ADR 0019 — Estratégia de testes

- **Status:** Aceita
- **Data:** 2026-11-23

## Contexto

Os ADRs anteriores já exigem testes específicos (isolamento entre donos, integridade no banco, corpus de XSS e de imagens, tradução de erros). Faltava definir **onde** cada um roda, com **qual banco** e com **quais ferramentas**.

O Vault testa a Persistence com o provedor InMemory. O guia já registra que ele não aplica CHECK, chave estrangeira nem índice único, e o modelo do Flashcard (ADR 0017) depende justamente dessas garantias, além de `bytea` e `timestamptz`.

O projeto não terá integração contínua por enquanto. Portanto, nada executa os testes sem o desenvolvedor: a disciplina de rodá-los antes de cada entrega é parte do processo.

## Decisão

1. **Um projeto de teste por projeto de produção**, com o sufixo `.Test` (ADR 0002): Domain, Application, Persistence, Service e Presentation. O teste de arquitetura de dependências e os testes de integração da API ficam no `Service.Test`. Sempre xUnit.
2. **Banco dos testes de integração:** PostgreSQL real em contêiner, com **Testcontainers** (`Testcontainers.PostgreSql`), subido e derrubado pelo próprio `dotnet test`. O provedor InMemory e o SQLite **não** são usados em teste de persistência.
3. **Esquema e isolamento:**
   - Um contêiner por execução, compartilhado por uma fixture.
   - O esquema é criado pelas **migrations** (`Migrate`), não por `EnsureCreated`, para que as migrations também sejam testadas. Um teste confere que o modelo não tem mudanças pendentes sem migration.
   - O isolamento entre testes é **por dono**: cada teste usa um `ownerId` novo. Não há biblioteca de limpeza do banco.
4. **Application:**
   - Regra, validação e orquestração com **fakes escritos à mão**: um `FakeRepositoryManager` em memória e um relógio fixo (`IDateTimeProvider`). Sem biblioteca de mock.
   - O que depende do banco (isolamento entre donos, fluxos completos) roda em teste de integração com o PostgreSQL real.
5. **API:** `WebApplicationFactory` (`Microsoft.AspNetCore.Mvc.Testing`) com o PostgreSQL do contêiner e o **JWT real**. A chave de assinatura é gerada aleatoriamente a cada execução, sem valor fixo no repositório. Os testes registram dois usuários de verdade, o que cobre isolamento, "forjar o dono" e o 401 de ponta a ponta.
6. **Presentation:**
   - A lógica pesada fica em classes testáveis sem interface gráfica (tradutor de erros, validação de limites, renderizador de Markdown).
   - Os componentes das ilhas (sessão de estudo, editor com preview, seleção de pastas) são testados com **bUnit**.
   - **Sem testes de ponta a ponta com navegador na v1.** O circuito autenticado é coberto pelo spike do ADR 0014.
7. **Asserções:** `Assert` do próprio xUnit. O FluentAssertions passou a ser comercial na versão 8, então não é adotado.
8. **Projeto de suporte compartilhado:** `Flashcard.Test.Suporte`, com a fixture do contêiner, a fábrica da API, construtores de dados, o relógio fixo e o `FakeRepositoryManager`. Os projetos de teste o referenciam, e ele nunca é referenciado por código de produção.
9. **Convenção de nomes (a do Vault):** classe `<Tipo>Tests`; método `<Membro>_When<Condicao>_<Resultado>`, com "When" em inglês e condição e resultado em português, em PascalCase sem acentos. Exemplo: `ExecuteAsync_WhenCartaoNaoTemPasta_LancaCommandValidationException`.
10. **Sem meta percentual de cobertura.** A meta é a **lista de testes obrigatórios** abaixo, que bloqueia a entrega.
11. **Dependências de teste aprovadas** (somente em projetos `.Test`):

    | Pacote | Uso | Licença |
    | --- | --- | --- |
    | `Testcontainers.PostgreSql` | PostgreSQL real nos testes | MIT |
    | `Microsoft.AspNetCore.Mvc.Testing` | Subir a API nos testes | Microsoft |
    | `bunit` | Testar os componentes das ilhas | MIT |

    Não entram Moq, FluentAssertions, Respawn nem Playwright.

## Testes obrigatórios (consolidação)

Cada item vem do ADR indicado e **bloqueia a entrega** da funcionalidade correspondente.

| Origem | Teste |
| --- | --- |
| ADR 0002 | Teste de arquitetura de dependências entre camadas |
| ADR 0003 | Contratos entre Presentation e Service; tetos fixos das anotações da Presentation iguais aos da API |
| ADR 0005 | Tabela de casos da avaliação; fila de estudo (corte de instante; "Errei" e "Foi difícil" não voltam na mesma sessão) |
| ADR 0008 | Convenção: todo Command, Query e método de repositório recebe `ownerId`. Isolamento com dois donos em cada operação (PostgreSQL real). "Forjar o dono" |
| ADR 0009 | Configuração obrigatória ausente falha na inicialização, sem valor de reserva |
| ADR 0014 | Spike de autenticação em circuito: refresh concorrente, expiração, logout, isolamento por `sid`, `SignOutAsync` limpando o store por `sid` |
| ADR 0015 | Corpus de XSS no renderizador; `MarkupString` só no componente de Markdown (varredura); limite acima do teto falha na inicialização; regra textual de imagens |
| ADR 0016 | Corpus de imagens; 404 para imagem de outro dono; ciclo de vida (vincular, remover, limpar pendentes); cabeçalhos, ETag e 304 |
| ADR 0017 | FK composta recusa vínculo entre donos; imagem pendente; cascatas e `Restrict`; CHECKs e unicidade; `timestamptz` em UTC; filho novo em agregado rastreado insere; contagem por pasta; toda entidade carrega `OwnerId` |
| ADR 0018 | Validação por tabela de casos com mensagens em português; tradução de erros do banco (e violação desconhecida relançada); mapeamento exceção → status; 500 sem detalhes internos; tradução de `ApiException` (inclusive corpo não reconhecível) |
| ADR 0013 | 429 com `ErrorDetails` e `Retry-After`; cota por dono independente; limite de cadastro, de login e de refresh por IP; interruptor de cadastro; ordem do pipeline; configuração inválida falha na inicialização |
| ADR 0020 | `TraceId` igual na tela de erro e no log da API (inclusive numa ação de ilha); log sem token, senha e texto de cartão; `nlog.config` válido e sem `Debug` em produção; liveness e readiness (503 com o banco fora); health checks isentos do limite; Presentation responde só o estado; integração do logging (falha enquanto o `AddNLog()` não estiver configurado) |

## Como rodar

- `dotnet test <Projeto>.Test`, ou na solution para tudo.
- Domain, Application e Presentation rodam sem Docker. Persistence e Service (integração) exigem o **Docker em execução**.

## Consequências

- Os testes de persistência provam o que o modelo promete (FK composta, CHECK, `bytea`). Em troca, exigem Docker e são mais lentos que os de memória. O contêiner compartilhado limita o custo.
- Sem Moq, o `FakeRepositoryManager` é código a manter, e pode divergir do banco real. Por isso o que depende do banco roda em integração.
- Três dependências novas, só de teste. O bUnit usa o AngleSharp, o mesmo do sanitizador do ADR 0015, e as versões devem ser alinhadas na criação do esqueleto.
- Sem integração contínua, a lista de testes obrigatórios só vale se for executada antes de cada entrega. Se o projeto crescer ou ganhar mais colaboradores, a CI entra em novo ADR, e o Testcontainers funciona nos executores do GitHub.
- O bUnit não cobre circuito, cookie nem CSP reais. Isso fica com os spikes e com testes de integração.

## Alternativas consideradas

- **InMemory ou SQLite nos testes de persistência:** rápidos, mas não provam FK composta, CHECK nem `bytea`.
- **PostgreSQL do `docker compose` de desenvolvimento:** sem dependência, mas exige passo manual e arrisca rodar contra o banco de desenvolvimento.
- **Moq na Application:** menos código de fake, mas testes acoplados à forma das chamadas e dependência nova.
- **Autenticação falsa nos testes da API:** mais simples, mas não testa a autenticação.
- **Respawn (limpeza do banco):** desnecessário, pois o isolamento é por dono.
- **Playwright (testes de ponta a ponta):** cobre circuito e cookies reais, mas é pesado e instável. Prematuro.
- **FluentAssertions:** licença comercial a partir da versão 8.
- **Meta percentual de cobertura:** estimula teste de baixo valor.
