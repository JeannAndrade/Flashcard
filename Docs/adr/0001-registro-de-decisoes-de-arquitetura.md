# ADR 0001 — Registro de decisões de arquitetura (ADRs)

- **Status:** Aceita
- **Data:** 2026-11-15

## Contexto

O Flashcard evolui por decisões sucessivas (banco, renderização, camadas, segurança). Sem registro, o "porquê" se perde e decisões reversíveis viram dogma por esquecimento.

## Decisão

Toda decisão que impacte o projeto vira um ADR curto, em português, em `docs/adr/`, numerado em sequência (`NNNN-titulo-em-kebab-case.md`), com o formato:

- **Status** (Proposta, Aceita, Substituída por ADR NNNN, Rejeitada) e **Data**
- **Contexto**
- **Decisão**
- **Consequências**
- **Alternativas consideradas**

Regras:
1. Nenhuma decisão de arquitetura, design ou dependência é tomada sem opções, prós/contras e recomendação discutidos antes.
2. ADR aceito não é editado para mudar o sentido: cria-se um novo ADR que o substitui e o status do antigo passa a "Substituída por ADR NNNN".
3. O índice em `docs/adr/README.md` é atualizado a cada novo ADR.

## Consequências

- Rastreabilidade das decisões e do raciocínio, útil também para o aprendizado.
- Custo pequeno de escrita a cada decisão relevante.

## Alternativas consideradas

- **Registrar só em README/Wiki:** mistura decisão com documentação descritiva e não preserva o histórico.
- **Sem registro formal:** rejeitada; contraria o objetivo de evolutividade documentada.
