using BlockCraft.Core;

namespace BlockCraft.Render;

/// <summary>Dostarcza czcionkę Unicode tworzoną przy pierwszym użyciu z podanego źródła plików. Jeśli powstała, zanim
/// zapasowe źródło (client.jar) było gotowe, po jego przygotowaniu tworzy ją raz na nowo.</summary>
public sealed class UnicodeFontProvider : IDisposable
{
    private readonly Func<string, byte[]> _read;
    private readonly Func<bool> _fallbackReady;
    private UnicodeFont _font;
    private bool _createdBeforeFallback;

    public UnicodeFontProvider(Func<string, byte[]> read, Func<bool> fallbackReady)
    {
        _read = read;
        _fallbackReady = fallbackReady;
    }

    /// <summary>Paczki zasobów (w kolejności pierwszeństwa), a bez nich pobrany client.jar.</summary>
    public static UnicodeFontProvider FromPacksAndClientJar() =>
        new(path => ResourcePacks.ReadAndClose(path) ?? ClientAssets.TryReadFile(path), () => ClientAssets.Ready);

    public UnicodeFont Font
    {
        get
        {
            if (_font != null && _createdBeforeFallback && _fallbackReady()) Reset();
            if (_font != null) return _font;
            _createdBeforeFallback = !_fallbackReady();
            return _font = new UnicodeFont(_read);
        }
    }

    /// <summary>Zwalnia czcionkę (np. po zmianie paczek); następne użycie wczyta ją od nowa.</summary>
    public void Reset() { _font?.Dispose(); _font = null; }

    public void Dispose() => Reset();
}
