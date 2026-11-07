---
name: sankhya-protocolo
description: Comportamento constatado da API do Sankhya (service.sbr) neste projeto — login/logout e cookie JSESSIONID, status 1/0/3, base64 e encoding, loadRecords (metadata, paginação de 50, orderByExpression, parâmetros S/I/F/D/H), executeQuery (teto de 5000, burstLimit, maxRows, sem parâmetros), saveRecord/removeRecord, chave composta, dicionário TDDINS/TDDCAM/TDDOPC e tipos de campo. Use antes de pesquisar na web sobre a API do Sankhya, ao mexer em envelopes, conversão de tipos, sessão ou consultas do SankhyaAPI.Client, ou ao escrever teste real contra a homologação.
---

# Protocolo do Sankhya

A referência está em [`docs/sankhya-protocolo.md`](../../../docs/sankhya-protocolo.md). Leia a seção do assunto antes de pesquisar na web: ela registra o que foi **sondado na homologação** (H), coberto por **teste real** (T) ou só **documentado** (D). Só a parte marcada D ainda precisa de confirmação.

## Regras

1. **Fato antes de suposição.** Se a referência não cobre o caso, sonde a homologação com `scripts/sondar-sankhya.sh` (que lê as credenciais de `~/.config/sankhyaapi/teste.env`, nunca as imprime e faz logout). Use a web só para a documentação oficial listada na seção 1 da referência. Não existe documentação do executeQuery.
2. **Registre o que descobrir.** Todo fato novo entra em `docs/sankhya-protocolo.md`, na seção certa, com a fonte (H/T/D). Se ele mudar o comportamento do pacote, ganha também um teste real em `tests/SankhyaAPI.Client.Tests/Real/`.
3. **Escrita real só na `AD_TESTE`.** Use ID aleatório alto, apague no `finally` e confira que a tabela ficou vazia. Testes de escrita usam `[Trait("Category", "RealEscrita")]`, e os de leitura, `Category=Real`.
4. **Segredos.** URL, usuário e senha nunca entram em arquivo versionado, log ou chat. O repositório é público.

## Armadilhas mais caras (detalhe na referência)

- Um logout em rota errada ou sem `JSESSIONID=` responde 200 e não encerra a sessão.
- Erro decide-se pelo `status` (`3` = sessão inválida), não pela presença de mensagem.
- Sem `outputType=json`, o executeQuery com erro responde em XML.
- O executeQuery corta em 5000 linhas (`burstLimit`). O `maxRows` só reduz e, com ele, o `burstLimit` não é marcado.
- O loadRecords sem paginação traz tudo, sem limite: cuidado com memória.
- As colunas do loadRecords se mapeiam pela `<metadata>`, porque os campos de apresentação vêm no fim.
- O servidor lê o corpo XML como UTF-8, qualquer que seja o charset declarado.
- `ORDER BY` dentro de subconsulta é recusado pelo SQL Server.

## Rodar os testes reais

```bash
./scripts/test-real.sh                                        # só leitura
./scripts/test-real.sh "Category=Real|Category=RealEscrita"   # leitura e escrita na AD_TESTE
```
