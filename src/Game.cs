using Raylib_cs;

namespace TitusTheFox;

// Port parcial de engine.c: playtitus() (carga) y playlevel() (bucle). Sin fisica, enemigos ni objetos.
public static class Game
{
    // titus.conf:20-36: orden de los 15 niveles de Titus (indice = levelnumber)
    // Con FinalExtraLevel=T en el cfg se agrega LEVELD al final (calculado al iniciar)
    static string[] LevelFiles = BuildLevelFiles();
    // Recalcula la lista de niveles (y LevelCount) tras cambiar FinalExtraLevel
    public static void RefreshLevels() => LevelFiles = BuildLevelFiles();

    static string[] BuildLevelFiles()
    {
        var l = new List<string>
        {
            "LEVEL0", "LEVELJ", "LEVEL1", "LEVEL2", "LEVEL3", "LEVEL4", "LEVEL5", "LEVEL6",
            "LEVEL7", "LEVEL8", "LEVEL9", "LEVELB", "LEVELC", "LEVELE", "LEVELG",
        };
        if (Config.FinalExtraLevel()) l.Add("LEVELD");
        return l.ToArray();
    }

    static byte[] screen = Array.Empty<byte>(); // VIEW_W x TILESCREEN_H (se crea en DrawFrame)

    /// engine.c:74 playtitus(firstlevel) + playlevel(). firstlevel es 0-based (retval-1).
    /// Devuelve false si se cerro la ventana.
    public const int ExtraIndex = 15; // posicion de LEVELD (codigo 3B9C)
    public static int LevelCount => LevelFiles.Length;

    static string Original_Dir = "";

    public static bool Run(string dir, int firstlevel)
    {
        // Codigo 3B9C (indice 15): LEVELD por contrasena aunque no este en la secuencia; solo ese nivel
        bool extraOnly = firstlevel == ExtraIndex && LevelFiles.Length == ExtraIndex;
        string[] files = extraOnly ? new[] { "LEVELD" } : LevelFiles;
        int baseIdx = extraOnly ? ExtraIndex : 0;
        if (firstlevel < 0 || (!extraOnly && firstlevel >= LevelFiles.Length)) return true;
        Original_Dir = dir;

        Original.InitOriginal();
        // engine.c:101-107 sprites; engine.c:122 loadobjects
        var sprites = Sprites.LoadSprites(File.ReadAllBytes(Path.Combine(dir, "SPREXP.bin")));
        var objects = new TITUS_objectdata[Original.ORIG_OBJECT_COUNT];
        for (int i = 0; i < objects.Length; i++)
            objects[i] = new TITUS_objectdata
            {
                maxspeedY = Original.OBJECT_MAXSPEEDY[i],
                support = Original.OBJECT_SUPPORT[i],
                bounce = Original.OBJECT_BOUNCE[i],
                gravity = Original.OBJECT_GRAVITY[i],
                droptobottom = Original.OBJECT_DROPTOBOTTOM[i],
                no_damage = Original.OBJECT_NO_DAMAGE[i],
            };

        // engine.c:131-145
        var level = new TITUS_level { levelnumber = (short)firstlevel, lives = Config.LivesAtStart(), extrabonus = 0 };
        Render.Init();
        bool ok = true;
        // engine.c:136 bucle de niveles: avanza mientras NEWLEVEL_FLAG termine el nivel
        for (level.levelnumber = (short)firstlevel; level.levelnumber < (extraOnly ? ExtraIndex + 1 : LevelFiles.Length) && ok; level.levelnumber++)
        {
        string lfile = files[level.levelnumber - baseIdx];
        level.levelid = Original.GetLevelId(level.levelnumber);
        if (level.levelid < 0 && lfile == "LEVELD") level.levelid = 13; // LEVELD no esta en levelid_titus: id = letra D (13)
        byte[] leveldata = File.ReadAllBytes(Path.Combine(dir, lfile + ".bin"));
        Level.LoadLevel(level, leveldata, sprites, objects);
        Crash.SetLevel(level); // CrashLog.cs: contexto de crash
        Audio.SelectMusic(0); // engine.c:158
        if (!ShowStatus(dir, level)) { ok = false; break; }
        while (true)
        {
            // engine.c:156 bucle de reinicio del nivel
            Audio.SelectMusic(Original.LEVEL_MUSIC[level.levelid]); // engine.c:181
            ClearData(level);
            InitScreenM(level);
            exitCode = 0;
            if (!OpenScreen(level)) { ok = false; break; } // draw.c:485 (INIT_SCREENM)
            if (!PlayLevel(level)) { ok = false; break; }
            if (exitCode == 5) break; // ESC: al menu
            if (Gates.NEWLEVEL_FLAG && (!WaitSong(level) || !CloseScreen(level))) { ok = false; break; } // gates.c:65-68 SELECT_MUSIC(4) + WAIT_SONG + CLOSE_SCREEN
            if (exitCode == 3) // RESETLEVEL_FLAG == 1: muerte con vidas restantes
            {
                level.lives--;
                Crash.Event($"muerte: vidas={level.lives} x={level.player.sprite.x} y={level.player.sprite.y}");
                if (!Death(level)) { ok = false; break; }
                continue;
            }
            if (exitCode == 2) { Crash.Event("game over"); ok = GameOver(level); break; } // GAMEOVER_FLAG
            if (Gates.NEWLEVEL_FLAG) break; // engine.c:199 siguiente nivel
        }
        if (exitCode == 2 || exitCode == 5) break; // game over / ESC: al menu
        }
        // engine.c:255-258: tras el ultimo nivel (no por game over ni salida) pantalla final (titus.conf:69 LEVELA.SQZ, formato 0)
        if (ok && exitCode != 2 && exitCode != 5 && !extraOnly && level.levelnumber >= LevelFiles.Length)
            ok = Title.ViewUntilKey(Path.Combine(dir, "LEVELA.bin"));
        return ok;
    }

