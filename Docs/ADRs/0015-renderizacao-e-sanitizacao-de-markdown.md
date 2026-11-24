# ADR 0015 — Renderização e sanitização de Markdown

- **Status:** Aceita (detalhes validados no spike de renderização)
- **Data:** 2026-11-21

## Contexto

A frente e o verso do `Cartao` são escritos em Markdown. O conteúdo digitado deve ser tratado como **hostil**: hoje um `Cartao` só é exibido ao próprio dono, sem compartilhamento nem importação, então o risco imediato é XSS contra o próprio usuário (por exemplo, colar de terceiros um conteúdo malicioso). Ainda assim, um script na página age como o usuário dentro do circuito das ilhas interativas (ADR 0007), e compartilhamento ou importação podem vir depois.

Fatos que pesaram na decisão:

- O Markdig **não sanitiza**. `DisableHtml()` faz o HTML bruto aparecer como texto, mas não trata URLs: links e imagens em Markdown puro geram `<a>` e `<img>` legítimos, inclusive com esquemas perigosos.
- O nuget.org usava "Markdig + `DisableHtml()` + filtro próprio de URLs sobre o documento", e os *autolinks* passaram pelo filtro (CVE-2024-37304). Filtro artesanal de URL é frágil.
- O HtmlSanitizer trabalha com listas de permissão de tags, atributos e esquemas de URL (padrão `http` e `https`), sobre o AngleSharp.

## Decisão

1. **Fonte da verdade:** o Markdown cru é guardado como digitado. A API **não gera HTML nem sanitiza**: valida tamanho e as regras de imagem (itens 6 e 7).
2. **Renderização na Presentation:** serviço `IMarkdownRenderer` (interface e implementação na própria Presentation). Pipeline: Markdig com **HTML bruto desligado**, seguido de **HtmlSanitizer com lista de permissão** sobre o HTML final. Duas camadas independentes: se uma falhar, a outra segura.
3. **Dependências aprovadas:** Markdig (1.6.0 na data desta decisão; BSD-2-Clause) e HtmlSanitizer (série 11.x, com AngleSharp transitivo). Versões exatas e a licença do HtmlSanitizer são confirmadas no spike.
4. **Recursos da v1:**

   | Recurso | v1 |
   | --- | --- |
   | Texto, ênfase, listas, citação, código em linha e em bloco | Sim (sem destaque de sintaxe) |
   | Tabelas | Sim |
   | Quebra de linha simples visível | Sim |
   | Imagem inline de esquema próprio (item 5) | Sim |
   | Links | Somente `https`; qualquer outro esquema perde o `href` e o texto permanece |
   | HTML bruto | Não (aparece como texto) |
   | Fórmulas (LaTeX) | Não (fora da v1; exigiria ADR próprio) |

   Recursos não listados ficam fora da v1.
5. **Política do sanitizador:** esquemas de URL somente `https`; sem `style`; sem atributos `on*`; sem `class` livre; links externos com `rel="noopener noreferrer"`; o `src` de imagem só é aceito no padrão exato do endpoint de imagens do projeto. Como esse padrão se encaixa no sanitizador é validado no spike.
6. **Imagens dentro do Markdown (esclarece o ADR 0004):** forma inline `![texto](cartao-imagem:ID)`, em que `ID` é o identificador do recurso de imagem do dono. O renderizador a reescreve para o endpoint de imagens (detalhes no [ADR 0016](0016-armazenamento-validacao-e-entrega-de-imagens.md)) **antes** da sanitização. Qualquer outra URL de imagem e a forma por referência (`![alt][r]`) são descartadas (confirmado no spike). O endpoint filtra por `ownerId`: o ID de outro usuário se comporta como inexistente (404).
7. **Regra de imagens ao salvar (regra textual estrita no Domain):** o Domain reconhece as referências pelo padrão textual exato do esquema próprio e impõe **no máximo uma por lado**, portanto no máximo duas por `Cartao`. A Application valida ainda que cada `ID` referenciado existe e pertence ao `ownerId`, consultando o repositório. A Application continua só com Domain e Core (ADR 0012), **sem Markdig**. Uma referência dentro de bloco de código também conta (escolha conservadora). O renderizador também limita a uma imagem por lado ao exibir, e um **teste de contrato** garante que ele nunca exibe mais imagens do que a regra do Domain contou.
8. **Limites de tamanho por lado:** **teto rígido constante de 5000 caracteres** no Domain e em `HasMaxLength` no mapeamento. O **limite operacional é configurável**, com padrão **2000**, em `appsettings` do Service, e é validado na Application. Valor de configuração acima do teto falha na inicialização, com mensagem clara que diz qual configuração está errada (regra do ADR 0009). O nome da seção de configuração é definido na implementação.
9. **Salvaguardas:**
   - `MarkupString` só aparece em um componente `MarkdownView`, com teste que varre os fontes da Presentation e falha se ele surgir em outro arquivo.
   - O preview do editor é renderizado no servidor pelo mesmo `IMarkdownRenderer`, com *debounce*.
   - Sem cache do HTML por ora.
   - A CSP é uma terceira camada, tratada em ADR de segurança de borda, pois o Blazor Server tem exigências próprias.

