using Raylib_cs;

namespace TitusTheFox;

// Pantalla de opciones: edita TitusTheFox.cfg con la fuente del juego
static class OptionsScreen
{
    static readonly string[] GfxKeys =
        { "Bilinear", "SharpBilinear", "XBR", "Scale3x", "FSR", "Vibrance", "Scanlines" };
    // null = fila especial (submenus de la pantalla principal; BACK siempre al final).
    // LEVEL EDITOR solo aparece con Editor.cfg (MODE=ON) junto al exe.
    static (string?[] Keys, string[] Special) MainRows()
    {
        var keys = new List<string?> { "FullScreen", "WideScreen", "Fps", "ShowFps", "LivesAtStart", "FinalExtraLevel", "Volume", null, null };
        var special = new List<string> { "CONTROLLER", "GRAPHIC FILTERS" };
        if (EditorCfg.Enabled()) { keys.Add(null); special.Add("LEVEL EDITOR"); }
        return (keys.ToArray(), special.ToArray());
    }

    const int Y0 = 26, Step = 14; // 10 filas + BACK: 26 + 10*14 + 12 = 178 <= 200

    // Devuelve 0 al volver con ESC/BACK, -1 si se cierra la ventana
    public static int Run(ref Texture2D tex, string dir)
    {
        EditorCfg.Reload(); // permite habilitar/deshabilitar el visor editando Editor.cfg
        while (true)
        {
            var (keys, special) = MainRows();
            int r = Page(ref tex, "OPTIONS", keys, special);
            if (r < 1) return r;
            if (r == 1) r = ControllerScreen.Run(ref tex);
            else if (r == 2) r = Page(ref tex, "GRAPHIC FILTERS", GfxKeys);
            else r = LevelViewer.Run(dir); // r == 3: solo existe con el modo editor activo
            if (r < 0) return r;
            Menu.WaitRelease(); // BACK del submenu: no reabrir ni propagar la tecla
        }
    }

    // 0 = BACK/ESC, -1 = cerrar ventana, 1.. = submenu especial (indice en special + 1)
    static int Page(ref Texture2D tex, string title, string?[] keys, string[]? special = null)
    {
        var buf = new byte[Fonts.W * Fonts.H];
        int n = keys.Length, sel = 0;
        var names = new string[n];
        for (int i = 0; i < n; i++)
        {
            if (keys[i] != null) { names[i] = Config.Description(keys[i]!); continue; }
            int si = 0;
            for (int j = 0; j < i; j++) if (keys[j] == null) si++;
            names[i] = (special != null && si < special.Length ? special[si] : "MENU") + "...";
        }
        while (Raylib.GetKeyPressed() != 0) { }
        while (true)
        {
            if (Raylib.WindowShouldClose()) return -1;
            Input.UpdatePad();
            if (Input.Pressed(KeyboardKey.Escape) || Input.PadEscape()) return 0;
            if (Input.Pressed(KeyboardKey.Up) || Input.PadUp()) sel = (sel + n) % (n + 1);
            if (Input.Pressed(KeyboardKey.Down) || Input.PadDown()) sel = (sel + 1) % (n + 1);
            int d = 0;
            bool ok = Input.Confirm() || Input.PadConfirm(); // se lee una sola vez (Pressed consume el evento)
            if (ok || Input.Pressed(KeyboardKey.Right) || Input.PadRight()) d = 1;
            if (Input.Pressed(KeyboardKey.Left) || Input.PadLeft()) d = -1;
            if (sel == n)
            {
                if (ok) return 0;
                d = 0;
            }
            else if (keys[sel] == null)
            {
                if (ok)
                {
                    int si = 0;
                    for (int j = 0; j < sel; j++) if (keys[j] == null) si++;
                    return si + 1;
                }
                d = 0;
            }
            if (d != 0 && keys[sel] != null && Change(keys[sel]!, d))
            {
                Title.ApplyOptions(); // todas las opciones en caliente (Lives aplica al proximo inicio de partida)
            }

            Array.Fill(buf, (byte)1);
            Fonts.Print(buf, title, 160 - title.Length * Fonts.CW / 2, 8);
            for (int i = 0; i < n; i++)
            {
                int y = Y0 + i * Step;
                Fonts.Print(buf, names[i], 52, y);
                if (keys[i] == null) continue;
                string val = Show(keys[i]!);
                Fonts.Print(buf, val, 296 - val.Length * Fonts.CW, y);
            }
            // cursor = sprite Sel[0] de la textura del menu; BACK en rojo de START
            int cy = Y0 + sel * Step + (Fonts.CH - 8) / 2;
            Texture2D t = tex; // copia: un parametro ref no se puede capturar en la lambda
            Menu.Present(buf, () =>
            {
                Menu.OverlayText(t, "BACK", 52, Y0 + n * Step);
                Title.DrawRegion(t, Menu.Sel[0], 40, cy, Color.White);
            });
        }
    }

    static string Show(string key)
    {
        string v = Config.GetRaw(key) ?? "";
        return key is "Fps" or "LivesAtStart" or "Volume" ? v : (v.Equals("T", StringComparison.OrdinalIgnoreCase) ? "ON" : "OFF");
    }

    // Aplica el cambio; true si se modifico el cfg
    static bool Change(string key, int d)
    {
        string v = Config.GetRaw(key) ?? "";
        if (key == "Fps")
        {
            int n = int.TryParse(v, out int a) ? a : 60;
            n += 10 * d;
            if (n > 240) n = 30; else if (n < 30) n = 240;
            Config.SetRaw(key, n.ToString());
        }
        else if (key == "LivesAtStart")
        {
            int n = int.TryParse(v, out int a) ? a : 3;
            n += d;
            if (n > 30) n = 1; else if (n < 1) n = 30;
            Config.SetRaw(key, n.ToString());
        }
        else if (key == "Volume")
        {
            int n = int.TryParse(v, out int a) && a >= 0 && a <= 100 ? a : 80;
            n = Math.Clamp(n + 2 * d, 0, 100); // sin dar la vuelta
            Config.SetRaw(key, n.ToString());
        }
        else Config.SetRaw(key, v.Equals("T", StringComparison.OrdinalIgnoreCase) ? "F" : "T");
        return true;
    }
}