    // engine.c: viewstatus al iniciar el nivel; espera una tecla
    static bool ShowStatus(string dir, TITUS_level level)
    {
        Fonts.Load(dir);
        var buf = new byte[Fonts.W * Fonts.H];
        Fonts.ViewStatus(buf, level);
        while (Raylib.GetKeyPressed() != 0) { }
        while (Raylib.GetKeyPressed() == 0)
        {
            if (Raylib.WindowShouldClose()) return false;
            Title.Begin();
            Raylib.ClearBackground(Color.Black);
            Render.Draw(OriginalTables.PaletteFontColour, buf);
            Title.End();
        }
        // draw.c:471 INIT_SCREENM: CLOSE_SCREEN sobre la pantalla de estado
        for (int i = 0; i < 10; i++)
        {
            if (Raylib.WindowShouldClose()) return false;
            Title.Begin();
            Raylib.ClearBackground(Color.Black);
            Render.Draw(OriginalTables.PaletteFontColour, buf);
            MaskOutside(i * 16, i * 9, 320 - i * 16, 192 - i * 9);
            Title.End(100); // NO_FAST_CPU(false): 10 ms
        }
        return true;
    }

    // draw.c:593 view_password: pantalla con el codigo del nivel; false si se cierra la ventana
    internal static bool ViewPassword(TITUS_level level, int levelIndex)
    {
        Crash.SetScreen("password");
        if (!CloseScreen(level)) return false;
        var buf = new byte[Fonts.W * Fonts.H];
        Array.Fill(buf, (byte)1); // fondo negro en PaletteFontColour (igual que Menu.PasswordScreen)
        string num = (levelIndex + 1).ToString();
        Fonts.Print(buf, "LEVEL", 13 * 8, 13 * 8);
        Fonts.Print(buf, num, 25 * 8 - num.Length * 8, 13 * 8);
        Fonts.Print(buf, "CODE", 14 * 8, 10 * 8);
        Fonts.Print(buf, Menu.LevelCode[levelIndex], 20 * 8, 10 * 8);
        while (Raylib.GetKeyPressed() != 0) { }
        while (true)
        {
            if (Raylib.WindowShouldClose()) return false;
            if (Input.Confirm()) break;
            Title.Begin();
            Raylib.ClearBackground(Color.Black);
            Render.Draw(OriginalTables.PaletteFontColour, buf);
            Title.End();
        }
        return OpenScreen(level);
    }

