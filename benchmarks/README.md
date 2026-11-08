# Benchmarks do SankhyaAPI.Client

Mede o custo do mapeamento da 6.0 com [BenchmarkDotNet](https://benchmarkdotnet.org/) (issue #35). Compara o modelo **por atributos** (`[SankhyaEntity]`, `[Key]`, `[XmlElement]`) com o POCO de **mapa fluente** (`SankhyaEntityMap<T>`) nas três operações que tocam o mapeamento:

| Classe | O que mede |
|---|---|
| `LeituraEntidadesBenchmarks` | `EntityXml.ReadEntities<T>`: lê uma resposta de loadRecords (`<entities>` com `<metadata>` e N `<entity>`, colunas `f0`..`f4`). |
| `InsertBenchmarks` | `EntityXml.Insert`: monta o envelope de insert para N registros. |
| `ExecuteQueryBenchmarks` | `SankhyaSqlClient.Map<T>`: mapeia as linhas de um executeQuery (`fieldsMetadata` + `rows`). O JSON é lido uma vez no `GlobalSetup`, então a medição não inclui o parse da resposta. |

Cada classe roda com `Linhas` = 1000 e 10000. Os dados de entrada são montados no `[GlobalSetup]`, fora da medição.

## Como rodar

Não há `dotnet` local neste ambiente, então o benchmark roda no SDK 10 em container (podman):

```sh
podman run --rm \
  -v "$PWD":/src:ro,Z \
  -v "$PWD/out":/out:Z \
  mcr.microsoft.com/dotnet/sdk:10.0 \
  sh -c 'cp -r /src/. /work && find /work -type d \( -name bin -o -name obj -o -name BenchmarkDotNet.Artifacts \) -prune -exec rm -rf {} + && cd /work && dotnet run -c Release --project benchmarks/SankhyaAPI.Client.Benchmarks -- --filter "*" --job short && cp -r /work/BenchmarkDotNet.Artifacts /out/'
```

Os relatórios ficam em `out/results/*-report-github.md` (no caso desta medição, em `BenchmarkDotNet.Artifacts/results/` na raiz do repositório). A pasta `BenchmarkDotNet.Artifacts/` está no `.gitignore` e não é versionada.

Por que a cópia para `/work`: rodar `dotnet run` direto sobre o repositório montado no container falhou com `UnauthorizedAccessException` quando o BenchmarkDotNet escrevia o log e os binários gerados dentro do bind mount. Compilando e rodando numa cópia dentro do container, a execução termina normalmente. O comando acima é o mesmo fluxo usado nesta medição; a diferença é só o caminho do diretório de saída montado.

Para medir de verdade (números finais, não só conferir que o benchmark roda), use o job padrão, sem `--job short`. O `short` é mais rápido, mas com intervalos de confiança grandes (ver abaixo).

## Ambiente da medição

Saída de cabeçalho do BenchmarkDotNet nesta execução:

- BenchmarkDotNet v0.15.8
- SO (dentro do container): Ubuntu 24.04.5 LTS (Noble Numbat)
- CPU: 13th Gen Intel Core i7-13620H 0.97GHz, 1 CPU, 16 lógicos e 10 físicos
- .NET SDK 10.0.403
- Runtime: .NET 10.0.14 (10.0.14, 10.0.1426.52326), X64 RyuJIT x86-64-v3
- Job: ShortRun, IterationCount=3, LaunchCount=1, WarmupCount=3

O host de desenvolvimento é Fedora; o container roda sobre ele. Números de máquina de desenvolvimento variam bastante, então compare as proporções (coluna `Ratio`) mais que os tempos absolutos.

## Resultados

A coluna `Ratio` usa como base o modelo **Atributos**. `Ratio` menor que 1,00 significa que o mapa fluente foi mais rápido.

### ExecuteQueryBenchmarks (`MapearAtributos` / `MapearFluente`)

| Method         | Linhas | Mean      | Error      | StdDev    | Ratio | RatioSD | Gen0     | Gen1     | Allocated   | Alloc Ratio |
|--------------- |------- |----------:|-----------:|----------:|------:|--------:|---------:|---------:|------------:|------------:|
| **Atributos**      | **1000**   |  **1.639 ms** |  **0.7493 ms** | **0.0411 ms** |  **1.00** |    **0.03** |  **91.7969** |  **31.2500** |  **1136.14 KB** |        **1.00** |
| &#39;Mapa fluente&#39; | 1000   |  1.084 ms |  1.5457 ms | 0.0847 ms |  0.66 |    0.05 |  78.1250 |  25.3906 |   972.05 KB |        0.86 |
|                |        |           |            |           |       |         |          |          |             |             |
| **Atributos**      | **10000**  | **16.653 ms** |  **1.5791 ms** | **0.0866 ms** |  **1.00** |    **0.01** | **906.2500** | **781.2500** | **11339.04 KB** |        **1.00** |
| &#39;Mapa fluente&#39; | 10000  | 12.128 ms | 14.8209 ms | 0.8124 ms |  0.73 |    0.04 | 781.2500 | 640.6250 |  9698.19 KB |        0.86 |

### InsertBenchmarks (`MontarAtributos` / `MontarFluente`)

| Method         | Linhas | Mean       | Error       | StdDev    | Ratio | RatioSD | Gen0     | Gen1     | Allocated  | Alloc Ratio |
|--------------- |------- |-----------:|------------:|----------:|------:|--------:|---------:|---------:|-----------:|------------:|
| **Atributos**      | **1000**   |   **745.3 μs** |  **1,828.7 μs** | **100.24 μs** |  **1.01** |    **0.16** |  **79.1016** |  **48.8281** |  **969.97 KB** |        **1.00** |
| &#39;Mapa fluente&#39; | 1000   |   584.3 μs |    178.3 μs |   9.77 μs |  0.79 |    0.09 |  65.4297 |  38.0859 |  805.91 KB |        0.83 |
|                |        |            |             |           |       |         |          |          |            |             |
| **Atributos**      | **10000**  | **8,476.0 μs** | **12,070.2 μs** | **661.61 μs** |  **1.00** |    **0.09** | **781.2500** | **593.7500** | **9688.74 KB** |        **1.00** |
| &#39;Mapa fluente&#39; | 10000  | 8,570.6 μs | 12,097.6 μs | 663.11 μs |  1.02 |    0.10 | 656.2500 | 460.9375 | 8048.13 KB |        0.83 |

### LeituraEntidadesBenchmarks (`LerAtributos` / `LerFluente`)

| Method         | Linhas | Mean      | Error     | StdDev    | Ratio | RatioSD | Gen0      | Gen1      | Gen2     | Allocated | Alloc Ratio |
|--------------- |------- |----------:|----------:|----------:|------:|--------:|----------:|----------:|---------:|----------:|------------:|
| **Atributos**      | **1000**   |  **2.577 ms** | **1.5117 ms** | **0.0829 ms** |  **1.00** |    **0.04** |  **281.2500** |   **62.5000** |        **-** |    **3.4 MB** |        **1.00** |
| &#39;Mapa fluente&#39; | 1000   |  1.964 ms | 0.8722 ms | 0.0478 ms |  0.76 |    0.03 |  261.7188 |   66.4063 |        - |   3.17 MB |        0.93 |
|                |        |           |           |           |       |         |           |           |          |           |             |
| **Atributos**      | **10000**  | **24.739 ms** | **4.7959 ms** | **0.2629 ms** |  **1.00** |    **0.01** | **2968.7500** | **1125.0000** | **156.2500** |  **34.05 MB** |        **1.00** |
| &#39;Mapa fluente&#39; | 10000  | 20.879 ms | 7.1776 ms | 0.3934 ms |  0.84 |    0.02 | 2750.0000 |  968.7500 | 125.0000 |  31.76 MB |        0.93 |

## Leitura dos números

- **executeQuery:** o mapa fluente aloca cerca de 14% menos e fica mais rápido (ratio 0,66 a 0,73).
- **Leitura de entidades:** o mapa fluente aloca cerca de 7% menos e fica mais rápido (ratio 0,76 a 0,84).
- Nas medições de 10000 linhas de executeQuery e de leitura, o erro é maior que a diferença de tempo, então o ganho de tempo ali é só indicação.
- **Insert:** tempos empatados. Em 10000 linhas o mapa fluente aloca cerca de 17% menos, mas o tempo fica igual (ratio 1,02).
- **Intervalos de confiança:** no job `short` o `Error` é do mesmo tamanho ou maior que o `Mean` em vários casos (ex.: Insert 1000 linhas, `Atributos`: 745 µs ± 1.829 µs). Por isso, as diferenças de tempo nesta tabela não são conclusivas. A proporção de alocação é estável e a conclusão mais segura. Para números que sirvam de decisão, rode sem `--job short`.
- Provavelmente, a diferença vem do caminho de acesso aos membros: o mapa por atributos usa `PropertyInfo.GetValue`/`SetValue` (reflexão), enquanto o mapa fluente compila os acessores uma vez. Isso é uma hipótese a confirmar, não um resultado medido.
