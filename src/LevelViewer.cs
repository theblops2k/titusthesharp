using System.Numerics;
using Raylib_cs;

namespace TitusTheFox;

// Visor y editor de niveles. Se abre desde OPTIONS -> LEVEL EDITOR, solo si
// Editor.cfg (junto al exe) contiene MODE=ON.
//
// Interfaz moderna dibujada directamente en la ventana (no en la textura 320x200):
// lista de niveles, paleta de tiles, capas con switches, zoom, minimapa y barra de
// estado. Todo se puede manejar con el mouse; las teclas siguen como atajos.
// Los cambios se guardan en CustomLevels\LEVELx.bin, con el mismo formato que el original.
static class LevelViewer
{
    // Datos del nivel y sprites
    static TITUS_level? level;
    static TITUS_spritedata[]? sprites;
    static TITUS_objectdata[]? objectdata;
    static string viewDir = "";
    static int index = -1;
    static string? currentFile;                       // archivo .bin que se esta editando
    static List<string> customFiles = new();          // solo lo que existe en CustomLevels

    // Editor: se trabaja sobre una copia en memoria del .bin y se guarda al pulsar SAVE.
    static byte[]? sourceData;
    static bool editMode;
    static int editLayer;              // 0..5: tiles, bonuses, enemies, objects, elevators, gates
    static int tool;                   // 0 brush, 1 erase, 2 pan
    static int markerTool;             // 0 none, 1 start, 2 finish
    static int selectedTile = 1;
    static int paletteScroll;
    static bool dirty;
    static string toast = "";
    static long toastUntil;
    static bool editingStroke;
    static bool strokeErase;
    static int lastEditTx = int.MinValue, lastEditTy = int.MinValue;
    static int dragEntLayer = -1, dragEntIndex = -1;
    static int dragGate = -1, dragGateHandle = -1;
    static int selLayer = -1, selIndex = -1;
    static Texture2D tileAtlas;
    static int leftTab;                // 0 levels, 1 tiles

    // Confirmacion de cambios sin guardar
    static int confirm;                // 0 none, 1 exit, 2 load level
    static int pendingLevel = -1;

    // Importar un nivel original a CustomLevels
    static bool importOpen;
    static int importScroll;

    // Crear un nivel vacio usando el tileset de cualquier nivel original
    static bool newOpen;
    static int newTilesetIndex = -1;
    static int newSlotIndex = -1;
    static int newHeight = 40;
    static int newScroll;
    static Texture2D newPreviewTex;
    static int newPreviewIndex = -1;

    // Elegir el enemigo que se coloca con la capa ENEMIES
    static bool enemyOpen;
    static int enemyScroll;
    static EnemyPreset? enemySpawn;
    static List<EnemyPreset>? enemyPresets;
    static EnemyPreset? enemyDefault;

    // Elegir el objeto que se coloca con la capa OBJECTS
    static bool objectOpen;
    static int objectScroll;
    static ObjectPreset? objectSpawn;
    static List<ObjectPreset>? objectPresets;
    static ObjectPreset? objectDefault;

    // Borrar un nivel custom (siempre con confirmacion)
    static bool deleteOpen;
    static string? deleteFile;

    sealed class EnemyPreset
    {
        public byte[] record = new byte[26]; // registro tal cual esta en el nivel original
        public ushort initsprite;
        public ushort type;
        public short power;
        public int initspeedX;
        public int initspeedY;
        public uint rangeX;
        public uint rangeY;
        public uint delay;
        public byte direction;
        public byte walkspeedX;
        public int centerX;
        public string source = "";
        public string key = "";
        public int SpriteNumber => (initsprite & 0x1FFF) + Objects.FIRST_NMI;
        public bool IsBoss => (SpriteNumber >= 248 && SpriteNumber <= 251) ||
                              (SpriteNumber >= 252 && SpriteNumber <= 256) ||
                              (SpriteNumber >= 257 && SpriteNumber <= 261) ||
                              (SpriteNumber >= 263 && SpriteNumber <= 267) ||
                              (SpriteNumber >= 284 && SpriteNumber <= 288) ||
                              (SpriteNumber >= 329 && SpriteNumber <= 332);
        public bool CenterFollowsX => type == 0 || type == 1 || type == 3 || type == 4 || type == 5 || type == 6;

        public void WriteTo(byte[] data, int offset, int x, int y, TITUS_enemy e)
        {
            byte[] rec = (byte[])record.Clone();
            Write16(rec, 0, x);
            Write16(rec, 2, y);
            if (CenterFollowsX) Write16(rec, 15, x);
            Array.Copy(rec, 0, data, offset, 26);

            e.initsprite = initsprite;
            e.initX = x;
            e.initY = y;
            e.init_enabled = true;
            e.type = type;
            e.initspeedX = initspeedX;
            e.initspeedY = initspeedY;
            e.power = power;
            e.centerX = CenterFollowsX ? x : centerX;
            e.rangeX = rangeX;
            e.rangeY = rangeY;
            e.delay = delay;
            e.direction = direction;
            e.walkspeedX = walkspeedX;
            e.dying = 0;
            e.boss = false;
        }
    }

    sealed class ObjectPreset
    {
        public ushort initsprite;
        public string source = "ENGINE";
        public string key = "";
        public int SpriteNumber => initsprite & 0x1FFF;
        public bool Visible => (initsprite & 0x2000) != 0;
        public bool Flash => (initsprite & 0x4000) != 0;
        public bool Flipped => (initsprite & 0x8000) != 0;

        public void WriteTo(byte[] data, int offset, int x, int y, TITUS_object o)
        {
            Write16(data, offset, initsprite);
            Write16(data, offset + 2, x);
            Write16(data, offset + 4, y);

            o.initsprite = initsprite;
            o.init_enabled = true;
            o.initX = x;
            o.initY = y;
            o.mass = 0;
        }
    }

    // Camara y presentacion
    static float camX, camY;
    static int zoom = 2;
    static bool centerPending = true;

    // Capas (teclas 1..8 y G)
    static bool showTiles = true, showBonus = true, showNmi = true, showObjects = true;
    static bool showElevators = true, showGates = true, showSolid, showMinimap = true, showGrid = true;
    static bool help;
    static bool helpJustOpened; // evita que el click de HELP cierre el modal en el mismo frame
    static long helpOpenedAt;
    static bool exiting;
    static bool testPending; // se lanza fuera del frame de dibujo del editor

    // Textura con la porcion visible del nivel (se recompone solo si cambia algo)
    static Texture2D levelTex;
    static Color[] frame = Array.Empty<Color>();
    static int frameW, frameH;
    static bool needCompose = true;
    static int lastAnim = -1, lastCx = int.MinValue, lastCy = int.MinValue;

    // Mouse
    static bool dragging;
    static Vector2 dragMouse;
    static float dragCamX, dragCamY;
    static bool scrubbing;

    // Layout de la pantalla
    static Rectangle rTop, rLeft, rRight, rView, rStatus, rList, rMinimap;
    static Rectangle rPrev, rNext, rCenter, rBack, rHelp, rSave, rViewTab, rEditTab;
    static Rectangle rLevelsTab, rTilesTab, rImport, rNew, rTest;
    static int listScroll;
    static string hoverInfo = "";
    static string statusHint = "";

    // Geometria del minimapa en pixels de pantalla
    static int mapX0, mapY0, mapBlock = 1, mapW, mapH;

    // Devuelve 0 al volver con ESC/BACK, -1 si se cierra la ventana
    public static int Run(string dir)
    {
        if (!EditorCfg.Enabled()) return 0; // seguridad: sin Editor.cfg MODE=ON no se entra
        viewDir = dir;
        Fonts.Load(dir);
        Original.InitOriginal(); // tablas de sprites y animaciones (idempotente)
        sprites ??= Sprites.LoadSprites(File.ReadAllBytes(Path.Combine(dir, "SPREXP.bin")));
        objectdata ??= Game.BuildObjectData();

        RefreshCustomFiles();
        if (customFiles.Count > 0)
        {
            index = Math.Clamp(index, 0, customFiles.Count - 1);
            if (level == null || currentFile == null) LoadLevel(index);
            else if (tileAtlas.Id == 0) BuildTileAtlas();
        }

        EditorUI.Load();
        Menu.WaitRelease(); // la tecla que abrio el visor no se relee
        exiting = false;
        confirm = 0;
        pendingLevel = -1;
        importOpen = false;
        importScroll = 0;
        newOpen = false;
        enemyOpen = false;
        objectOpen = false;
        deleteOpen = false;
        deleteFile = null;
        testPending = false;

        while (true)
        {
            if (Raylib.WindowShouldClose()) return -1;
            Input.UpdatePad();

            Raylib.SetMouseCursor(MouseCursor.Default);
            Layout();
            EditorUI.Locked = help || confirm != 0 || importOpen || newOpen || enemyOpen || objectOpen || deleteOpen;
            statusHint = "";

            Keyboard();

            Raylib.BeginDrawing();
            Raylib.ClearBackground(EditorUI.Bg);

            DrawTopBar();
            if (exiting) { Raylib.EndDrawing(); break; }

            DrawLeftPanel();
            DrawLayersPanel();
            DrawViewport();
            DrawStatusBar();
            if (help) DrawHelpModal();
            if (confirm != 0) DrawConfirmModal();
            if (importOpen) DrawImportModal();
            if (newOpen) DrawNewLevelModal();
            if (enemyOpen) DrawEnemyModal();
            if (objectOpen) DrawObjectModal();
            if (deleteOpen) DrawDeleteModal();

            Raylib.EndDrawing();

            if (testPending)
            {
                testPending = false;
                RunPendingTest();
            }
        }

        Unload();
        return 0;
    }

    static void Layout()
    {
        float s = EditorUI.Scale;
        int sw = Raylib.GetScreenWidth(), sh = Raylib.GetScreenHeight();
        int margin = (int)(12 * s), topH = (int)(54 * s), statusH = (int)(30 * s);
        int leftW = (int)((editMode ? 260 : 186) * s);
        int rightW = (int)(186 * s), gap = (int)(10 * s);

        rTop = new Rectangle(0, 0, sw, topH);
        rStatus = new Rectangle(0, sh - statusH, sw, statusH);
        int bodyH = Math.Max(180, sh - topH - statusH - margin);
        rLeft = new Rectangle(margin, topH, leftW, bodyH);
        rRight = new Rectangle(sw - rightW - margin, topH, rightW, bodyH);

        float vx = rLeft.X + rLeft.Width + gap;
        float vw = Math.Max(120, rRight.X - gap - vx);
        rView = new Rectangle(vx, topH, vw, bodyH);

        float listTop = rLeft.Y + (editMode ? 44 : 34) * s;
        rImport = new Rectangle(rLeft.X + 8 * s, rLeft.Y + rLeft.Height - 40 * s,
            rLeft.Width - 16 * s, 30 * s);
        rNew = new Rectangle(rImport.X, rImport.Y - 36 * s, rImport.Width, 30 * s);
        rTest = new Rectangle(rNew.X, rNew.Y - 36 * s, rNew.Width, 30 * s);
        rList = new Rectangle(rLeft.X + 8 * s, listTop, rLeft.Width - 16 * s,
            Math.Max(40 * s, rTest.Y - 8 * s - listTop));

        float selX = 220 * s;
        rPrev = new Rectangle(selX, 12 * s, 34 * s, 30 * s);
        rNext = new Rectangle(selX + 230 * s, 12 * s, 34 * s, 30 * s);
        rBack = new Rectangle(sw - margin - 72 * s, 12 * s, 72 * s, 30 * s);
        rHelp = new Rectangle(rBack.X - 8 * s - 64 * s, 12 * s, 64 * s, 30 * s);
        rCenter = new Rectangle(rHelp.X - 8 * s - 78 * s, 12 * s, 78 * s, 30 * s);
        rSave = new Rectangle(rCenter.X - 8 * s - 64 * s, 12 * s, 64 * s, 30 * s);
        rViewTab = new Rectangle(selX + 272 * s, 12 * s, 62 * s, 30 * s);
        rEditTab = new Rectangle(rViewTab.X + 68 * s, 12 * s, 62 * s, 30 * s);

        float tabGap = 8 * s;
        float tabW = (rLeft.Width - 16 * s - tabGap) / 2f;
        rLevelsTab = new Rectangle(rLeft.X + 8 * s, rLeft.Y + 8 * s, tabW, 28 * s);
        rTilesTab = new Rectangle(rLevelsTab.X + tabW + tabGap, rLeft.Y + 8 * s, tabW, 28 * s);

        if (level != null)
        {
            int mmw = Math.Min((int)(200 * s), Math.Max((int)(120 * s), (int)rView.Width - (int)(28 * s)));
            int mmh = Math.Min((int)(128 * s), Math.Max((int)(84 * s), (int)rView.Height - (int)(28 * s)));
            rMinimap = new Rectangle(rView.X + rView.Width - 12 * s - mmw, rView.Y + 12 * s, mmw, mmh);

            if (centerPending) { CenterOnStart(); centerPending = false; }
            ClampCam();
        }
    }

    // ------------------------------------------------------------------ teclado
    static void Keyboard()
    {
        if (confirm != 0)
        {
            if (Input.Pressed(KeyboardKey.Escape)) { confirm = 0; pendingLevel = -1; }
            return;
        }
        if (help)
        {
            if (Input.Pressed(KeyboardKey.Escape) || Input.Pressed(KeyboardKey.F1)) { help = false; helpJustOpened = false; }
            return;
        }
        if (importOpen)
        {
            if (Input.Pressed(KeyboardKey.Escape)) importOpen = false;
            return;
        }
        if (newOpen)
        {
            if (Input.Pressed(KeyboardKey.Escape)) newOpen = false;
            return;
        }
        if (enemyOpen)
        {
            if (Input.Pressed(KeyboardKey.Escape)) enemyOpen = false;
            return;
        }
        if (objectOpen)
        {
            if (Input.Pressed(KeyboardKey.Escape)) objectOpen = false;
            return;
        }
        if (deleteOpen)
        {
            if (Input.Pressed(KeyboardKey.Escape)) { deleteOpen = false; deleteFile = null; }
            return;
        }

        bool ctrl = Raylib.IsKeyDown(KeyboardKey.LeftControl) || Raylib.IsKeyDown(KeyboardKey.RightControl);
        if (ctrl && Input.Pressed(KeyboardKey.S) && editMode) { SaveCustom(); return; }

        if (Input.Pressed(KeyboardKey.Escape) || Input.PadEscape()) { RequestExit(); return; }
        if (Input.Pressed(KeyboardKey.F1)) { help = true; helpJustOpened = true; helpOpenedAt = Environment.TickCount64; return; }
        if (Input.Pressed(KeyboardKey.F5)) { TestLevel(); return; }
        if (Input.Pressed(KeyboardKey.I)) { OpenImport(); return; }
        if (Input.Pressed(KeyboardKey.N)) { OpenNewLevel(); return; }
        if (Input.Pressed(KeyboardKey.Delete) && currentFile != null)
        {
            deleteOpen = true;
            deleteFile = currentFile;
            return;
        }
        if (Input.Pressed(KeyboardKey.Tab)) { SetEditMode(!editMode); return; }

        int n = customFiles.Count;
        if (n > 0 && (Input.Pressed(KeyboardKey.PageUp) || Input.PadPrev())) RequestLoad((index - 1 + n) % n);
        if (n > 0 && (Input.Pressed(KeyboardKey.PageDown) || Input.PadNext())) RequestLoad((index + 1) % n);
        if (Input.Pressed(KeyboardKey.Home)) CenterOnStart();

        if (Input.Pressed(KeyboardKey.One)) ToggleLayer(0);
        if (Input.Pressed(KeyboardKey.Two)) ToggleLayer(1);
        if (Input.Pressed(KeyboardKey.Three)) ToggleLayer(2);
        if (Input.Pressed(KeyboardKey.Four)) ToggleLayer(3);
        if (Input.Pressed(KeyboardKey.Five)) ToggleLayer(4);
        if (Input.Pressed(KeyboardKey.Six)) ToggleLayer(5);
        if (Input.Pressed(KeyboardKey.Seven)) ToggleLayer(6);
        if (Input.Pressed(KeyboardKey.Eight)) ToggleLayer(7);
        if (Input.Pressed(KeyboardKey.G)) ToggleLayer(8);

        if (level == null) return;
        bool fast = Raylib.IsKeyDown(KeyboardKey.LeftShift) || Raylib.IsKeyDown(KeyboardKey.RightShift);
        float step = (fast ? 1000f : 430f) * Raylib.GetFrameTime() / zoom;
        float ox = camX, oy = camY;
        if (Raylib.IsKeyDown(KeyboardKey.Left) || Raylib.IsKeyDown(KeyboardKey.A)) camX -= step;
        if (Raylib.IsKeyDown(KeyboardKey.Right) || Raylib.IsKeyDown(KeyboardKey.D)) camX += step;
        if (Raylib.IsKeyDown(KeyboardKey.Up) || Raylib.IsKeyDown(KeyboardKey.W)) camY -= step;
        if (Raylib.IsKeyDown(KeyboardKey.Down) || Raylib.IsKeyDown(KeyboardKey.S)) camY += step;

        if (Input.PadLeft()) camX -= 16;
        if (Input.PadRight()) camX += 16;
        if (Input.PadUp()) camY -= 16;
        if (Input.PadDown()) camY += 16;

        if (ox != camX || oy != camY) { ClampCam(); needCompose = true; }
    }

