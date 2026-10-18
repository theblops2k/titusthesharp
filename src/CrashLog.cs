using System.Runtime.InteropServices;
using System.Text;

namespace TitusTheFox;

// Registro de crash: contexto estatico + cola circular de eventos + volcado (append) a Log_error.txt.
// Coste por frame nulo: el juego solo actualiza el contexto en cambios de pantalla/nivel/muerte;
// posicion y flags del jugador se leen unicamente al escribir el log.
public static class Crash
{
    static readonly string LogPath = Path.Combine(AppContext.BaseDirectory, "Log_error.txt");
    const int MaxEvents = 50;

    static readonly string[] events = new string[MaxEvents];
    static int evCount;       // total historico; indice de escritura = evCount % MaxEvents
    static readonly object gate = new();
    static bool written;      // evita doble log (UnhandledException + catch de Program.cs)

    // Contexto del juego (Game.cs: inicio de nivel; Program.cs/Menu.cs/Game.cs: cambio de pantalla)
    public static string Screen = "inicio";
    public static TITUS_level? Level;
    public static int LevelNumber = -1;

    public static void SetScreen(string name) { Screen = name; Event("pantalla: " + name); }

    public static void SetLevel(TITUS_level level)
    {
        Level = level; LevelNumber = level.levelnumber; Screen = "nivel";
        Event($"nivel cargado: numero={level.levelnumber} id={level.levelid} vidas={level.lives}");
    }

    public static void Event(string text)
    {
        lock (gate) events[evCount++ % MaxEvents] = $"{DateTime.Now:HH:mm:ss.fff} {text}";
    }

    // Program.cs: AppDomain.UnhandledException
    public static void OnUnhandled(object? sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex) Write(ex);
    }

    // Nunca lanza: si falla escribir el log se ignora.
    public static void Write(Exception ex)
    {
        try
        {
            lock (gate) { if (written) return; written = true; }
            string body = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} ===== CRASH =====" + Environment.NewLine + Build(ex);
            File.AppendAllText(LogPath, body, Encoding.UTF8);
            string path = LogPath;
            Console.Error.WriteLine("Crash log: " + path);
        }
        catch { }
    }

    static string Build(Exception ex)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Titus The Fox - crash log");
        sb.AppendLine($"Fecha: {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}");
        Safe(sb, ".NET", () => RuntimeInformation.FrameworkDescription);
        Safe(sb, "SO", () => RuntimeInformation.OSDescription + " " + RuntimeInformation.OSArchitecture);
        Safe(sb, "Raylib-cs", () => typeof(Raylib_cs.Raylib).Assembly.GetName().Version?.ToString() ?? "?");
        Safe(sb, "Raylib", () => Raylib_cs.Raylib.RAYLIB_VERSION);
        Safe(sb, "FpsGame", () => Title.FpsGame.ToString());
        Safe(sb, "Audio", () => Audio.Status);

        sb.AppendLine().AppendLine("== Contexto ==");
        Safe(sb, "Pantalla", () => Screen);
        Safe(sb, "Nivel (levelnumber)", () => LevelNumber.ToString());
        try
        {
            var l = Level;
            if (l != null)
            {
                Safe(sb, "levelid", () => l.levelid.ToString());
                Safe(sb, "Vidas", () => l.lives.ToString());
                Safe(sb, "Jugador x,y", () => $"{l.player.sprite.x},{l.player.sprite.y}");
                Safe(sb, "Flags jugador", () =>
                    $"CARRY={Player.CARRY_FLAG} DROP={Player.DROP_FLAG} GRAVITY={Player.GRAVITY_FLAG} " +
                    $"SEECHOC={Player.SEECHOC_FLAG} CROSS={Player.CROSS_FLAG} CHOC={Player.CHOC_FLAG} KICK={Player.KICK_FLAG} " +
                    $"RESETLEVEL={Player.RESETLEVEL_FLAG} GAMEOVER={Player.GAMEOVER_FLAG} GRANDBRULE={Player.GRANDBRULE_FLAG} " +
                    $"FURTIF={Player.FURTIF_FLAG} IMAGE_COUNTER={Player.IMAGE_COUNTER}");
            }
            else sb.AppendLine("Nivel no cargado");
        }
        catch { sb.AppendLine("(contexto no disponible)"); }

        sb.AppendLine().AppendLine("== Excepcion ==");
        int depth = 0;
        for (Exception? e = ex; e != null && depth < 10; e = e.InnerException, depth++)
        {
            sb.AppendLine(depth == 0 ? "Tipo: " + e.GetType().FullName : $"InnerException[{depth}]: {e.GetType().FullName}");
            sb.AppendLine("Mensaje: " + e.Message);
            sb.AppendLine("StackTrace:").AppendLine(e.StackTrace ?? "(sin stack)").AppendLine();
        }

        sb.AppendLine("== Ultimos eventos (antiguo -> reciente) ==");
        lock (gate)
        {
            int n = Math.Min(evCount, MaxEvents);
            for (int i = 0; i < n; i++) sb.AppendLine(events[(evCount - n + i) % MaxEvents]);
        }
        return sb.ToString();
    }

    static void Safe(StringBuilder sb, string name, Func<string> f)
    {
        try { sb.AppendLine($"{name}: {f()}"); }
        catch (Exception e) { sb.AppendLine($"{name}: (error {e.GetType().Name})"); }
    }
}
