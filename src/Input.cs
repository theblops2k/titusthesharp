using System.Collections.Generic;
using Raylib_cs;

// Filtro de pulsacion: Pressed devuelve true una sola vez por pulsacion fisica
// (consume el evento) y respeta un tiempo minimo entre pulsaciones de la misma tecla.
static class Input
{
    public const double MinMs = 80;
    public const double LevelKeyMs = 150; // P/ESC dentro del nivel (pausa)
    static readonly System.Diagnostics.Stopwatch clock = System.Diagnostics.Stopwatch.StartNew();
    static readonly Dictionary<KeyboardKey, double> last = new();

    public static bool Pressed(KeyboardKey k, double minMs = MinMs)
    {
        if (!Raylib.IsKeyPressed(k)) return false;
        double now = clock.Elapsed.TotalMilliseconds;
        if (last.TryGetValue(k, out double t) && now - t < minMs) return false;
        last[k] = now;
        return true;
    }

    // Enter, Enter del teclado numerico o Espacio (se evaluan todas para consumir cada una)
    public static bool Confirm()
    {
        bool a = Pressed(KeyboardKey.Enter), b = Pressed(KeyboardKey.KpEnter), c = Pressed(KeyboardKey.Space);
        return a || b || c;
    }

    // Marca la tecla como recien usada (p. ej. al entrar en pausa) sin leerla
    public static void Consume(KeyboardKey k) => last[k] = clock.Elapsed.TotalMilliseconds;
}
