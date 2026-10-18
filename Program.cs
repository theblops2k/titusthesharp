using Raylib_cs;
using TitusTheFox;

AppDomain.CurrentDomain.UnhandledException += Crash.OnUnhandled; // CrashLog.cs

Raylib.SetTraceLogLevel(TraceLogLevel.None); // sin logs de raylib (WinExe, sin consola)

try
{
    string? files = Extractor.Ensure(); // FILES\ junto al exe; extrae del original si falta
    if (files == null) return;
    string dir = files;

    Raylib.SetConfigFlags(ConfigFlags.HiddenWindow); // ventana oculta hasta el primer cuadro (Title.ShowWindowOnce)
    Raylib.InitWindow(Title.W * Render.SCALE, Title.H * Render.SCALE, Config.ShowFps() && !Config.FullScreen() ? "Titus The Fox PC - Fps 0" : "Titus The Fox PC");
    Raylib.SetTargetFPS(60);
    if (Config.FullScreen() && Config.WideScreen()) // vista ampliada segun la proporcion real del monitor
    {
        int mon = Raylib.GetCurrentMonitor();
        Scroll.SetViewWidth(Raylib.GetMonitorWidth(mon), Raylib.GetMonitorHeight(mon));
    }
    Render.Init();
    Audio.Init(dir);
    // ESC lo gestionan Title/Menu: se desactiva la tecla de salida por defecto
    Raylib.SetExitKey(KeyboardKey.Null);

    // Titulo de ventana con FPS, refrescado cada 500 ms
    long lastTitle = 0;
    bool titleShowsFps = Config.ShowFps() && !Config.FullScreen();
    void UpdateTitle()
    {
        long now = Environment.TickCount64;
        bool showFps = Config.ShowFps() && !Config.FullScreen();
        if (showFps && now - lastTitle >= 500)
        {
            lastTitle = now;
            titleShowsFps = true;
            Raylib.SetWindowTitle($"Titus The Fox PC - Fps {Title.Fps}");
        }
        else if (!showFps && titleShowsFps) // al desactivar ShowFps (o pasar a pantalla completa) vuelve el titulo fijo
        {
            titleShowsFps = false;
            Raylib.SetWindowTitle("Titus The Fox PC");
        }
    }

    Title.OnFrame = UpdateTitle;

    // Flujo de opentitus.c:99-110: menu -> playtitus(retval-1); ESC en el juego vuelve al menu
    string menuPath = Path.Combine(dir, "MENU.bin");
    Crash.SetScreen("intro");
    if (Title.Run(dir))
    {
        while (true)
        {
            Crash.SetScreen("menu");
            int level = Menu.View(menuPath);
            if (level < 1) break;
            if (!Game.Run(dir, level - 1)) break;
        }
    }

    Audio.Close();
    Raylib.CloseWindow();
}
catch (Exception ex)
{
    Crash.Write(ex);
    throw; // mantiene la muerte del juego
}
