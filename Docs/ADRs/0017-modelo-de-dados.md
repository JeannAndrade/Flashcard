# ADR 0017 — Modelo de dados

- **Status:** Aceita (mapeamento validado nos testes de persistência)
- **Data:** 2026-11-23

## Contexto

O domínio tem pastas, cartões (com estado de repetição espaçada), o vínculo N:N entre os dois e imagens. As decisões anteriores já fixam várias restrições:

- isolamento por dono e índices que começam por `OwnerId` (ADR 0008);
- Leitner, com sessão definida por corte de instante UTC (ADR 0005);
- PostgreSQL com duas `DbContext` (domínio e Identity) (ADR 0006);
- imagens em `bytea`, em tabela própria, com estado pendente (ADR 0016);
- texto do cartão com teto rígido de 5000 caracteres por lado (ADR 0015).

Faltava definir entidades, relacionamentos, constraints, índices e a regra de atualização dos agregados.

## Decisão

### 1. Entidades

```text
Pasta 1 ── N CartaoPasta N ── 1 Cartao 1 ── 0..2 Imagem 1 ── 1 ImagemConteudo
```

| Entidade | Campos | Notas |
| --- | --- | --- |
| `Pasta` | `Id`, `OwnerId`, `Nome` | Nome com no máximo 60 caracteres. |
| `Cartao` | `Id`, `OwnerId`, `Frente`, `Verso`, `Caixa` (1 a 5), `ProximaRevisaoEm` (UTC), `CriadoEm` | Frente e verso em Markdown. Raiz do agregado: dona dos vínculos com pastas. Nasce na caixa 1 com `ProximaRevisaoEm = CriadoEm` (devido de imediato). |
| `CartaoPasta` | `OwnerId`, `CartaoId`, `PastaId` | Junção explícita. Chave primária `(CartaoId, PastaId)`. |
| `Imagem` | `Id`, `OwnerId`, `CartaoId` (vazio = pendente), `TipoImagem` (`Gif`, `Jpeg`, `Png`), `TamanhoBytes`, `Largura`, `Altura`, `Hash` (SHA-256), `CriadaEm` | Só metadados (ADR 0016). |
| `ImagemConteudo` | `ImagemId` (chave primária e FK), `Conteudo` (`bytea`) | Lida só na entrega da imagem. |

### 2. Dono em todas as entidades

`OwnerId` está em `Pasta`, `Cartao`, `CartaoPasta` e `Imagem`. A única exceção é `ImagemConteudo`, que só é alcançada pela `Imagem` do dono e tem FK obrigatória para ela.

As relações usam **FKs compostas**, para o banco recusar vínculo entre donos diferentes mesmo que o código tenha um bug:

| Relação | FK composta | Aponta para |
| --- | --- | --- |
| `CartaoPasta` → `Cartao` | `(OwnerId, CartaoId)` | chave alternativa `Cartao(OwnerId, Id)` |
| `CartaoPasta` → `Pasta` | `(OwnerId, PastaId)` | chave alternativa `Pasta(OwnerId, Id)` |
| `Imagem` → `Cartao` | `(OwnerId, CartaoId)` | chave alternativa `Cartao(OwnerId, Id)` |

Em `Imagem`, `CartaoId` aceita nulo (pendente). No PostgreSQL, uma FK composta com uma coluna nula não é verificada (comportamento `MATCH SIMPLE`, o padrão). Assim, a imagem pendente passa e a imagem vinculada fica protegida. Como o EF Core trata uma FK composta com parte anulável é **ponto de teste obrigatório** (item 7).

Uma base `EntidadeDoDono` no Domain garante a presença do campo. Um teste de arquitetura verifica que toda entidade do Flashcard carrega `OwnerId`, com `ImagemConteudo` como exceção explícita. A forma exata da base (herança ou interface) depende de como o `Entity` da Foundation gera a chave e é definida na implementação: `CartaoPasta` tem chave composta e não tem `Id`.

### 3. Exclusões

| Operação | Efeito |
| --- | --- |
| Excluir `Pasta` com cartões | **Bloqueada** (`EntityInUseException`, que no contrato atual da Foundation resulta em 422, ADR 0018). No banco, a FK de `CartaoPasta` para `Pasta` é `Restrict`. |
| Excluir `Pasta` sem cartões | Permitida. |
| Excluir `Cartao` | Remove suas linhas de `CartaoPasta` e suas `Imagem` (e o `ImagemConteudo`) em cascata. |
| Excluir `Imagem` | Remove o `ImagemConteudo` em cascata. |

