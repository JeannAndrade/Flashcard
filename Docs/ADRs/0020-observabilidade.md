# ADR 0020 — Observabilidade

- **Status:** Aceita (itens "Em aberto" a confirmar na implementação)
- **Data:** 2026-11-23

## Contexto

O projeto exige NLog com log estruturado e health checks. Os ADRs anteriores já fixam que a Application não loga (ADR 0010) e deixaram em aberto o identificador de correlação para o usuário informar um erro 500 (ADR 0018). Fatos que pesaram:

- O renderizador `${activity}` do NLog, que lê o `TraceId` da requisição, está no pacote separado `NLog.DiagnosticSource` (BSD-3-Clause). Ter o identificador nas linhas de log exige essa dependência ou um renderizador próprio.
- A propagação do contexto de rastreio entre Presentation e API pelo `HttpClient` depende de existir uma `Activity` iniciada. O ASP.NET Core cria uma por requisição recebida. Dentro de uma ilha interativa (circuito), não há garantia de que exista uma quando o usuário age.
- O `MapLumiaHealthChecks` devolve só `Healthy` ou `Unhealthy`, em texto puro, sem detalhes das verificações (conferido no código, com testes).
- O `ILoggerManager` da Foundation usa o NLog e aceita argumentos posicionais (`params object[]`), mas **não** oferece escopo (`BeginScope`) nem propriedades nomeadas próprias (conferido no código em 2026-11-23).
- O `DomainExceptionHandler` registra as exceções de domínio (4xx como aviso, 5xx como erro com a exceção). O `Http.Client` não registra falhas de chamada: o `ApiConnection` só lança `ApiException`.
- A Foundation **não liga o NLog ao `Microsoft.Extensions.Logging`**: o `ConfigureLoggerService()` registra só `ILoggerManager`, sem `AddNLog()`. Os logs do próprio framework (ASP.NET Core, EF, Kestrel) não chegam ao NLog por essa configuração, e o console padrão do framework os escreve em texto.

## Decisão

1. **Correlação pelo contexto de rastreio nativo (W3C).** O `TraceId` da requisição é o mesmo na Presentation e na API, e é mostrado na tela de erro da Presentation para o usuário informar. O `ErrorDetails` **não muda** (ADR 0018, item 7). Para as ações das ilhas, a Presentation registra um `ActivityListener` e uma `ActivitySource` (APIs nativas do .NET) e abre uma `Activity` em torno de cada chamada à API, garantindo o cabeçalho `traceparent`. O spike prova que o `TraceId` mostrado na tela é o que aparece no log da API.
2. **`TraceId` nas linhas de log:** um **renderizador de layout no `Lumia.Foundation.Logger`**, mudança aditiva em nova versão do pacote (nenhum impacto para o Vault; a numeração é decisão do usuário). Contingência, se o `Logger` não comportar a extensão: o pacote `NLog.DiagnosticSource`, com nova aprovação. O `nlog.config` dos dois hosts inclui `TraceId` e `OwnerId` em cada linha. Como o `ILoggerManager` não tem escopo, **nenhum dos dois depende da chamada de log**: o `TraceId` vem da `Activity.Current`, e o `OwnerId` (aprovado pelo usuário em 2026-11-23) vem de uma **tag da `Activity` da requisição**, definida por um middleware depois da autenticação, que o mesmo renderizador lê (subindo aos pais da `Activity`, se preciso). A interface `ILoggerManager` não é alterada.
3. **Destino e formato:** NLog em **JSON, em arquivo e no console**. O arquivo tem rotação por tamanho (10 MB, 30 arquivos), e o console permite `docker compose logs`. O arquivo de log fica em um volume do `compose`. A base é o `nlog.config` do Vault, com quatro correções:
   - sem alvo de texto declarado e sem regra;
   - sem propriedades duplicadas (`includeEventProperties` junto de `properties`);
   - nível `Information` em produção e `Debug` só no desenvolvimento;
   - o arquivo mora no projeto host (`Service` e `Presentation`), e não na Application.

   Além disso, os hosts do Flashcard chamam `AddNLog()` (`NLog.Extensions.Logging`, já referenciado pelo `Lumia.Foundation.Logger`, sem pacote novo) e removem os provedores padrão, para os logs do framework irem ao mesmo JSON e não duplicarem no console. As regras do `nlog.config` mantêm `Microsoft.*` e `System.*` em `Warning` (exceto `Microsoft.Hosting.Lifetime`, em `Information`), para o EF não registrar cada comando SQL. Foi a direção sugerida pelo usuário; se preferir um interruptor opt-in na Foundation, vale uma nova decisão.
4. **O que se registra:**
   - **Pontos únicos de log:** na API, os handlers globais (ADR 0010; o `DomainExceptionHandler` já registra as exceções de domínio, 4xx como aviso e 5xx como erro com a exceção). Na Presentation, o tradutor de `ApiException`, necessário porque o `Http.Client` não registra falhas (5xx como erro, 4xx como informação, com status, rota e `TraceId`) e o `ErrorBoundary` das ilhas.
   - **Nunca:** tokens, senhas, texto dos cartões e bytes das imagens. `EnableSensitiveDataLogging` do EF fica **desligado** em qualquer ambiente com dados reais.
   - **`ownerId`** em cada linha das requisições autenticadas, pelo renderizador do item 2 (é um `Guid`, sem dado pessoal direto). Propriedades de um evento específico (status, rota) vão como **placeholders nomeados** na mensagem, que o NLog captura como propriedades.
   - **IP só em eventos de segurança:** rejeições do limite de taxa (429), falhas de login e de cadastro. O IP é dado pessoal, então a retenção segue a rotação do arquivo.
