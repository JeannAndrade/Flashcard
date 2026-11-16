# Decisões de arquitetura (ADRs)

Registro das decisões do projeto Flashcard. Formato e regras no [ADR 0001](0001-registro-de-decisoes-de-arquitetura.md).

| ADR | Decisão | Status |
| --- | --- | --- |
| [0001](0001-registro-de-decisoes-de-arquitetura.md) | Registro de decisões de arquitetura (ADRs) | Aceita |
| [0002](0002-estrutura-da-solution-e-nomes-dos-projetos.md) | Estrutura da solution e nomes dos projetos | Aceita |
| [0003](0003-contrato-entre-presentation-e-service.md) | Contrato entre Presentation e Service | Aceita |
| [0004](0004-imagens-nos-cartoes.md) | Imagens nos cartões (máximo de duas) | Aceita |
| [0005](0005-repeticao-espacada-leitner.md) | Repetição espaçada com Leitner | Aceita |
| [0006](0006-banco-de-dados-postgresql.md) | Banco de dados: PostgreSQL | Aceita |
| [0007](0007-front-blazor-web-app-com-ssr-e-ilhas-interativas.md) | Front: Blazor Web App com SSR e ilhas interativas | Aceita |
| [0008](0008-isolamento-multi-tenant.md) | Isolamento multi-tenant | Aceita |
| [0009](0009-endurecimento-de-seguranca-do-auth.md) | Endurecimento de segurança do Auth | Aceita |
| [0010](0010-application-com-commands-e-queries.md) | Application com Commands e Queries | Aceita |
| [0011](0011-linguagem-do-dominio-e-idioma.md) | Linguagem do domínio e idioma | Aceita |
| [0012](0012-portas-de-repositorio-na-application.md) | Portas de repositório na Application | Aceita |
| [0013](0013-cadastro-aberto-e-limite-de-taxa.md) | Cadastro aberto e limite de taxa | Aceita / Proposta (limite de taxa) |
| [0014](0014-autenticacao-nas-ilhas-interativas.md) | Autenticação nas ilhas interativas | Aceita |

## Previstos (rodada seguinte)

Renderização e sanitização de Markdown; armazenamento e entrega de imagens; estratégia de testes (xUnit, PostgreSQL real em contêiner, testes de contrato e de arquitetura); padrão de erros e validação; modelagem das entidades (`Entity`/`Guid` e `trackChanges`); limite de taxa; observabilidade (NLog e health checks).
