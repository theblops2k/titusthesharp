using System.Numerics;
using Raylib_cs;
namespace TitusTheFox;

/// Prepara FILES\ (*.bin = Sqz.Decompress de cada .SQZ original + music.bin de FOX.COM).
/// En ejecucion el juego solo lee FILES\. La ventana de aviso y la de extraccion son propias
/// (720x420, fuentes del sistema) y se cierran antes de abrir la ventana del juego.
public static class Extractor
{
    public static readonly string[] Expected =
    {
        "FONTS.bin", "MENU.bin", "SPREXP.bin", "TITRE.bin", "TITUS.bin", "LEVELA.bin", "music.bin",
        "LEVEL0.bin", "LEVEL1.bin", "LEVEL2.bin", "LEVEL3.bin", "LEVEL4.bin", "LEVEL5.bin", "LEVEL6.bin",
        "LEVEL7.bin", "LEVEL8.bin", "LEVEL9.bin", "LEVELB.bin", "LEVELC.bin", "LEVELE.bin", "LEVELG.bin", "LEVELJ.bin",
    };

    // ---------------------------------------------------------------- ventana
    const int W = 720, H = 420;
    const float CardX = 36, CardW = W - 72;

    static readonly Color Accent = new(226, 140, 62, 255);
    static readonly Color AccentHi = new(246, 168, 94, 255);
    static readonly Color AccentDown = new(190, 112, 44, 255);
    static readonly Color BgTop = new(22, 27, 41, 255);
    static readonly Color BgBottom = new(9, 11, 18, 255);
    static readonly Color Panel = new(25, 31, 48, 255);
    static readonly Color PanelLine = new(45, 55, 80, 255);
    static readonly Color Ink = new(238, 241, 248, 255);
    static readonly Color InkMid = new(168, 177, 197, 255);
    static readonly Color InkDim = new(108, 119, 142, 255);
    static readonly Color BtnInk = new(28, 21, 12, 255);
    static readonly Color WarnFill = new(57, 28, 35, 255);
    static readonly Color WarnLine = new(124, 50, 62, 255);
    static readonly Color WarnInk = new(246, 174, 182, 255);

    static Font fBold, fReg, fSmall;
    static bool boldLoaded, regLoaded, smallLoaded, firstFrame;
    static string progressName = "";

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