    static void ToggleLayer(int i)
    {
        switch (i)
        {
            case 0: showTiles = !showTiles; needCompose = true; break;
            case 1: showBonus = !showBonus; break;
            case 2: showNmi = !showNmi; needCompose = true; break;
            case 3: showObjects = !showObjects; needCompose = true; break;
            case 4: showElevators = !showElevators; needCompose = true; break;
            case 5: showGates = !showGates; break;
            case 6: showSolid = !showSolid; break;
            case 7: showMinimap = !showMinimap; break;
            case 8: showGrid = !showGrid; break;
        }
    }

    static bool LayerOn(int i) => i switch
    {
        0 => showTiles, 1 => showBonus, 2 => showNmi, 3 => showObjects,
        4 => showElevators, 5 => showGates, 6 => showSolid, 7 => showMinimap,
        _ => showGrid,
    };

    static void SetEditMode(bool on)
    {
        if (editMode == on) return;
        editMode = on;
        EndEdit();
        markerTool = 0;
        if (on)
        {
            leftTab = 1;
            if (editLayer < 0 || editLayer > 5) editLayer = 0;
        }
        else leftTab = 0;
        needCompose = true;
    }

    static void SetTool(int t)
    {
        tool = t;
        markerTool = 0;
        EndEdit();
    }

    static void SetMarkerTool(int m)
    {
        markerTool = markerTool == m ? 0 : m;
        if (markerTool != 0) tool = 0;
        EndEdit();
        if (markerTool != 0) selLayer = selIndex = -1;
    }

    static string LayerName(int i) => i switch
    {
        0 => "TILES", 1 => "BONUSES", 2 => "ENEMIES", 3 => "OBJECTS",
        4 => "ELEVATORS", 5 => "GATES", _ => "TILES",
    };

    static string ToolName() => markerTool switch
    {
        1 => "SET START", 2 => "SET FINISH",
        _ => tool switch { 1 => "ERASE", 2 => "PAN", _ => "BRUSH" },
    };

    // --------------------------------------------------------------- carga nivel
    static void RefreshCustomFiles()
    {
        customFiles = EditorCfg.CustomLevelFiles();
        if (customFiles.Count == 0)
        {
            ClearLevel();
            return;
        }

        int i = currentFile == null ? -1 : customFiles.FindIndex(f =>
            f.Equals(currentFile, StringComparison.OrdinalIgnoreCase));
        if (i < 0)
        {
            // El nivel abierto ya no esta en CustomLevels: no mostrar datos viejos.
            ClearLevel();
            index = 0;
            return;
        }
        index = i;
    }

    static void ClearLevel()
    {
        level = null;
        sourceData = null;
        currentFile = null;
        index = -1;
        dirty = false;
        leftTab = 0;
        EndEdit();
        selLayer = selIndex = -1;
        if (levelTex.Id != 0)
        {
            Raylib.UnloadTexture(levelTex);
            levelTex = default;
            frameW = frameH = 0;
            frame = Array.Empty<Color>();
        }
        needCompose = true;
    }

    static void LoadLevel(int i)
    {
        if (sprites == null || objectdata == null) return;
        if (i < 0 || i >= customFiles.Count) return;
        string file = customFiles[i];
        byte[] data;
        try
        {
            data = File.ReadAllBytes(EditorCfg.CustomLevelPath(file));
        }
        catch (Exception ex)
        {
            ShowToast("CANNOT READ " + file + ": " + ex.Message);
            RefreshCustomFiles();
            return;
        }

        var lv = new TITUS_level { levelnumber = (short)i, lives = 1, extrabonus = 0 };
        lv.levelid = EditorCfg.LevelIdForFile(file);
        sourceData = data;
        Level.LoadLevel(lv, data, sprites, objectdata);
        // La paleta del nivel depende de su identidad original, no de la posicion
        // que ocupa dentro de la campaña custom.
        int paletteLevel = EditorCfg.KnownLevelIndex(file);
        if (paletteLevel >= 0 && paletteLevel < OriginalTables.PaletteLevelColour.GetLength(0))
            for (int k = 0; k < 3; k++)
                lv.palette[14, k] = OriginalTables.PaletteLevelColour[paletteLevel, k];

        // Sprites en su posicion inicial, como al empezar el nivel
        Objects.InitElevators(lv);
        Objects.Init(lv);
        Enemies.Init(lv);
        var p = lv.player;
        p.sprite.x = p.initX;
        p.sprite.y = p.initY;
        Game.UpdateSprite(lv, p.sprite, 0, true);

        level = lv;
        index = i;
        currentFile = file;
        dirty = false;
        EndEdit();
        selLayer = selIndex = -1;
        int rowH = (int)(24 * EditorUI.Scale);
        listScroll = Math.Clamp(index * rowH - (int)rList.Height / 2, 0, Math.Max(0, customFiles.Count * rowH - (int)rList.Height));
        if (rView.Width > 0) CenterOnStart(); else centerPending = true;
        BuildTileAtlas();
        needCompose = true;
    }

    static void RequestLoad(int i)
    {
        if (i < 0 || i >= customFiles.Count) return;
        if (i == index) return;
        if (dirty)
        {
            pendingLevel = i;
            confirm = 2;
            return;
        }
        LoadLevel(i);
    }

    static void RequestExit()
    {
        if (dirty)
        {
            pendingLevel = -1;
            confirm = 1;
            return;
        }
        exiting = true;
    }

    static void CompleteConfirm()
    {
        int action = confirm;
        int target = pendingLevel;
        confirm = 0;
        pendingLevel = -1;
        if (action == 1) exiting = true;
        else if (action == 2 && target >= 0) LoadLevel(target);
    }

    static void CenterOnStart()
    {
        if (level == null) return;
        camX = level.player.initX - rView.Width / (2f * zoom);
        camY = level.player.initY - rView.Height / (2f * zoom);
        ClampCam();
        needCompose = true;
    }

    static void ClampCam()
    {
        if (level == null) return;
        camX = Math.Clamp(camX, 0, Math.Max(0, level.width * 16 - rView.Width / zoom));
        camY = Math.Clamp(camY, 0, Math.Max(0, level.height * 16 - rView.Height / zoom));
    }

    static void SetZoom(int z)
    {
        ZoomAt(new Vector2(rView.X + rView.Width / 2f, rView.Y + rView.Height / 2f), z);
    }

    static void ZoomAt(Vector2 mouse, int newZoom)
    {
        newZoom = Math.Clamp(newZoom, 1, 4);
        if (newZoom == zoom) return;
        float wx = camX + (mouse.X - rView.X) / zoom;
        float wy = camY + (mouse.Y - rView.Y) / zoom;
        zoom = newZoom;
        camX = wx - (mouse.X - rView.X) / zoom;
        camY = wy - (mouse.Y - rView.Y) / zoom;
        ClampCam();
        needCompose = true;
    }

    // ---------------------------------------------------------------- barra superior
    static void DrawTopBar()
    {
        float s = EditorUI.Scale;
        int n = customFiles.Count;
        Raylib.DrawRectangle(0, 0, Raylib.GetScreenWidth(), (int)rTop.Height, new Color(20, 22, 27, 255));
        Raylib.DrawRectangle(0, (int)rTop.Height - 1, Raylib.GetScreenWidth(), 1, EditorUI.Line);

        EditorUI.Text("LEVEL EDITOR", 16 * s, 8 * s, 20, EditorUI.Fg, true);
        EditorUI.Text("Titus the Fox - level tools", 16 * s, 31 * s, 12, EditorUI.Muted);

        if (n > 0 && EditorUI.Button(rPrev, "<", false, 18)) RequestLoad((index - 1 + n) % n);
        EditorUI.Text($"{index + 1:00} / {n:00}", rPrev.X + 44 * s, 9 * s, 11, EditorUI.Muted, true);
        string file = n > 0 ? customFiles[index] : "NO CUSTOM LEVELS";
        EditorUI.Text(file + (dirty ? " *" : ""), rPrev.X + 44 * s, 25 * s, 17, dirty ? EditorUI.Accent : EditorUI.Fg, true);
        if (n > 0 && EditorUI.Button(rNext, ">", false, 18)) RequestLoad((index + 1) % n);

        if (EditorUI.Button(rViewTab, "VIEW", !editMode, 12)) SetEditMode(false);
        if (EditorUI.Button(rEditTab, "EDIT", editMode, 12)) SetEditMode(true);

        if (editMode && EditorUI.Button(rSave, dirty ? "SAVE *" : "SAVE", dirty, 12)) SaveCustom();
        if (EditorUI.Button(rCenter, "CENTER", false, 13)) CenterOnStart();
        if (EditorUI.Button(rHelp, "HELP", false, 13)) { help = true; helpJustOpened = true; helpOpenedAt = Environment.TickCount64; }
        if (EditorUI.Button(rBack, "BACK", false, 13)) RequestExit();
    }

    // ------------------------------------------------------------------ panel izquierdo
    static void DrawLeftPanel()
    {
        if (!editMode)
        {
            EditorUI.Panel(rLeft, "LEVELS");
            DrawLevelList();
        }
        else
        {
            EditorUI.Panel(rLeft);
            if (EditorUI.Button(rLevelsTab, "LEVELS", leftTab == 0, 11)) leftTab = 0;
            if (EditorUI.Button(rTilesTab, "TILES", leftTab == 1, 11)) leftTab = 1;
            if (leftTab == 1) DrawTilePalette();
            else DrawLevelList();
        }

        if (EditorUI.Button(rTest, "TEST LEVEL", level != null, 12)) TestLevel();
        if (EditorUI.Button(rNew, "NEW LEVEL", false, 12)) OpenNewLevel();
        if (EditorUI.Button(rImport, "IMPORT LEVEL", customFiles.Count == 0, 12)) OpenImport();
    }

    static void DrawLevelList()
    {
        float s = EditorUI.Scale;
        var mouse = Raylib.GetMousePosition();

        int rows = customFiles.Count;
        int rowH = (int)(24 * s);
        int contentH = rows * rowH;
        int maxScroll = Math.Max(0, contentH - (int)rList.Height);
        if (EditorUI.Hover(rList))
        {
            float wheel = Raylib.GetMouseWheelMove();
            if (wheel != 0) listScroll = Math.Clamp(listScroll - (int)(wheel * 48 * s), 0, maxScroll);
        }
        listScroll = Math.Clamp(listScroll, 0, maxScroll);

        if (rows == 0)
        {
            float cy = rList.Y + Math.Min(rList.Height * 0.34f, 110 * s);
            EditorUI.TextCentered("NO CUSTOM LEVELS", new Rectangle(rList.X, cy, rList.Width, 24 * s), 16, EditorUI.Fg, true);
            EditorUI.TextCentered("Use NEW LEVEL or IMPORT LEVEL", new Rectangle(rList.X, cy + 30 * s, rList.Width, 20 * s), 11, EditorUI.Muted);
            return;
        }

        Raylib.BeginScissorMode((int)rList.X, (int)rList.Y, (int)rList.Width, (int)rList.Height);
        for (int i = 0; i < rows; i++)
        {
            var row = new Rectangle(rList.X, rList.Y + i * rowH - listScroll, rList.Width, rowH);
            if (row.Y + row.Height < rList.Y || row.Y > rList.Y + rList.Height) continue;

            bool hover = !EditorUI.Locked && Raylib.CheckCollisionPointRec(mouse, row);
            bool active = i == index;
            if (active) Raylib.DrawRectangleRounded(row, 0.3f, 6, EditorUI.AccentSoft);
            else if (hover) Raylib.DrawRectangleRounded(row, 0.3f, 6, EditorUI.Panel2);
            if (active) Raylib.DrawRectangle((int)row.X, (int)(row.Y + 4 * s), (int)(3 * s), (int)(row.Height - 8 * s), EditorUI.Accent);

            string file = customFiles[i];
            EditorUI.Text($"{i + 1:00}", row.X + 12 * s, row.Y + 6 * s, 11, active ? EditorUI.Accent : EditorUI.Muted, true);
            EditorUI.Text(file, row.X + 40 * s, row.Y + 4 * s, 14, EditorUI.Fg);

            string tag = file == "LEVELG" ? "END" : file == "LEVELD" ? "EXTRA" : "CUSTOM";
            EditorUI.TextRight(tag, row.X + row.Width - 28 * s, row.Y + 7 * s, 9,
                tag == "CUSTOM" ? EditorUI.Accent : EditorUI.Muted, tag == "CUSTOM");

            var del = new Rectangle(row.X + row.Width - 22 * s, row.Y + 3 * s, 18 * s, row.Height - 6 * s);
            bool overDel = hover && Raylib.CheckCollisionPointRec(mouse, del);
            if (overDel) Raylib.DrawRectangleRounded(del, 0.3f, 4, new Color(80, 35, 34, 255));
            EditorUI.TextCentered("x", del, 11, overDel ? EditorUI.Danger : EditorUI.Muted, true);

            if (hover && !overDel) Raylib.SetMouseCursor(MouseCursor.PointingHand);
            if (overDel) Raylib.SetMouseCursor(MouseCursor.PointingHand);
            if (overDel) statusHint = "Remove " + file + " from CustomLevels";
            if (hover && Raylib.IsMouseButtonPressed(MouseButton.Left))
            {
                if (overDel)
                {
                    deleteOpen = true;
                    deleteFile = file;
                }
                else RequestLoad(i);
            }
        }
        Raylib.EndScissorMode();
    }

    static void DrawTilePalette()
    {
        if (level == null) return;
        float s = EditorUI.Scale;
        int cols = 12;
        float pad = 5 * s;
        float cell = (rList.Width - pad * 2) / cols;
        int rows = (256 + cols - 1) / cols;
        float headH = 28 * s;
        float gridY = rList.Y + headH;
        float gridH = Math.Max(20 * s, rList.Height - headH);

        int maxScroll = Math.Max(0, (int)Math.Ceiling(rows * cell - gridH));
        if (!EditorUI.Locked && EditorUI.Hover(rList))
        {
            float wheel = Raylib.GetMouseWheelMove();
            if (wheel != 0) paletteScroll = Math.Clamp(paletteScroll - (int)(wheel * 60 * s), 0, maxScroll);
        }
        paletteScroll = Math.Clamp(paletteScroll, 0, maxScroll);

        // Cabecera: preview y datos del tile elegido
        var t = level.tile[selectedTile];
        var prev = new Rectangle(rList.X, rList.Y, 22 * s, 22 * s);
        Raylib.DrawRectangleRec(prev, EditorUI.Panel2);
        if (tileAtlas.Id != 0)
            Raylib.DrawTexturePro(tileAtlas,
                new Rectangle((selectedTile % 16) * 16, (selectedTile / 16) * 16, 16, 16),
                new Rectangle(prev.X + 2 * s, prev.Y + 2 * s, prev.Width - 4 * s, prev.Height - 4 * s),
                Vector2.Zero, 0f, Color.White);
        Raylib.DrawRectangleLinesEx(prev, Math.Max(1f, s), EditorUI.Accent);
        EditorUI.Text($"TILE {selectedTile:000}", rList.X + 30 * s, rList.Y + 2 * s, 11, EditorUI.Fg, true);
        EditorUI.Text(TileFlags(selectedTile), rList.X + 30 * s, rList.Y + 14 * s, 9, EditorUI.Muted);
        EditorUI.TextRight("ALL 256", rList.X + rList.Width - 2 * s, rList.Y + 2 * s, 9, EditorUI.Muted, true);

        Raylib.BeginScissorMode((int)rList.X, (int)gridY, (int)rList.Width, (int)gridH);
        var mouse = Raylib.GetMousePosition();
        for (int i = 0; i < 256; i++)
        {
            float x = rList.X + pad + (i % cols) * cell;
            float y = gridY + (i / cols) * cell - paletteScroll;
            if (y + cell < gridY || y > gridY + gridH) continue;

            var cellR = new Rectangle(x, y, cell, cell);
            bool selected = i == selectedTile;
            bool hover = !EditorUI.Locked && Raylib.CheckCollisionPointRec(mouse, cellR);
            var bg = selected ? EditorUI.AccentSoft : hover ? EditorUI.Panel2 : new Color(25, 28, 34, 255);
            Raylib.DrawRectangleRounded(cellR, 0.18f, 4, bg);

            float inset = Math.Max(1.5f, 2 * s);
            var dest = new Rectangle(x + inset, y + inset, cell - inset * 2, cell - inset * 2);
            if (tileAtlas.Id != 0)
                Raylib.DrawTexturePro(tileAtlas,
                    new Rectangle((i % 16) * 16, (i / 16) * 16, 16, 16),
                    dest, Vector2.Zero, 0f, Color.White);
            if (selected)
                Raylib.DrawRectangleLinesEx(cellR, Math.Max(1f, 1.6f * s), EditorUI.Accent);
            else if (hover)
                Raylib.DrawRectangleLinesEx(cellR, Math.Max(1f, s), new Color(255, 255, 255, 90));

            if (hover)
            {
                Raylib.SetMouseCursor(MouseCursor.PointingHand);
                statusHint = $"TILE {i:000}: {TileFlags(i)} - click to paint with it";
                if (Raylib.IsMouseButtonPressed(MouseButton.Left))
                {
                    selectedTile = i;
                    editLayer = 0;
                    markerTool = 0;
                    tool = 0;
                    selLayer = selIndex = -1;
                    if (!showTiles) ToggleLayer(0);
                }
            }
        }
        Raylib.EndScissorMode();
    }