5. **Health checks:**
   - **API:** `MapLumiaHealthChecks`, com liveness (o processo vive) e readiness (o banco responde, pela tag do `EFRepository`). A resposta já é mínima (`Healthy` ou `Unhealthy`, em texto puro, sem detalhes), conferido no código com testes.
   - **Presentation:** o mesmo `MapLumiaHealthChecks`, com liveness simples e readiness que verifica se a API responde. Como ela é publicada, a resposta mínima da Foundation é a adequada.
   - **`docker compose`:** `healthcheck` em cada serviço (`pg_isready` no PostgreSQL) e `depends_on` com `service_healthy`, na ordem PostgreSQL, `Service`, `Presentation`.
   - Os endpoints de health check são **isentos do limite de taxa** (ADR 0013).
6. **Fora da v1:** métricas e rastreio completo (OpenTelemetry). Não há destino para receber os dados, e seria dependência sem uso. Reavalia-se se surgir necessidade de painéis.

## Consequências

- Um único `TraceId` liga a tela de erro, o log da Presentation e o log da API, sem mudar o contrato da Foundation.
- Uma mudança aditiva no `Lumia.Foundation.Logger`, que precisa ser publicada antes da implementação dos hosts. O guia de uso deve registrar o renderizador.
- Log em dois destinos exige volume no `compose` para reter os arquivos, e o console depende da rotação de log do Docker (a configurar no `compose`).
- Registrar o IP em eventos de segurança é uma escolha consciente: ajuda a analisar abuso (ADR 0013) ao custo de guardar dado pessoal por um período curto.
- Se a ilha não tiver `Activity` no circuito, o item 1 não basta. O spike decide se a `ActivitySource` própria resolve ou se é preciso um cabeçalho próprio (alternativa abaixo).
- As exceções de domínio (404 e 422) aparecem no log como aviso, por comportamento da Foundation. É útil para ver tentativas anormais, e o nível `Information` de produção as mantém.
- Ligar o NLog ao framework aumenta o volume de log. Os filtros por categoria (`Warning` para `Microsoft.*` e `System.*`) mantêm o ruído baixo.

## Em aberto (a confirmar na implementação)

1. Se os placeholders nomeados chegam como propriedades no JSON (teste simples na criação do esqueleto).
2. Se existe `Activity` nas ações das ilhas (spike do ADR 0014).
3. Se o `AddNLog()` nos hosts, junto do `ConfigureLoggerService()`, não duplica a saída (teste do esqueleto).

**Confirmados no código em 2026-11-23:** o `DomainExceptionHandler` registra as exceções de domínio (4xx com `LogWarn`, 5xx com `LogError` e a exceção, com testes para os dois caminhos); o `Http.Client` não registra falhas de chamada; o `ILoggerManager` não tem escopo nem propriedades nomeadas próprias; o `ConfigureLoggerService()` registra só `ILoggerManager` (sem `AddNLog()`); o `MapLumiaHealthChecks` já devolve só `Healthy` ou `Unhealthy` em texto puro, com testes.

## Como será testado

- **Correlação:** teste de integração mostra que o erro 500 devolve a tela com o `TraceId` e que a linha de log da API traz o mesmo valor (alvo de log em memória). O mesmo para uma ação de ilha, no spike.
- **Dados sensíveis:** um teste varre a saída de log de um fluxo completo em busca de token, senha e texto de cartão.
- **Configuração:** o `nlog.config` carrega sem erro, e o nível de produção não é `Debug`.
- **Propriedades:** um placeholder nomeado numa chamada do `ILoggerManager` aparece como propriedade no JSON; `TraceId` e `OwnerId` aparecem em cada linha de uma requisição autenticada.
- **Integração do logging:** um teste de integração do esqueleto **falha enquanto o `AddNLog()` não estiver configurado** e confere que um log do framework e um do `ILoggerManager` chegam ao mesmo JSON, sem duplicar no console.
- **Health checks:** liveness devolve 200; readiness devolve 503 com o banco indisponível; os endpoints não são limitados pelo limitador de taxa; a Presentation responde só o estado.

## Alternativas consideradas

- **Cabeçalho próprio (`X-Correlation-Id`):** independe de `Activity`, mas é um mecanismo paralelo ao que a plataforma oferece, com mais código nos dois lados. Fica como plano de contingência se o spike mostrar que a `Activity` não é viável nas ilhas.
- **`TraceId` no `ErrorDetails`:** o id da API chegaria direto ao usuário, mas muda contrato público (Vault e `Http.Client`) e é desnecessário, pois o id é o mesmo.
- **`NLog.DiagnosticSource`:** pronto, mas dependência nova nos dois hosts.
- **Só arquivo ou só console:** o arquivo sozinho perde o `docker compose logs`, e o console sozinho perde a rotação e a retenção do NLog.
- **OpenTelemetry na v1:** sem destino para os dados.
- **Estender o `ILoggerManager` com escopo ou propriedades nomeadas:** logs mais ricos, mas acrescentar membros à interface quebra quem a implementa (mudança de contrato). Só se o renderizador não bastar.
- **Passar o `OwnerId` em cada chamada de log:** repetitivo e fácil de esquecer.
- **Ligar o NLog ao framework dentro do `ConfigureLoggerService()` (Foundation):** beneficia todos os projetos, mas muda o volume e o destino dos logs do Vault. Pode virar um interruptor opt-in depois.
- **Não ligar o NLog ao framework:** os logs do framework ficam fora do JSON e em texto no console.
