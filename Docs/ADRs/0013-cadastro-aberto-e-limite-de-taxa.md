# ADR 0013 — Cadastro aberto e limite de taxa

- **Status:** Aceita (limite de taxa detalhado em 2026-11-23)
- **Data:** 2026-11-15 (atualizada em 2026-11-23)

## Contexto

O cadastro de usuários é aberto. Mesmo para uso inicialmente pessoal, o cadastro, o login e o refresh são superfície de ataque (abuso de cadastro, força bruta), e o upload de imagem (ADR 0016) permite acumular dados.

Fatos que pesaram no desenho:

- **A API não enxerga o IP do usuário.** Quem chama a API é a `Presentation`, servidor a servidor. Um limite por IP na API veria sempre o endereço da `Presentation`. O IP real só existe na `Presentation`.
- **Ações dentro de uma ilha interativa não são requisições HTTP** da `Presentation`: trafegam pelo circuito. O middleware de limite da `Presentation` não as conta. Quem as recebe, uma a uma e já com o `ownerId`, é a API.
- O limitador nativo do ASP.NET Core guarda os contadores **em memória, por instância**: com N instâncias, o limite efetivo é N vezes o configurado.
- Cada chave de partição cria um limitador em cache. Chave derivada de entrada ilimitada do usuário (por exemplo, o nome informado no login) esgota memória.
- Particionar por IP só é confiável se o IP for o real: atrás de proxy, `X-Forwarded-For` aceito de qualquer origem permite burlar o limite.
- O `UseRateLimiter` precisa vir depois do `UseRouting` (política por endpoint) e depois da autenticação (partição por usuário). O `UseStatusCodePagesWithReExecute` precisa vir **antes** do `UseRouting` e do limitador.

## Decisão

1. **Cadastro aberto** (decisão do usuário).
2. **Interruptor de cadastro:** uma chave booleana no `appsettings` da `Presentation` (nome definido na implementação; padrão: aberto). Fechada, a página de cadastro informa que o cadastro está indisponível e não processa o envio. Permite parar um abuso sem trocar de versão. Como a API fica acessível só pela rede interna (item 4), a `Presentation` é o único ponto de entrada do cadastro.
3. **Limitador nativo do ASP.NET Core** (`Microsoft.AspNetCore.RateLimiting`), sem dependência nova. Algoritmo: **janela deslizante** nas políticas particionadas (evita o pico duplo na virada da janela do fixo; o token bucket permitiria rajadas, e o ritmo do estudo é baixo e constante). O teto global usa limite de **requisições simultâneas**.
4. **Duas camadas:** a `Presentation` limita por IP o que é anônimo; a API limita por `ownerId` o que é autenticado e tem um teto global. **A API não é publicada para fora**: só a rede interna do `docker compose` a alcança (regra de implantação, documentada no README do `Service` e do `compose`).
5. **Políticas (valores iniciais, configuráveis no `appsettings`, validados na inicialização e calibrados depois com teste de carga):**

   | Onde | Alvo | Partição | Valor inicial |
   | --- | --- | --- | --- |
   | `Presentation` | Cadastro | IP | 5 por hora |
   | `Presentation` | Login | IP | 10 por 5 minutos |
   | `Presentation` | Refresh | IP | 10 por 5 minutos (mesmos valores do login, **contador separado**) |
   | API | Uso autenticado em geral | `ownerId` | 120 por minuto |
   | API | Upload de imagem | `ownerId` | 20 por 10 minutos |
   | API | Teto global contra inundação | sem partição | limite de requisições simultâneas, valor a calibrar |

   - Os valores não são segredos, então podem ter padrão no `appsettings.json`. Valor não positivo falha na inicialização, com mensagem que diz qual chave está errada.
   - Não há limite por conta (nome de usuário informado), pelo risco de memória descrito no contexto.
   - O **refresh** de token, que fica na `Presentation` e pode ser alcançado pelo navegador, recebe a mesma política do login (decisão do usuário, 2026-11-23). Login e refresh usam **contadores separados**, com os mesmos valores, para que renovar a sessão não consuma as tentativas de login. O refresh também continua serializado por `sid` (ADR 0014). Como o refresh é automático, e não acionado pelo usuário, o limite só incomoda atrás de um NAT com muitos usuários, e por isso os valores são configuráveis.
   - Nas Razor Pages, o atributo de limite é aplicado à **página**, e não aos métodos (`OnGet`, `OnPost`).
   - Os endpoints de **health check** são isentos do limite (ADR 0020), para a sondagem do `compose` não consumir a cota.
