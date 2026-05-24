# Oryn Manual

Current version: **2.0.1**

## Start here

Use the visual-first command:

```bash
./Oryn.sh new
```

This opens `OrynVisualConfigurator`, asks the current version's questions from `Questions/*.question.json`, saves the answers, generates the OS, builds it, and runs it.

## Reopen configuration

```bash
./Oryn.sh configure <OS name or path>
```

You can also search or load:

```bash
./Oryn.sh configure --search
./Oryn.sh configure --load
```

## Important naming rules

`OS Title` is friendly and may contain spaces.

`OS Name` and `Kernel Name` must not contain spaces. They must start with a letter and contain only letters and numbers.

## Build and run

Once configured, Oryn does not ask the questions again unless new required questions are available or existing answers are missing/invalid.

```bash
./Oryn.sh build <OS>
./Oryn.sh run <OS>
```

From inside a generated OS project directory, the OS name/path can be omitted:

```bash
./Oryn.sh build
./Oryn.sh run
```
