# ADR 0022 — Ordem de implementação

- **Status:** Aceita
- **Data:** 2026-11-24

## Contexto

Os ADRs 0001 a 0021 definem o que construir. Faltava definir **em que ordem**. Fatos que pesaram:

- O ADR 0014 exige o spike de autenticação em circuito e seus testes **antes de qualquer tela**.
- Quatro mudanças aditivas na `Lumia.Foundation` são necessárias em momentos diferentes (renderizador do `Logger`, store por `sid`, `SaveAsync` virtual e, provavelmente, leitura binária no cliente tipado).
- Não há integração contínua (ADR 0019): a disciplina de rodar os testes obrigatórios antes de cada entrega é do processo.
- Os pontos mais incertos (autenticação nas ilhas, Markdown, imagens) são validados por spikes, e os spikes devem virar código de produção com testes, e não código descartável.

## Decisão

1. **Estratégia:** esqueleto primeiro; depois spikes e fatias verticais, na ordem das fases abaixo. O risco maior (autenticação) vem primeiro.
2. **Primeira fatia vertical: Pasta**, por ser o menor agregado e exercitar todos os padrões transversais (validação, erros, isolamento, tradução de erro do banco, `ErrorBoundary`).
3. **Imagens depois do estudo:** o ciclo de estudo fica utilizável com cartões só de texto antes de as imagens entrarem.
4. **Migrations incrementais**, uma por funcionalidade, como no Vault. Não há migration inicial com o modelo inteiro do ADR 0017.
5. **Fases:**

   | Fase | Entrega | Critério de conclusão |
   | --- | --- | --- |
   | 1. Esqueleto | Solution e projetos com as regras de dependência (ADR 0002) e teste de arquitetura; `Flashcard.Test.Suporte` com PostgreSQL em contêiner; `docker compose` (PostgreSQL, Service, Presentation) com healthchecks e `depends_on`; hosts com `AddNLog()`, tratadores de exceção, health checks, `/Error` e 404, limitador (ADR 0013) e configuração sem segredo padrão; ADRs em `docs/adr/` do repositório. Foundation: renderizador do `Logger`. | `docker compose up` sobe os três contêineres saudáveis; `dotnet test` verde (arquitetura, registro de DI, logging, health, pipeline sem HTTPS). |
   | 2. Spike de autenticação | Cadastro, login, logout e refresh na `Presentation`; store por `sid`; endpoint autenticado trivial e uma ilha que o chama. Foundation: store por `sid`. | Testes do ADR 0014 (refresh concorrente, expiração, logout, isolamento por `sid`); `Activity` e `traceparent` provados na ilha; 401 na ilha leva ao Login. |
   | 3. Pasta | Criar, listar com contagem, renomear e excluir pasta vazia, de ponta a ponta. Foundation: `SaveAsync` virtual. | Padrões exercitados: validação, 404 e 422 em português, tradução de erro do banco, `TraceId` na tela de erro, isolamento entre donos. |
   | 4. Spike de Markdown | `IMarkdownRenderer`, `MarkdownView`, política do sanitizador, esquema `cartao-imagem:ID`, endpoint de limites. | Corpus de XSS e varredura de `MarkupString` (ADR 0015). |
   | 5. Cartão (só texto) | Cartão, vínculo com pastas (FK composta), estado de Leitner; criar, editar, excluir e listar; seleção de pastas e preview. Exclusão de pasta com cartões passa a ser testável. | Teste do filho novo em agregado rastreado, FK composta e limites configuráveis (ADR 0017 e 0018). |
   | 6. Estudo | Escolher a pasta, fila por corte de instante, virar o cartão, avaliar. | Tabela de casos do ADR 0005 e fila de estudo; ciclo completo com cartões de texto. |
   | 7. Imagens | Upload, entrega, ciclo de vida, limpeza oportunista. Foundation: leitura binária no cliente, se confirmada. | Corpus de imagens, 404 para outro dono, cabeçalhos e medição de `bytea` (ADR 0016). |
   | 8. Acabamento | Calibrar limites de taxa, README por projeto, atualização do guia, revisão final dos ADRs. | Lista de testes obrigatórios (ADR 0019) toda verde. |

6. **Mudanças na Foundation entram na hora em que a fase as exige**, sempre aditivas, com nova versão do pacote e sinalização de impacto em Web/Api.
7. **Portões:**
   - Antes da fase 2: confirmar a política dos cookies (ADR 0021, item 4) e decidir o bloqueio de tentativas por conta (ADR 0013).
   - Antes da fase 7: confirmar os cabeçalhos da resposta de imagem (ADR 0016; seguem como aprovados salvo objeção).
   - A numeração das versões dos pacotes da Foundation é decisão do usuário, a cada publicação.
8. **Modo de trabalho por fase:** plano curto (arquivos, testes, riscos) e alinhamento; só então código por arquivo, com testes e "como validar". O usuário faz o commit, em português. Ao fim, o resumo do que mudou nos ADRs e no guia. Uma fase só termina com os testes obrigatórios dela passando.

## Consequências

- O produto só fica utilizável a partir da fase 6. Em troca, cada fase entrega algo testado e o risco técnico cai cedo.
- O esqueleto exige mais cerimônia antes da primeira funcionalidade.
- Migrations incrementais mantêm o banco sem tabelas sem uso, mas exigem ordem consistente entre as fases.
- Imagens depois do estudo adiam a validação do ponto mais incerto (upload da ilha e leitura binária no cliente). Se o spike de imagens revelar mudança de desenho, o impacto sobre cartão e estudo é pequeno, pois o vínculo é por referência no Markdown.
- Sem CI, a ordem depende de rodar `dotnet test` a cada fase.

## Como será testado

A ordem em si não tem teste. Cada fase tem seu critério de conclusão na tabela, derivado da consolidação do ADR 0019.

## Alternativas consideradas

- **Spikes isolados primeiro, em projetos descartáveis:** reduz risco antes de comprometer o esqueleto, mas descarta código, e o spike de autenticação precisa do esqueleto e da Foundation de qualquer forma.
- **Só fatias verticais, sem fases de spike:** menos planejamento, mas contraria o ADR 0014.
- **Começar pelo Cartão:** entrega valor central mais cedo, mas arrasta Markdown, pastas e limites de uma só vez.
- **Imagens antes do estudo:** derrisca o ponto mais incerto, mas adia o produto utilizável.
- **Migration inicial com o modelo inteiro:** uma só migration, mas cria tabelas que nenhum código usa ainda.
