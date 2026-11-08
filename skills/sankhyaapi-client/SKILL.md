---
name: sankhyaapi-client
description: Guia de uso do pacote NuGet SankhyaAPI.Client (integração .NET com o ERP Sankhya) — setup inicial com ou sem injeção de dependência, mapeamento de entidades (mapa fluente e atributos), CRUD com update parcial, consultas paginadas, SQL seguro, relatórios, tratamento de erros tipados com status HTTP, testes com SankhyaAPI.Client.Testing e migração entre versões do pacote a partir do CHANGELOG. Use quando o usuário pedir para integrar com o Sankhya, configurar ou atualizar o SankhyaAPI.Client, mapear uma tabela/entidade do Sankhya, consultar ou gravar dados, tratar erros do Sankhya, escrever testes da integração ou migrar de uma versão anterior do pacote.
---

# SankhyaAPI.Client

Pacote .NET (net8.0 e net10.0) que fala com o `service.sbr` do Sankhya: sessão, CRUD de entidades (loadRecords, saveRecord, removeRecord), SQL (executeQuery) e relatórios.

## Primeiro passo: descobrir a situação

1. **Versão instalada**: procure `PackageReference Include="SankhyaAPI.Client"` nos `.csproj` do projeto do usuário.
2. **Versão alvo**: a que o usuário pediu, ou a mais recente (`dotnet package search SankhyaAPI.Client --exact-match`, ou a pasta mais nova em `~/.nuget/packages/sankhyaapi.client/`).
3. Escolha o roteiro:

| Situação | Leia |
|---|---|
| Sem o pacote, integração nova | [`references/setup.md`](references/setup.md) e, para cada tabela, [`references/mapeamento.md`](references/mapeamento.md) |
| Pacote instalado em versão anterior à alvo | [`references/migracao.md`](references/migracao.md): **sempre** leia o CHANGELOG entre as duas versões antes de mudar código |
| Gravar, consultar, SQL, relatório | [`references/operacoes.md`](references/operacoes.md) |
| Tratar erro, devolver status HTTP, regra de negócio em trigger | [`references/erros.md`](references/erros.md) |
| Testar a integração sem Sankhya real | [`references/testes.md`](references/testes.md) |

## Onde estão as fontes de verdade

Com o pacote restaurado, a pasta da versão traz tudo (troque `{versao}`):

- `~/.nuget/packages/sankhyaapi.client/{versao}/CHANGELOG.md`: o que mudou em cada versão, com as mudanças de comportamento marcadas com ⚠.
- `~/.nuget/packages/sankhyaapi.client/{versao}/docs/migracao-6.0.md`: guia 5.x → 6.0, funcionalidade por funcionalidade.
- `~/.nuget/packages/sankhyaapi.client/{versao}/README.md`: referência completa da API.
- `~/.nuget/packages/sankhyaapi.client/{versao}/lib/net8.0/SankhyaAPI.Client.xml`: comentários XML de cada membro público.

Sem acesso à pasta, use o repositório: https://github.com/SamuelGFDias/SankhyaAPI (arquivos `CHANGELOG.md`, `README.md`, `docs/`).

Nesta skill, prefira o que está nesses arquivos ao que você lembra: a API muda entre versões e o CHANGELOG da versão instalada é quem manda.

## Regras que valem sempre

- **Credenciais** vêm de user-secrets, variável de ambiente ou cofre. Nunca escreva senha em código, `appsettings.json` versionado, log ou chat.
- **URL base** termina em `/mge` (por exemplo `https://erp.empresa.com.br/mge`), com https. `AllowHttp` só em desenvolvimento.
- **Valor vindo do usuário** nunca é concatenado em expressão ou SQL: use `Query().Where("... ?", valor)`, `FindAsync(expr, [Parameter...])` ou `SankhyaSql` interpolado.
- **Um cliente por aplicação**: `AddSankhya` registra singleton. Fora de DI, crie um `SankhyaClient` e descarte com `await using`.
- **Trate erro por `ex.Kind`**, nunca por texto da mensagem.

## Armadilhas do Sankhya que o pacote já cobre (e que o usuário precisa saber)

- O saveRecord **cria** o registro quando a chave do update não existe (upsert). O pacote confere antes e lança `SankhyaNotFoundException`, pela opção `VerifyExistenceOnUpdate`, que vem ligada.
- O executeQuery corta em **5000 linhas**. O pacote lança `SankhyaTruncatedResultException`; para mais linhas, use `QueryPagedAsync`.
- O Sankhya recusa duas requisições simultâneas na mesma sessão. O pacote usa um pool de sessões (`MaxSessions`, padrão 4).
- Itens de nota (`ItemNota`/TGFITE) não aceitam inclusão pelo CRUD genérico. O servidor recusa sem mensagem, e o pacote lança `Unknown` com a explicação.
- `ModifiedSince` exige o parâmetro `LOGTABOPER` habilitado no Sankhya.
- O servidor não valida tudo: data inválida e opção fora da lista podem ser gravadas. Valide no domínio.
