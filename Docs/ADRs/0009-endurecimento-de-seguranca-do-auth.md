# ADR 0009 — Endurecimento de segurança do Auth da Foundation

- **Status:** Aceita (correções já entregues na Foundation)
- **Data:** 2026-11-15

## Contexto

Ao avaliar o pacote `Auth`, três pontos de segurança foram identificados na Foundation:

1. `JWT_SECRET` tinha valor de reserva público, embutido no pacote: um deploy sem a variável aceitaria tokens forjados por qualquer pessoa.
2. O refresh token era persistido em texto puro.
3. O `DbConnectionHelper` imprimia a connection string, com senha, no console em Development.

A regra era resolvê-los antes do primeiro deploy fora do ambiente local.

## Decisão

Os três itens foram corrigidos na Foundation. O Flashcard exige, no mínimo:

- **`Lumia.Foundation.Auth` 0.3.1**
- **`Lumia.Foundation.EFRepository` 0.14.1**

Regras do projeto, alinhadas ao guia da Foundation:
- Credenciais e segredos (`JWT_SECRET`, usuário e senha do banco, connection strings) **nunca têm valor padrão no código-fonte**. Em produção e homologação vêm de variáveis de ambiente ou secret manager; em desenvolvimento, de `appsettings.Development.json`, *user-secrets* ou do `docker compose` de desenvolvimento.
- Parâmetros não sensíveis (host, porta, nome do banco) podem ter padrão de desenvolvimento, de preferência fora do código.
- A ausência de configuração obrigatória gera erro claro na inicialização, dizendo **qual** variável falta, sem imprimir valores.
- Arquivos de ambiente com valores reais ficam fora do repositório (`.gitignore`); versiona-se só um modelo fictício (por exemplo, `.env.example`).
- Teste de configuração: construir a configuração sem as variáveis obrigatórias deve falhar.

## Consequências

- Os pacotes mínimos acima fixam a baseline de segurança; versões anteriores não podem ser usadas.
- Falha explícita em vez de comportamento perigoso por configuração ausente.
- O Vault deve estar nas mesmas versões mínimas, pois as correções o atingem.

## Alternativas consideradas

- **Tratar só no primeiro deploy:** aceita como prazo máximo, mas a correção já existe, então não há motivo para adiar.
- **Contornar por configuração no Flashcard sem corrigir a Foundation:** deixaria a vulnerabilidade latente em outros projetos.