    // Pinta de negro todo lo exterior al rectangulo visible (coords 320x192)
    static void MaskOutside(int x0, int y0, int x1, int y1, int ox = 0, int vw = 320)
    {
        int s = Render.SCALE, W = vw * s, H = Title.H * s;
        x0 += ox; x1 += ox; // ox: margen de la vista ancha (el rectangulo 320 queda centrado)
        x0 *= s; x1 *= s; y0 *= s; y1 *= s;
        Raylib.DrawRectangle(0, 0, W, Math.Max(0, y0), Color.Black);
        Raylib.DrawRectangle(0, Math.Min(H, y1), W, Math.Max(0, H - y1), Color.Black);
        Raylib.DrawRectangle(0, 0, Math.Max(0, x0), H, Color.Black);
        Raylib.DrawRectangle(Math.Min(W, x1), 0, Math.Max(0, W - x1), H, Color.Black);
    }

    // gates.c:114 CLOSE_SCREEN: 10 pasos, bordes negros de 16x9 px por paso, 10 ms cada uno
    static bool CloseScreen(TITUS_level level)
    {
        for (int i = 0; i < 10; i++)
        {
            if (Raylib.WindowShouldClose()) return false;
            DrawFrame(level, 0, true, i * 16, i * 9, 320 - i * 16, 192 - i * 9, 100); // 10 ms
        }
        return true;
    }

    // gates.c:156 OPEN_SCREEN: pantalla negra y solo tiles; el rectangulo visible crece 10 pasos
    static bool OpenScreen(TITUS_level level)
    {
        for (int k = 0; k < 10; k++)
        {
            if (Raylib.WindowShouldClose()) return false;
            if (k == 0) { Title.Begin(); Raylib.ClearBackground(Color.Black); Title.End(100); }
            else
            {
                int j = 10 - k; // rectangulo copiado en la iteracion anterior (i=2k): (j*16, j*9, 32k, 18k+9)
                int x0 = j * 16, y0 = j * 9, x1 = x0 + 32 * k, y1 = y0 + 18 * k + 9;
                DrawFrame(level, 0, false, x0, y0, x1, y1, 100);
            }
        }
        return true;
    }

    // Pasos de logica a 35/s (original) independientes de la tasa de dibujo (60 Hz)
    sealed class Ticker
    {
        static double TICK_MS => Title.TickMs; // 1000 / FpsGame
        readonly System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
        double last, acc = TICK_MS; // primer frame ejecuta un paso
        double logAcc = 1.0 - 1e-9; // acumulador de pasos de logica (LogicScale por tick)
        public int Due()
        {
            double now = sw.Elapsed.TotalMilliseconds;
            acc = Math.Min(acc + (now - last), TICK_MS * 4 / Math.Min(1.0, Title.LogicScale)); last = now;
            int n = 0;
            while (acc >= TICK_MS)
            {
                acc -= TICK_MS; logAcc += Title.LogicScale;
                while (logAcc >= 1.0 - 1e-9) { logAcc -= 1.0; n++; }
            }
            return n;
        }
    }

    static int exitCode; // 0 salir, 2 game over, 3 muerte

    // Dibuja un frame completo (tiles, sprites, barra de energia, fundido opcional)
    static void DrawFrame(TITUS_level level, int fade = 0, bool sprites = true, int vx0 = 0, int vy0 = 0, int vx1 = 320, int vy1 = 192, int hz = 0, Action? overlay = null)
    {
        if (screen.Length != Scroll.VIEW_W * Scroll.TILESCREEN_H) screen = new byte[Scroll.VIEW_W * Scroll.TILESCREEN_H];
        Title.ViewW = Scroll.VIEW_W; // vista ancha solo en el nivel (Title.Present lo restablece a 320)
        Title.Begin();
        Raylib.ClearBackground(Color.Black);
        if (Scroll.VIEW_W > Scroll.TILESCREEN_W) ComposeTilesWide(level); else ComposeTiles();
        if (sprites)
        {
            foreach (var el in level.elevator) DisplaySprite(level, el.sprite);
            foreach (var o in level.@object) DisplaySprite(level, o.sprite);
            foreach (var en in level.enemy) if (en.visible) DisplaySprite(level, en.sprite);
            foreach (var t in level.trash) DisplaySprite(level, t);
            DisplaySprite(level, level.player.sprite);
            DisplaySprite(level, level.player.sprite2);
            DisplaySprite(level, level.player.sprite3);
        }
        Render.Draw(level, screen, Scroll.VIEW_W);
        if (sprites && Player.BAR_FLAG != 0) DrawEnergy(level);
        if (vx0 != 0 || vy0 != 0 || vx1 != 320 || vy1 != 192) MaskOutside(vx0, vy0, vx1, vy1, Scroll.MARGIN, Scroll.VIEW_W);
        overlay?.Invoke();
        if (fade > 0)
            Raylib.DrawRectangle(0, 0, Scroll.VIEW_W * Render.SCALE, Title.H * Render.SCALE, new Color((byte)0, (byte)0, (byte)0, (byte)Math.Min(255, fade)));
        Title.End(hz);
    }

