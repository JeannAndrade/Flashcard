# ADR 0020 — Observabilidade

- **Status:** Aceita (itens "Em aberto" a confirmar na implementação)
- **Data:** 2026-11-23

## Contexto

O projeto exige NLog com log estruturado e health checks. Os ADRs anteriores já fixam que a Application não loga (ADR 0010) e deixaram em aberto o identificador de correlação para o usuário informar um erro 500 (ADR 0018). Fatos que pesaram:

- O renderizador `${activity}` do NLog, que lê o `TraceId` da requisição, está no pacote separado `NLog.DiagnosticSource` (BSD-3-Clause). Ter o identificador nas linhas de log exige essa dependência ou um renderizador próprio.
- A propagação do contexto de rastreio entre Presentation e API pelo `HttpClient` depende de existir uma `Activity` iniciada. O ASP.NET Core cria uma por requisição recebida. Dentro de uma ilha interativa (circuito), não há garantia de que exista uma quando o usuário age.
- O `MapLumiaHealthChecks` devolve **detalhes por verificação**. A API só é alcançável pela rede interna (ADR 0013), mas a Presentation é publicada.
- O `ILoggerManager` da Foundation usa o NLog.

## Decisão

1. **Correlação pelo contexto de rastreio nativo (W3C).** O `TraceId` da requisição é o mesmo na Presentation e na API, e é mostrado na tela de erro da Presentation para o usuário informar. O `ErrorDetails` **não muda** (ADR 0018, item 7). Para as ações das ilhas, a Presentation registra um `ActivityListener` e uma `ActivitySource` (APIs nativas do .NET) e abre uma `Activity` em torno de cada chamada à API, garantindo o cabeçalho `traceparent`. O spike prova que o `TraceId` mostrado na tela é o que aparece no log da API.
2. **`TraceId` nas linhas de log:** um **renderizador de layout no `Lumia.Foundation.Logger`**, mudança aditiva em nova versão do pacote (nenhum impacto para o Vault; a numeração é decisão do usuário). Contingência, se o `Logger` não comportar a extensão: o pacote `NLog.DiagnosticSource`, com nova aprovação. O `nlog.config` dos dois hosts inclui `TraceId` e `OwnerId` em cada linha.
3. **Destino e formato:** NLog em **JSON, em arquivo e no console**. O arquivo tem rotação por tamanho (10 MB, 30 arquivos), e o console permite `docker compose logs`. O arquivo de log fica em um volume do `compose`. A base é o `nlog.config` do Vault, com quatro correções:
   - sem alvo de texto declarado e sem regra;
   - sem propriedades duplicadas (`includeEventProperties` junto de `properties`);
   - nível `Information` em produção e `Debug` só no desenvolvimento;
   - o arquivo mora no projeto host (`Service` e `Presentation`), e não na Application.
4. **O que se registra:**
   - **Pontos únicos de log:** na API, os handlers globais (ADR 0010). Na Presentation, o tradutor de `ApiException` (5xx como erro, 4xx como informação, com status, rota e `TraceId`) e o `ErrorBoundary` das ilhas.
   - **Nunca:** tokens, senhas, texto dos cartões e bytes das imagens. `EnableSensitiveDataLogging` do EF fica **desligado** em qualquer ambiente com dados reais.
   - **`ownerId`** como propriedade estruturada nas requisições autenticadas (é um `Guid`, sem dado pessoal direto).
   - **IP só em eventos de segurança:** rejeições do limite de taxa (429), falhas de login e de cadastro. O IP é dado pessoal, então a retenção segue a rotação do arquivo.
5. **Health checks:**
   - **API:** `MapLumiaHealthChecks`, com liveness (o processo vive) e readiness (o banco responde, pela tag do `EFRepository`). Os detalhes por verificação são aceitáveis porque a API só é alcançável pela rede interna.
   - **Presentation:** liveness simples e readiness que verifica se a API responde. Como a Presentation é publicada, a resposta é **mínima** (apenas o estado): se o `MapLumiaHealthChecks` não permitir isso, usa-se o mapeamento nativo do ASP.NET Core, cuja resposta padrão já é só o estado.
   - **`docker compose`:** `healthcheck` em cada serviço (`pg_isready` no PostgreSQL) e `depends_on` com `service_healthy`, na ordem PostgreSQL, `Service`, `Presentation`.
   - Os endpoints de health check são **isentos do limite de taxa** (ADR 0013).
6. **Fora da v1:** métricas e rastreio completo (OpenTelemetry). Não há destino para receber os dados, e seria dependência sem uso. Reavalia-se se surgir necessidade de painéis.

## Consequências

- Um único `TraceId` liga a tela de erro, o log da Presentation e o log da API, sem mudar o contrato da Foundation.
- Uma mudança aditiva no `Lumia.Foundation.Logger`, que precisa ser publicada antes da implementação dos hosts. O guia de uso deve registrar o renderizador.
- Log em dois destinos exige volume no `compose` para reter os arquivos, e o console depende da rotação de log do Docker (a configurar no `compose`).
- Registrar o IP em eventos de segurança é uma escolha consciente: ajuda a analisar abuso (ADR 0013) ao custo de guardar dado pessoal por um período curto.
- Se a ilha não tiver `Activity` no circuito, o item 1 não basta. O spike decide se a `ActivitySource` própria resolve ou se é preciso um cabeçalho próprio (alternativa abaixo).

## Em aberto (a confirmar na implementação)

1. Se o `ILoggerManager` aceita propriedades estruturadas e escopo (para `OwnerId` e `TraceId`) e se o NLog também alimenta o `Microsoft.Extensions.Logging`.
2. Se o `DomainExceptionHandler` registra as exceções de domínio (404 e 422) e em que nível.
3. Se o `Http.Client` registra as chamadas que falham.
4. Se o `MapLumiaHealthChecks` permite resposta mínima.
5. Se existe `Activity` nas ações das ilhas (spike do ADR 0014).

## Como será testado

- **Correlação:** teste de integração mostra que o erro 500 devolve a tela com o `TraceId` e que a linha de log da API traz o mesmo valor (alvo de log em memória). O mesmo para uma ação de ilha, no spike.
- **Dados sensíveis:** um teste varre a saída de log de um fluxo completo em busca de token, senha e texto de cartão.
- **Configuração:** o `nlog.config` carrega sem erro, e o nível de produção não é `Debug`.
- **Health checks:** liveness devolve 200; readiness devolve 503 com o banco indisponível; os endpoints não são limitados pelo limitador de taxa; a Presentation responde só o estado.

## Alternativas consideradas

- **Cabeçalho próprio (`X-Correlation-Id`):** independe de `Activity`, mas é um mecanismo paralelo ao que a plataforma oferece, com mais código nos dois lados. Fica como plano de contingência se o spike mostrar que a `Activity` não é viável nas ilhas.
- **`TraceId` no `ErrorDetails`:** o id da API chegaria direto ao usuário, mas muda contrato público (Vault e `Http.Client`) e é desnecessário, pois o id é o mesmo.
- **`NLog.DiagnosticSource`:** pronto, mas dependência nova nos dois hosts.
- **Só arquivo ou só console:** o arquivo sozinho perde o `docker compose logs`, e o console sozinho perde a rotação e a retenção do NLog.
- **OpenTelemetry na v1:** sem destino para os dados.