6. **IP:** sem proxy reverso na frente da `Presentation`. O IP é o da conexão (`RemoteIpAddress`) e o middleware de cabeçalhos encaminhados **não é habilitado**. Se um proxy for introduzido, um novo ADR configura os proxies confiáveis (nunca aceitar `X-Forwarded-For` de qualquer origem).
7. **Resposta rejeitada:** status **429** com `ErrorDetails` em português (o mesmo contrato do ADR 0018) e cabeçalho `Retry-After` arredondado **para cima**. O 429 sai do limitador, antes dos handlers de exceção, então o corpo é escrito explicitamente com o mesmo tipo de erro da Foundation. A `Presentation` mostra a mensagem do próprio 429, e não a mensagem fixa genérica (emenda no ADR 0018).
8. **Ordem do pipeline:**
   - **API:** roteamento, autenticação e então o limitador.
   - **Presentation:** a página de status (ADR 0018) antes do roteamento e do limitador.
9. **Senha:** política do Identity da Foundation, sem mudança.

## Consequências

- O isolamento entre usuários (ADR 0008) é requisito de segurança, com testes obrigatórios.
- Com cadastro aberto, registros indesejados continuam possíveis. O limite por IP e o interruptor reduzem o risco. Confirmação por e-mail não está prevista (exigiria envio de e-mail, fora do escopo).
- Os contadores em memória reiniciam com o contêiner, e a v1 tem uma instância (ADR 0014). Escalar para várias instâncias multiplica o limite efetivo e exige limitador com armazenamento compartilhado ou um gateway, em novo ADR.
- Se a API for exposta por engano, o limite por IP da `Presentation` é contornável chamando a API direto. Por isso a rede interna é regra, e não detalhe.
- O limite por IP penaliza usuários atrás do mesmo NAT. Os valores são configuráveis por esse motivo.
- **Lacuna confirmada (2026-11-23):** o `Lumia.Foundation.Auth` **não habilita o bloqueio de tentativas por conta**. Ele usa `UserManager.CheckPasswordAsync` diretamente, e não `SignInManager.PasswordSignInAsync`, então as tentativas malsucedidas não são contabilizadas, mesmo com os padrões de bloqueio do Identity. Por enquanto, a única proteção contra força bruta é o limite por IP. O bloqueio por conta fica **pendente para a implementação da autenticação** e exige decisão própria (opções, impacto no Vault e o efeito de um atacante poder bloquear a conta de outra pessoa).
- Limite de taxa não substitui proteção contra DDoS distribuído (WAF ou CDN na frente), que fica fora da v1.

## Como será testado

- **API:** passado o limite, 429 com `ErrorDetails` e `Retry-After`; a cota esgotada por um dono não afeta outro; o upload tem política própria; o teto global rejeita requisições simultâneas excedentes.
- **Presentation:** o limite de cadastro, de login e de refresh por IP (com contadores separados entre login e refresh); o interruptor de cadastro fechado não processa o envio; a ordem do pipeline com a página de status.
- **Configuração:** valor não positivo falha na inicialização.
- Nos testes de integração, os limites são altos por padrão, e só os testes de limite os reduzem.

## Alternativas consideradas

- **Cadastro fechado (convite ou administrador):** menor superfície, mas contraria a decisão do usuário.
- **Proteção só no proxy reverso:** útil, mas não substitui o limite na aplicação (e não há proxy hoje).
- **Só na `Presentation`:** não protege a API de uso autenticado abusivo (por exemplo, o upload).
- **Só na API:** sem o IP real, o limite de cadastro e de login seria inútil.
- **Limite por conta (nome de usuário) no login:** protegeria contra força bruta por conta, mas a chave vem de entrada do usuário e esgota memória. Fica com o bloqueio de tentativas do Identity.
- **Janela fixa:** mais simples, mas permite o dobro do limite na virada da janela.
- **Token bucket:** permite rajadas, desnecessário para o ritmo do estudo.
- **Limitador distribuído ou gateway:** necessário só com várias instâncias.
