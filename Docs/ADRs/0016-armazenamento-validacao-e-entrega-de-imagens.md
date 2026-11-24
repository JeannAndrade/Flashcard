# ADR 0016 — Armazenamento, validação e entrega de imagens

- **Status:** Aceita (detalhes validados no spike de imagens)
- **Data:** 2026-11-22

## Contexto

O ADR 0004 limita o `Cartao` a duas imagens (uma na frente, uma no verso) e o ADR 0015 define a referência dentro do Markdown (`![texto](cartao-imagem:ID)`), a regra textual no Domain e um endpoint de imagens. Faltava decidir onde guardar os bytes, o que aceitar, como o ciclo de vida funciona e como a imagem chega ao navegador.

Fatos que pesaram:

- Uma imagem de até 960 px costuma ter de dezenas a poucas centenas de KB. A [wiki do PostgreSQL](https://wiki.postgresql.org/wiki/BinaryFilesInDB) indica o banco quando o arquivo faz parte do registro e precisa de garantias ACID, e o sistema de arquivos para arquivos muito grandes (da ordem de 100 MB) com desempenho crítico. Os custos do banco são memória no servidor ao ler binários e backups mais demorados conforme o volume cresce.
- O navegador não tem o token da API (ADRs 0007 e 0014), então não pode pedir a imagem direto a ela.
- O cadastro é aberto (ADR 0013).

## Decisão

1. **Armazenamento:** PostgreSQL, coluna `bytea`, em **tabela própria** (entidade `Imagem`). Metadados: dono (`OwnerId`), vínculo com o `Cartao` (vazio enquanto a imagem está pendente), tipo de mídia, tamanho em bytes, largura, altura, hash do conteúdo e data de criação (colunas, constraints e índices no [ADR 0017](0017-modelo-de-dados.md), com índices começando por `OwnerId`). **O conteúdo nunca é carregado em listagens nem em consultas de metadados**; o repositório expõe leitura de metadados e leitura de conteúdo separadamente, e o conteúdo é lido só no endpoint de entrega.
2. **Formatos aceitos:** GIF, JPEG e PNG. O tipo é determinado pela **assinatura do arquivo**, nunca pelo tipo declarado nem pela extensão. SVG, WebP, AVIF e demais formatos ficam fora (SVG é vetor de XSS). GIF animado é aceito, guardado como veio, desde que respeite os limites.
3. **Validação por rejeição, sem biblioteca de imagem:** o Domain traz regras puras (sem dependência) que conferem a assinatura, leem as dimensões do **cabeçalho** (PNG, GIF e JPEG) e rejeitam arquivo truncado ou com cabeçalho inválido. Imagem com **lado maior acima de 960 px é rejeitada** (constante do Domain), com mensagem em português que diz o limite (ADR 0011). O arquivo é guardado exatamente como veio, sem redimensionar nem re-codificar. Os metadados EXIF permanecem, o que é aceitável porque cada usuário só vê as próprias imagens.
4. **Limites de tamanho em bytes:** **teto rígido constante de 2 MB** no Domain; **limite operacional configurável, padrão 1 MB**, em `appsettings` do Service e validado na Application. Valor configurado acima do teto falha na inicialização com mensagem clara (mesmo molde do ADR 0015, item 8). A rota de upload define o limite do corpo da requisição um pouco acima do teto (por causa do multipart) e lê o corpo de forma limitada, para recusar cedo sem carregar o arquivo inteiro. Os limites (bytes, lado maior em px, tipos aceitos) são expostos pelo mesmo endpoint de limites da API do ADR 0015.
5. **Sem cota total por usuário por enquanto.** Mitigações em vigor: no máximo duas imagens por `Cartao`, limpeza das pendentes (item 6) e limite de taxa também no upload (ADR 0013: 20 uploads por 10 minutos por dono, valor inicial).
6. **Ciclo de vida (upload independente, vínculo ao salvar):**
   - O upload cria uma `Imagem` do dono sem `Cartao` (pendente) e devolve o `ID`; o editor insere `![texto](cartao-imagem:ID)` no Markdown.
   - Ao criar ou atualizar o `Cartao`, a Application extrai as referências pela regra textual do Domain (ADR 0015, item 7) e valida que cada `ID` existe, pertence ao `ownerId` e não está vinculado a **outro** `Cartao`. Então vincula as imagens ao cartão.
   - Na mesma operação e no mesmo `SaveAsync`, as imagens do cartão que deixaram de ser referenciadas são removidas.
   - Excluir o `Cartao` remove as suas imagens.
   - A limpeza é **oportunista**, sem serviço em background: a cada upload, removem-se as imagens pendentes do mesmo usuário com mais de 24 h.
7. **Entrega:** navegador → `Presentation` (endpoint de imagens, autenticado por cookie) → API (endpoint de imagem, JWT e filtro por `ownerId`) → banco.
   - A resposta leva o tipo gravado, `X-Content-Type-Options: nosniff`, `Cache-Control: private` (validade definida na implementação), ETag pelo hash do conteúdo (permite 304) e, nas respostas de imagem, uma CSP restritiva (`default-src 'none'; sandbox`) para o caso de abertura direta.
   - ID inexistente ou de outro dono recebe a mesma resposta: 404.
   - A `Presentation` repassa a resposta em *stream*. O cliente tipado da Foundation é pensado para JSON, então pode ser necessário um método aditivo para leitura binária, a validar no spike e a documentar no guia.
   - O padrão exato do endpoint da `Presentation` é definido na implementação e usado na política do sanitizador (ADR 0015, item 5).

## Consequências

- Um único backup e consistência transacional entre `Cartao` e imagens. Em troca, o banco cresce com as imagens e o backup fica mais demorado; o spike mede o armazenamento e a leitura de imagens de ~300 KB no PostgreSQL em contêiner.
- Nenhuma dependência nova. Em troca, o usuário precisa reduzir imagens grandes antes de enviar, e o leitor de cabeçalho é código próprio, coberto por um corpus de testes.
- Sem cota total, com cadastro aberto nada impede um usuário de acumular imagens. **Risco aceito**: reavaliar se o app deixar de ser de uso pessoal ou se o crescimento do banco preocupar.
- A limpeza oportunista dispensa agendador, mas pendentes de um usuário que não envia mais nada permanecem até o próximo upload dele.
- Em aberto: como o envio chega da ilha do editor ao servidor (componente de upload do Blazor via circuito, com seu limite de leitura configurado para o teto, ou POST HTTP comum a um endpoint da `Presentation`), a validar no spike.
- O spike de imagens junta-se aos spikes de autenticação em circuito (ADR 0014) e de renderização (ADR 0015).

## Como será testado

- **Validação de upload (xUnit):** corpus de arquivos (PNG, GIF e JPEG válidos; extensão trocada; arquivo truncado; cabeçalho que mente sobre o tamanho; acima de 960 px; acima do limite em bytes; GIF animado dentro do limite).
- **Isolamento:** com dois `OwnerId`, o segundo recebe 404 ao pedir a imagem do primeiro e não consegue referenciá-la no seu `Cartao`.
- **Ciclo de vida:** vincular ao salvar, referenciar imagem de outro cartão (rejeitada), remover imagem não referenciada, remover ao excluir o cartão e limpar pendentes com mais de 24 h.
- **Persistência:** teste de integração contra PostgreSQL real, porque o InMemory não prova o comportamento de `bytea`.
- **Entrega:** cabeçalhos (`nosniff`, cache, ETag), resposta 304 e uso do mesmo padrão de URL pelo renderizador e pelo sanitizador.
- **Configuração:** limite acima do teto falha na inicialização.

## Alternativas consideradas

- **Volume Docker com arquivos:** leitura direta do disco, mas risco de divergência entre arquivo e registro, dois itens de backup, permissões e nomes gerados no servidor para evitar *path traversal*.
- **Large Objects do PostgreSQL ou armazenamento de objetos:** voltados a arquivos muito grandes ou acesso parcial; exagero aqui.
- **Reduzir e re-codificar no servidor:** com SkiaSharp (MIT; binário nativo na imagem Docker; até onde sei o Skia não codifica GIF, então a animação se perderia, a validar) ou ImageSharp (gerenciado, mas exige chave de licença Six Labors no build). Fica como evolução, em novo ADR, se a rejeição gerar atrito.
- **Redimensionar no navegador:** não é confiável como única defesa.
- **Upload só para `Cartao` já salvo:** ciclo mais simples, mas obriga a salvar o cartão antes de anexar a imagem.
- **URL com token temporário direto na API:** quebra o princípio de o navegador nunca falar com a API.
- **Cota total por usuário:** adiada por decisão do usuário.