        InitUi();
        try
        {
            string? src = FindOriginal();
            string? error = null, detail = null;
            while (true)
            {
                if (src != null)
                {
                    var (exErr, closed) = Extract(src, files);
                    if (closed) return null;
                    if (exErr == null) return files;
                    error = "No se pudieron extraer los datos del juego.";
                    detail = exErr;
                    src = null;
                }
                string? picked = Prompt(error, detail);
                if (picked == null) return null;
                if (IsOriginal(picked))
                {
                    src = picked;
                    error = detail = null;
                }
                else
                {
                    error = "La carpeta seleccionada no es una copia válida del juego.";
                    detail = "Debe contener LEVEL0.SQZ y FOX.COM.";
                }
            }
        }
        finally { CloseUi(); }
    }

    // ---------------------------------------------------------------- ciclo de la ventana
    static void InitUi()
    {
        Raylib.SetConfigFlags(ConfigFlags.HiddenWindow); // se muestra recien tras el primer cuadro
        Raylib.InitWindow(W, H, "Titus The Fox PC");
        Raylib.SetTargetFPS(60);
        Raylib.SetExitKey(KeyboardKey.Null);
        int mon = Raylib.GetCurrentMonitor();
        int mw = Raylib.GetMonitorWidth(mon), mh = Raylib.GetMonitorHeight(mon);
        Raylib.SetWindowPosition(Math.Max(0, (mw - W) / 2), Math.Max(0, (mh - H) / 2));
        LoadFonts();
        SetWindowIconSafe();
    }

    // Icono de la barra de titulo: el .ico embebido en el ejecutable (se carga desde memoria, sin temporales)
    static void SetWindowIconSafe()
    {
        try
        {
            string? exe = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exe)) return;
            using var ico = System.Drawing.Icon.ExtractAssociatedIcon(exe);
            if (ico == null) return;
            byte[] png;
            using (var bmp = ico.ToBitmap())
            using (var ms = new MemoryStream())
            {
                bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                png = ms.ToArray();
            }
            Image img = Raylib.LoadImageFromMemory(".png", png);
            if (!Raylib.IsImageValid(img)) return;
            Raylib.SetWindowIcon(img);
            Raylib.UnloadImage(img);
        }
        catch { }
    }

    static void CloseUi()
    {
        Raylib.SetMouseCursor(MouseCursor.Default);
        UnloadFont(ref fBold, boldLoaded);
        UnloadFont(ref fReg, regLoaded);
        UnloadFont(ref fSmall, smallLoaded);
        Raylib.CloseWindow();
    }

    static void UnloadFont(ref Font f, bool custom)
    {
        if (custom) Raylib.UnloadFont(f);
        f = default;
    }

    static void ShowFirstFrame()
    {
        if (firstFrame) return;
        firstFrame = true;
        Raylib.ClearWindowState(ConfigFlags.HiddenWindow);
    }

    // ---------------------------------------------------------------- tipografia
    static readonly string[] BoldFiles = { "segoeuib.ttf", "seguisb.ttf", "arialbd.ttf", "tahomabd.ttf" };
    static readonly string[] RegFiles = { "segoeui.ttf", "arial.ttf", "tahoma.ttf" };

    static void LoadFonts()
    {
        fBold = LoadUiFont(BoldFiles, 24, out boldLoaded);
        fReg = LoadUiFont(RegFiles, 18, out regLoaded);
        fSmall = LoadUiFont(RegFiles, 15, out smallLoaded);
    }

    // Fuente del sistema (Segoe UI si existe); si no, la fuente por defecto de raylib.
    static Font LoadUiFont(string[] candidates, int size, out bool custom)
    {
        custom = false;
        string dir = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
        if (!Directory.Exists(dir)) dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Fonts");
        int[] cps = UiCodepoints();
        foreach (string name in candidates)
        {
            try
            {
                string path = Path.Combine(dir, name);
                if (!File.Exists(path)) continue;
                Font f = Raylib.LoadFontEx(path, size, cps, cps.Length);
                if (f.GlyphCount > 1 && f.Texture.Id != Raylib.GetFontDefault().Texture.Id)
                {
                    Raylib.SetTextureFilter(f.Texture, TextureFilter.Bilinear);
                    custom = true;
                    return f;
                }
            }
            catch { }
        }
        return Raylib.GetFontDefault();
    }

    // ASCII imprimible + acentos y signos usados en los textos de esta ventana
    static int[] UiCodepoints()
    {
        var set = new SortedSet<int>();
        for (int c = 32; c <= 126; c++) set.Add(c);
        foreach (char c in "áéíóúÁÉÍÓÚñÑüÜ¿¡°º·…—–") set.Add(c);
        return set.ToArray();
    }

    static float TW(Font f, string s, float size, float sp = 0f) => Raylib.MeasureTextEx(f, s, size, sp).X;

    static void T(Font f, string s, float x, float y, float size, Color c, float sp = 0f)
        => Raylib.DrawTextEx(f, s, new Vector2(x, y), size, sp, c);

    static void TCfit(Font f, string s, float y, float size, Color c, float maxW, float sp = 0f)
    {
        float w = TW(f, s, size, sp);
        if (w > maxW)
        {
            size *= maxW / w;
            w = TW(f, s, size, sp);
        }
        T(f, s, (W - w) / 2f, y, size, c, sp);
    }

    static string Trunc(Font f, string s, float maxW, float size)
    {
        if (TW(f, s, size) <= maxW) return s;
        while (s.Length > 0 && TW(f, s + "…", size) > maxW) s = s[..^1];
        return s + "…";
    }

    // ---------------------------------------------------------------- dibujo comun
    static void DrawBackground()
    {
        Raylib.DrawRectangleGradientV(0, 0, W, H, BgTop, BgBottom);
        Raylib.DrawCircleGradient(new Vector2(W / 2f, -140), 460, new Color(226, 140, 62, 24), new Color(226, 140, 62, 0));
        Raylib.DrawRectangle(0, 0, W, 3, Accent);
    }

    static void DrawHeader()
    {
        T(fBold, "TITUS THE FOX", 36, 22, 20, Ink, 2.4f);
        float bw = TW(fBold, "TITUS THE FOX", 20, 2.4f);
        T(fSmall, "PC", 36 + bw + 12, 26, 15, Accent, 1f);
        Raylib.DrawRectangle(36, 58, W - 72, 1, new Color(40, 48, 70, 255));
    }

    static void DrawCard()
    {
        var card = new Rectangle(CardX, 88, CardW, 232);
        Raylib.DrawRectangleRounded(new Rectangle(card.X, card.Y + 6, card.Width, card.Height), 0.05f, 14, new Color(0, 0, 0, 80));
        Raylib.DrawRectangleRounded(card, 0.05f, 14, Panel);
        Raylib.DrawRectangleRoundedLinesEx(card, 0.05f, 14, 1f, PanelLine);
    }

    // Disco (copia original): cuerpo oscuro, aro y brillo calido
    static void DrawDisc(float cx, float cy)
    {
        var c = new Vector2(cx, cy);
        Raylib.DrawCircleV(new Vector2(cx, cy + 6), 38, new Color(0, 0, 0, 45));
        Raylib.DrawCircleV(c, 32, new Color(34, 43, 64, 255));
        Raylib.DrawRing(c, 26.5f, 28.5f, 0, 360, 64, new Color(58, 71, 101, 255));
        Raylib.DrawRing(c, 19, 21, 200, 335, 40, new Color(226, 140, 62, 130));
        Raylib.DrawCircleV(c, 10, new Color(20, 26, 40, 255));
        Raylib.DrawCircleV(c, 7.5f, Panel);
    }

    static void DrawButton(Rectangle r, string label, bool hover, bool down)
    {
        Color fill = down ? AccentDown : hover ? AccentHi : Accent;
        Raylib.DrawRectangleRounded(new Rectangle(r.X, r.Y + 4, r.Width, r.Height), 0.26f, 12, new Color(0, 0, 0, 90));
        Raylib.DrawRectangleRounded(r, 0.26f, 12, fill);
        Raylib.DrawRectangleRoundedLinesEx(new Rectangle(r.X + 1, r.Y + 1, r.Width - 2, r.Height - 2), 0.26f, 12, 1f, new Color(255, 236, 212, 50));

        float tw = TW(fBold, label, 19);
        const float iw = 20, gap = 11;
        float x = r.X + (r.Width - (iw + gap + tw)) / 2f;
        float th = Raylib.MeasureTextEx(fBold, label, 19, 0).Y;
        DrawFolder(x, r.Y + (r.Height - th) / 2f + th / 2f - 8, BtnInk);
        T(fBold, label, x + iw + gap, r.Y + (r.Height - th) / 2f, 19, BtnInk);
    }

    static void DrawFolder(float x, float y, Color c)
    {
        Raylib.DrawRectangleRounded(new Rectangle(x + 1, y - 3, 9, 7), 0.5f, 6, c); // pestana
        Raylib.DrawRectangleRounded(new Rectangle(x, y + 1, 20, 14), 0.3f, 8, c);   // cuerpo
    }

    static void DrawAlert(string msg, string? detail)
    {
        var r = new Rectangle(CardX, 328, CardW, 52);
        Raylib.DrawRectangleRounded(r, 0.3f, 10, WarnFill);
        Raylib.DrawRectangleRoundedLinesEx(r, 0.3f, 10, 1f, WarnLine);

        float cx = CardX + 26, cy = r.Y + 26;
        Raylib.DrawTriangle(new Vector2(cx, cy - 9), new Vector2(cx - 10, cy + 7), new Vector2(cx + 10, cy + 7), new Color(226, 106, 118, 255));
        Raylib.DrawRectangle((int)cx - 1, (int)cy - 5, 2, 6, WarnFill);
        Raylib.DrawCircleV(new Vector2(cx, cy + 4), 1.2f, WarnFill);

        float tx = CardX + 48, maxW = CardW - 62;
        T(fReg, Trunc(fReg, msg, maxW, 17), tx, r.Y + 4, 17, WarnInk);
        if (!string.IsNullOrEmpty(detail)) T(fSmall, Trunc(fSmall, detail!, maxW, 15), tx, r.Y + 27, 15, new Color(204, 148, 158, 255));
    }

    // ---------------------------------------------------------------- pantalla de aviso
    static string? Prompt(string? error, string? detail)
    {
        var btn = new Rectangle(W / 2f - 110, 252, 220, 46);
        while (!Raylib.WindowShouldClose())
        {
            if (Raylib.IsKeyPressed(KeyboardKey.Escape))
            {
                Raylib.SetMouseCursor(MouseCursor.Default);
                return null;
            }

            Vector2 m = Raylib.GetMousePosition();
            bool hover = Raylib.CheckCollisionPointRec(m, btn);
            Raylib.SetMouseCursor(hover ? MouseCursor.PointingHand : MouseCursor.Default);
            bool down = hover && Raylib.IsMouseButtonDown(MouseButton.Left);
            bool go = (hover && Raylib.IsMouseButtonPressed(MouseButton.Left))
                   || Raylib.IsKeyPressed(KeyboardKey.Enter)
                   || Raylib.IsKeyPressed(KeyboardKey.KpEnter)
                   || Raylib.IsKeyPressed(KeyboardKey.Space);

            Raylib.BeginDrawing();
            DrawBackground();
            DrawHeader();
            DrawCard();

            DrawDisc(W / 2f, 128);
            TCfit(fBold, "Se requiere una copia original de Titus The Fox", 168, 24, Ink, CardW - 48);
            TCfit(fReg, "Puede buscar el juego usando el botón de abajo", 206, 18, InkMid, CardW - 48);

            DrawButton(btn, "Examinar...", hover, down);
            if (error != null) DrawAlert(error, detail);

            const string hint = "ESC para salir";
            T(fSmall, hint, W - 36 - TW(fSmall, hint, 15), 392, 15, InkDim);

            Raylib.EndDrawing();
            ShowFirstFrame();

            if (go)
            {
                Raylib.SetMouseCursor(MouseCursor.Default);
                string? sel = Browse();
                if (sel != null) return sel;
            }
        }
        return null;
    }

    // ---------------------------------------------------------------- extraccion
    // Extrae en segundo plano y dibuja el progreso. err = texto de error; closed = ventana cerrada.
    static (string? err, bool closed) Extract(string src, string files)
    {
        string[] sqz;
        try { sqz = Directory.GetFiles(src, "*.SQZ"); }
        catch (Exception ex) { return (ex.Message, false); }

        int total = Math.Max(1, sqz.Length + 1), done = 0;
        string? error = null;
        bool finished = false, cancel = false;
        Volatile.Write(ref progressName, "Iniciando...");

        var t = new Thread(() =>
        {
            try
            {
                Directory.CreateDirectory(files);
                foreach (var f in sqz)
                {
                    if (cancel) return;
                    string name = Path.GetFileName(f);
                    Volatile.Write(ref progressName, name);
                    // mismo decodificador que usaba el juego; se escribe directo en FILES\
                    byte[] d = Sqz.Decompress(f);
                    File.WriteAllBytes(Path.Combine(files, Path.GetFileNameWithoutExtension(f) + ".bin"), d);
                    Interlocked.Increment(ref done);
                }
                if (cancel) return;
                Volatile.Write(ref progressName, "FOX.COM (música)");
                var m = Packed.ReadMusic(Path.Combine(src, "FOX.COM"));
                if (m == null) throw new InvalidDataException("FOX.COM no válido");
                File.WriteAllBytes(Path.Combine(files, "music.bin"), m);
                Interlocked.Increment(ref done);
                if (!FilesReady(files)) throw new FileNotFoundException("faltan archivos tras extraer");
            }
            catch (Exception ex) { error = ex.Message; }
            finally { Volatile.Write(ref finished, true); }
        });
        t.IsBackground = true;
        long start = Environment.TickCount64;
        t.Start();

        float shown = 0f; // progreso suavizado (la barra no salta)
        while (true)
        {
            bool fin = Volatile.Read(ref finished);
            int dn = Volatile.Read(ref done);
            float target = fin && error == null ? 1f : dn / (float)total;
            shown += (target - shown) * 0.25f;
            if (target - shown < 0.002f) shown = target;

            if (Raylib.WindowShouldClose())
            {
                cancel = true;
                t.Join();
                return (null, true);
            }

            DrawProgress(shown, dn, total, Volatile.Read(ref progressName));

            if (fin)
            {
                if (error != null) break;
                if (shown >= 1f && Environment.TickCount64 - start >= 900) break; // feedback visible aunque sea instantaneo
            }
        }
        t.Join();
        return (error, false);
    }

    static void DrawProgress(float p, int done, int total, string name)
    {
        Raylib.BeginDrawing();
        DrawBackground();
        DrawHeader();
        DrawCard();

        // indicador giratorio
        float a = (float)(Raylib.GetTime() * 220 % 360);
        var c = new Vector2(W / 2f, 130);
        Raylib.DrawRing(c, 15, 18, a, a + 260, 48, Accent);
        Raylib.DrawRing(c, 15, 18, a + 290, a + 350, 20, new Color(226, 140, 62, 60));

        TCfit(fBold, "Preparando los archivos del juego", 168, 22, Ink, W - 240);
        TCfit(fReg, "Extrayendo los datos de la copia original", 204, 17, InkMid, W - 260);

        float bx = 132, bw = W - 264, by = 262, bh = 10;
        T(fSmall, Trunc(fSmall, name, bw - 90, 15), bx, 238, 15, InkDim);
        string pc = ((int)Math.Round(p * 100)) + "%";
        T(fBold, pc, bx + bw - TW(fBold, pc, 17), 235, 17, Ink);

        Raylib.DrawRectangleRounded(new Rectangle(bx, by, bw, bh), 1f, 12, new Color(36, 44, 64, 255));
        float pw = bw * Math.Clamp(p, 0f, 1f);
        if (pw > 0.5f)
        {
            Raylib.DrawRectangleRounded(new Rectangle(bx, by, pw, bh), 1f, 12, Accent);
            Raylib.DrawRectangleRounded(new Rectangle(bx, by, pw, bh * 0.45f), 1f, 12, new Color(255, 238, 214, 28));
        }
        T(fSmall, $"{done} de {total} archivos", bx, 282, 15, InkDim);

        Raylib.EndDrawing();
        ShowFirstFrame();
    }

    // ---------------------------------------------------------------- dialogo del sistema
    static string? Browse()
    {
        string? sel = null;
        var th = new Thread(() =>
        {
            try
            {
                using var dlg = new System.Windows.Forms.FolderBrowserDialog
                {
                    Description = "Seleccione la carpeta que contiene LEVEL0.SQZ y FOX.COM",
                    UseDescriptionForTitle = true,
                    ShowNewFolderButton = false,
                };
                if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK) sel = dlg.SelectedPath;
            }
            catch { }
        });
        th.SetApartmentState(ApartmentState.STA);
        th.IsBackground = true;
        th.Start();
        th.Join();
        return sel;
    }
}