    // draw.c:497 DISPLAY_ENERGY
    static void DrawEnergy(TITUS_level level)
    {
        int s = Render.SCALE, off = 96 + Scroll.MARGIN, hp = Math.Max(0, (int)level.player.hp);
        for (int i = 0; i < hp; i++, off += 8) Raylib.DrawRectangle(off * s, 9 * s, 4 * s, 16 * s, Color.White);
        for (int i = 0; i < Player.MAXIMUM_ENERGY - hp; i++, off += 8) Raylib.DrawRectangle(off * s, 15 * s, 4 * s, 4 * s, Color.White);
    }

    static bool FadeOut(TITUS_level level)
    {
        long t0 = Environment.TickCount64; // draw.c:519 fadeout: 1000 ms por tiempo
        for (int a = 0; a < 255;)
        {
            if (Raylib.WindowShouldClose()) return false;
            a = (int)Math.Min(255, (Environment.TickCount64 - t0) * 256 / 1000);
            DrawFrame(level, a);
        }
        return true;
    }

    // engine.c:378 death(): sprite de muerte que sube y cae, luego fundido
    static bool Death(TITUS_level level)
    {
        var p = level.player;
        Audio.SelectMusic(1); // engine.c:382
        Player.FORCE_POSE(level);
        UpdateSprite(level, p.sprite, 13, true);
        p.sprite.speedY = 15;
        var tk = new Ticker();
        for (int i = 0; i < 60;) // 60 pasos de logica a 35/s, dibujo a 60 Hz
        {
            if (Raylib.WindowShouldClose()) return false;
            DrawFrame(level, 0, true, 0, 0, 320, 192, Title.FpsGame);
            for (int n = tk.Due(); n > 0 && i < 60; n--, i++)
            {
                p.sprite.speedY--;
                if (p.sprite.speedY < -16) p.sprite.speedY = -16;
                p.sprite.y -= p.sprite.speedY;
            }
        }
        if (!WaitSong(level)) return false; // engine.c:400 WAIT_SONG
        Audio.SelectMusic(0); // engine.c:401
        return CloseScreen(level); // engine.c:410 CLOSE_SCREEN
    }

    // audio.c:682 WAIT_SONG: espera a que termine la cancion (se sigue dibujando para atender la ventana)
    static bool WaitSong(TITUS_level level)
    {
        while (!Audio.SongDone)
        {
            if (Raylib.WindowShouldClose()) return false;
            if (Input.Pressed(KeyboardKey.Escape)) break;
            DrawFrame(level, 0, true, 0, 0, 320, 192, Title.FpsGame);
        }
        return true;
    }

    // engine.c:413 gameover(): "Game" y "Over" entran por los lados; espera tecla
    static bool GameOver(TITUS_level level)
    {
        var p = level.player;
        Audio.SelectMusic(2); // engine.c:417
        UpdateSprite(level, p.sprite, 13, true);
        UpdateSprite(level, p.sprite2, 333, true);
        p.sprite2.x = (short)((Scroll.BITMAP_X << 4) - (120 - 2));
        p.sprite2.y = (short)((Scroll.BITMAP_Y << 4) + 100);
        UpdateSprite(level, p.sprite3, 334, true);
        p.sprite3.x = (short)((Scroll.BITMAP_X << 4) + (320 + 120 - 2));
        p.sprite3.y = (short)((Scroll.BITMAP_Y << 4) + 100);
        var tk = new Ticker();
        for (int i = 0; i < 31;)
        {
            if (Raylib.WindowShouldClose()) return false;
            DrawFrame(level, 0, true, 0, 0, 320, 192, Title.FpsGame);
            for (int n = tk.Due(); n > 0 && i < 31; n--, i++)
            {
                p.sprite2.x += 8;
                p.sprite3.x -= 8;
            }
        }
        // engine.c:413-434 SCREEN_5C: LCtrl+LAlt+E (sin otras teclas) -> pantalla final + musica 9
        if (Raylib.IsKeyDown(KeyboardKey.LeftControl) && Raylib.IsKeyDown(KeyboardKey.LeftAlt) && Raylib.IsKeyPressed(KeyboardKey.E) && !OtherKeyDown())
        {
            if (!Title.ViewUntilKey(Path.Combine(Original_Dir, "LEVELA.bin"))) return false;
            Audio.SelectMusic(9);
        }
        while (Raylib.GetKeyPressed() != 0) { }
        while (true) // waitforbutton
        {
            if (Raylib.WindowShouldClose()) return false;
            DrawFrame(level, 0, true, 0, 0, 320, 192, Title.FpsGame);
            if (Raylib.GetKeyPressed() != 0) break;
        }
        return FadeOut(level);
    }

