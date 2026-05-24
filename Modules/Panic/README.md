# Panic Module

The Panic module is introduced in Stage 5.

It provides an approved kernel-safe panic call:

- `Panic.Halt(string Reason)` writes a failure diagnostic and halts forever.

Stage 5 proves the panic call is available through the same binding catalogue as every other approved Oryn module. User-facing C# can request a panic, but the unsafe halt loop remains inside approved native module code.
