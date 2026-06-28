using BenchmarkDotNet.Running;

// Run with a real job:   dotnet run -c Release
// Quick smoke (no stats): dotnet run -c Release -- --job dry --filter *
BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