    // reset.c:193 CLEAR_DATA (solo lo relativo al scroll y al jugador)
    static void ClearData(TITUS_level level)
    {
        Gates.NEWLEVEL_FLAG = false; // reset.c:222
        Scroll.loop_cycle = 0;
        Scroll.tile_anim = 0;
        Scroll.NOSCROLL_FLAG = false;
        Scroll.LADDER_FLAG = false;
        Scroll.ACTION_TIMER = 0;
        Scroll.XSCROLL_CENTER = false;
        Scroll.YSCROLL_CENTER = false;
        Scroll.XLIMIT_SCROLL = 0;
        Scroll.YLIMIT_SCROLL = 0;
        Scroll.LAST_ORDER = 0;
        Player.Init();

        // reset.c:340-371 SET_ALL_SPRITES (jugador)
        var p = level.player;
        p.sprite = new TITUS_sprite();
        p.sprite2 = new TITUS_sprite();
        p.sprite3 = new TITUS_sprite();
        p.sprite.x = p.initX;
        p.sprite.y = p.initY;
        p.hp = p.inithp;
        p.animcycle = 0;
        UpdateSprite(level, p.sprite, 0, true);
        Objects.InitElevators(level); // reset.c:329
        Objects.Init(level); // reset.c:344
        Enemies.Init(level); // reset.c:310 + SET_DATA_NMI
    }

    // sprites.c:323 updatesprite()
    internal static void UpdateSprite(TITUS_level level, TITUS_sprite spr, short number, bool clearflags)
    {
        spr.number = number;
        spr.spritedata = level.spritedata![number];
        spr.enabled = true;
        if (clearflags)
        {
            spr.flipped = false;
            spr.flash = false;
            spr.visible = false;
            spr.droptobottom = false;
            spr.killing = false;
        }
        spr.invisible = false;
    }

    // draw.c:470 INIT_SCREENM
    static void InitScreenM(TITUS_level level)
    {
        Scroll.BITMAP_X = 0;
        Scroll.BITMAP_Y = 0;
        Scroll.BITMAP_XM = 0;
        Scroll.BITMAP_YM = 0;
        for (byte i = 0; i < Scroll.screen_width; i++)
            for (byte j = 0; j < Scroll.screen_height; j++)
                Scroll.DISPLAY_CHAR(level, level.tilemap[Scroll.BITMAP_Y + j][Scroll.BITMAP_X + i], j, i);
        do
        {
            Scroll.scroll(level);
        } while (Scroll.YSCROLL_CENTER || Scroll.XSCROLL_CENTER);
    }

    // engine.c:268 playlevel(): de momento solo scroll y dibujo
    // Interpolacion de dibujo: alpha 0..1 entre el estado previo y el actual (1 = sin interpolar)
    static double drawAlpha = 1.0;

    static bool PlayLevel(TITUS_level level)
    {
        try { return PlayLevelLoop(level); }
        finally { drawAlpha = 1.0; }
    }

    static void SnapshotPrev(TITUS_level level)
    {
        foreach (var el in level.elevator) { el.sprite.prevX = el.sprite.x; el.sprite.prevY = el.sprite.y; }
        foreach (var o in level.@object) { o.sprite.prevX = o.sprite.x; o.sprite.prevY = o.sprite.y; }
        foreach (var en in level.enemy) { en.sprite.prevX = en.sprite.x; en.sprite.prevY = en.sprite.y; }
        foreach (var t in level.trash) { t.prevX = t.x; t.prevY = t.y; }
        foreach (var s in new[] { level.player.sprite, level.player.sprite2, level.player.sprite3 }) { s.prevX = s.x; s.prevY = s.y; }
    }

