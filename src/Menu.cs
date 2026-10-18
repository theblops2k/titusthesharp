using Raylib_cs;

namespace TitusTheFox;

// Puerto de viewmenu (menu.c:53-334), juego Titus (game == 0), formato 2.
public static class Menu
{
    const int FadeTime = 1000;

    // Devuelve el nivel elegido (1 = Start, menu.c:332), o -1 si se sale (ESC / cierre).
    // Password (seleccion 1, menu.c:260-273): enterpassword; si es valido fija curlevel y vuelve al menu.
    static int curlevel = 1;

    internal static readonly string[] LevelCode =
    {
        "EFE8", "5165", "67D4", "2BDA", "11E5", "86EE", "4275", "A0B9",
        "501C", "E9ED", "D4E6", "A531", "CE96", "B1A4", "EBEA", "3B9C",
    };

    internal static string? MenuPath; // para que Game cargue la textura del menu (menu de pausa)

    public static int View(string path)
    {
        Fonts.Load(Path.GetDirectoryName(path) ?? ".");
        MenuPath = path;
        Texture2D tex = Title.LoadFormat2(path);
        int r = Run(ref tex);
        Raylib.UnloadTexture(tex);
        return r;
    }

    // sel[0] = (120,160) 8x8; sel[1] = (120,173) 8x8 (menu.c:127-135)
    internal static readonly Rectangle[] Sel =
    {
        new Rectangle(120, 160, 8, 8),
        new Rectangle(120, 173, 8, 8),
        new Rectangle(120, 186, 8, 8), // OPTIONS (posicion a verificar visualmente)
    };

    // Texto con la fuente del juego sobre la imagen del menu (indices 1 y 255 = transparentes)
    static Color? startColour;

    // Color de texto de START: el mas frecuente no negro en la region x=128..199,y=158..169 de la imagen
    static Color StartColour(Texture2D tex)
    {
        if (startColour is Color c0) return c0;
        Image im = Raylib.LoadImageFromTexture(tex);
        var cnt = new Dictionary<uint, int>();
        for (int py = 158; py < 170; py++)
            for (int px = 128; px < 200; px++)
            {
                Color c = Raylib.GetImageColor(im, px, py);
                if (c.R == 0 && c.G == 0 && c.B == 0) continue;
                uint k = (uint)(c.R << 16 | c.G << 8 | c.B);
                cnt[k] = cnt.GetValueOrDefault(k) + 1;
            }
        Raylib.UnloadImage(im);
        uint best = cnt.Count == 0 ? 0xA02020u : cnt.MaxBy(e => e.Value).Key;
        startColour = new Color((byte)(best >> 16), (byte)(best >> 8), (byte)best, (byte)255);
        return startColour.Value;
    }

    internal static void OverlayText(Texture2D tex, string text, int x, int y, int alpha = 255)
    {
        var b = new byte[Fonts.W * Fonts.H]; // Fonts.Print escribe en 320x200
        Array.Fill(b, (byte)255);
        Fonts.Print(b, text, x, y);
        Color sc0 = StartColour(tex); Color col = new Color((byte)(sc0.R * alpha / 255), (byte)(sc0.G * alpha / 255), (byte)(sc0.B * alpha / 255), (byte)255);
        int s = Render.SCALE;
        for (int py = y; py < y + 12 && py < Fonts.H; py++)
            for (int px = x; px < x + text.Length * 8 && px < Fonts.W; px++)
            {
                int c = b[py * Fonts.W + px];
                if (c == 255 || c == 1) continue;
                
                Raylib.DrawRectangle(px * s, py * s, s, s, col);
            }
    }

