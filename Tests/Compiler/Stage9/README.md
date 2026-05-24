# Stage 9 tests

Stage 9 tests prove generated kernel template composition from selected modules and prove invalid calls fail before backend/native compilation.

Tests:

1. `01-compose-stage9-kernel-template.sh` composes `Kernel.Generated.cs` from `OSes/Stage9/Templates/Kernel.template.cs` and selected module manifests.
2. `02-compile-composed-stage9-kernel.sh` compiles the generated kernel into Oryn IR, reference C/assembly, and a direct ELF64 relocatable object.
3. `03-invalid-call-fails-before-native-compile.sh` proves an unapproved call is rejected before generated C, generated assembly, or object output exists.
4. `04-qemu-stage9-boot.sh` boots the generated kernel and checks for deterministic composition proof diagnostics.
