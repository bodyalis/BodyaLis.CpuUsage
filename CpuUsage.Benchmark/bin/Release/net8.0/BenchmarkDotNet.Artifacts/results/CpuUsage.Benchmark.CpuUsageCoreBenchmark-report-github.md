```

BenchmarkDotNet v0.15.2, Windows 10 (10.0.19045.3930/22H2/2022Update)
AMD Ryzen 5 3500X 3.95GHz, 1 CPU, 6 logical and 6 physical cores
.NET SDK 8.0.302
  [Host]     : .NET 8.0.18 (8.0.1825.31117), X64 RyuJIT AVX2
  DefaultJob : .NET 8.0.18 (8.0.1825.31117), X64 RyuJIT AVX2


```
| Method                     | Mean     | Error     | StdDev   | Median   | Rank | Gen0   | Allocated |
|--------------------------- |---------:|----------:|---------:|---------:|-----:|-------:|----------:|
| MeasureCurrentProcessUsage | 9.875 μs | 0.5808 μs | 1.638 μs | 9.374 μs |    1 | 0.1373 |   1.14 KB |
