namespace BlockCraft.Gui;

/// <summary>Stan wejścia z chwili zdarzenia: pozycja myszy w GUI, Shift i czas w ms (Environment.TickCount64).</summary>
public readonly record struct InputContext(int MouseX, int MouseY, bool Shift, long Time);

/// <summary>
/// Kolejka zdarzeń wejścia, które przyszły, gdy nie wolno ich obsłużyć (np. w połowie dzielonego ticku).
/// Zdarzenie odłożone wykonuje się później w kolejności nadejścia, z kontekstem z chwili, w której przyszło.
/// </summary>
public sealed class DeferredInputQueue
{
    private readonly Func<bool> _isBlocked;
    private readonly Queue<(Action action, InputContext context)> _pending = new();

    /// <param name="isBlocked">Czy zdarzenie trzeba teraz odłożyć.</param>
    public DeferredInputQueue(Func<bool> isBlocked) => _isBlocked = isBlocked;

    /// <summary>Kontekst odtwarzanego zdarzenia (null: zdarzenie bieżące).</summary>
    public InputContext? Replaying { get; private set; }

    public int Count => _pending.Count;

    /// <summary>Wykonuje od razu albo odkłada; po odłożonym kolejne też czekają, żeby zachować kolejność.</summary>
    public void RunOrDefer(Action action, InputContext context)
    {
        if (_pending.Count == 0 && !_isBlocked()) action();
        else _pending.Enqueue((action, context));
    }

    /// <summary>Odtwarza odłożone zdarzenia, póki nic ich nie blokuje; błąd jednego nie zatrzymuje pozostałych.</summary>
    public void Flush(Action<Exception> onError)
    {
        while (!_isBlocked() && _pending.TryDequeue(out var item))
        {
            Replaying = item.context;
            try { item.action(); }
            catch (Exception ex) { onError(ex); }
            finally { Replaying = null; }
        }
    }
}
