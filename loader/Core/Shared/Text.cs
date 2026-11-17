using System.Globalization;
using System.Text.RegularExpressions;
using GamesByMalcs.Melvor2;
using Godot;
using HarmonyLib;
using SmartFormat;

namespace MelvorMods.Core;

/// <summary>
/// A mod's translated text. Each mod has its own Godot translation domain, <c>melvormods.&lt;id&gt;</c>, filled
/// from its <c>data/locale/*.po</c> files when it loads and removed when it unloads, so its translations never mix
/// with the game's or another mod's. A message is the English text itself; an untranslated message comes back as
/// it is. Formatting is the game's own: SmartFormat with the game's culture. A mod may show its text in a language
/// of its own instead (<see cref="Language"/>). Main thread only.
/// </summary>
sealed partial class ModText
{
    private const string PseudoPrefix = "[";
    private const string PseudoSuffix = "]";

    // Cached plural lookups are dropped wholesale past this many; counts make the key space open-ended.
    private const int MaxPluralCache = 4096;

    private readonly string _directory;
    private readonly Action<string> _warn;
    private readonly Dictionary<(string? Context, string Message), string> _cache = [];
    private readonly Dictionary<(string? Context, string Singular, string Plural, int Count), string> _plurals = [];
    private readonly HashSet<(string? Context, string Message)> _badLogged = [];
    private readonly List<Translation> _catalogues = [];
    private readonly ModLanguage _language = new();
    private TranslationDomain? _domain;
    private int _templateMessages;
    private bool _pseudo;

    // Pseudolocalization is a dev tool: a Release build of Core never turns it on.
#if DEBUG
    private static bool DevBuild => true;
#else
    private static bool DevBuild => false;
#endif

    internal ModText(string id, string directory, Action<string> warn)
    {
        Id = id;
        _directory = directory;
        _warn = warn;
        Domain = "melvormods." + id.ToLowerInvariant();
    }

    internal string Id { get; }

    // The Godot translation domain holding this mod's catalogues.
    internal string Domain { get; }

    /// <summary>
    /// The language this mod's text is in, as Godot names it (<c>en_GB</c>, <c>fr</c>, …): its own
    /// <see cref="Language"/> when set, else the game's.
    /// </summary>
    public string Locale => _language.Locale(TextHost.Locale);

    /// <summary>
    /// The culture this mod formats numbers and times with: the game's, or <see cref="Language"/>'s when it is set
    /// (the game's again when .NET has no culture for it).
    /// </summary>
    public CultureInfo Culture => _language.Culture(TextHost.Culture);

    /// <summary>
    /// The language this mod shows its text in when it isn't the game's (<c>fr</c>, <c>pt_BR</c>), for a player who
    /// prefers one the game doesn't offer; null follows the game's. Leaves the game's language and every other mod's
    /// alone. A language without a translation shows English. Setting it raises <see cref="Changed"/>.
    /// </summary>
    public string? Language
    {
        get => _language.Chosen;
        set
        {
            if (!_language.Set(value))
                return;
            ApplyLanguage();
            TextHost.Mark(this);
        }
    }

    /// <summary>The languages this mod has translations for, each with the share of its messages translated.</summary>
    public IReadOnlyList<LanguageShare> Languages =>
        ModLanguage.Shares(
            _catalogues.Where(GodotObject.IsInstanceValid).Select(c => (c.Locale, c.GetMessageCount())),
            _templateMessages
        );

    /// <summary>Messages in this mod's loaded catalogues, all languages together.</summary>
    public int MessageCount => _catalogues.Sum(c => GodotObject.IsInstanceValid(c) ? c.GetMessageCount() : 0);

    /// <summary>The languages this mod's loaded catalogues are for, in load order.</summary>
    public IReadOnlyList<string> CatalogueLocales =>
        [.. _catalogues.Where(GodotObject.IsInstanceValid).Select(c => c.Locale)];

    /// <summary>
    /// Raised on the main thread, at most once a frame, after the game's language or this mod's own language or
    /// pseudolocalization changes. Text looked up before it is stale: look it up again.
    /// </summary>
    public event Action? Changed;