    static string TileFlags(int tileIndex)
    {
        if (level == null) return "EMPTY";
        var tile = level.tile[tileIndex];
        string flags = "";
        if (tile.horizflag == 1) flags += "WALL ";
        else if (tile.horizflag == 3) flags += "DEADLY ";
        else if (tile.horizflag == 5) flags += "LOCK ";
        if (tile.floorflag == 1) flags += "FLOOR ";
        else if (tile.floorflag == 6) flags += "LADDER ";
        else if (tile.floorflag == 8) flags += "WATER ";
        else if (tile.floorflag == 9) flags += "FIRE ";
        if (tile.ceilflag != 0) flags += "CEILING ";
        return flags.Length == 0 ? "EMPTY" : flags.TrimEnd();
    }

    static void BuildTileAtlas()
    {
        if (tileAtlas.Id != 0) Raylib.UnloadTexture(tileAtlas);
        tileAtlas = default;
        if (level == null) return;

        Image img = Raylib.GenImageColor(256, 256, new Color(0, 0, 0, 255));
        for (int i = 0; i < 256; i++)
        {
            var tile = level.tile[i];
            int ox = (i % 16) * 16, oy = (i / 16) * 16;
            for (int y = 0; y < 16; y++)
                for (int x = 0; x < 16; x++)
                {
                    byte c = tile.tiledata[y * 16 + x];
                    var col = new Color(level.palette[c, 0], level.palette[c, 1], level.palette[c, 2], (byte)255);
                    Raylib.ImageDrawPixel(ref img, ox + x, oy + y, col);
                }
        }
        tileAtlas = Raylib.LoadTextureFromImage(img);
        Raylib.UnloadImage(img);
        Raylib.SetTextureFilter(tileAtlas, TextureFilter.Point);
    }

    // ------------------------------------------------------------------ panel capas
    static void DrawLayersPanel()
    {
        float s = EditorUI.Scale;
        EditorUI.Panel(rRight, "LAYERS");

        string[] names = { "TILES", "BONUSES", "ENEMIES", "OBJECTS", "ELEVATORS", "GATES", "SOLIDITY", "MINIMAP", "GRID" };
        string[] keys = { "1", "2", "3", "4", "5", "6", "7", "8", "G" };
        string[] desc =
        {
            "Show level tiles", "Bonus cells", "Enemy spawns", "Object spawns",
            "Elevator platforms", "Gate entrances and exits", "Wall / floor / ladder flags",
            "Level overview map", "Tile grid overlay"
        };
        Color[] dots =
        {
            new(225, 228, 234, 255), new(255, 212, 70, 255), new(240, 102, 92, 255),
            new(92, 162, 255, 255), new(92, 220, 142, 255), new(230, 112, 230, 255),
            new(255, 152, 62, 255), new(86, 220, 255, 255), new(158, 168, 184, 255)
        };

        int y0 = (int)(rRight.Y + 34 * s);
        int step = (int)(29 * s), rowH = (int)(27 * s);
        for (int i = 0; i < names.Length; i++)
        {
            var row = new Rectangle(rRight.X + 6 * s, y0 + i * step, rRight.Width - 12 * s, rowH);
            bool selected = editMode && markerTool == 0 && i < 6 && i == editLayer;
            bool clicked = EditorUI.Toggle(row, names[i], LayerOn(i), keys[i], dots[i], selected, out bool switchHit);
            if (clicked)
            {
                if (!editMode || i >= 6 || switchHit) ToggleLayer(i);
                else
                {
                    editLayer = i;
                    markerTool = 0;
                    selLayer = selIndex = -1;
                    if (!LayerOn(i)) ToggleLayer(i);
                }
            }
            if (!EditorUI.Locked && EditorUI.Hover(row))
                statusHint = editMode && i < 6 ? desc[i] + " - click to edit this layer" : desc[i];
        }

        int y = y0 + names.Length * step + (int)(4 * s);
        Raylib.DrawRectangle((int)(rRight.X + 14 * s), y, (int)(rRight.Width - 28 * s), 1, EditorUI.Line);

        EditorUI.Text("ZOOM", rRight.X + 14 * s, y + 10 * s, 11, EditorUI.Muted, true);
        var bMinus = new Rectangle(rRight.X + 14 * s, y + 26 * s, 32 * s, 26 * s);
        var bPlus = new Rectangle(rRight.X + rRight.Width - 46 * s, y + 26 * s, 32 * s, 26 * s);
        if (EditorUI.Button(bMinus, "-", false, 16)) SetZoom(zoom - 1);
        EditorUI.TextCentered($"{zoom}X", new Rectangle(rRight.X + 46 * s, y + 26 * s, rRight.Width - 92 * s, 26 * s), 14, EditorUI.Fg, true);
        if (EditorUI.Button(bPlus, "+", false, 16)) SetZoom(zoom + 1);

        if (!editMode)
        {
            var bCenter = new Rectangle(rRight.X + 14 * s, y + 58 * s, rRight.Width - 28 * s, 28 * s);
            if (EditorUI.Button(bCenter, "CENTER ON START", false, 11)) CenterOnStart();
            return;
        }

        int ty = y + (int)(56 * s);
        Raylib.DrawRectangle((int)(rRight.X + 14 * s), ty, (int)(rRight.Width - 28 * s), 1, EditorUI.Line);
        EditorUI.Text("TOOL", rRight.X + 14 * s, ty + 8 * s, 10, EditorUI.Muted, true);

        float gap = 6 * s;
        float bw = (rRight.Width - 28 * s - gap * 2) / 3f;
        float by = ty + 24 * s;
        var bBrush = new Rectangle(rRight.X + 14 * s, by, bw, 25 * s);
        var bErase = new Rectangle(bBrush.X + bw + gap, by, bw, 25 * s);
        var bPan = new Rectangle(bErase.X + bw + gap, by, bw, 25 * s);
        if (EditorUI.Button(bBrush, "BRUSH", markerTool == 0 && tool == 0, 9)) SetTool(0);
        if (EditorUI.Button(bErase, "ERASE", markerTool == 0 && tool == 1, 9)) SetTool(1);
        if (EditorUI.Button(bPan, "PAN", markerTool == 0 && tool == 2, 9)) SetTool(2);

        float my = by + 31 * s;
        float mw = (rRight.Width - 28 * s - gap) / 2f;
        var bStart = new Rectangle(rRight.X + 14 * s, my, mw, 25 * s);
        var bFinish = new Rectangle(bStart.X + mw + gap, my, mw, 25 * s);
        if (EditorUI.Button(bStart, "SET START", markerTool == 1, 8)) SetMarkerTool(1);
        if (EditorUI.Button(bFinish, "SET FINISH", markerTool == 2, 8)) SetMarkerTool(2);

        if (editLayer == 2 || editLayer == 3)
        {
            bool enemyLayer = editLayer == 2;
            var presetSprite = enemyLayer ? EnemySpawnPreset().SpriteNumber : ObjectSpawnPreset().SpriteNumber;
            string presetTitle = enemyLayer ? "ENEMY TO ADD" : "OBJECT TO ADD";
            string presetLabel = enemyLayer ? EnemyShortLabel(EnemySpawnPreset()) : ObjectShortLabel(ObjectSpawnPreset());

            float ey = my + 31 * s;
            Raylib.DrawRectangle((int)(rRight.X + 14 * s), (int)ey, (int)(rRight.Width - 28 * s), 1, EditorUI.Line);
            ey += 6 * s;
            var prev = new Rectangle(rRight.X + 14 * s, ey, 24 * s, 24 * s);
            Raylib.DrawRectangleRec(prev, EditorUI.Panel2);
            DrawSpritePreview(presetSprite, new Rectangle(prev.X + 2 * s, prev.Y + 2 * s, prev.Width - 4 * s, prev.Height - 4 * s));
            Raylib.DrawRectangleLinesEx(prev, Math.Max(1f, s), EditorUI.Line);
            EditorUI.Text(presetTitle, rRight.X + 44 * s, ey + 1 * s, 9, EditorUI.Muted, true);
            EditorUI.Text(presetLabel, rRight.X + 44 * s, ey + 14 * s, 10, EditorUI.Fg);
            var bChange = new Rectangle(rRight.X + rRight.Width - 72 * s, ey, 58 * s, 24 * s);
            if (EditorUI.Button(bChange, "CHANGE", false, 9))
            {
                if (enemyLayer) OpenEnemyPicker();
                else OpenObjectPicker();
            }
        }
    }

    // ------------------------------------------------------------------ visor
    static void DrawViewport()
    {
        if (level == null)
        {
            Raylib.DrawRectangleRec(rView, new Color(9, 10, 13, 255));
            Raylib.DrawRectangleLinesEx(rView, Math.Max(1f, EditorUI.Scale), new Color(255, 255, 255, 26));
            float s = EditorUI.Scale;
            float emptyY = rView.Y + rView.Height * 0.36f;
            EditorUI.TextCentered("NO CUSTOM LEVELS", new Rectangle(rView.X, emptyY, rView.Width, 28 * s), 20, EditorUI.Fg, true);
            EditorUI.TextCentered("Create one with NEW LEVEL or import an original",
                new Rectangle(rView.X, emptyY + 42 * s, rView.Width, 20 * s), 12, EditorUI.Muted);
            return;
        }
        MinimapGeometry();
        var mouse = Raylib.GetMousePosition();
        bool overMini = showMinimap && !EditorUI.Locked && Raylib.CheckCollisionPointRec(mouse, rMinimap);
        bool overView = !EditorUI.Locked && Raylib.CheckCollisionPointRec(mouse, rView);
        bool overCanvas = overView && !overMini;

        if (!EditorUI.Locked)
        {
            // Minimapa: click y arrastre para saltar por el nivel
            if (overMini && Raylib.IsMouseButtonPressed(MouseButton.Left))
            {
                scrubbing = true;
                ScrubTo(mouse);
            }
            else if (scrubbing)
            {
                if (Raylib.IsMouseButtonDown(MouseButton.Left)) ScrubTo(mouse);
                else scrubbing = false;
            }

            bool space = Raylib.IsKeyDown(KeyboardKey.Space);
            bool middlePan = overView && Raylib.IsMouseButtonPressed(MouseButton.Middle);
            bool leftPan = overCanvas &&
                (Raylib.IsMouseButtonPressed(MouseButton.Left) &&
                 (!editMode || tool == 2 || space));
            if (middlePan || leftPan) BeginPan(mouse);

            if (overCanvas && editMode && tool != 2 && !space)
            {
                if (Raylib.IsMouseButtonPressed(MouseButton.Left)) BeginEdit(mouse, false);
                else if (Raylib.IsMouseButtonPressed(MouseButton.Right)) BeginEdit(mouse, true);
            }

            if (dragging) UpdatePan(mouse);
            if (editingStroke)
            {
                if (Raylib.IsMouseButtonDown(MouseButton.Left) || Raylib.IsMouseButtonDown(MouseButton.Right))
                    EditAt(mouse, strokeErase, false);
                else EndEdit();
            }

            if (overCanvas)
            {
                float wheel = Raylib.GetMouseWheelMove();
                if (wheel != 0) ZoomAt(mouse, zoom + (wheel > 0 ? 1 : -1));
            }

            if (overMini) Raylib.SetMouseCursor(MouseCursor.Crosshair);
            else if (overView)
                Raylib.SetMouseCursor(dragging || !editMode || (editMode && tool == 2)
                    ? MouseCursor.ResizeAll : MouseCursor.Crosshair);
        }

        int cx = (int)Math.Round(camX), cy = (int)Math.Round(camY);
        ComposeIfNeeded(cx, cy, (int)((Environment.TickCount64 / 150) % 3));

        Raylib.BeginScissorMode((int)rView.X, (int)rView.Y, (int)rView.Width, (int)rView.Height);
        Raylib.DrawRectangleRec(rView, new Color(9, 10, 13, 255));
        Raylib.DrawTexturePro(levelTex, new Rectangle(0, 0, frameW, frameH),
            new Rectangle(rView.X, rView.Y, frameW * zoom, frameH * zoom), Vector2.Zero, 0f, Color.White);

        hoverInfo = "";
        DrawMarkers(cx, cy);
        if (showGrid) DrawGrid(cx, cy);
        if (!EditorUI.Locked && overCanvas)
        {
            DrawHoverTile(cx, cy, mouse);
            if (editMode) DrawEditHover(cx, cy);
        }
        if (showMinimap) DrawMinimap(cx, cy);
        Raylib.EndScissorMode();

        Raylib.DrawRectangleLinesEx(rView, Math.Max(1f, EditorUI.Scale), new Color(255, 255, 255, 26));
    }

    static void BeginPan(Vector2 mouse)
    {
        dragging = true;
        dragMouse = mouse;
        dragCamX = camX;
        dragCamY = camY;
    }

    static void UpdatePan(Vector2 mouse)
    {
        if (!Raylib.IsMouseButtonDown(MouseButton.Left) && !Raylib.IsMouseButtonDown(MouseButton.Middle))
        {
            dragging = false;
            return;
        }
        camX = dragCamX - (mouse.X - dragMouse.X) / zoom;
        camY = dragCamY - (mouse.Y - dragMouse.Y) / zoom;
        ClampCam();
        needCompose = true;
    }

    static void ScrubTo(Vector2 mouse)
    {
        if (level == null) return;
        int tx = Math.Clamp((int)((mouse.X - mapX0) / Math.Max(1, mapBlock)), 0, level.width - 1);
        int ty = Math.Clamp((int)((mouse.Y - mapY0) / Math.Max(1, mapBlock)), 0, level.height - 1);
        camX = tx * 16 + 8 - rView.Width / (2f * zoom);
        camY = ty * 16 + 8 - rView.Height / (2f * zoom);
        ClampCam();
        needCompose = true;
    }

    // ---------------------------------------------------------------- edicion
    static bool TileUnder(Vector2 mouse, out int tx, out int ty)
    {
        tx = ty = 0;
        if (level == null) return false;
        int wx = (int)Math.Round(camX) + (int)Math.Floor((mouse.X - rView.X) / zoom);
        int wy = (int)Math.Round(camY) + (int)Math.Floor((mouse.Y - rView.Y) / zoom);
        tx = (int)Math.Floor(wx / 16f);
        ty = (int)Math.Floor(wy / 16f);
        return tx >= 0 && tx < level.width && ty >= 0 && ty < level.height;
    }

    static void BeginEdit(Vector2 mouse, bool erase)
    {
        if (!TileUnder(mouse, out _, out _)) return;
        editingStroke = true;
        strokeErase = erase;
        lastEditTx = int.MinValue;
        lastEditTy = int.MinValue;
        dragEntLayer = -1;
        dragEntIndex = -1;
        dragGate = -1;
        dragGateHandle = -1;
        EditAt(mouse, erase, true);
    }

    static void EndEdit()
    {
        editingStroke = false;
        dragEntLayer = -1;
        dragEntIndex = -1;
        dragGate = -1;
        dragGateHandle = -1;
        lastEditTx = int.MinValue;
        lastEditTy = int.MinValue;
    }

    static void EditAt(Vector2 mouse, bool erase, bool first)
    {
        if (level == null || sourceData == null) return;
        if (!TileUnder(mouse, out int tx, out int ty)) return;

        if (markerTool != 0)
        {
            if (tx == lastEditTx && ty == lastEditTy && !first) return;
            SetMarkerAt(tx, ty);
            lastEditTx = tx;
            lastEditTy = ty;
            return;
        }

        switch (editLayer)
        {
            case 0:
                if (tx == lastEditTx && ty == lastEditTy && !first) return;
                PaintTile(tx, ty, erase);
                break;
            case 1:
                if (tx == lastEditTx && ty == lastEditTy && !first) return;
                EditBonus(tx, ty, erase);
                break;
            case 2:
            case 3:
            case 4:
                EditEntity(tx, ty, erase, first);
                break;
            case 5:
                EditGate(mouse, tx, ty, erase, first);
                break;
        }
        lastEditTx = tx;
        lastEditTy = ty;
    }

    static void MarkChanged()
    {
        dirty = true;
        needCompose = true;
    }

