```

BenchmarkDotNet v0.14.0, Windows 11 (10.0.26100.1742)
Unknown processor
.NET SDK 9.0.116
  [Host]     : .NET 9.0.15 (9.0.1526.17522), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI
  DefaultJob : .NET 9.0.15 (9.0.1526.17522), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI


```
| Method              | Mean     | Error    | StdDev   | Ratio | Allocated | Alloc Ratio |
|-------------------- |---------:|---------:|---------:|------:|----------:|------------:|
| FullDecode_Fps1     | 19.328 s | 0.0980 s | 0.0765 s |  1.00 | 764.52 KB |        1.00 |
| KeyframeOnly_Scaled |  4.621 s | 0.0201 s | 0.0178 s |  0.24 | 218.75 KB |        0.29 |
