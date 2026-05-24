# Stage 9: generated kernel template composition

Stage 9 introduces a pre-compilation composition step.

Input:

- selected module manifests from `Source/Sdk/ModuleManifests/`,
- approved bindings from `Source/Sdk/Bindings/`,
- approved API contracts from `Source/Sdk/ApiContracts/`,
- a kernel template from `OSes/Stage9/Templates/Kernel.template.cs`.

Output:

- generated safe C# kernel source under the Stage 9 build tree,
- normal Oryn IR/CFG/backend artifacts after validation passes.

Invalid generated calls are blocked before backend/native compilation. That means a failing invalid-call test must not leave generated C, generated assembly, or ELF64 object files behind.