    static void PaintTile(int tx, int ty, bool erase)
    {
        if (level == null || sourceData == null) return;
        int b = level.width * level.height;
        int bi = ty * level.width + tx;

        int bonus = FindBonusAt(tx, ty);
        if (bonus >= 0) RemoveBonus(bonus);

        byte value = erase ? (byte)0 : (byte)selectedTile;
        bool changed = sourceData[bi] != value;
        if (changed) sourceData[bi] = value;
        if (level.tilemap[ty][tx] != value)
        {
            level.tilemap[ty][tx] = value;
            needCompose = true;
        }
        if (changed) dirty = true;
    }

    static int FindBonusAt(int tx, int ty)
    {
        if (level == null) return -1;
        for (int i = 0; i < level.bonus.Length; i++)
        {
            var b = level.bonus[i];
            if (b.exists && b.x == tx && b.y == ty) return i;
        }
        return -1;
    }

    static int FreeBonusSlot()
    {
        if (level == null) return -1;
        for (int i = 0; i < level.bonus.Length; i++) if (!level.bonus[i].exists) return i;
        return -1;
    }

    static void EditBonus(int tx, int ty, bool erase)
    {
        if (level == null || sourceData == null) return;
        int at = FindBonusAt(tx, ty);
        if (erase)
        {
            if (at >= 0) RemoveBonus(at);
            return;
        }

        int b = level.width * level.height;
        int bi = ty * level.width + tx;
        if (at >= 0)
        {
            var bo = level.bonus[at];
            if (bo.bonustile == selectedTile && bo.replacetile == sourceData[bi]) return;
            int off = b + 35082 + at * 4;
            sourceData[off] = (byte)selectedTile;
            sourceData[off + 1] = sourceData[bi];
            bo.bonustile = (byte)selectedTile;
            bo.replacetile = sourceData[bi];
            level.tilemap[ty][tx] = (byte)selectedTile;
            MarkChanged();
            return;
        }

        int slot = FreeBonusSlot();
        if (slot < 0) { ShowToast("NO FREE BONUS SLOTS"); return; }
        int o = b + 35082 + slot * 4;
        sourceData[o] = (byte)selectedTile;
        sourceData[o + 1] = sourceData[bi];
        sourceData[o + 2] = (byte)tx;
        sourceData[o + 3] = (byte)ty;
        var nb = level.bonus[slot];
        nb.exists = true;
        nb.bonustile = (byte)selectedTile;
        nb.replacetile = sourceData[bi];
        nb.x = tx;
        nb.y = ty;
        level.tilemap[ty][tx] = (byte)selectedTile;
        selLayer = 1;
        selIndex = slot;
        MarkChanged();
    }

    static void RemoveBonus(int slot)
    {
        if (level == null || sourceData == null) return;
        var bo = level.bonus[slot];
        if (!bo.exists) return;
        int b = level.width * level.height;
        int off = b + 35082 + slot * 4;
        sourceData[off + 2] = 0xFF;
        sourceData[off + 3] = 0xFF;
        bo.exists = false;
        if (bo.y >= 0 && bo.y < level.height && bo.x >= 0 && bo.x < level.width)
            level.tilemap[bo.y][bo.x] = sourceData[bo.y * level.width + bo.x];
        if (selLayer == 1 && selIndex == slot) selLayer = selIndex = -1;
        MarkChanged();
    }

    static void SetMarkerAt(int tx, int ty)
    {
        if (level == null || sourceData == null) return;
        int b = level.width * level.height;
        if (markerTool == 1)
        {
            int x = tx * 16 + 8, y = ty * 16 + 8;
            if (level.player.initX == x && level.player.initY == y) return;
            sourceData[b + 33778] = (byte)(x & 0xFF);
            sourceData[b + 33779] = (byte)((x >> 8) & 0xFF);
            sourceData[b + 33780] = (byte)(y & 0xFF);
            sourceData[b + 33781] = (byte)((y >> 8) & 0xFF);
            level.player.initX = (short)x;
            level.player.initY = (short)y;
            level.player.sprite.x = (short)x;
            level.player.sprite.y = (short)y;
            Game.UpdateSprite(level, level.player.sprite, 0, true);
            MarkChanged();
        }
        else if (markerTool == 2)
        {
            int x = tx * 16, y = ty * 16;
            if (level.finishX == x && level.finishY == y) return;
            sourceData[b + 35824] = (byte)(x & 0xFF);
            sourceData[b + 35825] = (byte)((x >> 8) & 0xFF);
            sourceData[b + 35826] = (byte)(y & 0xFF);
            sourceData[b + 35827] = (byte)((y >> 8) & 0xFF);
            level.finishX = x;
            level.finishY = y;
            MarkChanged();
        }
    }

    static int FindEntityAt(int layer, int tx, int ty)
    {
        if (level == null) return -1;
        if (layer == 2)
        {
            for (int i = 0; i < level.enemy.Length; i++)
            {
                var e = level.enemy[i];
                if (e.init_enabled && (e.initX >> 4) == tx && (e.initY >> 4) == ty) return i;
            }
        }
        else if (layer == 3)
        {
            for (int i = 0; i < level.@object.Length; i++)
            {
                var o = level.@object[i];
                if (o.init_enabled && (o.initX >> 4) == tx && (o.initY >> 4) == ty) return i;
            }
        }
        else if (layer == 4)
        {
            for (int i = 0; i < level.elevator.Length; i++)
            {
                var e = level.elevator[i];
                if (e.init_enabled && (e.initX >> 4) == tx && (e.initY >> 4) == ty) return i;
            }
        }
        return -1;
    }

    static void EditEntity(int tx, int ty, bool erase, bool first)
    {
        if (level == null || sourceData == null) return;
        int layer = editLayer;

        if (first)
        {
            int found = FindEntityAt(layer, tx, ty);
            if (erase)
            {
                if (found >= 0) DeleteEntity(layer, found);
                return;
            }
            if (found >= 0)
            {
                dragEntLayer = layer;
                dragEntIndex = found;
                selLayer = layer;
                selIndex = found;
                return;
            }
            int added = AddDefaultEntity(layer, tx, ty);
            if (added >= 0)
            {
                dragEntLayer = layer;
                dragEntIndex = added;
                selLayer = layer;
                selIndex = added;
            }
            return;
        }

        if (erase)
        {
            int found = FindEntityAt(layer, tx, ty);
            if (found >= 0) DeleteEntity(layer, found);
            if (dragEntLayer == layer) { dragEntLayer = -1; dragEntIndex = -1; }
            return;
        }

        if (dragEntLayer == layer && dragEntIndex >= 0)
            MoveEntity(layer, dragEntIndex, tx, ty);
    }

    static int AddDefaultEntity(int layer, int tx, int ty)
    {
        if (level == null || sourceData == null) return -1;
        int b = level.width * level.height;
        int x = tx * 16 + 8, y = ty * 16 + 8;

        if (layer == 2)
        {
            int slot = -1;
            for (int i = 0; i < level.enemy.Length; i++) if (!level.enemy[i].init_enabled) { slot = i; break; }
            if (slot < 0) { ShowToast("NO FREE ENEMY SLOTS"); return -1; }
            int off = b + 33782 + slot * 26;
            Array.Clear(sourceData, off, 26);
            var e = level.enemy[slot];
            EnemySpawnPreset().WriteTo(sourceData, off, x, y, e);
            RefreshEntities();
            ShowToast("ENEMY ADDED: " + EnemyShortLabel(EnemySpawnPreset()));
            return slot;
        }
        if (layer == 3)
        {
            int slot = -1;
            for (int i = 0; i < level.@object.Length; i++) if (!level.@object[i].init_enabled) { slot = i; break; }
            if (slot < 0) { ShowToast("NO FREE OBJECT SLOTS"); return -1; }
            int off = b + 33536 + slot * 6;
            Array.Clear(sourceData, off, 6);
            var preset = ObjectSpawnPreset();
            preset.WriteTo(sourceData, off, x, y, level.@object[slot]);
            RefreshEntities();
            ShowToast("OBJECT ADDED: " + ObjectShortLabel(preset));
            return slot;
        }
        if (layer == 4)
        {
            int slot = -1;
            for (int i = 0; i < level.elevator.Length; i++) if (!level.elevator[i].init_enabled) { slot = i; break; }
            if (slot < 0) { ShowToast("NO FREE ELEVATOR SLOTS"); return -1; }
            int off = b + 35624 + slot * 20;
            Array.Clear(sourceData, off, 20);
            Write16(off + 4, 0);
            sourceData[off + 7] = 1;
            Write16(off + 10, 32);
            Write16(off + 12, x);
            Write16(off + 14, y);
            sourceData[off + 16] = 0;
            var e = level.elevator[slot];
            e.initsprite = 0;
            e.init_enabled = true;
            e.enabled = true;
            e.initX = x;
            e.initY = y;
            e.initspeedX = 0;
            e.initspeedY = -1;
            e.range = 32;
            e.init_direction = 0;
            RefreshEntities();
            ShowToast("ELEVATOR ADDED");
            return slot;
        }
        return -1;
    }

    static void MoveEntity(int layer, int index, int tx, int ty)
    {
        if (level == null || sourceData == null) return;
        int b = level.width * level.height;
        int x = tx * 16 + 8, y = ty * 16 + 8;

        if (layer == 2)
        {
            var e = level.enemy[index];
            if (e.initX == x && e.initY == y) return;
            e.initX = x;
            e.initY = y;
            e.sprite.x = (short)x;
            e.sprite.y = (short)y;
            int off = b + 33782 + index * 26;
            Write16(off + 0, x);
            Write16(off + 2, y);
        }
        else if (layer == 3)
        {
            var o = level.@object[index];
            if (o.initX == x && o.initY == y) return;
            o.initX = x;
            o.initY = y;
            o.sprite.x = (short)x;
            o.sprite.y = (short)y;
            int off = b + 33536 + index * 6;
            Write16(off + 2, x);
            Write16(off + 4, y);
        }
        else if (layer == 4)
        {
            var e = level.elevator[index];
            if (e.initX == x && e.initY == y) return;
            e.initX = x;
            e.initY = y;
            e.sprite.x = (short)x;
            e.sprite.y = (short)y;
            int off = b + 35624 + index * 20;
            Write16(off + 12, x);
            Write16(off + 14, y);
        }
        MarkChanged();
    }

    static void DeleteEntity(int layer, int index)
    {
        if (level == null || sourceData == null) return;
        int b = level.width * level.height;
        if (layer == 2)
        {
            var e = level.enemy[index];
            int off = b + 33782 + index * 26;
            Write16(off + 4, 0xFFFF);
            e.initsprite = 0xFFFF;
            e.init_enabled = false;
            e.sprite.enabled = false;
        }
        else if (layer == 3)
        {
            var o = level.@object[index];
            int off = b + 33536 + index * 6;
            Write16(off + 0, 0xFFFF);
            o.initsprite = 0xFFFF;
            o.init_enabled = false;
            o.sprite.enabled = false;
        }
        else if (layer == 4)
        {
            var e = level.elevator[index];
            int off = b + 35624 + index * 20;
            Write16(off + 4, 0xFFFF);
            e.initsprite = 0xFFFF;
            e.init_enabled = false;
            e.enabled = false;
            e.sprite.enabled = false;
        }
        if (selLayer == layer && selIndex == index) selLayer = selIndex = -1;
        MarkChanged();
    }

    static bool FindGateHandle(Vector2 mouse, out int gi, out int handle)
    {
        gi = handle = -1;
        if (level == null) return false;
        int cx = (int)Math.Round(camX), cy = (int)Math.Round(camY);
        float best = Math.Max(7f, 9f * EditorUI.Scale);
        for (int i = 0; i < level.gate.Length; i++)
        {
            var g = level.gate[i];
            if (!g.exists) continue;
            float[] xs = { g.entranceX * 16 + 8, g.exitX * 16 + 8, g.screenX * 16 + 8 };
            float[] ys = { g.entranceY * 16 + 8, g.exitY * 16 + 8, g.screenY * 16 + 8 };
            for (int h = 0; h < 3; h++)
            {
                float dx = mouse.X - SX(xs[h], cx);
                float dy = mouse.Y - SY(ys[h], cy);
                float d = MathF.Sqrt(dx * dx + dy * dy);
                if (d <= best) { best = d; gi = i; handle = h; }
            }
        }
        return gi >= 0;
    }

    static void EditGate(Vector2 mouse, int tx, int ty, bool erase, bool first)
    {
        if (level == null || sourceData == null) return;
        bool found = FindGateHandle(mouse, out int gi, out int handle);

        if (first)
        {
            if (erase)
            {
                if (found) DeleteGate(gi);
                return;
            }
            if (found)
            {
                dragGate = gi;
                dragGateHandle = handle;
                selLayer = 5;
                selIndex = gi;
                return;
            }
            int slot = AddGate(tx, ty);
            if (slot >= 0)
            {
                dragGate = slot;
                dragGateHandle = 0;
                selLayer = 5;
                selIndex = slot;
            }
            return;
        }

        if (erase)
        {
            if (found) DeleteGate(gi);
            dragGate = -1;
            dragGateHandle = -1;
            return;
        }

        if (dragGate >= 0 && dragGateHandle >= 0)
        {
            var g = level.gate[dragGate];
            int b = level.width * level.height;
            int off = b + 35484 + dragGate * 7;
            if (dragGateHandle == 0)
            {
                g.entranceX = (uint)tx;
                g.entranceY = (uint)ty;
                sourceData[off] = (byte)tx;
                sourceData[off + 1] = (byte)ty;
            }
            else if (dragGateHandle == 1)
            {
                g.exitX = (uint)tx;
                g.exitY = (uint)ty;
                sourceData[off + 4] = (byte)tx;
                sourceData[off + 5] = (byte)ty;
            }
            else
            {
                g.screenX = (uint)tx;
                g.screenY = (uint)ty;
                sourceData[off + 2] = (byte)tx;
                sourceData[off + 3] = (byte)ty;
            }
            MarkChanged();
        }
    }

    static int AddGate(int tx, int ty)
    {
        if (level == null || sourceData == null) return -1;
        int slot = -1;
        for (int i = 0; i < level.gate.Length; i++) if (!level.gate[i].exists) { slot = i; break; }
        if (slot < 0) { ShowToast("NO FREE GATE SLOTS"); return -1; }

        int b = level.width * level.height;
        int off = b + 35484 + slot * 7;
        Array.Clear(sourceData, off, 7);
        sourceData[off + 0] = (byte)tx;
        sourceData[off + 1] = (byte)ty;
        sourceData[off + 2] = (byte)tx;
        sourceData[off + 3] = (byte)ty;
        sourceData[off + 4] = (byte)tx;
        sourceData[off + 5] = (byte)ty;

        var g = level.gate[slot];
        g.exists = true;
        g.entranceX = g.screenX = g.exitX = (uint)tx;
        g.entranceY = g.screenY = g.exitY = (uint)ty;
        g.noscroll = false;
        MarkChanged();
        ShowToast("GATE ADDED - drag the coloured handles");
        return slot;
    }

    static void DeleteGate(int slot)
    {
        if (level == null || sourceData == null) return;
        var g = level.gate[slot];
        if (!g.exists) return;
        int b = level.width * level.height;
        sourceData[b + 35484 + slot * 7 + 1] = 0xFF;
        g.exists = false;
        if (selLayer == 5 && selIndex == slot) selLayer = selIndex = -1;
        dragGate = -1;
        dragGateHandle = -1;
        MarkChanged();
    }

    static void Write16(int offset, int value)
    {
        if (sourceData == null) return;
        sourceData[offset] = (byte)(value & 0xFF);
        sourceData[offset + 1] = (byte)((value >> 8) & 0xFF);
    }

    static void Write16(byte[] data, int offset, int value)
    {
        data[offset] = (byte)(value & 0xFF);
        data[offset + 1] = (byte)((value >> 8) & 0xFF);
    }

    static int ReadU16(byte[] data, int offset)
        => data[offset] | (data[offset + 1] << 8);

    static void RefreshEntities()
    {
        if (level == null) return;
        Objects.InitElevators(level);
        Objects.Init(level);
        Enemies.Init(level);
        var p = level.player;
        p.sprite.x = p.initX;
        p.sprite.y = p.initY;
        Game.UpdateSprite(level, p.sprite, 0, true);
        needCompose = true;
    }

    // ------------------------------------------------------------------ enemigos
    static EnemyPreset EnemySpawnPreset()
    {
        if (enemySpawn != null) return enemySpawn;
        EnsureEnemyPresets();
        if (enemyPresets is { Count: > 0 })
        {
            enemySpawn = enemyPresets.FirstOrDefault(p => !p.IsBoss) ?? enemyPresets[0];
            return enemySpawn;
        }
        if (enemyDefault == null)
        {
            enemyDefault = new EnemyPreset { initsprite = 53, type = 0 };
            enemyDefault.record[4] = 53;
        }
        return enemyDefault;
    }

    static string EnemyShortLabel(EnemyPreset p)
        => p.IsBoss ? $"BOSS #{p.SpriteNumber:000}" : $"#{p.SpriteNumber:000} T{p.type}";

