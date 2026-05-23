
## 0.2.4 Runqemu build visibility fix

`Runqemu.sh` now displays `dotnet build` output while building `Oryn.Compiler` and stores the same output in `Build/Oryn.Compiler.build.log`. The build step is timeout-protected using `ORYN_COMPILER_BUILD_TIMEOUT` so a stuck restore or build server no longer looks like a silent update hang.