    /// <summary>
    /// <paramref name="message"/> translated into this mod's <see cref="Locale"/> in its catalogues; the message itself if
    /// it has no translation, or a translation whose <c>{n}</c> placeholders differ from the message's (logged once).
    /// <paramref name="context"/> tells apart two meanings of the same English text.
    /// </summary>
    public string Get(string message, string? context = null)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (message.Length == 0 || _domain == null)
            return message;
        if (_cache.TryGetValue((context, message), out var hit))
            return hit;
        var text = (string)_domain.Translate(message, context ?? "");
        if (string.IsNullOrEmpty(text))
            text = message;
        else if (!Placeholders.Same(text, message))
            text = Bad(context, message);
        text = Pseudolocalized(text);
        _cache[(context, message)] = text;
        return text;
    }

    /// <summary><see cref="Get"/>, then formatted by the game's SmartFormat with <see cref="Culture"/>.</summary>
    public string Format(string message, object?[] args, string? context = null) =>
        Formatted(Get(message, context), message, args);

    /// <summary>
    /// The singular or plural form for <paramref name="count"/> by the language's plural rules, translated like
    /// <see cref="Get"/>, then formatted like <see cref="Format"/>. Pass the count in <paramref name="args"/> too if
    /// the message shows it.
    /// </summary>
    public string Plural(string singular, string plural, long count, object?[] args, string? context = null)
    {
        ArgumentNullException.ThrowIfNull(singular);
        ArgumentNullException.ThrowIfNull(plural);
        var n = (int)Math.Clamp(count, int.MinValue, int.MaxValue);
        var english = n == 1 ? singular : plural;
        if (_domain == null)
            return Formatted(english, english, args);
        if (!_plurals.TryGetValue((context, singular, plural, n), out var text))
        {
            text = (string)_domain.TranslatePlural(singular, plural, n, context ?? "");
            if (string.IsNullOrEmpty(text))
                text = english;
            else if (!Placeholders.Within(text, singular, plural))
                text = Bad(context, singular, english);
            text = Pseudolocalized(text);
            if (_plurals.Count >= MaxPluralCache)
                _plurals.Clear();
            _plurals[(context, singular, plural, n)] = text;
        }
        return Formatted(text, english, args);
    }

    /// <summary>
    /// Marks <paramref name="message"/> as one of this mod's messages, translated later (by Core when it draws a label
    /// the mod gave it, or by the mod when it shows text it stored); returns it unchanged.
    /// </summary>
    public static string Key(string message) => message;

    /// <summary>
    /// Sets <paramref name="root"/>'s translation domain to this mod's and turns Godot's automatic translation off
    /// for it and the nodes below it, so only text the mod looks up is translated. Returns the root.
    /// </summary>
    public T Adopt<T>(T root)
        where T : Node
    {
        ArgumentNullException.ThrowIfNull(root);
        root.SetTranslationDomain(Domain);
        root.AutoTranslateMode = Node.AutoTranslateModeEnum.Disabled;
        return root;
    }

    /// <summary>
    /// Dev only: pseudolocalizes this mod's text (accents, 40% longer, in brackets, placeholders kept), so text that
    /// skipped the lookup or doesn't fit stands out. Raises <see cref="Changed"/>. Always false, and setting it does
    /// nothing, in a Release build of Core.
    /// </summary>
    public bool Pseudo
    {
        get => _pseudo;
        set
        {
            if (!DevBuild || _pseudo == value)
                return;
            _pseudo = value;
            ApplyPseudo();
            TextHost.Mark(this);
        }
    }

    /// <summary>The game's current language; the same as every mod's <see cref="Locale"/>.</summary>
    public static string GameLocale => TextHost.Locale;

    /// <summary>
    /// Dev: sets the game's language, for the game and every mod, as the game's own language option does.
    /// <see cref="GameLocale"/> reads the new language at once; <see cref="Changed"/> follows at the end of the frame.
    /// </summary>
    public static void SetGameLocale(string locale)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(locale);
        TranslationServer.SetLocale(locale);
        TextHost.LocaleChanged(refreshNow: true);
    }

    /// <summary>Dev: the languages the game's own catalogues are loaded for.</summary>
    public static IReadOnlyList<string> GameLocales => TranslationServer.GetLoadedLocales();

    // Creates the domain afresh (nothing left from an earlier copy) and loads the catalogues into it. Needs Godot
    // ready: Core calls it once the scene tree is, before a mod loaded later runs its Load.
    internal void Attach()
    {
        if (_domain != null)
            return;
        if (TranslationServer.HasDomain(Domain))
            TranslationServer.RemoveDomain(Domain);
        _domain = TranslationServer.GetOrAddDomain(Domain);
        var dir = Path.Combine(_directory, "data", "locale");
        if (System.IO.Directory.Exists(dir))
        {
            foreach (var file in System.IO.Directory.GetFiles(dir, "*.po").Order(StringComparer.Ordinal))
                LoadCatalogue(file);
            var template = System.IO.Directory.GetFiles(dir, "*.pot").Order(StringComparer.Ordinal).FirstOrDefault();
            _templateMessages = template == null ? 0 : ModLanguage.TemplateMessages(System.IO.File.ReadAllText(template));
        }
        ApplyLanguage();
        ApplyPseudo();
        TextHost.Add(this);
    }

    // Removes the domain and forgets everything looked up; the mod is unloading.
    internal void Detach()
    {
        TextHost.Remove(this);
        Changed = null;
        _cache.Clear();
        _plurals.Clear();
        _catalogues.Clear();
        if (_domain == null)
            return;
        _domain = null;
        if (TranslationServer.HasDomain(Domain))
            TranslationServer.RemoveDomain(Domain);
    }

    internal void RaiseChanged()
    {
        _cache.Clear();
        _plurals.Clear();
        try
        {
            Changed?.Invoke();
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            Core.Log.Write(Id, $"ERROR language change handler failed: {e}");
        }
    }

    private void LoadCatalogue(string file)
    {
        var name = Path.GetFileName(file);
        try
        {
            // Not from the cache: a reload must read the files as they are now.
            var catalogue = ResourceLoader.Load<Translation>(
                file.Replace('\\', '/'),
                null,
                ResourceLoader.CacheMode.Ignore
            );
            if (catalogue == null)
            {
                _warn($"locale file {name} couldn't be read; skipped");
                return;
            }
            _domain!.AddTranslation(catalogue);
            _catalogues.Add(catalogue);
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            _warn($"locale file {name} couldn't be read ({e.Message}); skipped");
        }
    }

    private string Bad(string? context, string message, string? english = null)
    {
        if (_badLogged.Add((context, message)))
            _warn(
                $"translation of \"{message}\""
                    + (context == null ? "" : $" (context \"{context}\")")
                    + $" in {Locale} has different {{n}} placeholders; showing English"
            );
        return english ?? message;
    }

    // A translation that formats badly falls back to the English message; English that formats badly is the
    // mod's own mistake and throws.
    private string Formatted(string text, string english, object?[] args)
    {
        ArgumentNullException.ThrowIfNull(args);
        if (ReferenceEquals(text, english) || text == english)
            return Smart.Format(Culture, english, args);
        try
        {
            return Smart.Format(Culture, text, args);
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            if (_badLogged.Add((null, text)))
                _warn($"translation \"{text}\" can't be formatted ({e.Message}); showing English");
            return Smart.Format(Culture, english, args);
        }
    }

    // Every looked-up text is pseudolocalized here, translated or not. Godot's placeholder skipping knows only
    // %s-style placeholders, so each SmartFormat {…} is swapped for %s around the call and put back after: its
    // format ({0:N1}) must reach SmartFormat untouched.
    private string Pseudolocalized(string text)
    {
        if (!_pseudo || _domain == null || text.Length == 0)
            return text;
        var held = new List<string>();
        var masked = Braces()
            .Replace(
                text.Replace("%", "%%", StringComparison.Ordinal),
                m =>
                {
                    held.Add(m.Value);
                    return "%s";
                }
            );
        _domain.PseudolocalizationEnabled = true;
        string pseudo;
        try
        {
            pseudo = _domain.Pseudolocalize(masked);
        }
        finally
        {
            _domain.PseudolocalizationEnabled = false;
        }
        var next = 0;
        var restored = PercentS()
            .Replace(
                pseudo,
                m =>
                    m.Value == "%%" ? "%"
                    : next < held.Count ? held[next++]
                    : m.Value
            );
        return restored;
    }

    [GeneratedRegex(@"\{[^{}]*\}")]
    private static partial Regex Braces();

    [GeneratedRegex("%%|%s")]
    private static partial Regex PercentS();

    // The domain's override is what Godot translates and picks plural forms with; empty follows the game.
    private void ApplyLanguage() => _domain?.SetLocaleOverride(_language.Chosen ?? "");

    private void ApplyPseudo()
    {
        if (_domain == null || !DevBuild)
            return;
        _domain.PseudolocalizationAccentsEnabled = true;
        _domain.PseudolocalizationDoubleVowelsEnabled = false;
        _domain.PseudolocalizationFakeBidiEnabled = false;
        _domain.PseudolocalizationOverrideEnabled = false;
        _domain.PseudolocalizationSkipPlaceholdersEnabled = true;
        _domain.PseudolocalizationExpansionRatio = 0.4f;
        _domain.PseudolocalizationPrefix = PseudoPrefix;
        _domain.PseudolocalizationSuffix = PseudoSuffix;
        // Off in Godot: lookups pseudolocalize themselves (see Pseudolocalized), so formats survive.
        _domain.PseudolocalizationEnabled = false;
    }

    /// <summary>The loaded mod's text, or null when it isn't loaded or Godot isn't ready yet.</summary>
    internal static ModText? For(string id) => TextHost.Find(id);
}

