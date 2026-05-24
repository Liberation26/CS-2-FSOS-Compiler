# Generate Your First OS

Oryn 2.0.2 uses the visual configurator.

```bash
./Oryn.sh new
```

The configurator shows every question from the installed version's `Questions/*.question.json` files.

Use `OS Title` for the friendly title. Use `OS Name` for the strict technical name with no spaces. Use `Kernel Name` for the strict generated kernel name.

After saving, Oryn creates:

```text
OSes/<OsName>/
```

Then it can build and run the generated freestanding OS.

To change answers later:

```bash
./Oryn.sh configure <OsName>
```