    // engine.c:448-453: el atajo solo vale si no hay otra tecla pulsada ademas de LCtrl, LAlt y E
    static bool OtherKeyDown()
    {
        foreach (KeyboardKey k in Enum.GetValues<KeyboardKey>())
        {
            if (k == KeyboardKey.Null || k == KeyboardKey.LeftControl || k == KeyboardKey.LeftAlt || k == KeyboardKey.E) continue;
            if (Raylib.IsKeyDown(k)) return true;
        }
        return false;
    }

    static Texture2D? pauseTex;

    // Recuadro negro 120x56 centrado en 320x200 (desplazado por MARGIN en vista ancha)
    static void DrawPauseMenu(Texture2D tex, int sel)
    {
        int s = Render.SCALE, ox = Scroll.MARGIN, bx = ox + 100, by = 72;
        Raylib.DrawRectangle(bx * s, by * s, 120 * s, 56 * s, new Color(0, 0, 0, 204));
        Menu.OverlayTextPal("GAME PAUSED", ox + 116, by + 6); // dorado de la intro (PaletteFontColour)
        Menu.OverlayText(tex, "CONTINUE", ox + 128, by + 22);
        Menu.OverlayText(tex, "EXIT", ox + 128, by + 36);
        Title.DrawRegion(tex, Menu.Sel[0], ox + 114, by + 24 + sel * 14, Color.White);
    }

    static bool PlayLevelLoop(TITUS_level level)
    {
        // draw.c:319 dibuja a ~70 Hz y la logica corre cada 29 ms (acumulador): aqui dibujo a 60 Hz y logica a 35 ticks/s
        double TICK_MS = Title.TickMs; // 1000 / FpsGame
        var sw = System.Diagnostics.Stopwatch.StartNew();
        double last = 0, acc = TICK_MS; // acc inicial: primer frame ejecuta logica
        double logAcc = 1.0 - 1e-9; // acumulador de logica: cada tick suma LogicScale (35/FpsGame); al llegar a 1 corre un paso entero
        while (true)
        {
            if (Raylib.WindowShouldClose()) return false;
            if (Input.Pressed(KeyboardKey.P, Input.LevelKeyMs) || Input.Pressed(KeyboardKey.Escape, Input.LevelKeyMs)) // player.c:130 t_pause; menu de pausa
            {
                var tmp = new TITUS_sprite();
                Objects.CopySprite(tmp, level.player.sprite);
                UpdateSprite(level, level.player.sprite, 29, true); // tile de pausa
                while (Raylib.GetKeyPressed() != 0) { } // vacia la cola (incluye la P)
                Input.Consume(KeyboardKey.P); Input.Consume(KeyboardKey.Escape); // la tecla que abrio la pausa no se relee
                if (pauseTex is not Texture2D)
                    pauseTex = Title.LoadFormat2(Menu.MenuPath!); // textura del menu, cargada una vez
                int psel = 0; bool resume = false, exit = false;
                Audio.PauseMusic();
                while (!resume)
                {
                    if (Raylib.WindowShouldClose()) return false;
                    if (Input.Pressed(KeyboardKey.Up)) psel = 0;
                    if (Input.Pressed(KeyboardKey.Down)) psel = 1;
                    if (Input.Pressed(KeyboardKey.P, Input.LevelKeyMs) || Input.Pressed(KeyboardKey.Escape, Input.LevelKeyMs)) resume = true; // = CONTINUE
                    else if (Input.Confirm())
                    { resume = true; exit = psel == 1; }
                    var ptex = pauseTex.Value; int ps = psel;
                    DrawFrame(level, 0, true, 0, 0, 320, 192, Title.FpsGame, () => DrawPauseMenu(ptex, ps)); // ultimo frame + recuadro
                }
                Audio.ResumeMusic(); // CONTINUE sigue donde quedo; en EXIT el menu cambia la pista despues
                while (Raylib.GetKeyPressed() != 0) { }
                Input.Consume(KeyboardKey.P); Input.Consume(KeyboardKey.Escape);
                if (exit) { exitCode = 5; return true; } // EXIT: al menu principal (no cierra)
                Objects.CopySprite(level.player.sprite, tmp);
                last = sw.Elapsed.TotalMilliseconds; acc = 0; logAcc = 0; // sin tiempo acumulado
                continue;
            }
            double now = sw.Elapsed.TotalMilliseconds;
            acc = Math.Min(acc + (now - last), TICK_MS * 4 / Math.Min(1.0, Title.LogicScale)); last = now; // tope: evita espiral tras una pausa larga
            while (acc >= TICK_MS)
            {
            acc -= TICK_MS; logAcc += Title.LogicScale;
            while (logAcc >= 1.0 - 1e-9)
            {
            logAcc -= 1.0;
            drawAlpha = 1.0;
            SnapshotPrev(level);
            SnapshotPrevTiles();
            Player.IMAGE_COUNTER = (ushort)((Player.IMAGE_COUNTER + 1) & 0x0FFF); // engine.c:284
            Objects.MoveElevators(level); // elevators.c MOVE_TRP (engine.c:286)
            Player.MovePlayer(level); // player.c move_player (engine.c:290)
            Objects.Move(level);      // objects.c move_objects
            Enemies.Move(level);      // enemies.c MOVE_NMI
            Enemies.SetNmi(level);    // enemies.c SET_NMI
            Objects.Animate(level);   // sprites.c animate_sprite (jaula, alfombra)
            Enemies.MoveTrash(level);// enemies.c MOVE_TRASH
            if (Gates.CrossingGate(level)) { exitCode = 4; return true; } // engine.c:301
            Scroll.scroll(level);

            if (Player.BAR_FLAG != 0) Player.BAR_FLAG--; // DISPLAY_COUNT: subto0
            Audio.ReturnMusic(); // engine.c:279 reinicia la musica si termino
            // RESET_LEVEL (reset.c:53)
            if (Player.GAMEOVER_FLAG) { DrawFrame(level, 0, true, 0, 0, 320, 192, Title.FpsGame); exitCode = 2; return true; }
            if (Player.RESETLEVEL_FLAG == 1) { DrawFrame(level, 0, true, 0, 0, 320, 192, Title.FpsGame); exitCode = 3; return true; }
            }
            }
            drawAlpha = Math.Clamp(logAcc + acc / TICK_MS * Title.LogicScale, 0.0, 1.0); // progreso hacia el proximo paso de logica
            DrawFrame(level, 0, true, 0, 0, 320, 192, Title.FpsGame); // dibuja siempre (draw.c:319 flip_screen)
        }
    }