/// <summary>A language a mod has a translation for, and the share of its messages translated (0 to 1).</summary>
internal readonly record struct LanguageShare(string Locale, double Share);

/// <summary>The language a mod chose for its own text, or none to follow the game's. Godot-free for tests.</summary>
internal sealed class ModLanguage
{
    public string? Chosen { get; private set; }

    /// <summary>Chooses a language (blank clears it). False when that was already the choice.</summary>
    public bool Set(string? locale)
    {
        var chosen = string.IsNullOrWhiteSpace(locale) ? null : locale.Trim();
        if (chosen == Chosen)
            return false;
        Chosen = chosen;
        return true;
    }

    public string Locale(string game) => Chosen ?? game;

    public CultureInfo Culture(CultureInfo game) => Chosen == null ? game : CultureFor(Chosen) ?? game;

    /// <summary>.NET's culture for a Godot locale (<c>pt_BR</c> is <c>pt-BR</c>), or null when it has none.</summary>
    public static CultureInfo? CultureFor(string locale)
    {
        try
        {
            return CultureInfo.GetCultureInfo(locale.Replace('_', '-'), predefinedOnly: true);
        }
        catch (CultureNotFoundException)
        {
            return null;
        }
    }

    /// <summary>
    /// Each language's share of the template's messages. A catalogue holds only translated messages (<c>mm locale
    /// pull</c> leaves the rest out), so its message count is what is translated.
    /// </summary>
    public static IReadOnlyList<LanguageShare> Shares(IEnumerable<(string Locale, int Translated)> catalogues, int messages) =>
        [
            .. catalogues
                .GroupBy(c => c.Locale, StringComparer.Ordinal)
                .Select(g =>
                {
                    var translated = g.Sum(c => c.Translated);
                    var total = Math.Max(messages, translated);
                    return new LanguageShare(g.Key, total == 0 ? 0 : (double)translated / total);
                })
                .OrderBy(s => s.Locale, StringComparer.Ordinal),
        ];

