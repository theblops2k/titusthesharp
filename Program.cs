using Raylib_cs;
using TitusTheFox;

AppDomain.CurrentDomain.UnhandledException += Crash.OnUnhandled; // CrashLog.cs

Raylib.SetTraceLogLevel(TraceLogLevel.None); // sin logs de raylib (WinExe, sin consola)

try
{
    string? files = Extractor.Ensure(); // FILES\ junto al exe; extrae del original si falta
    if (files == null) return;
    string dir = files;

    Controller.Ensure(); // Controller.cfg junto al exe (defaults de teclado si no existe)

    Raylib.SetConfigFlags(ConfigFlags.HiddenWindow); // ventana oculta hasta el primer cuadro (Title.ShowWindowOnce)
    Raylib.InitWindow(Title.W * Render.SCALE, Title.H * Render.SCALE, Config.ShowFps() && !Config.FullScreen() ? "Titus The Fox PC - Fps 0" : "Titus The Fox PC");
    Raylib.SetTargetFPS(60);

    // Si el monitor no entra en la ventana de 3x (960x600), abre a una escala entera menor (2x/1x) y la centra.
    // Solo afecta al tamano de la ventana: el juego se dibuja a 320x200 logicos y Present lo escala a lo que haya.
    void FitWindow()
    {
        int mon = Raylib.GetCurrentMonitor();
        int mw = Raylib.GetMonitorWidth(mon), mh = Raylib.GetMonitorHeight(mon);
        if (mw <= 0 || mh <= 0) return;
        int s = Math.Clamp(Math.Min(mw / Title.W, (mh - 80) / Title.H), 1, Render.SCALE); // -80: barra de tareas
        if (s >= Render.SCALE) return;
        int w = Title.W * s, h = Title.H * s;
        Raylib.SetWindowSize(w, h);
        Raylib.SetWindowPosition(Math.Max(0, (mw - w) / 2), Math.Max(0, (mh - h) / 2));
    }
    FitWindow();
    if (Config.FullScreen() && Config.WideScreen()) // vista ampliada segun la proporcion real del monitor
    {
        int mon = Raylib.GetCurrentMonitor();
        Scroll.SetViewWidth(Raylib.GetMonitorWidth(mon), Raylib.GetMonitorHeight(mon));
    }
    Render.Init();
    Audio.Init(dir);
    // ESC lo gestionan Title/Menu: se desactiva la tecla de salida por defecto
    Raylib.SetExitKey(KeyboardKey.Null);

    // Detecta los joysticks: raylib los enumera al atender los eventos del sistema, asi que hay
    // que atenderlos al menos una vez. Se hace aqui (al arrancar) para que, la primera vez, el
    // joystick quede como controlador y tambien sirve para saltar la intro.
    for (int i = 0; i < 5; i++) { Raylib.PollInputEvents(); Thread.Sleep(20); }
    Controller.ApplyDefaultPad();

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
            bool running = Menu.CustomMode ? Game.RunCustom(dir) : Game.Run(dir, level - 1);
            if (!running) break;
            Audio.SelectMusic(15); // de vuelta del nivel: corta la musica del juego y pone la del menu principal
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
