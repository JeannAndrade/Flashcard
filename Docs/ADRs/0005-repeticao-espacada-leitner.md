# ADR 0005 — Repetição espaçada com Leitner

- **Status:** Aceita
- **Data:** 2026-11-15

## Contexto

O app agenda quando cada `Cartao` volta a aparecer. Não é necessária muita precisão; a preferência é por algoritmos simples. O cartão errado deve reaparecer só na **próxima sessão de estudo**, e as datas de revisão são em **UTC**.

## Decisão

**Leitner (caixas).** Avaliações: "Errei", "Foi difícil", "Foi fácil".

| Avaliação | Caixa | Próxima revisão |
| --- | --- | --- |
| Errei | volta para a caixa 1 | instante da avaliação (agora) |
| Foi difícil | permanece na caixa atual | instante da avaliação (agora) |
| Foi fácil | sobe uma caixa (na última, permanece) | agora + intervalo da **nova** caixa |

Intervalos por caixa: **1, 3, 7, 14 e 30 dias**, como constantes no Domínio. Eles só se aplicam a "Foi fácil".

- A próxima revisão é um **instante UTC**. O tempo vem de `IDateTimeProvider` (`UtcDateTimeProvider`), nunca de `DateTime.Now`.
- **Sessão sem entidade persistida:** ao iniciar o estudo, o servidor fixa o instante UTC de início. A fila contém os cartões da pasta escolhida com revisão **≤ esse instante**. Um cartão errado recebe revisão = agora (> início), portanto não volta na mesma sessão e é o primeiro da próxima.
- O cálculo é uma **classe de domínio pura** (entra estado e avaliação, sai novo estado), sem interface. Extrai-se uma interface se surgir um segundo algoritmo.

## Consequências

- Regra trivial de explicar e de testar (tabela de casos), sem banco e sem relógio.
- Com instantes UTC não há problema de data truncada (um dia UTC termina às 21h em Brasília).
- A noção de "dia" para o usuário (por exemplo, "cartões de hoje") não é calendário local; se isso incomodar, revisita-se em novo ADR.
- Trocar de algoritmo exige migrar o estado do cartão (caixa e próxima revisão).
- Um cartão avaliado como "Foi difícil" volta a cada sessão até ser marcado como "Foi fácil", sem subir de caixa nesse meio tempo.

## Histórico

- **2026-11-23:** a versão original fazia o "Foi difícil" esperar o intervalo da caixa atual e deixava ambígua a próxima revisão do "Errei". Isso contrariava a definição do projeto (errou ou teve dificuldade → reaparece na próxima sessão). Corrigido por decisão do usuário: "Errei" e "Foi difícil" agendam a revisão para o instante da avaliação.

## Alternativas consideradas

- **Intervalo multiplicativo (×1,2 e ×2,5):** simples, mas o intervalo cresce sem teto.
- **SM-2 completo:** mais preciso, mas desnecessário para o objetivo.
- **Reapresentar o cartão errado na mesma sessão:** contraria a regra definida pelo usuário.