    // draw.c:52 TFR_SCREENM: reordena los 4 cuadrantes del tilescreen segun BITMAP_XM/YM
    static void TfrScreenM() => TfrScreenM(screen);

    static void TfrScreenM(byte[] dst)
    {
        int W = Scroll.screen_width * 16, H = Scroll.screen_height * 16;
        int xm = Scroll.BITMAP_XM * 16, ym = Scroll.BITMAP_YM * 16;
        byte[] ts = Scroll.tilescreen;
        for (int y = 0; y < H; y++)
        {
            int sy = (y + ym) % H;
            for (int x = 0; x < W; x++)
                dst[y * W + x] = ts[sy * W + (x + xm) % W];
        }
    }

    // Scroll suave (solo dibujo): capa de tiles del tick anterior + camara previa; desplazamiento sub-tile en px
    static readonly byte[] prevTiles = new byte[Scroll.TILESCREEN_W * Scroll.TILESCREEN_H];
    static int prevBX, prevBY;
    static int camShiftX, camShiftY; // px: (cam actual - cam interpolada) * 16, sumado a la posicion de sprites

    static void SnapshotPrevTiles()
    {
        TfrScreenM(prevTiles);
        prevBX = Scroll.BITMAP_X; prevBY = Scroll.BITMAP_Y;
    }

    // Compone en 'screen' la capa de tiles interpolada: pixel de salida (x,y) -> capa actual (x-sx,y-sy) o, fuera de ella, capa previa
    static void ComposeTiles()
    {
        int cdx = Scroll.BITMAP_X - prevBX, cdy = Scroll.BITMAP_Y - prevBY;
        camShiftX = camShiftY = 0;
        if (drawAlpha >= 1.0 || (cdx == 0 && cdy == 0) || Math.Abs(cdx) > 1 || Math.Abs(cdy) > 1)
        {
            TfrScreenM();
            return;
        }
        int sx = (int)Math.Round(cdx * 16 * (1.0 - drawAlpha));
        int sy = (int)Math.Round(cdy * 16 * (1.0 - drawAlpha));
        camShiftX = sx; camShiftY = sy;
        int W = Scroll.screen_width * 16, H = Scroll.screen_height * 16;
        int xm = Scroll.BITMAP_XM * 16, ym = Scroll.BITMAP_YM * 16;
        byte[] ts = Scroll.tilescreen;
        for (int y = 0; y < H; y++)
        {
            int cy = y - sy, py = cy + cdy * 16;
            for (int x = 0; x < W; x++)
            {
                int cx = x - sx;
                byte c;
                if (cx >= 0 && cx < W && cy >= 0 && cy < H)
                    c = ts[((cy + ym) % H) * W + (cx + xm) % W];
                else
                {
                    int px = cx + cdx * 16;
                    c = (px >= 0 && px < W && py >= 0 && py < H) ? prevTiles[py * W + px] : (byte)0;
                }
                screen[y * W + x] = c;
            }
        }
    }