    static string EnemyTypeName(int type) => type switch
    {
        0 or 1 => "WALKER",
        2 => "SHOOTER",
        3 or 4 => "LEAPER",
        5 or 6 => "FLYER",
        7 => "CHASER",
        8 or 14 => "WANDERER",
        9 => "BURROWER",
        10 => "CHARGER",
        11 => "GUNNER",
        12 => "FIREBALL",
        13 => "BOUNCER",
        15 => "DRIFTER",
        16 => "STATIC",
        17 => "DROPPER",
        18 => "GUARD",
        _ => "UNKNOWN",
    };

    static void OpenEnemyPicker()
    {
        EnsureEnemyPresets();
        enemyOpen = true;
        enemyScroll = 0;
    }

    static void EnsureEnemyPresets()
    {
        if (enemyPresets != null || sprites == null) return;
        enemyPresets = new List<EnemyPreset>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (string file in EditorCfg.OriginalLevelOrder)
        {
            string path = EditorCfg.OriginalLevelPath(viewDir, file);
            if (!File.Exists(path)) continue;
            byte[] data;
            try { data = File.ReadAllBytes(path); }
            catch { continue; }
            if (data.Length < 35828) continue;
            int height = (data.Length - 35828) >> 8;
            if (height <= 0) continue;
            int b = height * 256;
            if (data.Length < b + 35828) continue;

            for (int i = 0; i < 50; i++)
            {
                int off = b + 33782 + i * 26;
                ushort initsprite = (ushort)ReadU16(data, off + 4);
                if (initsprite == 0xFFFF) continue;
                ushort type = (ushort)(ReadU16(data, off + 6) & 0x1FFF);
                if (type > 18) continue;

                int anim = initsprite & 0x1FFF;
                if (Array.IndexOf(Original.ANIM_ENEMY, (short)anim) < 0) continue;
                int spriteNumber = anim + Objects.FIRST_NMI;
                if (spriteNumber < 0 || spriteNumber >= sprites.Length || sprites[spriteNumber].data == null) continue;

                byte[] rec = new byte[26];
                Array.Copy(data, off, rec, 0, 26);
                string key = $"{anim}:{type}";
                if (!seen.Add(key)) continue;

                var preset = ParseEnemyPreset(rec, file);
                preset.key = key;
                enemyPresets.Add(preset);
            }
        }

        enemyPresets.Sort((a, b) =>
        {
            int c = b.IsBoss.CompareTo(a.IsBoss); // los bosses primero
            if (c != 0) return c;
            c = a.SpriteNumber.CompareTo(b.SpriteNumber);
            return c != 0 ? c : a.type.CompareTo(b.type);
        });
    }

    static EnemyPreset ParseEnemyPreset(byte[] rec, string source)
    {
        var p = new EnemyPreset
        {
            record = rec,
            initsprite = (ushort)ReadU16(rec, 4),
            type = (ushort)(ReadU16(rec, 6) & 0x1FFF),
            initspeedX = (short)ReadU16(rec, 8),
            power = (short)ReadU16(rec, 12),
            source = source,
        };

        switch (p.type)
        {
            case 0:
            case 1:
                p.centerX = (short)ReadU16(rec, 15);
                p.rangeX = (uint)ReadU16(rec, 17);
                break;
            case 2:
                p.delay = rec[16];
                uint rx = (uint)ReadU16(rec, 17);
                p.direction = (byte)((rx >> 14) & 3);
                p.rangeX = rx & 0x3FFF;
                break;
            case 3:
            case 4:
            case 5:
            case 6:
                p.centerX = (short)ReadU16(rec, 15);
                p.rangeX = (uint)ReadU16(rec, 17);
                p.rangeY = rec[19];
                break;
            case 7:
            case 9:
            case 10:
            case 11:
                p.walkspeedX = rec[19];
                p.rangeX = (uint)ReadU16(rec, 23);
                break;
            case 8:
            case 14:
                p.walkspeedX = rec[19];
                break;
            case 12:
                p.rangeY = (uint)ReadU16(rec, 15);
                p.delay = rec[19];
                break;
            case 13:
                p.delay = rec[20];
                p.rangeX = (uint)ReadU16(rec, 23);
                break;
            case 17:
                p.rangeX = (uint)ReadU16(rec, 15);
                p.delay = (uint)ReadU16(rec, 17);
                p.rangeY = (uint)ReadU16(rec, 21);
                break;
            case 18:
                p.rangeX = (uint)ReadU16(rec, 15);
                p.rangeY = (uint)ReadU16(rec, 17);
                p.initspeedY = rec[19];
                break;
        }
        return p;
    }