    /// <summary>The messages in a template written by <c>mm strings</c>: its msgid lines, less the header's.</summary>
    public static int TemplateMessages(string pot) =>
        Math.Max(0, pot.Split('\n').Count(l => l.StartsWith("msgid ", StringComparison.Ordinal)) - 1);
}

/// <summary>SmartFormat's numbered placeholders (<c>{0}</c>, <c>{1:N0}</c>) in a message. Godot-free for tests.</summary>
internal static partial class Placeholders
{
    [GeneratedRegex(@"(?<!\{)\{(\d+)")]
    private static partial Regex Numbered();

    public static SortedSet<int> Of(string text) =>
        [.. Numbered().Matches(text).Select(m => int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture))];

    /// <summary>Whether a translation uses exactly the message's placeholders.</summary>
    public static bool Same(string translation, string message) => Of(translation).SetEquals(Of(message));

    /// <summary>Whether a plural form uses only placeholders its singular or plural message has (a form may drop the count).</summary>
    public static bool Within(string translation, string singular, string plural)
    {
        var allowed = Of(singular);
        allowed.UnionWith(Of(plural));
        return Of(translation).IsSubsetOf(allowed);
    }
}

/// <summary>
/// Every loaded mod's text, and the game's language. Watches for language changes and raises each mod's
/// <see cref="ModText.Changed"/> once, at the end of the frame. Main thread only, except <see cref="Locale"/>.
/// </summary>
internal static class TextHost
{
    // Before Godot is ready, as the game starts: its first act is to set this language.
    private const string StartLocale = "en_GB";

    private static readonly List<ModText> _texts = [];
    private static readonly HashSet<ModText> _dirty = [];
    private static bool _all;
    private static bool _queued;
    private static volatile string _locale = StartLocale;

    public static bool Ready { get; private set; }

    /// <summary>The live game's language; safe to read from any thread (a worker start reads it).</summary>
    public static string Locale => _locale;

    public static CultureInfo Culture { get; private set; } = CultureInfo.GetCultureInfo("en-GB");

    /// <summary>Raised after mods' Changed events, so Core's own labels for mods are drawn again.</summary>
    public static event Action? Retexted;

    /// <summary>Godot is ready: the language can be read and domains made.</summary>
    public static void Start()
    {
        if (Ready)
            return;
        Ready = true;
        Refresh();
    }