### 4. Regras que ficam fora do banco

- **Todo `Cartao` tem pelo menos uma pasta.** Não há constraint barata no PostgreSQL (exigiria *constraint trigger*). A regra fica na entidade (nasce com pastas e recusa remover a última), na Application (cria e atualiza cartão) e em testes.
- **No máximo uma referência de imagem por lado** do cartão (ADR 0015) e **no máximo duas imagens por cartão** (ADR 0004): regra textual do Domain e validação na Application.
- **Limite operacional de texto (padrão 2000)** fica no `appsettings` do Service. O banco guarda só o teto rígido.

### 5. Constraints e tipos

- **Nomes físicos** iguais aos do código: tabelas `Pastas`, `Cartoes`, `CartoesPastas`, `Imagens`, `ImagensConteudo`; colunas iguais às propriedades. Sem pacote de convenção `snake_case`.
- **Datas:** `DateTime` UTC em `timestamptz`. O Npgsql 6+ só grava `Kind=Utc`, e o `UtcDateTimeProvider` já cumpre isso.
- **`HasMaxLength` em toda coluna de texto**, usando as mesmas constantes do Domain (sem duplicar o número): `Pasta.Nome` 60; `Cartao.Frente` e `Cartao.Verso` 5000 (o teto, não o limite operacional); `Imagem.TipoImagem` curto. Subir um teto exige migration.
- **`TipoImagem` gravado como texto** (os nomes viram contrato). `Hash` em `bytea`.
- **CHECKs:** `Caixa` entre 1 e 5; `Largura` e `Altura` entre 1 e 960; `TamanhoBytes` entre 1 e o teto de 2 MB. Duplicam constantes do Domain, e subir o teto exige migration.
- **Nome da pasta único por dono:** índice único `(OwnerId, Nome)` mais checagem prévia sem diferenciar maiúsculas, com mensagem amigável. O índice é sensível a maiúsculas, então duas criações simultâneas que diferem só na caixa poderiam coexistir. Risco aceito, de impacto baixo.
- **Chaves geradas pela aplicação:** `Guid` definido no construtor da entidade, mapeado com `ValueGeneratedNever()` (veja o item 6).
- **Sem token de concorrência na v1.** O EF grava só as colunas alteradas, então editar o texto e avaliar o cartão em abas diferentes não colidem.
- **`CriadoEm`** só em `Cartao` (define a primeira revisão) e em `Imagem` (limpeza das pendentes).

### 6. Atualização de agregados