    // Igual que la intro (Title.IntroText): Fonts.Print sobre buffer y color por pixel desde PaletteFontColour (degradado dorado)
    internal static void OverlayTextPal(string text, int x, int y)
    {
        var b = new byte[Fonts.W * Fonts.H];
        Array.Fill(b, (byte)1);
        Fonts.Print(b, text, x, y);
        int s = Render.SCALE; var pal = OriginalTables.PaletteFontColour;
        for (int py = y; py < y + 12 && py < Fonts.H; py++)
            for (int px = x; px < x + text.Length * 8 && px < Fonts.W; px++)
            {
                int c = b[py * Fonts.W + px] & 15;
                if (c == 1) continue;
                Raylib.DrawRectangle(px * s, py * s, s, s, new Color(pal[c, 0], pal[c, 1], pal[c, 2], (byte)255));
            }
    }

    static void Draw(Texture2D tex, int alpha, int selection, bool fadeOut, int quitSel = -1)
    {
        Color tint = Title.FadeTint(alpha);
        Title.Begin();
        Raylib.ClearBackground(Color.Black);
        Title.DrawFull(tex, alpha);
        if (!fadeOut)
        {
            // tapa el cursor de la imagen con el fondo de sel[1] y dibuja el cursor en 'selection'
            Title.DrawRegion(tex, Sel[1], (int)Sel[0].X, (int)Sel[0].Y, tint);
        }
        else
        {
            // menu.c:317 SDL_FillRect(screen,&sel[0],0)
            int s = Render.SCALE;
            Raylib.DrawRectangle((int)Sel[0].X * s, (int)Sel[0].Y * s, 8 * s, 8 * s, Color.Black);
        }
        Title.DrawRegion(tex, Sel[0], (int)Sel[selection].X, (int)Sel[selection].Y, tint);
        OverlayText(tex, "OPTIONS", 128, 184, alpha);
        if (quitSel >= 0) DrawQuitPopup(tex, quitSel);
        Title.End();
    }

    // Popup QUIT: mismo estilo que el menu de pausa (Game.DrawPauseMenu)
    static void DrawQuitPopup(Texture2D tex, int sel)
    {
        int s = Render.SCALE, bx = 100, by = 72;
        Raylib.DrawRectangle(bx * s, by * s, 120 * s, 56 * s, new Color(0, 0, 0, 204));
        OverlayTextPal("QUIT", 144, by + 6);
        OverlayText(tex, "YES", 128, by + 22);
        OverlayText(tex, "NO", 128, by + 36);
        Title.DrawRegion(tex, Sel[0], 114, by + 24 + sel * 14, Color.White);
    }

    // Bucle del popup QUIT. Devuelve true si se elige YES. Cursor inicial en NO.
    static bool QuitPopup(Texture2D tex, int selection)
    {
        int q = 1;
        while (true)
        {
            if (Raylib.WindowShouldClose()) return true;
            if (Input.Pressed(KeyboardKey.Escape)) return false;
            if (Input.Pressed(KeyboardKey.Up)) q = Math.Max(0, q - 1);
            if (Input.Pressed(KeyboardKey.Down)) q = Math.Min(1, q + 1);
            if (Input.Confirm()) return q == 0;
            Draw(tex, 255, selection, false, q);
        }
    }

    // espera a soltar Enter/KpEnter/Espacio (consume el evento) y limpia la cola de teclas
    internal static void WaitRelease()
    {
        while (!Raylib.WindowShouldClose() &&
               (Raylib.IsKeyDown(KeyboardKey.Enter) || Raylib.IsKeyDown(KeyboardKey.KpEnter) || Raylib.IsKeyDown(KeyboardKey.Space)))
        {
            Raylib.BeginDrawing();
            Raylib.EndDrawing();
        }
        while (Raylib.GetKeyPressed() != 0) { }
    }