    public static void Add(ModText text)
    {
        if (!_texts.Contains(text))
            _texts.Add(text);
    }

    public static void Remove(ModText text)
    {
        _texts.Remove(text);
        _dirty.Remove(text);
    }

    public static ModText? Find(string id) =>
        _texts.FirstOrDefault(t => t.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

    public static IReadOnlyList<ModText> All => _texts;

    /// <summary>The game's language changed (or may have): every mod's text changes.</summary>
    public static void LocaleChanged(bool refreshNow = false)
    {
        if (refreshNow && Ready)
            Refresh();
        _all = true;
        Queue();
    }

    /// <summary>One mod's text changed (its pseudolocalization).</summary>
    public static void Mark(ModText text)
    {
        _dirty.Add(text);
        Queue();
    }

    private static void Queue()
    {
        if (_queued || !Ready)
            return;
        _queued = true;
        Callable.From(Flush).CallDeferred();
    }

    private static void Flush()
    {
        _queued = false;
        try
        {
            Refresh();
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            Log.Write("core", $"WARN reading the game's language failed: {e.Message}");
        }
        var changed = _all ? [.. _texts] : _dirty.Where(_texts.Contains).ToList();
        _all = false;
        _dirty.Clear();
        foreach (var text in changed)
            text.RaiseChanged();
        try
        {
            Retexted?.Invoke();
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            Log.Write("core", $"WARN mod labels not redrawn: {e}");
        }
    }

    private static void Refresh()
    {
        _locale = TranslationServer.GetLocale();
        Culture = GamesByMalcs.Melvor2.Localization.Locale.Current;
    }
}

/// <summary>
/// Labels Core draws for mods (header buttons, pause menu entries): each is the owning mod's message, looked up in
/// its domain now and again after every language change. A mod without catalogues gets its text back unchanged.
/// </summary>
internal static class CoreLabels
{
    private sealed record Entry(Node Node, string Owner, string Message, bool Tooltip);

    private static readonly List<Entry> _entries = [];
    private static bool _started;

    /// <summary>The mod's message in the game's language.</summary>
    public static string Text(string owner, string message) => ModText.For(owner)?.Get(message) ?? message;

    /// <summary>Sets a label's or button's text (or a control's tooltip) to the mod's message, and keeps it translated.</summary>
    public static void Set(Node node, string owner, string message, bool tooltip = false)
    {
        if (!_started)
        {
            _started = true;
            TextHost.Retexted += Refresh;
        }
        var entry = new Entry(node, owner, message, tooltip);
        Apply(entry);
        if (_entries.Count >= 32 && (_entries.Count & 31) == 0)
            Prune();
        _entries.Add(entry);
    }

    /// <summary>Sets the root of something Core builds for a mod to the mod's domain, with automatic translation off.</summary>
    public static void Adopt(Node root, string owner) => ModText.For(owner)?.Adopt(root);

    private static void Apply(Entry entry)
    {
        var text = Text(entry.Owner, entry.Message);
        switch (entry.Node)
        {
            case Control control when entry.Tooltip:
                control.TooltipText = text;
                break;
            case Button button:
                button.Text = text;
                break;
            case Label label:
                label.Text = text;
                break;
        }
    }

    private static void Prune() =>
        _entries.RemoveAll(e => !GodotObject.IsInstanceValid(e.Node) || e.Node.IsQueuedForDeletion());

    private static void Refresh()
    {
        Prune();
        foreach (var entry in _entries)
        {
            try
            {
                Apply(entry);
            }
            catch (Exception e) when (e is not OutOfMemoryException)
            {
                Log.Write(entry.Owner, $"WARN label \"{entry.Message}\" not redrawn: {e.Message}");
            }
        }
    }
}

// Godot tells every node in the tree when the language changes. Core can't add a node class of its own (it has no
// Godot script registration), so it listens on the game's main node, which is in the tree from start to exit.
[HarmonyPatchCategory(Standalone.PatchCategory)]
[HarmonyPatch(typeof(Main), nameof(Main._Notification))]
internal static class TextLanguageWatch
{
    private static void Postfix(int what)
    {
        if (what == Node.NotificationTranslationChanged)
            TextHost.LocaleChanged();
    }
}

// The game sets its language first thing in its main node's _Ready, after Core is ready: read it then too, in case
// that change reached the main node before it was ready to be told.
[HarmonyPatchCategory(Standalone.PatchCategory)]
[HarmonyPatch(typeof(Main), nameof(Main._Ready))]
internal static class TextLanguageSet
{
    private static void Postfix() => TextHost.LocaleChanged();
}
