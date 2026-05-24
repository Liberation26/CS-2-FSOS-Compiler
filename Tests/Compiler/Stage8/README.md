# Stage 8 compiler tests

Stage 8 tests prove module API contracts and approved calls from C#.

The tests check:

1. Stage 8 compiler/link output exists.
2. compiler diagnostics include API contract approval proof lines.
3. API contract JSON files exist for approved modules.
4. an uncontracted C# module call is rejected.
5. QEMU reaches the expected Stage 8 proof output and timeout-after-halt is treated as success.

Run:

```bash
./Tests/Compiler/Stage8/run.sh
```