    static int Run(ref Texture2D tex)
    {
        int selection = 0;
        long t0 = Environment.TickCount64;
        int alpha = 0;

        while (alpha < 255) // fundido de entrada
        {
            if (Raylib.WindowShouldClose()) return -1;
            alpha = (int)Math.Min(255, (Environment.TickCount64 - t0) * 256 / FadeTime);
            Draw(tex, alpha, selection, false);
        }

        while (true) // menu
        {
            bool chosen = false;
            while (!chosen)
            {
                if (Raylib.WindowShouldClose()) return -1;
                if (Input.Pressed(KeyboardKey.Up)) selection = Math.Max(0, selection - 1);
                if (Input.Pressed(KeyboardKey.Down)) selection = Math.Min(2, selection + 1);
                if (Input.Pressed(KeyboardKey.Escape) && QuitPopup(tex, selection)) return -1;
                if (Input.Confirm())
                    chosen = true;
                Draw(tex, 255, selection, false);
            }
            if (selection == 0) break;
            if (selection == 2)
            {
                Crash.SetScreen("options");
                if (OptionsScreen.Run(ref tex) < 0) return -1;
                WaitRelease();
                continue;
            }
            Crash.SetScreen("password");
            int pr = EnterPassword();
            if (pr < 0) return pr;
            WaitRelease();
            if (pr > 0 && pr <= Math.Min(Math.Max(Game.LevelCount, Game.ExtraIndex + 1), LevelCode.Length)) curlevel = pr;
            selection = 0;
        }

        t0 = Environment.TickCount64;
        alpha = 0;
        while (alpha < 255) // fundido de salida
        {
            if (Raylib.WindowShouldClose()) return -1;
            alpha = (int)Math.Min(255, (Environment.TickCount64 - t0) * 256 / FadeTime);
            Draw(tex, 255 - alpha, selection, true);
        }
        return curlevel;
    }

    // Pantalla de enterpassword (menu.c:343-347, 429): "CODE" en (111,80), codigo en (159,80)
    public static void PasswordScreen(byte[] buf, string code)
    {
        Array.Fill(buf, (byte)1);
        Fonts.Print(buf, "CODE", 111, 80);
        Fonts.Print(buf, code, 159, 80);
    }

    internal static void Present(byte[] buf, Action? overlay = null)
    {
        Title.Begin();
        Raylib.ClearBackground(Color.Black);
        Render.Draw(OriginalTables.PaletteFontColour, buf);
        overlay?.Invoke();
        Title.End();
    }

    // waitforbutton (keyboard.c:45): Enter/Espacio = 0, ESC/cierre = -1
    static int WaitForButton(byte[] buf)
    {
        while (true)
        {
            if (Raylib.WindowShouldClose()) return -1;
            if (Input.Confirm()) return 0;
            Present(buf);
        }
    }

    // menu.c:336 enterpassword: -1 salir, 0 invalido, n = nivel
    static int EnterPassword()
    {
        var buf = new byte[Fonts.W * Fonts.H];
        var code = new char[] { '_', '_', '_', '_' };
        while (Raylib.GetCharPressed() != 0) { }
        for (int i = 0; i < 4;)
        {
            if (Raylib.WindowShouldClose()) return -1;
            if (Input.Pressed(KeyboardKey.Escape)) return 0; // ESC vuelve al menu
            int ch;
            while ((ch = Raylib.GetCharPressed()) != 0 && i < 4)
            {
                if (ch >= 'a' && ch <= 'f') ch -= 'a' - 'A';
                if ((ch >= '0' && ch <= '9') || (ch >= 'A' && ch <= 'F')) code[i++] = (char)ch;
            }
            PasswordScreen(buf, new string(code));
            Present(buf);
        }
        string c = new string(code);
        int n = Math.Min(Math.Max(Game.LevelCount, Game.ExtraIndex + 1), LevelCode.Length);
        for (int k = 0; k < n; k++)
        {
            if (c != LevelCode[k]) continue;
            Fonts.Print(buf, "LEVEL", 103, 104);
            string num = (k + 1).ToString();
            Fonts.Print(buf, num, 199 - 8 * num.Length, 104);
            int r = WaitForButton(buf);
            return r < 0 ? r : k + 1;
        }
        Fonts.Print(buf, "!  WRONG CODE  !", 87, 104);
        return WaitForButton(buf);
    }
}
