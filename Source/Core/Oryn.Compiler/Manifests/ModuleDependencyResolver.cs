namespace Oryn.Compiler;

internal sealed class ModuleDependencyResolver
{
    public IReadOnlyList<ModuleManifestRecord> Resolve(IReadOnlyList<ModuleManifestRecord> SelectedModules)
    {
        Dictionary<string, ModuleManifestRecord> ByName = new(StringComparer.Ordinal);
        foreach (ModuleManifestRecord Module in SelectedModules)
        {
            if (ByName.ContainsKey(Module.ModuleName))
            {
                throw new OrynCompileException($"Duplicate module manifest selected: {Module.ModuleName}");
            }

            ByName.Add(Module.ModuleName, Module);
        }

        foreach (ModuleManifestRecord Module in SelectedModules)
        {
            foreach (string Dependency in Module.DependsOn)
            {
                if (!ByName.ContainsKey(Dependency))
                {
                    throw new OrynCompileException($"Module {Module.ModuleName} requires missing dependency {Dependency}.");
                }
            }
        }

        List<ModuleManifestRecord> Resolved = new();
        Dictionary<string, int> State = new(StringComparer.Ordinal);
        Stack<string> Stack = new();

        foreach (ModuleManifestRecord Module in SelectedModules.OrderBy(Module => Module.InitializeOrder).ThenBy(Module => Module.ModuleName, StringComparer.Ordinal))
        {
            Visit(Module.ModuleName, ByName, State, Stack, Resolved);
        }

        return Resolved;
    }

    private static void Visit(
        string ModuleName,
        IReadOnlyDictionary<string, ModuleManifestRecord> ByName,
        Dictionary<string, int> State,
        Stack<string> Stack,
        List<ModuleManifestRecord> Resolved)
    {
        if (State.TryGetValue(ModuleName, out int CurrentState))
        {
            if (CurrentState == 2)
            {
                return;
            }

            if (CurrentState == 1)
            {
                List<string> Cycle = Stack.Reverse().SkipWhile(Name => !Name.Equals(ModuleName, StringComparison.Ordinal)).ToList();
                Cycle.Add(ModuleName);
                throw new OrynCompileException("Circular module dependency detected: " + string.Join(" -> ", Cycle));
            }
        }

        State[ModuleName] = 1;
        Stack.Push(ModuleName);
        ModuleManifestRecord Module = ByName[ModuleName];
        foreach (string Dependency in Module.DependsOn.OrderBy(Dependency => ByName[Dependency].InitializeOrder).ThenBy(Dependency => Dependency, StringComparer.Ordinal))
        {
            Visit(Dependency, ByName, State, Stack, Resolved);
        }

        Stack.Pop();
        State[ModuleName] = 2;
        Resolved.Add(Module);
    }
}
