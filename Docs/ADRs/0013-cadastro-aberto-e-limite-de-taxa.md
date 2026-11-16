# ADR 0013 — Cadastro aberto e limite de taxa

- **Status:** Aceita para o cadastro aberto; limite de taxa é **Proposta** (a detalhar na rodada de segurança)
- **Data:** 2026-11-15

## Contexto

O cadastro de usuários é aberto. Mesmo para uso inicialmente pessoal, o endpoint público de registro e o de login são superfície de ataque (abuso de cadastro, força bruta).

## Decisão

- **Cadastro aberto** (decisão do usuário).
- **Proposta:** aplicar *rate limiting* nativo do ASP.NET Core (sem dependência nova) aos endpoints de login, registro e refresh, com políticas e limites definidos na rodada de segurança. Política de senha conforme o Identity da Foundation.

## Consequências

- O isolamento entre usuários (ADR 0008) deixa de ser teórico: é requisito de segurança com testes obrigatórios.
- Registros indesejados são possíveis até que o limite de taxa e outras proteções (por exemplo, confirmação) sejam decididos.

## Alternativas consideradas

- **Cadastro fechado (convite ou administrador):** menor superfície, mas contraria a decisão atual do usuário.
- **Proteção só no proxy reverso:** útil, mas não substitui o limite na aplicação.
