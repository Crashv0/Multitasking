using System.Runtime.InteropServices;

namespace MelvorMods.Core;

/// <summary>
/// What this process is for a mod: the live game, a worker of its own, another mod's worker, or a process whose
/// worker flag can't be read.
/// </summary>
internal enum WorkerKind
{
    Live,
    Own,
    Other,
    Malformed,
}

/// <summary>The worker a process was started as, if any, and what that means for one mod.</summary>
/// <param name="Role">The worker's role when <see cref="Kind"/> is <see cref="WorkerKind.Own"/>.</param>
/// <param name="Flag">The flag as it was given when <see cref="Kind"/> is <see cref="WorkerKind.Malformed"/>.</param>
internal readonly record struct WorkerDecision(WorkerKind Kind, string? Role = null, string? Flag = null);

/// <summary>The worker a command line names: a mod, and the part of it the process runs.</summary>
internal readonly record struct WorkerName(string Mod, string Role);

/// <summary>
/// Pure helpers for a mod that runs alone: which process it is in, and what its log is called. Godot-free, and safe
/// before Godot takes calls.
/// </summary>
internal static partial class Standalone
{
    public const string WorkerFlag = "--melvormods-worker=";

    /// <summary>A mod's settings file, at the top of its data folder.</summary>
    public const string SettingsFile = "settings.json";

    /// <summary>The Harmony category of the shared code's own patches, which its host applies, never a mod's PatchAll.</summary>
    public const string PatchCategory = "melvormods.shared";

    /// <summary>What a process started with a worker flag that can't be read exits with.</summary>
    public const int MalformedWorkerExit = 2;

    /// <summary>
    /// The worker <paramref name="args"/> name, as <c>&lt;mod&gt;:&lt;role&gt;</c> (letters, digits, <c>_</c> and
    /// <c>-</c>); null when there is no worker flag. A flag that can't be read gives null too, and
    /// <paramref name="malformed"/> is then the flag as given.
    /// </summary>
    public static WorkerName? Worker(IReadOnlyList<string> args, out string? malformed)
    {
        ArgumentNullException.ThrowIfNull(args);
        malformed = null;
        var flag = args.FirstOrDefault(a => a.StartsWith(WorkerFlag, StringComparison.Ordinal));
        if (flag == null)
            return null;
        var parts = flag[WorkerFlag.Length..].Split(':');
        if (parts.Length == 2 && parts.All(IsName))
            return new(parts[0], parts[1]);
        malformed = flag;
        return null;
    }

    private static bool IsName(string part) =>
        part.Length > 0 && part.All(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '-');

    /// <summary>
    /// What <paramref name="args"/> make this process for mod <paramref name="id"/>. A worker flag naming another mod
    /// means the mod isn't here at all; one that can't be read means the process must end: in neither does the mod
    /// load as if this were the live game.
    /// </summary>
    public static WorkerDecision Decide(IReadOnlyList<string> args, string id)
    {
        var worker = Worker(args, out var malformed);
        if (malformed != null)
            return new(WorkerKind.Malformed, Flag: malformed);
        if (worker is not { } named)
            return new(WorkerKind.Live);
        return named.Mod.Equals(id, StringComparison.OrdinalIgnoreCase)
            ? new(WorkerKind.Own, named.Role)
            : new(WorkerKind.Other);
    }

    /// <summary>The folder holding the game's exe.</summary>
    public static string GameDirectory(string exePath) =>
        Path.GetDirectoryName(Path.GetFullPath(exePath)) ?? throw new ArgumentException("no folder", nameof(exePath));

    /// <summary>
    /// A mod's folder for its settings, logs and data: the folder it is installed in, so removing that folder removes
    /// the mod and all it wrote, unless its loader keeps data elsewhere and says where.
    /// </summary>
    public static string DataRoot(string installFolder, string? given) => given ?? installFolder;

    /// <summary>A mod's log file name: <c>&lt;Id&gt;.log</c>, or <c>&lt;Id&gt;.&lt;role&gt;.log</c> in its own worker.</summary>
    public static string LogName(string id, string? role = null) => role == null ? $"{id}.log" : $"{id}.{role}.log";

    /// <summary>
    /// This process's own command line, split as Windows splits it. Read from the OS rather than from .NET, which
    /// may not have been given the arguments of a game that hosts it.
    /// </summary>
    public static IReadOnlyList<string> ProcessArgs()
    {
        if (!OperatingSystem.IsWindows())
            return Environment.GetCommandLineArgs();
        var argv = Native.CommandLineToArgvW(Native.GetCommandLineW(), out var count);
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
            Native.LocalFree(argv);
        }
    }

    private static partial class Native
    {
        [LibraryImport("kernel32.dll")]
        internal static partial nint GetCommandLineW();

        [LibraryImport("shell32.dll", SetLastError = true)]
        internal static partial nint CommandLineToArgvW(nint commandLine, out int count);

        [LibraryImport("kernel32.dll")]
        internal static partial nint LocalFree(nint memory);
    }
}
