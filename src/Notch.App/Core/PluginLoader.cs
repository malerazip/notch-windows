using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using Notch.Sdk;

namespace Notch.App.Core;

/// <summary>
/// Carrega plugins de %APPDATA%\ProjetoNotch\plugins\&lt;pasta&gt;\*.dll.
/// Cada pasta ganha seu próprio contexto de carregamento; Notch.Sdk e WPF são compartilhados com o app.
/// </summary>
internal static class PluginLoader
{
    public static List<INotchModule> Load(string pluginsDir)
    {
        var modules = new List<INotchModule>();
        if (!Directory.Exists(pluginsDir)) return modules;

        foreach (var dir in Directory.GetDirectories(pluginsDir))
        {
            foreach (var dll in Directory.GetFiles(dir, "*.dll"))
            {
                if (Path.GetFileName(dll).Equals("Notch.Sdk.dll", StringComparison.OrdinalIgnoreCase)) continue;
                try
                {
                    var context = new PluginContext(dll);
                    var assembly = context.LoadFromAssemblyPath(Path.GetFullPath(dll));
                    foreach (var type in assembly.GetExportedTypes())
                    {
                        if (type.IsAbstract || !typeof(INotchModule).IsAssignableFrom(type)) continue;
                        if (Activator.CreateInstance(type) is INotchModule module)
                        {
                            modules.Add(module);
                            Paths.WriteLog($"Plugin carregado: {module.Id} ({dll})");
                        }
                    }
                }
                catch (Exception ex) when (ex is BadImageFormatException or FileLoadException or ReflectionTypeLoadException or TypeLoadException or MissingMethodException or TargetInvocationException)
                {
                    Paths.WriteLog($"Falha ao carregar plugin {dll}: {ex.Message}");
                }
            }
        }
        return modules;
    }

    private sealed class PluginContext(string mainAssemblyPath) : AssemblyLoadContext(isCollectible: false)
    {
        private readonly AssemblyDependencyResolver _resolver = new(mainAssemblyPath);

        protected override Assembly? Load(AssemblyName name)
        {
            // Tipos compartilhados precisam vir do app, senão INotchModule não "casa".
            if (name.Name == "Notch.Sdk") return typeof(INotchModule).Assembly;
            // Reaproveita o que o app já carregou (WinRT, NAudio...) para não duplicar
            var shared = Default.Assemblies.FirstOrDefault(a => a.GetName().Name == name.Name);
            if (shared != null) return shared;
            var path = _resolver.ResolveAssemblyToPath(name);
            return path != null ? LoadFromAssemblyPath(path) : null;
        }
    }
}
