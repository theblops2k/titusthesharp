using System.Numerics;
using Raylib_cs;
namespace TitusTheFox;

/// Prepara FILES\ (*.bin = Sqz.Decompress de cada .SQZ original + music.bin de FOX.COM).
/// En ejecucion el juego solo lee FILES\.
public static class Extractor
{
    public static readonly string[] Expected =
    {
        "FONTS.bin", "MENU.bin", "SPREXP.bin", "TITRE.bin", "TITUS.bin", "LEVELA.bin", "music.bin",
        "LEVEL0.bin", "LEVEL1.bin", "LEVEL2.bin", "LEVEL3.bin", "LEVEL4.bin", "LEVEL5.bin", "LEVEL6.bin",
        "LEVEL7.bin", "LEVEL8.bin", "LEVEL9.bin", "LEVELB.bin", "LEVELC.bin", "LEVELE.bin", "LEVELG.bin", "LEVELJ.bin",
    };

    const int W = 480, H = 170;

    public static bool FilesReady(string files)
    {
        foreach (var f in Expected) if (!File.Exists(Path.Combine(files, f))) return false;
        return true;
    }

    static bool IsOriginal(string? d) => d != null && File.Exists(Path.Combine(d, "LEVEL0.SQZ")) && File.Exists(Path.Combine(d, "FOX.COM"));

    static string? FindOriginal()
    {
        string a = Path.Combine(AppContext.BaseDirectory, "TITUS");
        return IsOriginal(a) ? a : null;
    }

    /// Devuelve la carpeta FILES lista, o null si el usuario cierra la ventana.
    public static string? Ensure()
    {
        string files = Path.Combine(AppContext.BaseDirectory, "FILES");
        if (FilesReady(files)) return files;

        Raylib.InitWindow(W, H, "Titus The Fox PC");
        Raylib.SetTargetFPS(60);
        Raylib.SetExitKey(KeyboardKey.Null);
        int mon = Raylib.GetCurrentMonitor();
        Raylib.SetWindowPosition((Raylib.GetMonitorWidth(mon) - W) / 2, (Raylib.GetMonitorHeight(mon) - H) / 2);
        try
        {
            string? src = FindOriginal();
            string msg = "No se encontro el juego original (carpeta TITUS).";
            while (true)
            {
                if (src != null)
                {
                    string? err = Extract(src, files);
                    if (err == null) return files; // null de Extract = ok; false de ventana se maneja abajo
                    if (err == "#cerrado") return null;
                    msg = "Error al extraer: " + err;
                    src = null;
                }
                var r = AskFolder(msg);
                if (r == null) return null;
                if (IsOriginal(r)) src = r;
                else msg = "La carpeta no contiene LEVEL0.SQZ y FOX.COM.";
            }
        }
        finally { Raylib.CloseWindow(); }
    }

    static void Draw(string text, float p, bool bar, string? progress = null)
    {
        Raylib.BeginDrawing();
        Raylib.ClearBackground(new Color(24, 24, 40, 255));
        Raylib.DrawText(text, 20, 25, 20, Color.White);
        if (bar)
        {
            Raylib.DrawRectangle(20, 90, W - 40, 28, new Color(60, 60, 90, 255));
            Raylib.DrawRectangle(20, 90, (int)((W - 40) * p), 28, new Color(80, 200, 120, 255));
            Raylib.DrawRectangleLines(20, 90, W - 40, 28, Color.White);
            if (progress != null) Raylib.DrawText(progress, 20, 126, 18, Color.LightGray);
        }
        Raylib.EndDrawing();
    }

    // Extrae en segundo plano y dibuja la barra. null = ok; texto = error; "#cerrado" = ventana cerrada.
    static string? Extract(string src, string files)
    {
        var sqz = Directory.GetFiles(src, "*.SQZ");
        int total = sqz.Length + 1, done = 0;
        string? error = null;
        bool finished = false, cancel = false;
        var t = new Thread(() =>
        {
            try
            {
                Directory.CreateDirectory(files);
                foreach (var f in sqz)
                {
                    if (cancel) return;
                    // mismo decodificador que usaba el juego; escritura via .tmp para no dejar bin parciales
                    byte[] d = Sqz.Decompress(f);
                    string o = Path.Combine(files, Path.GetFileNameWithoutExtension(f) + ".bin");
                    File.WriteAllBytes(o, d);
                    Interlocked.Increment(ref done);
                    Thread.Sleep(150);
                }
                var m = Packed.ReadMusic(Path.Combine(src, "FOX.COM"));
                if (m == null) throw new InvalidDataException("FOX.COM no valido (musica)");
                File.WriteAllBytes(Path.Combine(files, "music.bin"), m);
                Interlocked.Increment(ref done);
                if (!FilesReady(files)) throw new FileNotFoundException("Faltan archivos tras extraer");
            }
            catch (Exception ex) { error = ex.Message; }
            finally { finished = true; }
        });
        t.IsBackground = true;
        t.Start();
        while (!Volatile.Read(ref finished))
        {
            if (Raylib.WindowShouldClose()) { cancel = true; t.Join(); return "#cerrado"; }
            int dn = Volatile.Read(ref done);
            Draw("Extrayendo", dn / (float)total, true, $"{dn}/{total} ({dn * 100 / total}%)");
        }
        t.Join();
        return error;
    }

    // Aviso + boton Examinar; devuelve carpeta elegida o null si se cierra la ventana.
    static string? AskFolder(string msg)
    {
        var btn = new Rectangle(W / 2 - 80, 100, 160, 40);
        while (!Raylib.WindowShouldClose())
        {
            bool over = Raylib.CheckCollisionPointRec(Raylib.GetMousePosition(), btn);
            Raylib.BeginDrawing();
            Raylib.ClearBackground(new Color(24, 24, 40, 255));
            Raylib.DrawText(msg, 20, 25, 16, Color.White);
            Raylib.DrawText("Elija la carpeta del juego original.", 20, 55, 16, Color.LightGray);
            Raylib.DrawRectangleRec(btn, over ? new Color(90, 130, 220, 255) : new Color(60, 90, 170, 255));
            Raylib.DrawText("Examinar...", (int)btn.X + 28, (int)btn.Y + 10, 20, Color.White);
            Raylib.EndDrawing();
            if (over && Raylib.IsMouseButtonPressed(MouseButton.Left))
            {
                string? sel = null;
                var th = new Thread(() =>
                {
                    using var dlg = new System.Windows.Forms.FolderBrowserDialog
                    { Description = "Carpeta con LEVEL0.SQZ y FOX.COM" };
                    if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK) sel = dlg.SelectedPath;
                });
                th.SetApartmentState(ApartmentState.STA);
                th.Start();
                th.Join();
                if (sel != null) return sel;
            }
        }
        return null;
    }
}
