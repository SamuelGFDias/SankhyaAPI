# Erros

Toda falha do Sankhya ou do transporte chega como `SankhyaException` (namespace `SankhyaAPI.Client.Exceptions`), com:

- `Kind` (`SankhyaErrorKind`): a categoria. **Trate por ela, nunca pelo texto.**
- `SuggestedHttpStatus`: o status para devolver na API do usuário. Nunca é 500 para erro conhecido.
- `Message`: a mensagem do servidor, limpa. Em `BusinessRule`, é o texto escrito pela regra, pronto para mostrar ao usuário final.
- `TsErrorCode`, `Field` (campo envolvido, quando o servidor diz), `Status` e `ServiceName`.
- `ToProblem()`: um `SankhyaProblem` no formato de ProblemDetails (RFC 9457).

| `Kind` | Exemplo real | HTTP |
|---|---|---|
| `AuthenticationFailed` | "Usuário/Senha inválido." (`CORE_E01434`/`CORE_E01428`) | 401 |
| `SessionExpired` | status 3 depois da repetição automática | 401 |
| `Concurrency` | "situação de concorrência" (mesma sessão em paralelo) | 409 |
| `InvalidRequest` | campo/entidade/relacionamento inexistente, sintaxe, DML no executeQuery, serviço inexistente | 400 |
| `QueryFailed` | "Erro de divisão por zero." | 400 |
| `InvalidValue` | "Erro de conversão...", "largura acima do limite" (`Field`), "Empresa não encontrada", "TOP não encontrada.", "Data truncation" | 422 |
| `DuplicateKey` | "Violação da restrição PRIMARY KEY..." | 409 |
| `ConstraintViolation` | FOREIGN KEY (`Field`), CHECK, NULL em coluna obrigatória | 409 |
| `BusinessRule` | **trigger** (RAISERROR / RAISE_APPLICATION_ERROR), validação do Sankhya ("O preenchimento do endereço do contato é obrigatório.") | 422 |
| `NotFound` | update de chave inexistente, "X não existe: PK[...]" | 404 |
| `ResultTruncated` | executeQuery acima de 5000 linhas | 422 |
| `NotConfigured` | `ModifiedSince` sem LOGTABOPER (`CORE_E07537`) | 501 |
| `Timeout` / `Unavailable` | tempo esgotado / HTTP 5xx, conexão, resposta ilegível | 504 / 503 |
| `Unknown` | sem categoria reconhecida, ou recusa sem mensagem (como incluir item de nota pelo CRUD) | 502 |

Subclasses para `catch`: `SankhyaAuthenticationException`, `SankhyaSessionExpiredException`, `SankhyaBusinessRuleException`, `SankhyaNotFoundException`, `SankhyaTruncatedResultException` e `SankhyaTransportException` (esta com `HttpStatus` e a exceção original em `InnerException`).

## Na API do usuário (minimal API / controller)

```csharp
try
{
    await pedidos.UpdateAsync(pedido, u => u.Set(x => x.Observacao));
    return Results.NoContent();
}
catch (SankhyaException ex)
{
    SankhyaProblem p = ex.ToProblem();
    return Results.Problem(detail: p.Detail, statusCode: p.Status, title: p.Title, type: p.Type);
}
```

Um filtro global (`IExceptionHandler`) que faz isso para toda `SankhyaException` evita repetir o bloco.

## Regras de negócio no banco (triggers)

Clientes Sankhya costumam guardar regras em trigger. Toda recusa de saveRecord ou removeRecord que não é um erro técnico reconhecido vira `SankhyaBusinessRuleException`, e a mensagem chega limpa:
- sem o prefixo `SQL-50001` do RAISERROR;
- sem a frase "A transação foi encerrada no gatilho" do SQL Server;
- sem o código `ORA-20xxx` e a pilha `ORA-06512` do Oracle.

```csharp
catch (SankhyaBusinessRuleException ex)
{
    return Results.UnprocessableEntity(new { mensagem = ex.Message });   // texto da regra, para o usuário
}
```

## Fora da `SankhyaException`

- `FormatException`: valor do servidor que não converte. O valor fica só em `ex.Data["Valor"]`, porque pode ser dado pessoal: não logue sem pensar.
- `ArgumentException`/`ArgumentNullException`/`InvalidOperationException`: uso errado, detectado antes de chamar o servidor (chave, mapa, update sem campos, quantidade de `?`).
- `OperationCanceledException`: o `CancellationToken` foi cancelado.
- `OptionsValidationException`: configuração inválida, na subida.

## O que não é erro, mas deveria preocupar

O servidor aceita com `status="1"`:
- delete de chave inexistente em algumas entidades;
- data inválida;
- opção fora da lista;
- insert sem a chave numa tabela de chave simples, que o servidor numera sozinho.

Valide no domínio o que importa.