**Esclarecimento sobre o ADR 0002:** a restrição "Presentation → somente pacotes da Foundation" trata de **projetos internos**. Pacotes de terceiros são permitidos na Presentation quando aprovados em ADR (hoje: Markdig e HtmlSanitizer).

## Consequências

- A API não conhece HTML; a política de sanitização muda sem migrar dados e vale também para cartões antigos.
- Duas dependências de terceiros na Presentation.
- A regra de imagens existe em dois lugares (Domain, textual, e renderizador), coberta por teste de contrato. Falsos positivos, como uma referência em bloco de código contando como imagem, são conservadores e inofensivos.
- Fórmulas, links `http` e destaque de sintaxe ficam fora da v1; cada um exigiria ADR próprio.
- Subir o limite acima de 5000 exige mudar o código e criar uma migration.
- O custo de processar o preview a cada tecla é mitigado pelo *debounce*, e o limite de tamanho protege contra entradas enormes.
- A Presentation conhece o limite configurável por um **endpoint da API** (fonte única, usado pelo contador de caracteres do editor), conforme decidido em 2026-11-22.
- O armazenamento e a entrega das imagens estão no [ADR 0016](0016-armazenamento-validacao-e-entrega-de-imagens.md). Fica em aberto a CSP.
- O primeiro trabalho técnico é um **spike de renderização**, junto do spike de autenticação em circuito (ADR 0014).

## Como será testado

- **Unidade do renderizador (xUnit):** um corpus de vetores de XSS (`<script>`, `img onerror`, `javascript:` em link, imagem e *autolink*, `data:`, entidades e percent-encoding, quebra de linha dentro do esquema, `svg`, `iframe`, `style`, formulário). Em cada caso, a saída só contém elementos e atributos da lista de permissão, verificado com AngleSharp, que já acompanha o sanitizador.
- **Markdown legítimo:** tabela, lista, bloco de código e quebra de linha continuam funcionando.
- **Congelamento da lista de permissão:** ampliá-la exige alterar o teste de propósito.
- **Entradas patológicas:** aninhamento profundo e texto no limite terminam rápido e sem exceção.
- **Contrato entre a regra do Domain e o renderizador** (item 7) e **varredura de `MarkupString`** (item 9).
- **Domain e Application:** referências de imagem (zero, uma, duas na mesma face, em bloco de código, de outro dono) e o limite configurável (no limite, acima, acima do teto na inicialização).
- Testes do componente `MarkdownView` dependem da decisão sobre bUnit, na rodada de testes.

## Alternativas consideradas

- **A API devolve HTML** (guardado ou calculado): acopla o contrato ao front, e o HTML guardado envelhece quando a política muda.
- **Biblioteca JavaScript no navegador:** dependência JS e testes mais difíceis.
- **Markdig com HTML desligado + filtro próprio sobre o AST:** uma dependência a menos, mas é o desenho que falhou no nuget.org.
- **Permitir um subconjunto de HTML** (`<kbd>`, `<sub>`): mais superfície de ataque sem necessidade.
- **Imagens como campos estruturados** (uma na frente, uma no verso): impõe o limite sem interpretar Markdown, mas a imagem não fica no meio do texto. Rejeitada em favor do esquema próprio.
- **URLs externas livres para imagens:** rastreamento, links quebrados e CSP aberta.
- **Markdig também na Application:** leitura fiel do Markdown, mas dependência de terceiros numa camada interna e pipeline duplicado.
- **Pacote novo na Foundation com pipeline único:** uma só interpretação do Markdown, mas contrato público novo e dependências de terceiros no pacote; reavaliar se a regra duplicada do item 7 gerar problemas.