    static void DrawSpritePreview(int spriteNumber, Rectangle dest)
    {
        if (sprites == null) return;
        if (spriteNumber < 0 || spriteNumber >= sprites.Length) return;
        var sd = sprites[spriteNumber];
        if (sd.data == null) return;
        int w = Original.SPRITEWIDTH[spriteNumber], h = Original.SPRITEHEIGHT[spriteNumber];
        if (w <= 0 || h <= 0) return;

        float scale = Math.Min(dest.Width / w, dest.Height / h);
        if (scale <= 0) return;
        float ox = dest.X + (dest.Width - w * scale) / 2f;
        float oy = dest.Y + (dest.Height - h * scale) / 2f;
        float px = MathF.Max(1f, scale + 0.35f);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                byte c = sd.data[y * w + x];
                if (c == 0) continue;
                c &= 15;
                Color col = level != null
                    ? new Color(level.palette[c, 0], level.palette[c, 1], level.palette[c, 2], (byte)255)
                    : new Color(OriginalTables.PaletteColour[c, 0], OriginalTables.PaletteColour[c, 1], OriginalTables.PaletteColour[c, 2], (byte)255);
                Raylib.DrawRectangleRec(new Rectangle(ox + x * scale, oy + y * scale, px, px), col);
            }
    }

    // --------------------------------------------------------- catalogo de objetos
    static void OpenObjectPicker()
    {
        EnsureObjectPresets();
        objectOpen = true;
        objectScroll = 0;
    }

    static bool ValidObjectSprite(int n)
        => sprites != null && n >= Objects.FIRST_OBJET && n < sprites.Length && sprites[n].data != null;

    static void EnsureObjectPresets()
    {
        if (objectPresets != null || sprites == null) return;
        objectPresets = new List<ObjectPreset>();
        var seen = new HashSet<ushort>();

        // Primero las variantes (flags incluidos) que usan los niveles originales.
        foreach (string file in EditorCfg.OriginalLevelOrder)
        {
            string path = EditorCfg.OriginalLevelPath(viewDir, file);
            if (!File.Exists(path)) continue;
            byte[] data;
            try { data = File.ReadAllBytes(path); }
            catch { continue; }
            if (data.Length < 35828) continue;
            int height = (data.Length - 35828) >> 8;
            if (height <= 0) continue;
            int b = height * 256;
            if (data.Length < b + 35828) continue;

            for (int i = 0; i < 40; i++)
            {
                int off = b + 33536 + i * 6;
                ushort raw = (ushort)ReadU16(data, off);
                if (raw == 0xFFFF) continue;
                if (!ValidObjectSprite(raw & 0x1FFF)) continue;
                if (!seen.Add(raw)) continue;
                objectPresets.Add(new ObjectPreset
                {
                    initsprite = raw,
                    source = file,
                    key = $"{raw:X4}",
                });
            }
        }

        // Y despues el resto de objetos que soporta el motor, visibles por defecto.
        for (int i = 0; i < Original.ORIG_OBJECT_COUNT; i++)
        {
            int n = Objects.FIRST_OBJET + i;
            if (!ValidObjectSprite(n)) continue;
            ushort raw = (ushort)(0x2000 | n);
            if (!seen.Add(raw)) continue;
            objectPresets.Add(new ObjectPreset
            {
                initsprite = raw,
                source = "ENGINE",
                key = $"{raw:X4}",
            });
        }

        objectPresets.Sort((a, b) =>
        {
            int c = a.SpriteNumber.CompareTo(b.SpriteNumber);
            return c != 0 ? c : a.initsprite.CompareTo(b.initsprite);
        });
    }

    static ObjectPreset ObjectSpawnPreset()
    {
        EnsureObjectPresets();
        if (objectPresets is { Count: > 0 })
        {
            objectSpawn ??= objectPresets[0];
            return objectSpawn;
        }
        objectDefault ??= new ObjectPreset
        {
            initsprite = (ushort)(0x2000 | Objects.FIRST_OBJET),
            source = "ENGINE",
            key = "DEFAULT",
        };
        return objectDefault;
    }

    static string ObjectShortLabel(ObjectPreset p)
    {
        string flags = p.Flash ? " FLASH" : "";
        flags += p.Flipped ? " FLIP" : "";
        return $"OBJ {p.SpriteNumber:000}" + flags;
    }

    static string ObjectBehavior(ObjectPreset p)
    {
        int idx = p.SpriteNumber - Objects.FIRST_OBJET;
        var parts = new List<string>();
        if (p.Flash) parts.Add("FLASH");
        if (p.Flipped) parts.Add("FLIPPED");
        if (objectdata != null && idx >= 0 && idx < objectdata.Length)
        {
            var d = objectdata[idx];
            if (d.support) parts.Add("SUPPORT");
            if (d.bounce) parts.Add("BOUNCE");
            if (d.gravity) parts.Add("GRAVITY");
            if (d.droptobottom) parts.Add("DROP");
            if (d.no_damage) parts.Add("SAFE");
            if (d.maxspeedY != 0) parts.Add("SPEED " + d.maxspeedY);
        }
        return parts.Count == 0 ? "STATIC" : string.Join("  ", parts);
    }

    static void TestLevel()
    {
        if (level == null || sourceData == null || currentFile == null)
        {
            ShowToast("NO LEVEL TO TEST");
            return;
        }
        testPending = true;
    }

    static void RunPendingTest()
    {
        if (level == null || sourceData == null || currentFile == null) return;

        // Se juega con la copia en memoria: incluye los cambios sin guardar.
        EndEdit();
        Game.RunTest(viewDir, currentFile, sourceData);
        needCompose = true;
        ShowToast("TEST FINISHED - BACK TO EDITOR");
    }

    static void ShowToast(string text)
    {
        toast = text;
        toastUntil = Environment.TickCount64 + 2600;
    }

    static bool SaveCustom()
    {
        if (level == null || sourceData == null || currentFile == null) return false;
        string file = currentFile;
        try
        {
            Directory.CreateDirectory(EditorCfg.CustomDir);
            File.WriteAllBytes(EditorCfg.CustomLevelPath(file), sourceData);
        }
        catch (Exception ex)
        {
            ShowToast("SAVE FAILED: " + ex.Message);
            return false;
        }
        dirty = false;
        ShowToast("SAVED TO CustomLevels\\" + file + ".bin");
        return true;
    }

    static void OpenImport()
    {
        if (dirty)
        {
            ShowToast("SAVE OR DISCARD BEFORE IMPORTING");
            return;
        }
        importOpen = true;
        importScroll = 0;
    }

    static void ImportLevel(string file)
    {
        if (EditorCfg.IsCustomLevel(file))
        {
            ShowToast(file + " IS ALREADY IN CustomLevels");
            return;
        }

        string source = EditorCfg.OriginalLevelPath(viewDir, file);
        if (!File.Exists(source))
        {
            ShowToast("ORIGINAL " + file + " NOT FOUND");
            return;
        }

        try
        {
            Directory.CreateDirectory(EditorCfg.CustomDir);
            File.Copy(source, EditorCfg.CustomLevelPath(file), false);
        }
        catch (Exception ex)
        {
            ShowToast("IMPORT FAILED: " + ex.Message);
            return;
        }

        RefreshCustomFiles();
        int ni = customFiles.FindIndex(f => f.Equals(file, StringComparison.OrdinalIgnoreCase));
        if (ni >= 0) LoadLevel(ni);
        leftTab = 0;
        importOpen = false;
        ShowToast("IMPORTED " + file);
    }

    static void OpenNewLevel()
    {
        if (dirty)
        {
            ShowToast("SAVE OR DISCARD BEFORE CREATING A LEVEL");
            return;
        }
        newOpen = true;
        newScroll = 0;

        if (newTilesetIndex < 0 || newTilesetIndex >= EditorCfg.OriginalLevelOrder.Length ||
            !File.Exists(EditorCfg.OriginalLevelPath(viewDir, EditorCfg.OriginalLevelOrder[newTilesetIndex])))
        {
            newTilesetIndex = 0;
            for (int i = 0; i < EditorCfg.OriginalLevelOrder.Length; i++)
                if (File.Exists(EditorCfg.OriginalLevelPath(viewDir, EditorCfg.OriginalLevelOrder[i])))
                {
                    newTilesetIndex = i;
                    break;
                }
        }

        newHeight = Math.Clamp(SourceHeight(EditorCfg.OriginalLevelOrder[newTilesetIndex]), 8, 200);
        if (newSlotIndex < 0 || newSlotIndex >= EditorCfg.OriginalLevelOrder.Length ||
            EditorCfg.IsCustomLevel(EditorCfg.OriginalLevelOrder[newSlotIndex]))
            newSlotIndex = FirstFreeSlot();
    }

    static int FirstFreeSlot()
    {
        for (int i = 0; i < EditorCfg.OriginalLevelOrder.Length; i++)
            if (!EditorCfg.IsCustomLevel(EditorCfg.OriginalLevelOrder[i])) return i;
        return -1;
    }

    static void BuildNewTilesetPreview(int sourceIndex)
    {
        if (newPreviewTex.Id != 0) Raylib.UnloadTexture(newPreviewTex);
        newPreviewTex = default;
        newPreviewIndex = sourceIndex;
        if (sourceIndex < 0 || sourceIndex >= EditorCfg.OriginalLevelOrder.Length) return;

        string file = EditorCfg.OriginalLevelOrder[sourceIndex];
        byte[] data;
        try { data = File.ReadAllBytes(EditorCfg.OriginalLevelPath(viewDir, file)); }
        catch { return; }
        if (data.Length < 35828) return;
        int height = (data.Length - 35828) >> 8;
        int b = height * 256;
        if (height <= 0 || data.Length < b + 35828) return;

        const int cols = 8, rows = 3, cell = 16;
        Image img = Raylib.GenImageColor(cols * cell, rows * cell, new Color(12, 14, 18, 255));
        int paletteLevel = EditorCfg.KnownLevelIndex(file);
        for (int t = 0; t < cols * rows; t++)
        {
            byte[] td = Level.LoadTile(data, b + t * 128);
            int ox = (t % cols) * cell, oy = (t / cols) * cell;
            for (int y = 0; y < 16; y++)
                for (int x = 0; x < 16; x++)
                {
                    byte c = (byte)(td[y * 16 + x] & 15);
                    if (c == 0) continue;
                    byte r = OriginalTables.PaletteColour[c, 0];
                    byte g = OriginalTables.PaletteColour[c, 1];
                    byte bl = OriginalTables.PaletteColour[c, 2];
                    if (c == 14 && paletteLevel >= 0 &&
                        paletteLevel < OriginalTables.PaletteLevelColour.GetLength(0))
                    {
                        r = OriginalTables.PaletteLevelColour[paletteLevel, 0];
                        g = OriginalTables.PaletteLevelColour[paletteLevel, 1];
                        bl = OriginalTables.PaletteLevelColour[paletteLevel, 2];
                    }
                    Raylib.ImageDrawPixel(ref img, ox + x, oy + y, new Color(r, g, bl, (byte)255));
                }
        }
        newPreviewTex = Raylib.LoadTextureFromImage(img);
        Raylib.UnloadImage(img);
    }

    static int SourceHeight(string file)
    {
        try
        {
            string path = EditorCfg.OriginalLevelPath(viewDir, file);
            long len = new FileInfo(path).Length;
            if (len < 35828) return 40;
            return Math.Clamp((int)((len - 35828) / 256), 8, 200);
        }
        catch { return 40; }
    }

    static void CreateBlankLevel(string tilesetFile, string slotFile, int height)
    {
        string source = EditorCfg.OriginalLevelPath(viewDir, tilesetFile);
        if (!File.Exists(source))
        {
            ShowToast("ORIGINAL " + tilesetFile + " NOT FOUND");
            return;
        }

        byte[] src;
        try { src = File.ReadAllBytes(source); }
        catch (Exception ex) { ShowToast("CANNOT READ TILESET: " + ex.Message); return; }
        if (src.Length < 35828)
        {
            ShowToast("INVALID TILESET " + tilesetFile);
            return;
        }

        int srcHeight = (src.Length - 35828) >> 8;
        int srcB = srcHeight * 256;
        if (srcHeight <= 0 || src.Length < srcB + 35828)
        {
            ShowToast("INVALID TILESET " + tilesetFile);
            return;
        }

        height = Math.Clamp(height, 8, 200);
        int b = height * 256;
        byte[] data = new byte[b + 35828];
        // El tileset completo (graficos + flags de los 256 tiles) viene del original elegido.
        Array.Copy(src, srcB, data, b, 35828);

        // Limpiar todo lo que no es tileset.
        for (int i = 0; i < 40; i++)
        {
            int off = b + 33536 + i * 6;
            Array.Clear(data, off, 6);
            Write16(data, off, 0xFFFF);
        }
        for (int i = 0; i < 50; i++)
        {
            int off = b + 33782 + i * 26;
            Array.Clear(data, off, 26);
            Write16(data, off + 4, 0xFFFF);
        }
        for (int i = 0; i < 100; i++)
        {
            int off = b + 35082 + i * 4;
            Array.Clear(data, off, 4);
            data[off + 2] = 0xFF;
            data[off + 3] = 0xFF;
        }
        for (int i = 0; i < 20; i++)
        {
            int off = b + 35484 + i * 7;
            Array.Clear(data, off, 7);
            data[off + 1] = 0xFF;
        }
        for (int i = 0; i < 10; i++)
        {
            int off = b + 35624 + i * 20;
            Array.Clear(data, off, 20);
            Write16(data, off + 4, 0xFFFF);
        }

        // Suelo de arranque: un tile con floorflag=1 del tileset elegido.
        int floorTile = 1;
        for (int i = 1; i < 256; i++)
        {
            byte hf = data[b + 32768 + i];
            byte ff = data[b + 32768 + 256 + i];
            if (ff == 1 && hf != 3 && hf != 5) { floorTile = i; break; }
        }
        int bottom = (height - 1) * 256;
        for (int x = 0; x < 256; x++) data[bottom + x] = (byte)floorTile;

        int startX = 32, startY = (height - 1) * 16;
        int finishX = (256 - 3) * 16, finishY = startY;
        Write16(data, b + 33776, Math.Max(0, height - 12)); // ALTITUDE_ZERO
        Write16(data, b + 33778, startX);
        Write16(data, b + 33780, startY);
        Write16(data, b + 35482, 236);                     // XLIMIT
        Write16(data, b + 35824, finishX);
        Write16(data, b + 35826, finishY);

        string path = EditorCfg.CustomLevelPath(slotFile);
        if (File.Exists(path))
        {
            ShowToast(slotFile + " IS ALREADY IN CustomLevels");
            return;
        }

        try
        {
            Directory.CreateDirectory(EditorCfg.CustomDir);
            File.WriteAllBytes(path, data);
        }
        catch (Exception ex)
        {
            ShowToast("CREATE FAILED: " + ex.Message);
            return;
        }

        RefreshCustomFiles();
        int ni = customFiles.FindIndex(f => f.Equals(slotFile, StringComparison.OrdinalIgnoreCase));
        newOpen = false;
        leftTab = 0;
        if (ni >= 0) LoadLevel(ni);
        ShowToast("CREATED " + slotFile + " WITH " + tilesetFile + " TILESET");
    }

    static void DrawNewLevelModal()
    {
        float s = EditorUI.Scale;
        int sw = Raylib.GetScreenWidth(), sh = Raylib.GetScreenHeight();
        float bw = Math.Min(680 * s, sw - 24 * s);
        float bh = Math.Min(510 * s, sh - 24 * s);
        var box = new Rectangle(sw / 2f - bw / 2f, sh / 2f - bh / 2f, bw, bh);

        Raylib.DrawRectangle(0, 0, sw, sh, new Color(0, 0, 0, 175));
        EditorUI.Panel(box);
        EditorUI.Text("NEW CUSTOM LEVEL", box.X + 24 * s, box.Y + 20 * s, 18, EditorUI.Fg, true);
        EditorUI.Text("Creates an empty level with the full 256-tile set of the chosen original.",
            box.X + 24 * s, box.Y + 50 * s, 11, EditorUI.Muted);

        float leftX = box.X + 20 * s, leftY = box.Y + 84 * s;
        float leftW = 300 * s, leftH = box.Height - 284 * s;
        var list = new Rectangle(leftX, leftY, leftW, leftH);
        EditorUI.Text("TILESET", leftX, leftY - 20 * s, 10, EditorUI.Accent, true);

        int rowH = (int)(28 * s);
        int maxScroll = Math.Max(0, EditorCfg.OriginalLevelOrder.Length * rowH - (int)list.Height);
        if (EditorUI.Hover(list))
        {
            float wheel = Raylib.GetMouseWheelMove();
            if (wheel != 0) newScroll = Math.Clamp(newScroll - (int)(wheel * 48 * s), 0, maxScroll);
        }
        newScroll = Math.Clamp(newScroll, 0, maxScroll);
        var mouse = Raylib.GetMousePosition();

        Raylib.BeginScissorMode((int)list.X, (int)list.Y, (int)list.Width, (int)list.Height);
        for (int i = 0; i < EditorCfg.OriginalLevelOrder.Length; i++)
        {
            string file = EditorCfg.OriginalLevelOrder[i];
            var row = new Rectangle(list.X, list.Y + i * rowH - newScroll, list.Width, rowH);
            if (row.Y + row.Height < list.Y || row.Y > list.Y + list.Height) continue;
            bool exists = File.Exists(EditorCfg.OriginalLevelPath(viewDir, file));
            bool hover = exists && Raylib.CheckCollisionPointRec(mouse, row);
            bool selected = i == newTilesetIndex;
            if (selected) Raylib.DrawRectangleRounded(row, 0.25f, 6, EditorUI.AccentSoft);
            else if (hover) Raylib.DrawRectangleRounded(row, 0.25f, 6, EditorUI.Panel2);
            if (selected) Raylib.DrawRectangle((int)row.X, (int)(row.Y + 3 * s), (int)(3 * s), (int)(row.Height - 6 * s), EditorUI.Accent);

            EditorUI.Text($"{i + 1:00}", row.X + 10 * s, row.Y + 6 * s, 10, selected ? EditorUI.Accent : EditorUI.Muted, true);
            EditorUI.Text(file, row.X + 38 * s, row.Y + 4 * s, 13, exists ? EditorUI.Fg : EditorUI.Muted);
            string tag = !exists ? "MISSING" : SourceHeight(file) + " ROWS";
            EditorUI.TextRight(tag, row.X + row.Width - 8 * s, row.Y + 7 * s, 9,
                exists ? EditorUI.Muted : EditorUI.Danger, false);

            if (hover)
            {
                Raylib.SetMouseCursor(MouseCursor.PointingHand);
                if (Raylib.IsMouseButtonPressed(MouseButton.Left))
                {
                    newTilesetIndex = i;
                    newHeight = Math.Clamp(SourceHeight(file), 8, 200);
                }
            }
        }
        Raylib.EndScissorMode();

        if (newPreviewIndex != newTilesetIndex) BuildNewTilesetPreview(newTilesetIndex);
        EditorUI.Text("TILESET PREVIEW (24 TILES)", leftX, leftY + leftH + 2 * s, 9, EditorUI.Muted, true);
        var pv = new Rectangle(leftX + (leftW - 256 * s) / 2f, leftY + leftH + 18 * s, 256 * s, 96 * s);
        if (newPreviewTex.Id != 0)
        {
            Raylib.DrawTexturePro(newPreviewTex,
                new Rectangle(0, 0, newPreviewTex.Width, newPreviewTex.Height),
                pv, System.Numerics.Vector2.Zero, 0, Color.White);
            Raylib.DrawRectangleLinesEx(pv, Math.Max(1f, s), EditorUI.Line);
        }
        else
        {
            Raylib.DrawRectangleRec(pv, EditorUI.Panel2);
            Raylib.DrawRectangleLinesEx(pv, Math.Max(1f, s), EditorUI.Line);
            EditorUI.TextCentered("NO PREVIEW", pv, 10, EditorUI.Muted);
        }

        float rightX = box.X + 342 * s, rightW = box.Width - 362 * s;
        EditorUI.Text("LEVEL SLOT", rightX, leftY - 20 * s, 10, EditorUI.Accent, true);
        float gridY = leftY + 6 * s;
        int cols = 2, rows = (EditorCfg.OriginalLevelOrder.Length + cols - 1) / cols;
        float gap = 6 * s;
        float slotW = (rightW - gap) / cols, slotH = 26 * s;
        for (int i = 0; i < EditorCfg.OriginalLevelOrder.Length; i++)
        {
            string file = EditorCfg.OriginalLevelOrder[i];
            bool used = EditorCfg.IsCustomLevel(file);
            bool selected = i == newSlotIndex;
            var cell = new Rectangle(rightX + (i % cols) * (slotW + gap), gridY + (i / cols) * (slotH + gap), slotW, slotH);
            Color fill = selected ? EditorUI.AccentSoft : used ? new Color(22, 24, 29, 255) : EditorUI.Panel2;
            Raylib.DrawRectangleRounded(cell, 0.28f, 8, fill);
            Raylib.DrawRectangleRoundedLinesEx(cell, 0.28f, 8, Math.Max(1f, s),
                selected ? EditorUI.Accent : EditorUI.Line);
            EditorUI.TextCentered(file + (used ? "  USED" : ""), cell, 9,
                used ? EditorUI.Muted : selected ? EditorUI.Accent : EditorUI.Fg, selected || !used);
            if (!used && EditorUI.Hover(cell))
            {
                Raylib.SetMouseCursor(MouseCursor.PointingHand);
                if (Raylib.IsMouseButtonPressed(MouseButton.Left)) newSlotIndex = i;
            }
        }

        float hy = gridY + rows * (slotH + gap) + 10 * s;
        EditorUI.Text("HEIGHT", rightX, hy + 5 * s, 10, EditorUI.Accent, true);
        var bMinus = new Rectangle(rightX + 70 * s, hy, 28 * s, 26 * s);
        var bPlus = new Rectangle(rightX + 160 * s, hy, 28 * s, 26 * s);
        if (EditorUI.Button(bMinus, "-", false, 13, true))
            newHeight = Math.Clamp(newHeight - 4, 8, 200);
        EditorUI.TextCentered($"{newHeight} ROWS", new Rectangle(rightX + 100 * s, hy, 58 * s, 26 * s), 11, EditorUI.Fg, true);
        if (EditorUI.Button(bPlus, "+", false, 13, true))
            newHeight = Math.Clamp(newHeight + 4, 8, 200);

        if (newSlotIndex < 0)
            EditorUI.Text("ALL 16 SLOTS ARE IN USE", rightX, hy + 38 * s, 10, EditorUI.Danger, true);
        else
            EditorUI.Text("The floor row is filled with a solid tile.", rightX, hy + 38 * s, 9, EditorUI.Muted);

        float by = box.Y + box.Height - 48 * s;
        var bCancel = new Rectangle(box.X + 24 * s, by, 100 * s, 30 * s);
        var bCreate = new Rectangle(box.X + box.Width - 130 * s, by, 106 * s, 30 * s);
        if (EditorUI.Button(bCancel, "CANCEL", false, 11, true)) newOpen = false;
        if (EditorUI.Button(bCreate, "CREATE", true, 11, true))
        {
            if (newSlotIndex < 0) ShowToast("NO FREE LEVEL SLOTS");
            else CreateBlankLevel(EditorCfg.OriginalLevelOrder[newTilesetIndex],
                EditorCfg.OriginalLevelOrder[newSlotIndex], newHeight);
        }
    }

    static void DrawImportModal()
    {        float s = EditorUI.Scale;
        int sw = Raylib.GetScreenWidth(), sh = Raylib.GetScreenHeight();
        float bw = Math.Min(470 * s, sw - 24 * s);
        float bh = Math.Min(430 * s, sh - 24 * s);
        var box = new Rectangle(sw / 2f - bw / 2f, sh / 2f - bh / 2f, bw, bh);

        Raylib.DrawRectangle(0, 0, sw, sh, new Color(0, 0, 0, 175));
        EditorUI.Panel(box);
        EditorUI.Text("IMPORT ORIGINAL LEVEL", box.X + 24 * s, box.Y + 22 * s, 18, EditorUI.Fg, true);
        EditorUI.Text("Click a level from FILES to copy it into CustomLevels.",
            box.X + 24 * s, box.Y + 52 * s, 11, EditorUI.Muted);

        var list = new Rectangle(box.X + 20 * s, box.Y + 82 * s, box.Width - 40 * s, box.Height - 146 * s);
        int rowH = (int)(28 * s);
        int maxScroll = Math.Max(0, EditorCfg.OriginalLevelOrder.Length * rowH - (int)list.Height);
        if (EditorUI.Hover(list))
        {
            float wheel = Raylib.GetMouseWheelMove();
            if (wheel != 0) importScroll = Math.Clamp(importScroll - (int)(wheel * 48 * s), 0, maxScroll);
        }
        importScroll = Math.Clamp(importScroll, 0, maxScroll);

        var mouse = Raylib.GetMousePosition();
        Raylib.BeginScissorMode((int)list.X, (int)list.Y, (int)list.Width, (int)list.Height);
        for (int i = 0; i < EditorCfg.OriginalLevelOrder.Length; i++)
        {
            string file = EditorCfg.OriginalLevelOrder[i];
            var row = new Rectangle(list.X, list.Y + i * rowH - importScroll, list.Width, rowH);
            if (row.Y + row.Height < list.Y || row.Y > list.Y + list.Height) continue;

            bool imported = EditorCfg.IsCustomLevel(file);
            bool hover = Raylib.CheckCollisionPointRec(mouse, row);
            if (hover) Raylib.DrawRectangleRounded(row, 0.25f, 6, EditorUI.Panel2);
            if (hover) Raylib.SetMouseCursor(MouseCursor.PointingHand);

            EditorUI.Text($"{i + 1:00}", row.X + 10 * s, row.Y + 6 * s, 10, EditorUI.Muted, true);
            EditorUI.Text(file, row.X + 38 * s, row.Y + 4 * s, 13, imported ? EditorUI.Muted : EditorUI.Fg);
            string tag = imported ? "IMPORTED" : file == "LEVELG" ? "END" : file == "LEVELD" ? "EXTRA" : "";
            if (tag.Length > 0)
                EditorUI.TextRight(tag, row.X + row.Width - 8 * s, row.Y + 7 * s, 9,
                    imported ? EditorUI.Muted : EditorUI.Accent, !imported);

            if (hover && Raylib.IsMouseButtonPressed(MouseButton.Left))
            {
                ImportLevel(file);
                if (!importOpen)
                {
                    Raylib.EndScissorMode();
                    return;
                }
            }
        }
        Raylib.EndScissorMode();

        float by = box.Y + box.Height - 46 * s;
        var bCancel = new Rectangle(box.X + box.Width - 130 * s, by, 106 * s, 30 * s);
        if (EditorUI.Button(bCancel, "CANCEL", false, 11, true)) importOpen = false;
    }

    static void DrawEnemyModal()
    {
        EnsureEnemyPresets();
        float s = EditorUI.Scale;
        int sw = Raylib.GetScreenWidth(), sh = Raylib.GetScreenHeight();
        float bw = Math.Min(640 * s, sw - 24 * s);
        float bh = Math.Min(500 * s, sh - 24 * s);
        var box = new Rectangle(sw / 2f - bw / 2f, sh / 2f - bh / 2f, bw, bh);

        Raylib.DrawRectangle(0, 0, sw, sh, new Color(0, 0, 0, 175));
        EditorUI.Panel(box);
        EditorUI.Text("CHOOSE ENEMY", box.X + 24 * s, box.Y + 20 * s, 18, EditorUI.Fg, true);
        EditorUI.Text("Every enemy used by the original levels. Bosses are marked and listed first.",
            box.X + 24 * s, box.Y + 50 * s, 11, EditorUI.Muted);

        var list = new Rectangle(box.X + 20 * s, box.Y + 80 * s, box.Width - 40 * s, box.Height - 144 * s);
        int rowH = (int)(46 * s);
        int count = enemyPresets?.Count ?? 0;
        int maxScroll = Math.Max(0, count * rowH - (int)list.Height);
        if (EditorUI.Hover(list))
        {
            float wheel = Raylib.GetMouseWheelMove();
            if (wheel != 0) enemyScroll = Math.Clamp(enemyScroll - (int)(wheel * 54 * s), 0, maxScroll);
        }
        enemyScroll = Math.Clamp(enemyScroll, 0, maxScroll);

        if (count == 0)
        {
            EditorUI.TextCentered("NO ENEMIES FOUND IN THE ORIGINAL FILES",
                new Rectangle(list.X, list.Y + list.Height / 2f - 14 * s, list.Width, 28 * s), 13, EditorUI.Muted);
        }
        else
        {
            var mouse = Raylib.GetMousePosition();
            Raylib.BeginScissorMode((int)list.X, (int)list.Y, (int)list.Width, (int)list.Height);
            for (int i = 0; i < count; i++)
            {
                var p = enemyPresets![i];
                var row = new Rectangle(list.X, list.Y + i * rowH - enemyScroll, list.Width, rowH - 3 * s);
                if (row.Y + row.Height < list.Y || row.Y > list.Y + list.Height) continue;

                bool hover = Raylib.CheckCollisionPointRec(mouse, row);
                bool selected = enemySpawn != null && enemySpawn.key == p.key && p.key.Length > 0;
                if (hover) Raylib.DrawRectangleRounded(row, 0.18f, 6, EditorUI.Panel2);
                else if (i % 2 == 0) Raylib.DrawRectangleRounded(row, 0.18f, 6, new Color(22, 24, 29, 150));
                if (selected) Raylib.DrawRectangleRoundedLinesEx(row, 0.18f, 6, Math.Max(1f, s), EditorUI.Accent);

                var prev = new Rectangle(row.X + 8 * s, row.Y + 4 * s, 34 * s, 34 * s);
                Raylib.DrawRectangleRec(prev, EditorUI.PanelBg);
                DrawSpritePreview(p.SpriteNumber, new Rectangle(prev.X + 2 * s, prev.Y + 2 * s, prev.Width - 4 * s, prev.Height - 4 * s));
                Raylib.DrawRectangleLinesEx(prev, Math.Max(1f, s), EditorUI.Line);

                string title = (p.IsBoss ? "BOSS   " : "") +
                    $"#{p.SpriteNumber:000}   TYPE {p.type}   {EnemyTypeName(p.type)}";
                EditorUI.Text(title, row.X + 52 * s, row.Y + 4 * s, 13,
                    selected ? EditorUI.Accent : p.IsBoss ? EditorUI.Danger : EditorUI.Fg, true);
                EditorUI.Text($"POWER {p.power}   SPEED {p.initspeedX}   RANGE X {p.rangeX}   Y {p.rangeY}   FROM {p.source}",
                    row.X + 52 * s, row.Y + 22 * s, 9, EditorUI.Muted);
                if (selected)
                    EditorUI.TextRight("USING", row.X + row.Width - 8 * s, row.Y + 15 * s, 9, EditorUI.Accent, true);

                if (hover)
                {
                    Raylib.SetMouseCursor(MouseCursor.PointingHand);
                    if (Raylib.IsMouseButtonPressed(MouseButton.Left))
                    {
                        enemySpawn = p;
                        enemyOpen = false;
                        ShowToast("ENEMY SELECTED: " + EnemyShortLabel(p) + " " + EnemyTypeName(p.type));
                        Raylib.EndScissorMode();
                        return;
                    }
                }
            }
            Raylib.EndScissorMode();
        }

        float by = box.Y + box.Height - 46 * s;
        var bCancel = new Rectangle(box.X + box.Width - 130 * s, by, 106 * s, 30 * s);
        if (EditorUI.Button(bCancel, "CANCEL", false, 11, true)) enemyOpen = false;
    }

    static void DrawObjectModal()
    {
        EnsureObjectPresets();
        float s = EditorUI.Scale;
        int sw = Raylib.GetScreenWidth(), sh = Raylib.GetScreenHeight();
        float bw = Math.Min(640 * s, sw - 24 * s);
        float bh = Math.Min(500 * s, sh - 24 * s);
        var box = new Rectangle(sw / 2f - bw / 2f, sh / 2f - bh / 2f, bw, bh);

        Raylib.DrawRectangle(0, 0, sw, sh, new Color(0, 0, 0, 175));
        EditorUI.Panel(box);
        EditorUI.Text("CHOOSE OBJECT", box.X + 24 * s, box.Y + 20 * s, 18, EditorUI.Fg, true);
        EditorUI.Text("All objects supported by the engine. New objects will use this sprite and behaviour.",
            box.X + 24 * s, box.Y + 50 * s, 11, EditorUI.Muted);

        var list = new Rectangle(box.X + 20 * s, box.Y + 80 * s, box.Width - 40 * s, box.Height - 144 * s);
        int rowH = (int)(46 * s);
        int count = objectPresets?.Count ?? 0;
        int maxScroll = Math.Max(0, count * rowH - (int)list.Height);
        if (EditorUI.Hover(list))
        {
            float wheel = Raylib.GetMouseWheelMove();
            if (wheel != 0) objectScroll = Math.Clamp(objectScroll - (int)(wheel * 54 * s), 0, maxScroll);
        }
        objectScroll = Math.Clamp(objectScroll, 0, maxScroll);

        if (count == 0)
        {
            EditorUI.TextCentered("NO OBJECTS FOUND",
                new Rectangle(list.X, list.Y + list.Height / 2f - 14 * s, list.Width, 28 * s), 13, EditorUI.Muted);
        }
        else
        {
            var mouse = Raylib.GetMousePosition();
            Raylib.BeginScissorMode((int)list.X, (int)list.Y, (int)list.Width, (int)list.Height);
            for (int i = 0; i < count; i++)
            {
                var p = objectPresets![i];
                var row = new Rectangle(list.X, list.Y + i * rowH - objectScroll, list.Width, rowH - 3 * s);
                if (row.Y + row.Height < list.Y || row.Y > list.Y + list.Height) continue;

                bool hover = Raylib.CheckCollisionPointRec(mouse, row);
                bool selected = objectSpawn != null && objectSpawn.key == p.key && p.key.Length > 0;
                if (hover) Raylib.DrawRectangleRounded(row, 0.18f, 6, EditorUI.Panel2);
                else if (i % 2 == 0) Raylib.DrawRectangleRounded(row, 0.18f, 6, new Color(22, 24, 29, 150));
                if (selected) Raylib.DrawRectangleRoundedLinesEx(row, 0.18f, 6, Math.Max(1f, s), EditorUI.Accent);

                var prev = new Rectangle(row.X + 8 * s, row.Y + 4 * s, 34 * s, 34 * s);
                Raylib.DrawRectangleRec(prev, EditorUI.PanelBg);
                DrawSpritePreview(p.SpriteNumber, new Rectangle(prev.X + 2 * s, prev.Y + 2 * s, prev.Width - 4 * s, prev.Height - 4 * s));
                Raylib.DrawRectangleLinesEx(prev, Math.Max(1f, s), EditorUI.Line);

                EditorUI.Text($"OBJECT {p.SpriteNumber:000}", row.X + 52 * s, row.Y + 4 * s, 13,
                    selected ? EditorUI.Accent : EditorUI.Fg, true);
                EditorUI.Text($"{ObjectBehavior(p)}   FROM {p.source}",
                    row.X + 52 * s, row.Y + 22 * s, 9, EditorUI.Muted);
                if (selected)
                    EditorUI.TextRight("USING", row.X + row.Width - 8 * s, row.Y + 15 * s, 9, EditorUI.Accent, true);

                if (hover)
                {
                    Raylib.SetMouseCursor(MouseCursor.PointingHand);
                    if (Raylib.IsMouseButtonPressed(MouseButton.Left))
                    {
                        objectSpawn = p;
                        objectOpen = false;
                        ShowToast("OBJECT SELECTED: " + ObjectShortLabel(p));
                        Raylib.EndScissorMode();
                        return;
                    }
                }
            }
            Raylib.EndScissorMode();
        }

        float by = box.Y + box.Height - 46 * s;
        var bCancel = new Rectangle(box.X + box.Width - 130 * s, by, 106 * s, 30 * s);
        if (EditorUI.Button(bCancel, "CANCEL", false, 11, true)) objectOpen = false;
    }

    static void DeleteCustomLevel(string file)
    {
        bool current = currentFile != null && currentFile.Equals(file, StringComparison.OrdinalIgnoreCase);
        int oldIndex = index;
        try
        {
            File.Delete(EditorCfg.CustomLevelPath(file));
        }
        catch (Exception ex)
        {
            ShowToast("REMOVE FAILED: " + ex.Message);
            deleteOpen = false;
            deleteFile = null;
            return;
        }

        deleteOpen = false;
        deleteFile = null;
        if (current) ClearLevel();
        RefreshCustomFiles();
        if (current && customFiles.Count > 0)
        {
            int next = Math.Clamp(oldIndex, 0, customFiles.Count - 1);
            LoadLevel(next);
        }
        ShowToast("REMOVED " + file + " FROM CustomLevels");
    }

    static void DrawDeleteModal()
    {
        if (deleteFile == null) { deleteOpen = false; return; }
        string file = deleteFile;
        float s = EditorUI.Scale;
        int sw = Raylib.GetScreenWidth(), sh = Raylib.GetScreenHeight();
        float bw = Math.Min(460 * s, sw - 24 * s);
        float bh = Math.Min(220 * s, sh - 24 * s);
        var box = new Rectangle(sw / 2f - bw / 2f, sh / 2f - bh / 2f, bw, bh);

        Raylib.DrawRectangle(0, 0, sw, sh, new Color(0, 0, 0, 175));
        EditorUI.Panel(box);
        EditorUI.Text("REMOVE CUSTOM LEVEL", box.X + 24 * s, box.Y + 22 * s, 18, EditorUI.Fg, true);
        EditorUI.Text("Delete " + file + ".bin from CustomLevels?",
            box.X + 24 * s, box.Y + 54 * s, 12, EditorUI.Fg);
        EditorUI.Text("This cannot be undone without importing it again.",
            box.X + 24 * s, box.Y + 76 * s, 11, EditorUI.Muted);
        if (currentFile != null && currentFile.Equals(file, StringComparison.OrdinalIgnoreCase) && dirty)
            EditorUI.Text("Unsaved changes in this level will be lost.", box.X + 24 * s, box.Y + 98 * s, 11, EditorUI.Danger);

        float gap = 8 * s;
        float bwid = (box.Width - 48 * s - gap) / 2f;
        float by = box.Y + box.Height - 56 * s;
        var bCancel = new Rectangle(box.X + 24 * s, by, bwid, 32 * s);
        var bDelete = new Rectangle(bCancel.X + bwid + gap, by, bwid, 32 * s);
        if (EditorUI.Button(bCancel, "CANCEL", false, 11, true))
        {
            deleteOpen = false;
            deleteFile = null;
        }
        if (EditorUI.DangerButton(bDelete, "REMOVE", 11, true))
            DeleteCustomLevel(file);
    }

    static void DrawConfirmModal()
    {
        float s = EditorUI.Scale;
        int sw = Raylib.GetScreenWidth(), sh = Raylib.GetScreenHeight();
        float bw = Math.Min(440 * s, sw - 24 * s);
        float bh = Math.Min(210 * s, sh - 24 * s);
        var box = new Rectangle(sw / 2f - bw / 2f, sh / 2f - bh / 2f, bw, bh);

        Raylib.DrawRectangle(0, 0, sw, sh, new Color(0, 0, 0, 175));
        EditorUI.Panel(box);
        EditorUI.Text("UNSAVED CHANGES", box.X + 24 * s, box.Y + 22 * s, 18, EditorUI.Fg, true);
        EditorUI.Text("Save this level in CustomLevels before continuing?", box.X + 24 * s, box.Y + 54 * s, 12, EditorUI.Muted);

        float gap = 8 * s;
        float bwid = (box.Width - 48 * s - gap * 2) / 3f;
        float by = box.Y + box.Height - 56 * s;
        var bSave = new Rectangle(box.X + 24 * s, by, bwid, 32 * s);
        var bDiscard = new Rectangle(bSave.X + bwid + gap, by, bwid, 32 * s);
        var bCancel = new Rectangle(bDiscard.X + bwid + gap, by, bwid, 32 * s);

        if (EditorUI.Button(bSave, "SAVE", true, 11, true))
        {
            if (SaveCustom()) CompleteConfirm();
        }
        if (EditorUI.Button(bDiscard, "DISCARD", false, 11, true))
        {
            if (confirm == 1) LoadLevel(index); // descarta de verdad al salir
            dirty = false;
            CompleteConfirm();
        }
        if (EditorUI.Button(bCancel, "CANCEL", false, 11, true))
        {
            confirm = 0;
            pendingLevel = -1;
        }
    }

    // ------------------------------------------------------------------ composicion
    static void ComposeIfNeeded(int cx, int cy, int anim)
    {
        int w = Math.Max(1, (int)Math.Ceiling(rView.Width / zoom));
        int h = Math.Max(1, (int)Math.Ceiling(rView.Height / zoom));
        if (levelTex.Id == 0 || w != frameW || h != frameH)
        {
            if (levelTex.Id != 0) Raylib.UnloadTexture(levelTex);
            frameW = w;
            frameH = h;
            frame = new Color[w * h];
            Image img = Raylib.GenImageColor(w, h, Color.Black);
            levelTex = Raylib.LoadTextureFromImage(img);
            Raylib.UnloadImage(img);
            Raylib.SetTextureFilter(levelTex, TextureFilter.Point);
            needCompose = true;
        }

        if (!needCompose && anim == lastAnim && cx == lastCx && cy == lastCy) return;

        Array.Fill(frame, EditorUI.Bg);
        if (showTiles) ComposeTiles(cx, cy, anim);
        if (showElevators && level != null) foreach (var el in level.elevator) DrawSprite(el.sprite, cx, cy);
        if (showObjects && level != null) foreach (var o in level.@object) DrawSprite(o.sprite, cx, cy);
        if (showNmi && level != null) foreach (var en in level.enemy) DrawSprite(en.sprite, cx, cy);
        if (level != null) DrawSprite(level.player.sprite, cx, cy);

        Raylib.UpdateTexture(levelTex, frame);
        lastAnim = anim;
        lastCx = cx;
        lastCy = cy;
        needCompose = false;
    }

    static void ComposeTiles(int cx, int cy, int anim)
    {
        var lv = level!;
        for (int y = 0; y < frameH; y++)
        {
            int wy = cy + y, ty = wy >> 4;
            if (wy < 0 || ty >= lv.height || ty >= lv.tilemap.Length) continue;
            byte[] row = lv.tilemap[ty];
            for (int x = 0; x < frameW; x++)
            {
                int wx = cx + x, tx = wx >> 4;
                if (wx < 0 || tx >= lv.width || tx >= row.Length) continue;
                var tile = lv.tile[row[tx]];
                byte a = tile.animation[anim];
                byte c = lv.tile[a].tiledata[(wy & 15) * 16 + (wx & 15)];
                frame[y * frameW + x] = new Color(lv.palette[c, 0], lv.palette[c, 1], lv.palette[c, 2], (byte)255);
            }
        }
    }

    // Igual que Game.DisplaySprite pero dentro de la textura del visor
    static void DrawSprite(TITUS_sprite spr, int cx, int cy)
    {
        if (!spr.enabled || spr.spritedata == null || level == null) return;
        int w = Original.SPRITEWIDTH[spr.number], h = Original.SPRITEHEIGHT[spr.number];
        int dx = spr.x - spr.spritedata.refwidth - cx;
        int dy = spr.y + spr.spritedata.refheight - h + 1 - cy;
        if (dx >= frameW || dx + w < 0 || dy + h < 0 || dy >= frameH) return;

        byte[] d = spr.spritedata.data!;
        for (int y = 0; y < h; y++)
        {
            int py = dy + y;
            if (py < 0 || py >= frameH) continue;
            for (int x = 0; x < w; x++)
            {
                int px = dx + x;
                if (px < 0 || px >= frameW) continue;
                byte c = d[y * w + (spr.flipped ? w - 1 - x : x)];
                if (c == 0) continue;
                byte p = (byte)(c & 15);
                frame[py * frameW + px] = new Color(level.palette[p, 0], level.palette[p, 1], level.palette[p, 2], (byte)255);
            }
        }
    }

    // ------------------------------------------------------------------ marcadores
    static float SX(float wx, int cx) => rView.X + (wx - cx) * zoom;
    static float SY(float wy, int cy) => rView.Y + (wy - cy) * zoom;

    static void DrawMarkers(int cx, int cy)
    {
        var lv = level!;
        bool blink = ((Environment.TickCount64 / 300) & 1) == 0;

        if (showBonus && (blink || editMode))
            foreach (var b in lv.bonus)
                if (b.exists)
                {
                    float s = Math.Max(3f, 2.5f * zoom);
                    Raylib.DrawRectangleRec(new Rectangle(SX(b.x * 16 + 8, cx) - s / 2, SY(b.y * 16 + 8, cy) - s / 2, s, s),
                        new Color(255, 214, 40, 235));
                }

        if (showGates)
        {
            var ent = new Color(255, 82, 82, 240);
            var ext = new Color(82, 255, 128, 240);
            var scr = new Color(255, 220, 0, 235);
            float r = Math.Max(2f, 2f * zoom);
            for (int gi = 0; gi < lv.gate.Length; gi++)
            {
                var g = lv.gate[gi];
                if (!g.exists) continue;
                float x1 = SX(g.entranceX * 16 + 8, cx), y1 = SY(g.entranceY * 16 + 8, cy);
                float x2 = SX(g.exitX * 16 + 8, cx), y2 = SY(g.exitY * 16 + 8, cy);
                Raylib.DrawLineEx(new Vector2(x1, y1), new Vector2(x2, y2), Math.Max(1f, zoom * 0.8f), new Color(255, 90, 90, 95));
                Raylib.DrawCircleV(new Vector2(x1, y1), r, ent);
                Raylib.DrawCircleV(new Vector2(x2, y2), r, ext);
                float s = Math.Max(4f, 3f * zoom);
                Raylib.DrawRectangleRec(new Rectangle(SX(g.screenX * 16 + 8, cx) - s / 2, SY(g.screenY * 16 + 8, cy) - s / 2, s, s), scr);

                if (editMode)
                {
                    float ring = r + Math.Max(1.5f, 1.5f * EditorUI.Scale);
                    var rc = selLayer == 5 && selIndex == gi ? Color.White : new Color(255, 255, 255, 110);
                    Raylib.DrawCircleLinesV(new Vector2(x1, y1), ring, rc);
                    Raylib.DrawCircleLinesV(new Vector2(x2, y2), ring, rc);
                    Raylib.DrawRectangleLinesEx(
                        new Rectangle(SX(g.screenX * 16 + 8, cx) - s / 2 - 2, SY(g.screenY * 16 + 8, cy) - s / 2 - 2, s + 4, s + 4),
                        Math.Max(1f, EditorUI.Scale), rc);
                }
            }
        }

        // Inicio (cyan) y meta (bandera blanca)
        float startR = Math.Max(3.5f, 2.2f * zoom);
        Raylib.DrawCircleV(new Vector2(SX(lv.player.initX, cx), SY(lv.player.initY, cy)), startR, new Color(0, 220, 255, 120));
        Raylib.DrawCircleLinesV(new Vector2(SX(lv.player.initX, cx), SY(lv.player.initY, cy)), startR, new Color(0, 220, 255, 255));
        float fx = SX(lv.finishX, cx), fy = SY(lv.finishY, cy);
        Raylib.DrawRectangleRec(new Rectangle(fx, fy - 16 * zoom, Math.Max(1f, zoom), 17 * zoom), Color.White);
        Raylib.DrawRectangleRec(new Rectangle(fx, fy - 16 * zoom, 9 * zoom, 6 * zoom), new Color(255, 255, 255, 235));

        if (editMode && selLayer >= 2 && selLayer <= 4 && selIndex >= 0)
        {
            int sx = 0, sy = 0;
            bool has = false;
            if (selLayer == 2 && selIndex < lv.enemy.Length) { sx = lv.enemy[selIndex].initX; sy = lv.enemy[selIndex].initY; has = lv.enemy[selIndex].init_enabled; }
            else if (selLayer == 3 && selIndex < lv.@object.Length) { sx = lv.@object[selIndex].initX; sy = lv.@object[selIndex].initY; has = lv.@object[selIndex].init_enabled; }
            else if (selLayer == 4 && selIndex < lv.elevator.Length) { sx = lv.elevator[selIndex].initX; sy = lv.elevator[selIndex].initY; has = lv.elevator[selIndex].init_enabled; }
            if (has)
                Raylib.DrawCircleLinesV(new Vector2(SX(sx, cx), SY(sy, cy)),
                    Math.Max(8f, 7f * zoom), new Color(255, 255, 255, 220));
        }

        if (showSolid) DrawSolidity(cx, cy);
    }

    // Solidez: marca en el centro de cada tile sus flags de pared/suelo/techo/escalera...
    static void DrawSolidity(int cx, int cy)
    {
        var lv = level!;
        int tx0 = cx >> 4, ty0 = cy >> 4;
        int tx1 = (cx + frameW - 1) >> 4, ty1 = (cy + frameH - 1) >> 4;
        float s = Math.Max(2f, zoom * 1.6f);
        for (int ty = ty0; ty <= ty1; ty++)
        {
            if (ty < 0 || ty >= lv.height) continue;
            for (int tx = tx0; tx <= tx1; tx++)
            {
                if (tx < 0 || tx >= lv.width) continue;
                var t = lv.tile[lv.tilemap[ty][tx]];
                Color? c = null;
                if (t.floorflag == 6) c = new Color(255, 220, 0, 220);        // escalera
                else if (t.floorflag == 8) c = new Color(0, 160, 255, 220);   // agua
                else if (t.floorflag == 9) c = new Color(255, 120, 0, 220);   // fuego
                else if (t.horizflag == 1) c = new Color(255, 255, 255, 220); // pared
                else if (t.horizflag == 3) c = new Color(255, 60, 60, 220);   // mortal
                else if (t.horizflag == 5) c = new Color(220, 80, 255, 220);  // candado
                else if (t.ceilflag != 0) c = new Color(0, 255, 200, 150);    // techo
                if (!c.HasValue) continue;
                float x = SX(tx * 16 + 8, cx), y = SY(ty * 16 + 8, cy);
                Raylib.DrawRectangleRec(new Rectangle(x - s / 2, y - s / 2, s, s), c.Value);
            }
        }
    }

    static void DrawGrid(int cx, int cy)
    {
        if (zoom < 2) return;
        var line = new Color(255, 255, 255, 16);
        int tx0 = cx >> 4, tx1 = (cx + frameW) >> 4;
        int ty0 = cy >> 4, ty1 = (cy + frameH) >> 4;
        for (int tx = tx0; tx <= tx1; tx++)
        {
            float x = SX(tx * 16, cx);
            Raylib.DrawLineEx(new Vector2(x, rView.Y), new Vector2(x, rView.Y + rView.Height), Math.Max(1f, EditorUI.Scale), line);
        }
        for (int ty = ty0; ty <= ty1; ty++)
        {
            float y = SY(ty * 16, cy);
            Raylib.DrawLineEx(new Vector2(rView.X, y), new Vector2(rView.X + rView.Width, y), Math.Max(1f, EditorUI.Scale), line);
        }
    }

    static void DrawHoverTile(int cx, int cy, Vector2 mouse)
    {
        var lv = level!;
        int wx = cx + (int)Math.Floor((mouse.X - rView.X) / zoom);
        int wy = cy + (int)Math.Floor((mouse.Y - rView.Y) / zoom);
        int tx = wx >> 4, ty = wy >> 4;
        if (tx < 0 || tx >= lv.width || ty < 0 || ty >= lv.height) return;

        byte t = lv.tilemap[ty][tx];
        hoverInfo = $"TILE {tx:000}, {ty:000}   ID {t:000}   {TileFlags(t)}";

        var mr = new Rectangle(rView.X + (tx * 16 - cx) * zoom, rView.Y + (ty * 16 - cy) * zoom, 16 * zoom, 16 * zoom);
        Raylib.DrawRectangleRec(mr, new Color(228, 168, 58, 24));
        Raylib.DrawRectangleLinesEx(mr, Math.Max(1f, 2f * EditorUI.Scale), EditorUI.Accent);
    }

    static void DrawEditHover(int cx, int cy)
    {
        if (level == null || !TileUnder(Raylib.GetMousePosition(), out int tx, out int ty)) return;
        var mr = new Rectangle(rView.X + (tx * 16 - cx) * zoom, rView.Y + (ty * 16 - cy) * zoom, 16 * zoom, 16 * zoom);
        float s = EditorUI.Scale;

        if (markerTool != 0)
        {
            var c = markerTool == 1 ? new Color(0, 220, 255, 230) : Color.White;
            float r = Math.Max(4f, 3f * zoom);
            Raylib.DrawCircleLinesV(new Vector2(mr.X + mr.Width / 2, mr.Y + mr.Height / 2), r, c);
            return;
        }

        if (editLayer == 0 && tool == 0 && tileAtlas.Id != 0)
        {
            Raylib.DrawTexturePro(tileAtlas,
                new Rectangle((selectedTile % 16) * 16, (selectedTile / 16) * 16, 16, 16),
                new Rectangle(mr.X, mr.Y, mr.Width, mr.Height), Vector2.Zero, 0f, new Color(255, 255, 255, 120));
            Raylib.DrawRectangleLinesEx(mr, Math.Max(1f, s), new Color(255, 220, 120, 220));
        }
        else if (editLayer == 0 && tool == 1)
        {
            Raylib.DrawRectangleRec(mr, new Color(220, 80, 70, 45));
            Raylib.DrawRectangleLinesEx(mr, Math.Max(1f, s), EditorUI.Danger);
        }
        else if (editLayer >= 2 && editLayer <= 4 && tool == 0)
        {
            Raylib.DrawRectangleLinesEx(mr, Math.Max(1f, 1.5f * s), new Color(255, 255, 255, 90));
        }
    }

    // ------------------------------------------------------------------ minimapa
    static void MinimapGeometry()
    {
        if (level == null) return;
        float s = EditorUI.Scale;
        int padX = (int)(8 * s), padTop = (int)(22 * s), padBottom = (int)(8 * s);
        int iw = Math.Max(1, (int)rMinimap.Width - padX * 2);
        int ih = Math.Max(1, (int)rMinimap.Height - padTop - padBottom);
        mapBlock = Math.Max(1, (int)Math.Ceiling(level.width / (double)iw));
        mapBlock = Math.Max(mapBlock, (int)Math.Ceiling(level.height / (double)ih));
        mapW = (level.width + mapBlock - 1) / mapBlock;
        mapH = (level.height + mapBlock - 1) / mapBlock;
        mapX0 = (int)(rMinimap.X + padX + (iw - mapW) / 2f);
        mapY0 = (int)(rMinimap.Y + padTop + (ih - mapH) / 2f);
    }

    static void DrawMinimap(int cx, int cy)
    {
        var lv = level!;
        Raylib.DrawRectangleRounded(rMinimap, 0.08f, 8, new Color(17, 19, 24, 235));
        Raylib.DrawRectangleRoundedLinesEx(rMinimap, 0.08f, 8, 1f, new Color(255, 255, 255, 28));
        EditorUI.Text("MAP", rMinimap.X + 10 * EditorUI.Scale, rMinimap.Y + 5 * EditorUI.Scale, 9, EditorUI.Muted, true);
        Raylib.DrawRectangleRec(new Rectangle(mapX0, mapY0, mapW, mapH), new Color(8, 9, 12, 255));

        var solid = new Color(158, 166, 178, 255);
        for (int ty = 0; ty < lv.height; ty += mapBlock)
        {
            for (int tx = 0; tx < lv.width; tx += mapBlock)
            {
                var t = lv.tile[lv.tilemap[ty][tx]];
                bool isSolid = t.horizflag == 1 || t.floorflag == 1 || t.floorflag == 6;
                if (isSolid) Raylib.DrawRectangle(mapX0 + tx / mapBlock, mapY0 + ty / mapBlock, 1, 1, solid);
            }
        }

        foreach (var b in lv.bonus)
            if (b.exists && b.x < lv.width && b.y < lv.height)
                Raylib.DrawRectangle(mapX0 + b.x / mapBlock, mapY0 + b.y / mapBlock, 1, 1, new Color(255, 214, 40, 255));

        Raylib.DrawRectangle(mapX0 + lv.player.initX / 16 / mapBlock, mapY0 + lv.player.initY / 16 / mapBlock, 2, 2, new Color(0, 220, 255, 255));
        Raylib.DrawRectangle(mapX0 + lv.finishX / 16 / mapBlock, mapY0 + lv.finishY / 16 / mapBlock, 2, 2, Color.White);

        int vx = mapX0 + cx / 16 / mapBlock;
        int vy = mapY0 + cy / 16 / mapBlock;
        int vw = Math.Max(2, (int)(rView.Width / zoom) / 16 / mapBlock);
        int vh = Math.Max(2, (int)(rView.Height / zoom) / 16 / mapBlock);
        Raylib.DrawRectangleLines(vx, vy, vw, vh, new Color(255, 220, 70, 255));
    }

    // ------------------------------------------------------------------ barra estado
    static void DrawStatusBar()
    {
        int sw = Raylib.GetScreenWidth();
        Raylib.DrawRectangle(0, (int)rStatus.Y, sw, (int)rStatus.Height, new Color(20, 22, 27, 255));
        Raylib.DrawRectangle(0, (int)rStatus.Y, sw, 1, EditorUI.Line);

        float s = EditorUI.Scale;
        string left = hoverInfo;
        if (left.Length == 0 && level != null)
        {
            left = editMode
                ? $"EDIT {LayerName(editLayer)}   TOOL {ToolName()}   TILE {selectedTile:000}   CAM {camX:0000}, {camY:0000}"
                : $"LEVEL {index + 1:00}/{customFiles.Count:00}   CAM {camX:0000}, {camY:0000}   ZOOM {zoom}X";
        }
        else if (left.Length == 0 && customFiles.Count == 0)
            left = "NO CUSTOM LEVELS - USE NEW LEVEL OR IMPORT LEVEL";
        EditorUI.Text(left, 16 * s, rStatus.Y + 9 * s, 12, EditorUI.Muted);

        bool hasToast = toast.Length > 0 && Environment.TickCount64 < toastUntil;
        string right = hasToast ? toast
            : statusHint.Length > 0 ? statusHint
            : editMode ? "Left paint / drag   Right erase   Middle or Space pans   F5 test   Ctrl+S save"
            : "Drag to pan   Wheel to zoom   F5 test   F1 help";
        Color rc = hasToast ? EditorUI.Accent : statusHint.Length > 0 ? EditorUI.Accent : EditorUI.Muted;
        EditorUI.TextRight(right, sw - 16 * s, rStatus.Y + 9 * s, 12, rc);
    }

    // ------------------------------------------------------------------ ayuda
    static void DrawHelpModal()
    {
        float s = EditorUI.Scale;
        int sw = Raylib.GetScreenWidth(), sh = Raylib.GetScreenHeight();
        float bw = Math.Min(620 * s, sw - 24 * s);
        float bh = Math.Min(430 * s, sh - 24 * s);
        var box = new Rectangle(sw / 2f - bw / 2f, sh / 2f - bh / 2f, bw, bh);

        // El click que abrio la ayuda no debe cerrarla en el mismo frame.
        // Se ignoran tambien los clicks de los 200 ms siguientes para absorber un doble click.
        bool ignoreClick = helpJustOpened || Environment.TickCount64 - helpOpenedAt < 200;
        helpJustOpened = false;

        Raylib.DrawRectangle(0, 0, sw, sh, new Color(0, 0, 0, 175));
        EditorUI.Panel(box);

        EditorUI.Text("CONTROLS", box.X + 24 * s, box.Y + 20 * s, 20, EditorUI.Fg, true);
        EditorUI.Text("Everything can be done with the mouse. Keyboard shortcuts still work.",
            box.X + 24 * s, box.Y + 48 * s, 12, EditorUI.Muted);

        float col1 = box.X + 28 * s, col2 = box.X + 330 * s, y = box.Y + 84 * s;
        EditorUI.Text("MOUSE", col1, y, 11, EditorUI.Accent, true);
        EditorUI.Text("KEYBOARD", col2, y, 11, EditorUI.Accent, true);
        y += 24 * s;

        string[] mouse =
        {
            "Left drag paints / moves / drags",
            "Right drag erases",
            "Middle or Space + drag pans",
            "Wheel over the map zooms",
            "Click a layer row to edit it",
            "CHANGE picks enemy / object (bosses first)",
            "TEST plays the current level",
            "NEW / IMPORT add levels here",
            "X on a row removes that custom level",
        };
        string[] keys =
        {
            "Tab - view / edit mode",
            "1..8 - layer visibility, G - grid",
            "Arrows / WASD - pan, Shift faster",
            "PgUp / PgDn - change level",
            "Home - center on start",
            "N / I - new / import",
            "F5 - test the current level",
            "Delete - remove selected level",
            "Ctrl+S - save, Esc - back",
        };
        for (int i = 0; i < mouse.Length; i++)
        {
            EditorUI.Text("- " + mouse[i], col1, y + i * 26 * s, 13, EditorUI.Fg);
            EditorUI.Text("- " + keys[i], col2, y + i * 26 * s, 13, EditorUI.Fg);
        }

        float ly = box.Y + box.Height - 78 * s;
        Raylib.DrawRectangle((int)(box.X + 24 * s), (int)ly, (int)(box.Width - 48 * s), 1, EditorUI.Line);
        float lx = col1;
        void Legend(string text, Color c)
        {
            EditorUI.Text(text, lx, ly + 18 * s, 10, c, true);
            lx += EditorUI.TextW(text, 10, true) + 26 * s;
        }
        Legend("START", new Color(0, 220, 255, 255));
        Legend("GATE IN", new Color(255, 82, 82, 255));
        Legend("GATE OUT", new Color(82, 255, 128, 255));
        Legend("FINISH", Color.White);
        Legend("BONUS", new Color(255, 214, 40, 255));

        var rb = new Rectangle(box.X + box.Width - 94 * s, box.Y + 18 * s, 70 * s, 30 * s);
        var mousePos = Raylib.GetMousePosition();
        bool outside = !ignoreClick && Raylib.IsMouseButtonPressed(MouseButton.Left) && !Raylib.CheckCollisionPointRec(mousePos, box);
        if (EditorUI.Button(rb, "CLOSE", false, 12, true) && !ignoreClick) help = false;
        if (outside) help = false;
    }

    static void Unload()
    {
        if (levelTex.Id != 0)
        {
            Raylib.UnloadTexture(levelTex);
            levelTex = default;
            frameW = frameH = 0;
            frame = Array.Empty<Color>();
        }
        if (tileAtlas.Id != 0)
        {
            Raylib.UnloadTexture(tileAtlas);
            tileAtlas = default;
        }
        if (newPreviewTex.Id != 0)
        {
            Raylib.UnloadTexture(newPreviewTex);
            newPreviewTex = default;
            newPreviewIndex = -1;
        }
        EditorUI.Unload();
    }
}