    // WideScreen: capa de tiles de VIEW_W px leida directo del tilemap (camara interpolada); fuera del mapa = NO_TILE (negro)
    static void ComposeTilesWide(TITUS_level level)
    {
        int cdx = Scroll.BITMAP_X - prevBX, cdy = Scroll.BITMAP_Y - prevBY;
        camShiftX = camShiftY = 0;
        if (!(drawAlpha >= 1.0 || (cdx == 0 && cdy == 0) || Math.Abs(cdx) > 1 || Math.Abs(cdy) > 1))
        {
            camShiftX = (int)Math.Round(cdx * 16 * (1.0 - drawAlpha));
            camShiftY = (int)Math.Round(cdy * 16 * (1.0 - drawAlpha));
        }
        int vw = Scroll.VIEW_W, H = Scroll.TILESCREEN_H;
        int camX = (Scroll.BITMAP_X << 4) - camShiftX - Scroll.MARGIN, camY = (Scroll.BITMAP_Y << 4) - camShiftY;
        for (int y = 0; y < H; y++)
        {
            int wy = camY + y, ty = wy >> 4;
            bool rowOk = ty >= 0 && ty < level.tilemap.Length;
            for (int x = 0; x < vw; x++)
            {
                int wx = camX + x, tx = wx >> 4;
                byte c = Render.NO_TILE;
                if (rowOk && tx >= 0 && tx < level.width && tx < level.tilemap[ty].Length)
                {
                    byte t = level.tilemap[ty][tx];
                    c = level.tile[level.tile[t].animation[Scroll.tile_anim]].tiledata[(wy & 15) * 16 + (wx & 15)];
                }
                screen[y * vw + x] = c;
            }
        }
    }

    // draw.c:~172 display_sprite (sin flip horizontal ni flash todavia)
    static void DisplaySprite(TITUS_level level, TITUS_sprite spr)
    {
        if (!spr.enabled || spr.invisible || spr.spritedata == null) return;
        spr.visible = false;
        int w = Original.SPRITEWIDTH[spr.number], h = Original.SPRITEHEIGHT[spr.number];
        int sw = Scroll.VIEW_W, sh = Scroll.TILESCREEN_H;
        // interpolacion solo de dibujo: prev + (cur - prev) * alpha; saltos grandes (teletransporte) no se interpolan
        int ix = spr.x, iy = spr.y;
        if (drawAlpha < 1.0 && Math.Abs(spr.x - spr.prevX) <= 64 && Math.Abs(spr.y - spr.prevY) <= 64)
        {
            ix = (int)Math.Round(spr.prevX + (spr.x - spr.prevX) * drawAlpha);
            iy = (int)Math.Round(spr.prevY + (spr.y - spr.prevY) * drawAlpha);
        }
        int dx, dy = iy + spr.spritedata.refheight - h + 1 - (Scroll.BITMAP_Y << 4);
        if (!spr.flipped) dx = ix - spr.spritedata.refwidth - (Scroll.BITMAP_X << 4);
        else dx = ix + spr.spritedata.refwidth - w - (Scroll.BITMAP_X << 4);
        dx += camShiftX + Scroll.MARGIN; dy += camShiftY;
        if (dx >= sw || dx + w < 0 || dy + h < 0 || dy >= sh) return;

        byte[] d = spr.spritedata.data!;
        for (int y = 0; y < h; y++)
        {
            int py = dy + y;
            if (py < 0 || py >= sh) continue;
            for (int x = 0; x < w; x++)
            {
                int px = dx + x;
                if (px < 0 || px >= sw) continue;
                byte c = d[y * w + (spr.flipped ? w - 1 - x : x)];
                if (c != 0) screen[py * sw + px] = (byte)(c & 15);
            }
        }
        spr.visible = true;
        spr.flash = false;
    }
}