Carrega-se o agregado **rastreado**, altera-se por **métodos do domínio** e chama-se `SaveAsync`. Nada de `Update(grafo)` desconectado: com `Guid` já preenchido, o EF marca a entidade como `Modified` (UPDATE de uma linha que não existe), como diz a [documentação de rastreamento explícito do EF Core](https://learn.microsoft.com/lt-lt/ef/core/change-tracking/explicit-tracking), que recomenda `DatabaseGenerated(None)` para chaves definidas pela aplicação.

A documentação não cobre filhos novos descobertos por navegação a partir de um agregado já rastreado. Isso vira teste obrigatório (item 7).

### 7. Consultas e índices

Todos os índices começam por `OwnerId`, e o mapeamento comenta a consulta e o ADR que justificam cada um.

| Consulta | Índice |
| --- | --- |
| Pastas com contagem de cartões; há cartões na pasta?; apoio à FK composta para `Pasta` | `CartoesPastas (OwnerId, PastaId, CartaoId)` |
| Cartão com suas pastas | Chave primária `(CartaoId, PastaId)` |
| Próximo cartão devido da pasta (junto com a junção) | `Cartoes (OwnerId, ProximaRevisaoEm, Id)` |
| Imagens do cartão; apoio à FK composta | `Imagens (OwnerId, CartaoId)` |
| Pendentes antigas do dono (limpeza oportunista, ADR 0016) | `Imagens (OwnerId, CriadaEm)` filtrado por `CartaoId IS NULL` |

- **Contagem de cartões por pasta:** agregação sob demanda, sem contador na `Pasta` (sem risco de divergência; o custo é trivial nessa escala).
- **Fila de estudo:** cartões da pasta escolhida com `ProximaRevisaoEm` ≤ instante de início da sessão, ordenados por `ProximaRevisaoEm` e `Id`.
- **Listagem de cartões:** mais recentes primeiro, com desempate por `Id`.
- **Estado do Leitner** em colunas do `Cartao`, sem histórico de revisões na v1. O histórico pode entrar depois como migration aditiva.

## Consequências

- O banco passa a recusar vínculos entre donos diferentes (cartão↔pasta, imagem↔cartão), como segunda barreira além do código e dos testes do ADR 0008.
- `OwnerId` se repete em quatro tabelas e `Pasta` e `Cartao` precisam de chave alternativa `(OwnerId, Id)`. O custo é mapeamento maior e a possibilidade de as cópias divergirem, mitigada pelas FKs compostas, que impedem a divergência.
- Excluir pasta exige mover ou apagar os cartões antes. Excluir pasta e cartões de uma vez pode virar um comando separado depois.
- Sem histórico, não há estatísticas de estudo; trocar de algoritmo exige migrar caixa e próxima revisão (ADR 0005).
- Os CHECKs e `HasMaxLength` duplicam constantes do Domain. Mantê-los sincronizados é responsabilidade do mapeamento, que lê as mesmas constantes.

## Como será testado

Contra **PostgreSQL real em contêiner**, porque o InMemory não prova FK composta, CHECK, `Restrict` nem `bytea`:

- **Integridade entre donos:** o banco recusa `CartaoPasta` e `Imagem` que misturam donos.
- **Imagem pendente:** inserir `Imagem` com `CartaoId` nulo funciona; com `CartaoId` de cartão de outro dono falha.
- **Cascatas e `Restrict`:** excluir cartão remove vínculos e imagens; excluir pasta com cartões falha.
- **CHECKs e unicidade:** caixa fora de 1 a 5, dimensões e tamanho fora dos limites, nome de pasta repetido por dono, mesmo nome por donos diferentes.
- **Datas:** ida e volta de `timestamptz` em UTC.
- **Agregado rastreado (item 6):** adicionar um `CartaoPasta` ou uma `Imagem` a um `Cartao` carregado e salvar **insere**, e não gera UPDATE.
- **Consultas:** contagem por pasta, e "próximo cartão devido" (ordem, corte de instante, "Errei" e "Foi difícil" não voltam na mesma sessão).

Nos testes de domínio (xUnit):

- `Cartao` nasce com pastas, recusa remover a última, não aceita pasta duplicada e respeita os limites de texto e as referências de imagem.
- A avaliação (ADR 0005) é testada por tabela de casos, com relógio injetado.

Nos testes de arquitetura:

- toda entidade carrega `OwnerId` (exceção explícita: `ImagemConteudo`);
- todo método de repositório recebe `ownerId` (ADR 0008).

## Alternativas consideradas

- **N:N implícita do EF para cartão↔pasta:** menos código, mas o banco não impediria vínculo entre donos.
- **`OwnerId` só nas raízes (`Pasta` e `Cartao`):** modelo menor, mas a proteção contra vínculo entre donos fica só no código. Rejeitada pelo usuário em 2026-11-23, que preferiu o dono em todas as entidades.
- **Imagem sem estado pendente (upload só para cartão salvo):** elimina `OwnerId` em `Imagem` e a limpeza por tempo, mas obriga salvar o cartão antes de anexar a imagem. Rejeitada em 2026-11-23, mantendo o ADR 0016.
- **Conteúdo da imagem na mesma tabela:** uma tabela só, mas a regra de nunca carregar o conteúdo dependeria de convenção.
- **`ImagemConteudo` com `OwnerId` próprio:** zero exceções na regra do dono, ao custo de mais uma cópia do campo. A tabela só é alcançada pela `Imagem` do dono.
- **Histórico de revisões na v1:** permitiria estatísticas e troca de algoritmo, mas é escopo que não foi pedido.
- **Cascata ao excluir pasta (apaga cartões que só existem nela):** menos cliques, mas destrutivo.
- **Contador de cartões na `Pasta`:** leitura imediata, porém pode divergir e exige manutenção em cada operação.
- **`Update(grafo)` desconectado:** menos leitura antes, mas trata filhos novos como existentes.
