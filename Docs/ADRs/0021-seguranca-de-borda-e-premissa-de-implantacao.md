# ADR 0021 — Segurança de borda e premissa de implantação

- **Status:** Aceita (item 4, política dos cookies, **Proposta**: a confirmar)
- **Data:** 2026-11-24

## Contexto

O Flashcard é um sistema de **uso pessoal**, executado **estritamente em contêineres Docker** e acessado **sem HTTPS**, como o Vault. O usuário decidiu que não haverá TLS, CSP de site nem outros mecanismos de segurança de borda.

Essa premissa já sustenta decisões anteriores: não há proxy reverso (ADR 0013) e a API só é alcançável pela rede interna do `docker compose` (ADR 0013). Também define o que os ADRs 0015 e 0016 podem assumir sobre a borda.

## Decisão

1. **Premissa de implantação:** uso pessoal, em contêineres Docker, acessado por **HTTP**, a partir da máquina do usuário ou de uma rede local confiável. O sistema **não é exposto à internet** nem a redes não confiáveis, e não tem proxy reverso.
2. **Sem TLS, sem HSTS, sem redirecionamento para HTTPS, sem CSP de site e sem cabeçalhos adicionais de segurança.** Os dois hosts escutam apenas HTTP (URLs `http://` na configuração). `UseHttpsRedirection` e `UseHsts` dos modelos de projeto são removidos, e não há certificado.
3. **O que permanece**, por já estar aprovado e não ser "borda":
   - sanitização do Markdown e a regra de imagens (ADR 0015);
   - os cabeçalhos da **resposta de imagem** (`X-Content-Type-Options: nosniff`, `Cache-Control: private`, ETag e a CSP restritiva `default-src 'none'; sandbox`) do ADR 0016. Essa CSP vale **só** para essas respostas, como defesa contra uma imagem maliciosa aberta diretamente, e não configura uma CSP de site;
   - limite de taxa (ADR 0013), validações, isolamento por dono (ADR 0008) e API só na rede interna.
4. **Cookies (Proposta):** `HttpOnly` e `SameSite=Lax`, com `SecurePolicy = SameAsRequest` (o cookie é `Secure` quando a requisição é HTTPS, e funciona em HTTP). O mesmo vale para o cookie do antiforgery. Não se usam os prefixos `__Host-` nem `__Secure-`, que exigem `Secure`. Motivo: com `Secure` sempre ligado, o navegador descarta o cookie em HTTP (alguns navegadores abrem exceção só para `localhost`, não para outro nome ou IP), e o login deixaria de funcionar. As flags ficam em configuração, para endurecer sem mudar código.
5. **Gatilhos de reavaliação (novo ADR obrigatório):**
   - exposição fora da máquina ou da rede local confiável, incluindo qualquer acesso pela internet;
   - uso por outras pessoas além do dono;
   - introdução de proxy reverso ou de TLS;
   - acesso por rede não confiável (por exemplo, Wi-Fi público).

   Nesses casos entram TLS (por proxy reverso), HSTS, cookies `Secure`, CSP, cabeçalhos de segurança e a revisão do ADR 0013 (cabeçalhos encaminhados e proxies confiáveis).
6. **Reversibilidade:** nada no código assume HTTP. Endereços e flags de cookie vêm de configuração, e a mudança futura para HTTPS é de configuração e de implantação.

## Riscos aceitos

- O tráfego entre o navegador e a Presentation viaja em texto claro: senha, cookie e conteúdo dos cartões podem ser lidos por quem observar a rede.
- Sem CSP de site, a **sanitização do Markdown é a única defesa** contra XSS no conteúdo dos cartões. O corpus de XSS (ADR 0015) passa a ser o teste mais importante do projeto.
- Sem proteção contra clickjacking nem cabeçalhos de endurecimento.
- O JWT entre a Presentation e a API trafega em HTTP na rede interna do Docker.

A mitigação é a própria premissa: acesso apenas local. Se ela deixar de valer, vale o item 5.

## Consequências

- Em contêiner, o IP visto pela Presentation **pode** ser o do gateway do Docker (depende do modo de publicação da porta). Nesse caso, o limite por IP (ADR 0013) se comporta como limite global, o que é aceitável para uso pessoal. Conferir na implantação.
- O Blazor Server usa WebSockets em HTTP sem restrição adicional. Os requisitos de CSP do Blazor deixam de ser um custo.
- O ADR 0015 deixa de ter a CSP como terceira camada em aberto. As camadas são a sanitização e os testes de XSS.
- O guia da Foundation e o README do `compose` devem registrar a premissa (HTTP, sem proxy, rede interna) para o próximo projeto não herdá-la sem perceber.

## Como será testado

- **Pipeline:** teste de integração mostra que uma requisição HTTP recebe 200, sem redirecionamento e sem `Strict-Transport-Security`.
- **Cookies:** têm `HttpOnly` e `SameSite=Lax`, e `Secure` conforme a requisição.
- **CSP:** nenhuma resposta do site leva `Content-Security-Policy`, exceto a resposta de imagem, que leva a do ADR 0016.
- **XSS:** o corpus do ADR 0015, sem alteração, como defesa única no conteúdo dos cartões.

## Alternativas consideradas

- **Proxy reverso com TLS local (certificado local):** protege o tráfego, mas acrescenta uma peça e certificados a manter, para um uso estritamente local. Rejeitada pelo usuário.
- **CSP com *nonce* para o Blazor Server:** terceira camada contra XSS, mas com exigências próprias do Blazor. Rejeitada.
- **Cabeçalhos básicos (`X-Content-Type-Options`, `Referrer-Policy`) em todo o site:** custo baixo, mas o usuário optou por não ter cabeçalhos adicionais.
- **`Secure` sempre ligado nos cookies:** exigiria HTTPS e quebra o login em HTTP.
