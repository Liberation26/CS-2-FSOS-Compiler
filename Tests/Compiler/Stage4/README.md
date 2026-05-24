# Stage 4 Compiler Tests

Stage 4 tests prove the approved module boundary.

| Test | Purpose |
| --- | --- |
| `01-approved-module-calls-compile.sh` | Approved `Diagnostics`, `Memory`, and `Cpu` module calls compile and emit an ELF64 relocatable object. |
| `02-forbidden-module-call-fails.sh` | A normal unapproved call such as `Console.WriteLine` is rejected with a clear Stage 4 diagnostic. |
| `03-forbidden-namespace-fails.sh` | An unapproved `Oryn.Kernel.*` namespace is rejected before IR lowering. |

Run all Stage 4 tests with:

```bash
./Tests/Compiler/Stage4/run.sh
```
