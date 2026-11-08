using BenchmarkDotNet.Running;

// Uso: dotnet run -c Release --project benchmarks/SankhyaAPI.Client.Benchmarks -- --filter '*' --job short
BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
