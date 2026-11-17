using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;

namespace MelvorMods.Loader;

// Called by the winmm proxy once the game's .NET runtime is up, before the game assembly loads. The proxy passes the
// folder of the Loader copy it chose (a mod's loader/ folder): this assembly's own, with the host and Harmony beside
// it and the logs under it.
public static partial class Entry
{
    private const string GameAssembly = "GamesByMalcs.Melvor2";
    private static string _copy = "";
    private static int _started;

    [UnmanagedCallersOnly]
    public static void Init(nint copyFolder)
    {
        try
        {
            _copy = Marshal.PtrToStringUni(copyFolder)!;
            Log.Open(Path.Combine(_copy, "logs", WorkerLogName() ?? "loader.log"));
            Log.Write($"Loader {typeof(Entry).Assembly.GetName().Version}, .NET {Environment.Version}");
            AppDomain.CurrentDomain.AssemblyLoad += OnAssemblyLoad;
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                if (assembly.GetName().Name == GameAssembly)
                    Start(assembly);
        }
        catch (Exception e)
        {
            Log.Write($"Init failed: {e}");
        }
    }

    private static void OnAssemblyLoad(object? sender, AssemblyLoadEventArgs e)
    {
        if (e.LoadedAssembly.GetName().Name == GameAssembly)
            Start(e.LoadedAssembly);
    }

    // A worker (a headless game instance a mod started) logs beside the live game's loader.log, never over it.
    // The host and the mod read the worker's arguments; here only the log name matters. A flag that can't be read
    // gets a log of its own too: that process is about to end, and must leave the live game's log alone.
    private static string? WorkerLogName()
    {
        const string flag = "--melvormods-worker=";
        foreach (var arg in CommandLine())
        {
            if (!arg.StartsWith(flag, StringComparison.Ordinal))
                continue;
            var parts = arg[flag.Length..].Split(':');
            var valid =
                parts.Length == 2
                && parts.All(p => p.Length > 0 && p.All(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '-'));
            return valid ? $"loader.{parts[0]}.{parts[1]}.log" : "loader.worker-error.log";
        }
        return null;
    }

    // The process's own command line from the OS: .NET may not have been given the arguments of the game hosting it.
    private static string[] CommandLine()
    {
        var argv = CommandLineToArgvW(GetCommandLineW(), out var count);
        if (argv == 0)
            return Environment.GetCommandLineArgs();
        try
        {
            var args = new string[count];
            for (var i = 0; i < count; i++)
                args[i] = Marshal.PtrToStringUni(Marshal.ReadIntPtr(argv, i * IntPtr.Size)) ?? "";
            return args;
        }
        finally
        {
            LocalFree(argv);
        }
    }

    [LibraryImport("kernel32.dll")]
    private static partial nint GetCommandLineW();

    [LibraryImport("shell32.dll")]
    private static partial nint CommandLineToArgvW(nint commandLine, out int count);

    [LibraryImport("kernel32.dll")]
    private static partial nint LocalFree(nint memory);

    // The host and mods go into the game assembly's load context so they bind to the same game types.
    private static void Start(Assembly game)
    {
        if (Interlocked.Exchange(ref _started, 1) == 1)
            return;
        AppDomain.CurrentDomain.AssemblyLoad -= OnAssemblyLoad;
        try
        {
            var context = AssemblyLoadContext.GetLoadContext(game)!;
            Log.Write($"Game assembly in load context '{context.Name}' ({context.GetType().Name})");
            context.Resolving += (ctx, name) =>
            {
                var path = Path.Combine(_copy, name.Name + ".dll");
                return File.Exists(path) ? ctx.LoadFromAssemblyPath(path) : null;
            };
            var host = context.LoadFromAssemblyPath(Path.Combine(_copy, "MelvorMods.Host.dll"));
            var start = host.GetType("MelvorMods.Host.ModHost", throwOnError: true)!
                .GetMethod("Start", BindingFlags.Public | BindingFlags.Static)!;
            Log.Write($"Host started: {start.Invoke(null, [_copy])}");
        }
        catch (Exception e)
        {
            Log.Write($"Start failed: {e}");
        }
    }
}

internal static class Log
{
    private static string? _path;

    public static void Open(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "");
        _path = path;
    }

    public static void Write(string message)
    {
        if (_path != null)
            File.AppendAllText(_path, $"{DateTime.Now:HH:mm:ss.fff} {message}\n");
    }
}
