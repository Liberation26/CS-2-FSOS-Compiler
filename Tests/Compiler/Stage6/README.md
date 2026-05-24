# Stage 6 tests

Stage 6 tests prove service/module manifest loading. They validate that the compiler sees manifest metadata, the build creates generated manifest initialization glue, and QEMU reaches the Stage 6 manifest proof lines.

Run:

```bash
./Tests/Compiler/Stage6/run.sh
```


## 0.6.1 additions

The QEMU boot test also checks for the native pre-kernel handoff line, ManifestLoader glue activation, and generated manifest runtime completion so Stage 6 cannot silently pass build-side manifest selection without proving runtime initialization.
