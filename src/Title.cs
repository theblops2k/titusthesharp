using Raylib_cs;
using System.Numerics;

namespace TitusTheFox;

// Puerto de viewimage.c (formato 2: 256 colores, paleta en el fichero, valor*4)
public static class Title
{
    public const int W = 320, H = 200;
    const int FadeTime = 1000; // ms, viewimage.c:64

    // Carga una imagen SQZ de formato 2 como textura 320x200 (viewimage.c:108-125)
    public static Texture2D LoadFormat2(string path)
    {
        byte[] d = File.ReadAllBytes(path); // FILES\*.bin ya descomprimido
        var pal = new Color[256];
        for (int c = 0; c < 256; c++)
            pal[c] = new Color(
                (byte)Math.Min(255, (d[c * 3] & 0xFF) * 4),
                (byte)Math.Min(255, (d[c * 3 + 1] & 0xFF) * 4),
                (byte)Math.Min(255, (d[c * 3 + 2] & 0xFF) * 4),
                (byte)255);
        var idx = new byte[W * H];
        Array.Copy(d, 256 * 3, idx, 0, idx.Length);
        return MakeTexture(idx, pal);
    }

    // Crea la textura 320x200 (Point) de una imagen fija (indices + paleta)
    static Texture2D MakeTexture(byte[] idx, Color[] pal)
    {
        var px = new Color[W * H];
        for (int i = 0; i < px.Length; i++) px[i] = pal[idx[i]];
        Image img = Raylib.GenImageColor(W, H, Color.Black);
        Texture2D tex = Raylib.LoadTextureFromImage(img);
        Raylib.UnloadImage(img);
        Raylib.SetTextureFilter(tex, TextureFilter.Point);
        Raylib.UpdateTexture(tex, px);
        return tex;
    }

    // Mezcla sobre negro: equivale a SDL_SetAlpha sobre fondo negro
    public static Color FadeTint(int alpha) => new Color((byte)alpha, (byte)alpha, (byte)alpha, (byte)255);

    public static void DrawRegion(Texture2D tex, Rectangle src, int dx, int dy, Color tint)
    {
        int s = Render.SCALE;
        Raylib.DrawTexturePro(tex, src,
            new Rectangle(dx * s, dy * s, src.Width * s, src.Height * s), // destino en coordenadas logicas 320x200
            Vector2.Zero, 0f, tint);
    }

    public static void DrawFull(Texture2D tex, int alpha)
        => DrawRegion(tex, new Rectangle(0, 0, W, H), 0, 0, FadeTint(alpha));

    // viewimage con displayformat 0 (fundido de entrada, espera, fundido de salida).
    // Devuelve false si la ventana se cierra. ESC tambien (viewimage.c:154-157, retval -1 -> salida).
    public static bool View(string path, int delayMs)
    {
        Texture2D tex = LoadFormat2(path);
        bool ok = Run(tex, delayMs);
        Raylib.UnloadTexture(tex);
        return ok;
    }

    // Formato 0: planar 16 colores en grises (viewimage.c:78-100)
    public static Texture2D LoadFormat0(string path)
    {
        byte[] d = File.ReadAllBytes(path); // FILES\*.bin ya descomprimido
        var pal = new Color[16];
        for (int c = 0; c < 16; c++) { byte v = (byte)(c * 16); pal[c] = new Color(v, v, v, (byte)255); }
        var idx = new byte[W * H];
        int g = (W * H) >> 3, p = 0;
        for (int i = 0; i < g; i++)
            for (int j = 7; j >= 0; j--)
                idx[p++] = (byte)(((d[i] >> j) & 1) | (((d[i + g] >> j) << 1) & 2) | (((d[i + g * 2] >> j) << 2) & 4) | (((d[i + g * 3] >> j) << 3) & 8));
        return MakeTexture(idx, pal);
    }

    // viewimage(file, 0, 1, 0): displayformat 1 = visible hasta Enter/Espacio y fundido a negro (viewimage.c:300-363).
    // false si ESC/cerrar ventana (retval < 0).
    public static bool ViewUntilKey(string path)
    {
        if (!File.Exists(path)) return true;
        Texture2D tex = LoadFormat0(path); // titusfinishformat 0 (titus.conf:70)
        try
        {
            while (Raylib.GetKeyPressed() != 0) { }
            while (true)
            {
                if (Raylib.WindowShouldClose()) return false;
                if (Input.Confirm()) break;
                Frame(tex, 255);
            }
            long t0 = Environment.TickCount64;
            int alpha = 0;
            while (alpha < 255)
            {
                if (Raylib.WindowShouldClose()) return false;
                alpha = (int)Math.Min(255, (Environment.TickCount64 - t0) * 256 / FadeTime);
                Frame(tex, 255 - alpha);
            }
            return true;
        }
        finally { Raylib.UnloadTexture(tex); }
    }

    static bool Run(Texture2D tex, int delay)
    {
        long t0 = Environment.TickCount64;
        int alpha = 0, fadeoutskip = 0;
        bool active = true;

        while (alpha < 255 && active) // fundido de entrada
        {
            if (Raylib.WindowShouldClose()) return false; // viewimage.c:154-157
            if (Input.Confirm())
            {
                active = false;
                fadeoutskip = 255 - alpha;
            }
            alpha = (int)Math.Min(255, (Environment.TickCount64 - t0) * 256 / FadeTime);
            Frame(tex, alpha);
        }

        while (active) // visible
        {
            if (Raylib.WindowShouldClose()) return false;
            if (Input.Confirm())
                active = false;
            Frame(tex, 255);
            if (Environment.TickCount64 - t0 + FadeTime >= delay) active = false;
        }

        alpha = 255 - alpha;
        t0 = Environment.TickCount64;
        while (alpha < 255) // fundido a negro
        {
            if (Raylib.WindowShouldClose()) return false; // viewimage.c:211-215
            alpha = (int)Math.Min(255, (Environment.TickCount64 - t0) * 256 / FadeTime + fadeoutskip);
            Frame(tex, 255 - alpha);
        }
        return true;
    }

    public static Action? OnFrame;
    public static int ViewW = W; // ancho logico del frame actual (320 o Scroll.VIEW_W en el nivel)

    public static void Begin()
    {
        OnFrame?.Invoke();
        Raylib.BeginDrawing();
        if (!target.HasValue)
        {
            var rt = Raylib.LoadRenderTexture(Scroll.VIEW_W * Render.SCALE, H * Render.SCALE);
            ApplyScaleFilter(rt.Texture);
            target = rt;
        }
        Raylib.BeginTextureMode(target.Value);
    }

    // Textura logica (W*SCALE x H*SCALE): todo se dibuja aqui y End() la escala a la ventana
    static RenderTexture2D? target;

    static void Present()
    {
        Raylib.EndTextureMode();
        var rt = target!.Value;
        float tw = ViewW * Render.SCALE, th = rt.Texture.Height; // solo la parte usada de la textura
        ViewW = W; // cada frame vuelve a 320 salvo que el nivel (Game.DrawFrame) pida la vista ancha
        float sw = Raylib.GetScreenWidth(), sh = Raylib.GetScreenHeight();
        float k = Math.Min(sw / tw, sh / th);
        float dw = tw * k, dh = th * k;
        Raylib.ClearBackground(Color.Black); // bordes negros (letterbox)
        bool useSh = scaleShader.HasValue;
        if (useSh)
        {
            var shd = scaleShader!.Value;
            Raylib.SetShaderValue(shd, locTex, new System.Numerics.Vector2(rt.Texture.Width, rt.Texture.Height), ShaderUniformDataType.Vec2);
            Raylib.SetShaderValue(shd, locSrc, new System.Numerics.Vector2(tw, th), ShaderUniformDataType.Vec2);
            Raylib.SetShaderValue(shd, locOut, new System.Numerics.Vector2(dw, dh), ShaderUniformDataType.Vec2);
            Raylib.BeginShaderMode(shd);
        }
        Raylib.DrawTexturePro(rt.Texture,
            new Rectangle(0, 0, tw, -th), // RenderTexture esta invertida en Y
            new Rectangle((sw - dw) / 2, (sh - dh) / 2, dw, dh),
            System.Numerics.Vector2.Zero, 0f, Color.White);
        if (useSh) Raylib.EndShaderMode();
        // Scanlines=T: una linea negra semitransparente (1 px) al final de cada fila logica escalada
        if (Config.Scanlines() && dh / H >= 2f)
        {
            float ox = (sw - dw) / 2, oy = (sh - dh) / 2, rowH = dh / H;
            var sc = new Color(0, 0, 0, 89); // alpha ~0.35
            for (int i = 0; i < H; i++)
                Raylib.DrawRectangle((int)ox, (int)(oy + (i + 1) * rowH) - 1, (int)dw, 1, sc);
        }
        // ShowFps=T en pantalla completa: solo el numero, amarillo, esquina superior izquierda
        if (Config.FullScreen() && Config.ShowFps())
            Raylib.DrawText(Fps.ToString(), 8, 4, Math.Max(8, (int)sh / 25), new Color(255, 255, 0, 179));
    }

    // Aplica FullScreen/WideScreen del cfg sin reiniciar: modo ventana, ancho de vista, textura logica y textura del nivel
    public static void ApplyDisplay()
    {
        if (!windowShown) return;
        bool fs = Config.FullScreen();
        if (fs != borderless) { Raylib.ToggleBorderlessWindowed(); borderless = fs; }
        Scroll.VIEW_W = Scroll.TILESCREEN_W;
        if (fs && Config.WideScreen())
        {
            int mon = Raylib.GetCurrentMonitor();
            Scroll.SetViewWidth(Raylib.GetMonitorWidth(mon), Raylib.GetMonitorHeight(mon));
        }
        if (target.HasValue) Raylib.UnloadRenderTexture(target.Value);
        target = null; // Begin() la recrea con VIEW_W*SCALE x H*SCALE
        Render.Resize();
    }

    static void Frame(Texture2D tex, int alpha)
    {
        Begin();
        Raylib.ClearBackground(Color.Black);
        DrawFull(tex, alpha);
        End();
    }

    // Limitador explicito a 60 FPS (independiente de raylib)
    static readonly System.Diagnostics.Stopwatch Clock = System.Diagnostics.Stopwatch.StartNew();
    static long nextTick; static int dbgN; static long dbgT;
    public static int Fps; // frames reales por segundo (contador propio)
    // Frecuencia de dibujo del juego; la logica del nivel sigue a 35 ticks/s como el original
    public static int FpsGame { get; private set; } = Config.Fps();
    public const double LogicHz = 35.0; // ticks/s de logica del original
    public static double LogicScale { get; private set; } = LogicHz / FpsGame; // fraccion de paso de logica por tick de FpsGame
    public static double TickMs { get; private set; } = 1000.0 / FpsGame;

    // Aplica en caliente todas las opciones del cfg (pantalla, Fps, filtro, lista de niveles)
    public static void ApplyOptions()
    {
        ApplyDisplay();
        Audio.ApplyVolume();
        FpsGame = Config.Fps();
        LogicScale = LogicHz / FpsGame;
        TickMs = 1000.0 / FpsGame;
        nextTick = 0; // reinicia el limitador con el nuevo periodo
        if (target.HasValue)
            ApplyScaleFilter(target.Value.Texture);
        Game.RefreshLevels();
    }

    // ---- Filtros de escalado: XBR > Scale3x > SharpBilinear > FSR > Bilinear > Point ----
    const string VS = "#version 330\n" +
        "in vec3 vertexPosition; in vec2 vertexTexCoord; in vec4 vertexColor;\n" +
        "uniform mat4 mvp; out vec2 fragTexCoord; out vec4 fragColor;\n" +
        "void main(){ fragTexCoord=vertexTexCoord; fragColor=vertexColor; gl_Position=mvp*vec4(vertexPosition,1.0); }\n";

    // Sharp bilinear: escala entera (floor) y suaviza solo el borde de cada pixel logico
    const string FsSharp = "#version 330\n" +
        "in vec2 fragTexCoord; in vec4 fragColor; uniform sampler2D texture0; uniform vec4 colDiffuse; out vec4 finalColor;\n" +
        "uniform vec2 texSize; uniform vec2 srcSize; uniform vec2 outSize;\n" +
        "void main(){\n" +
        " float sc = max(1.0, floor(min(outSize.x/srcSize.x, outSize.y/srcSize.y)));\n" +
        " vec2 t = fragTexCoord*texSize; vec2 tf = floor(t); vec2 s = t-tf;\n" +
        " float rr = 0.5-0.5/sc; vec2 cd = s-0.5;\n" +
        " vec2 f = (cd-clamp(cd,-rr,rr))*sc+0.5;\n" +
        " finalColor = texture(texture0,(tf+f)/texSize)*colDiffuse*fragColor; }\n";

    // xBR simplificado: Scale2x/EPX (bordes diagonales por coincidencia de vecinos), no es xBR completo
    const string FsXbr = "#version 330\n" +
        "in vec2 fragTexCoord; in vec4 fragColor; uniform sampler2D texture0; uniform vec4 colDiffuse; out vec4 finalColor;\n" +
        "uniform vec2 texSize; uniform vec2 srcSize; uniform vec2 outSize;\n" +
        "vec4 px(ivec2 p){ p=clamp(p,ivec2(0),ivec2(srcSize)-1); p.y=clamp(p.y,0,int(texSize.y)-1); return texelFetch(texture0,p,0); }\n" +
        "void main(){\n" +
        " vec2 t = fragTexCoord*texSize; ivec2 ip = ivec2(floor(t)); vec2 fr = t-floor(t);\n" +
        " vec4 P=px(ip), N=px(ip+ivec2(0,-1)), S=px(ip+ivec2(0,1)), W=px(ip+ivec2(-1,0)), E=px(ip+ivec2(1,0));\n" +
        " vec4 a = fr.y<0.5 ? N : S; vec4 ao = fr.y<0.5 ? S : N;\n" +
        " vec4 b = fr.x<0.5 ? W : E; vec4 bo = fr.x<0.5 ? E : W;\n" +
        " vec4 r = (a==b && a!=ao && b!=bo) ? a : P;\n" +
        " finalColor = r*colDiffuse*fragColor; }\n";

    // Scale3x (AdvMAME3x): para cada subpixel (3x3) de cada pixel logico usa E y sus 8 vecinos (texelFetch, bordes replicados).
    // lsc = texels de la textura por pixel logico (srcSize.y/200); la regla es la de AdvMAME3x.
    const string FsScale3x = "#version 330\n" +
        "in vec2 fragTexCoord; in vec4 fragColor; uniform sampler2D texture0; uniform vec4 colDiffuse; out vec4 finalColor;\n" +
        "uniform vec2 texSize; uniform vec2 srcSize; uniform vec2 outSize;\n" +
        "float lsc;\n" +
        "vec4 px(ivec2 p){ ivec2 lim=ivec2(floor(srcSize/lsc))-1; lim.y=min(lim.y,int(floor(texSize.y/lsc))-1); p=clamp(p,ivec2(0),lim); return texelFetch(texture0,ivec2((vec2(p)+0.5)*lsc),0); }\n" +
        "bool eq(vec4 a, vec4 b){ return all(equal(a,b)); }\n" +
        "void main(){\n" +
        " lsc = max(1.0, srcSize.y/200.0);\n" +
        " vec2 t = fragTexCoord*texSize/lsc; ivec2 ip = ivec2(floor(t)); vec2 fr = t-floor(t);\n" +
        " ivec2 sp = ivec2(min(floor(fr*3.0), vec2(2.0)));\n" +
        " vec4 A=px(ip+ivec2(-1,-1)), B=px(ip+ivec2(0,-1)), C=px(ip+ivec2(1,-1));\n" +
        " vec4 D=px(ip+ivec2(-1,0)),  E=px(ip),              F=px(ip+ivec2(1,0));\n" +
        " vec4 G=px(ip+ivec2(-1,1)),  H=px(ip+ivec2(0,1)),   I=px(ip+ivec2(1,1));\n" +
        " vec4 r = E;\n" +
        " if(!eq(B,H) && !eq(D,F)){\n" +
        "  if(sp.y==0){\n" +
        "   if(sp.x==0) r = eq(D,B) ? D : E;\n" +
        "   else if(sp.x==1) r = ((eq(D,B)&&!eq(E,C))||(eq(B,F)&&!eq(E,A))) ? B : E;\n" +
        "   else r = eq(B,F) ? F : E;\n" +
        "  } else if(sp.y==1){\n" +
        "   if(sp.x==0) r = ((eq(D,B)&&!eq(E,G))||(eq(D,H)&&!eq(E,A))) ? D : E;\n" +
        "   else if(sp.x==2) r = ((eq(B,F)&&!eq(E,I))||(eq(H,F)&&!eq(E,C))) ? F : E;\n" +
        "  } else {\n" +
        "   if(sp.x==0) r = eq(D,H) ? D : E;\n" +
        "   else if(sp.x==1) r = ((eq(D,H)&&!eq(E,I))||(eq(H,F)&&!eq(E,G))) ? H : E;\n" +
        "   else r = eq(H,F) ? F : E;\n" +
        "  }\n" +
        " }\n" +
        " finalColor = vec4(r.rgb,1.0)*colDiffuse*fragColor; }\n\n";

    // FSR simplificado (inspirado en EASU): reconstruccion Lanczos2 4x4 en pixeles logicos (srcSize.y/200 texels por pixel)
    // con antirringing (clamp al min/max de los 4 vecinos centrales). No es FSR completo (sin deteccion de bordes direccional ni RCAS).
    const string FsFsr = "#version 330\n" +
        "in vec2 fragTexCoord; in vec4 fragColor; uniform sampler2D texture0; uniform vec4 colDiffuse; out vec4 finalColor;\n" +
        "uniform vec2 texSize; uniform vec2 srcSize; uniform vec2 outSize;\n" +
        "float lz(float x){ x=abs(x); if(x<1e-4) return 1.0; if(x>=2.0) return 0.0; float px=3.14159265*x; return 2.0*sin(px)*sin(px*0.5)/(px*px); }\n" +
        "void main(){\n" +
        " float lsc = max(1.0, srcSize.y/200.0); vec2 lim = floor(srcSize/lsc)-1.0; lim.y = min(lim.y, floor(texSize.y/lsc)-1.0);\n" +
        " vec2 t = fragTexCoord*texSize/lsc - 0.5; vec2 b = floor(t); vec2 f = t-b;\n" +
        " vec4 mn = vec4(1e9), mx = vec4(-1e9), sum = vec4(0.0); float ws = 0.0;\n" +
        " for(int j=-1;j<=2;j++) for(int i=-1;i<=2;i++){\n" +
        "  vec2 p = clamp(b+vec2(i,j), vec2(0.0), lim);\n" +
        "  vec4 c = texelFetch(texture0, ivec2((p+0.5)*lsc), 0);\n" +
        "  float w = lz(float(i)-f.x)*lz(float(j)-f.y); sum += c*w; ws += w;\n" +
        "  if(i>=0 && i<=1 && j>=0 && j<=1){ mn=min(mn,c); mx=max(mx,c); } }\n" +
        " vec4 r = clamp(sum/ws, mn, mx);\n" +
        " finalColor = vec4(r.rgb,1.0)*colDiffuse*fragColor; }\n\n";
    // ---- Vibrance (saturacion selectiva) + contraste ligero, en el mismo pase que el filtro de escalado ----
    const string VibrAmount = "0.35"; // fijo: vibrance (sube mas los colores apagados que los vivos)
    const string ContrAmount = "1.05"; // fijo: contraste alrededor de 0.5
    const string VibFn = "float VIBRANCE = " + VibrAmount + "; float CONTRAST = " + ContrAmount + ";\n" +
        "vec3 vibr(vec3 c){\n" +
        " float mx = max(c.r,max(c.g,c.b)); float mn = min(c.r,min(c.g,c.b)); float sat = mx-mn;\n" +
        " float l = dot(c,vec3(0.299,0.587,0.114));\n" +
        " c = mix(vec3(l), c, 1.0 + VIBRANCE*(1.0-sat));\n" +
        " c = (c-0.5)*CONTRAST+0.5; return clamp(c,0.0,1.0); }\n";
    const string FsPlain = "#version 330\n" +
        "in vec2 fragTexCoord; in vec4 fragColor; uniform sampler2D texture0; uniform vec4 colDiffuse; out vec4 finalColor;\n" +
        "void main(){ finalColor = texture(texture0,fragTexCoord)*colDiffuse*fragColor; }\n";

    // main -> main0 y un main nuevo que post-procesa finalColor (despues del escalado)
    static string WithVibrance(string fs) =>
        fs.Replace("void main(){", VibFn + "void main0(){") +
        "void main(){ main0(); finalColor.rgb = vibr(finalColor.rgb); }\n";

    static string curScaler = "";
    static Shader? scaleShader; static string shaderKind = ""; static string failedKind = "";
    static int locTex, locSrc, locOut;

    static void LogShader(string msg)
    {
        try { File.AppendAllText(Path.Combine(AppContext.BaseDirectory, "Log_error.txt"), $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] AVISO shader: {msg}\n"); } catch { }
    }

    static void UnloadScaleShader()
    {
        if (scaleShader.HasValue) Raylib.UnloadShader(scaleShader.Value);
        scaleShader = null; shaderKind = ""; curScaler = "";
    }

    static Shader? TryLoad(string scaler, bool vib)
    {
        string name = (scaler == "" ? "PLAIN" : scaler) + (vib ? "+VIBRANCE" : "");
        try
        {
            string fs = scaler == "XBR" ? FsXbr : scaler == "SCALE3X" ? FsScale3x : scaler == "FSR" ? FsFsr : scaler == "SHARP" ? FsSharp : FsPlain;
            if (vib) fs = WithVibrance(fs);
            var sh = Raylib.LoadShaderFromMemory(VS, fs);
            if (sh.Id == 0 || !Raylib.IsShaderValid(sh))
            {
                if (sh.Id != 0) Raylib.UnloadShader(sh);
                LogShader(name + " no compilo; se usa " + (vib ? "sin vibrance" : "Bilinear/Point"));
                return null;
            }
            return sh;
        }
        catch (Exception ex) { LogShader(name + " fallo la carga: " + ex.Message); return null; }
    }

    // Elige/carga el shader segun el cfg y fija el filtro de la textura; ante fallo cae a Bilinear/Point
    static void ApplyScaleFilter(Texture2D tex)
    {
        string scaler = Config.XBR() ? "XBR" : Config.Scale3x() ? "SCALE3X" : Config.SharpBilinear() ? "SHARP" : Config.FSR() ? "FSR" : "";
        bool vib = Config.Vibrance();
        string want = scaler + (vib ? "+V" : "");
        if (want != failedKind) failedKind = "";
        if (want != shaderKind) UnloadScaleShader();
        if (want != "" && !scaleShader.HasValue && failedKind != want)
        {
            var sh = TryLoad(scaler, vib);
            if (!sh.HasValue && vib && scaler != "") sh = TryLoad(scaler, false); // sin vibrance, conserva el escalado
            if (sh.HasValue)
            {
                scaleShader = sh; shaderKind = want; curScaler = scaler;
                locTex = Raylib.GetShaderLocation(sh.Value, "texSize");
                locSrc = Raylib.GetShaderLocation(sh.Value, "srcSize");
                locOut = Raylib.GetShaderLocation(sh.Value, "outSize");
            }
            else failedKind = want;
        }
        bool bil = (!scaleShader.HasValue || curScaler == "") && Config.Bilinear();
        if (curScaler == "SHARP") bil = true; // sharp bilinear necesita muestreo bilineal
        Raylib.SetTextureFilter(tex, bil ? TextureFilter.Bilinear : TextureFilter.Point);
    }

    // La ventana nace oculta (Program.cs); se muestra tras el primer cuadro presentado
    static bool windowShown, borderless; // borderless = estado actual de ToggleBorderlessWindowed
    static void ShowWindowOnce()
    {
        if (windowShown) return;
        windowShown = true;
        Raylib.ClearWindowState(ConfigFlags.HiddenWindow);
        if (Config.FullScreen()) { Raylib.ToggleBorderlessWindowed(); borderless = true; } // FullScreen=T (tras mostrar)
    }

    public static void End(int hz = 0)
    {
        Present();
        Raylib.EndDrawing();
        ShowWindowOnce();
        dbgN++;
        long el = Clock.ElapsedMilliseconds - dbgT;
        if (el >= 500)
        {
            Fps = (int)Math.Round(dbgN * 1000.0 / el);
            dbgT = Clock.ElapsedMilliseconds; dbgN = 0;
        }
        if (hz <= 0) hz = FpsGame;
        long period = System.Diagnostics.Stopwatch.Frequency / hz;
        if (nextTick == 0) nextTick = Clock.ElapsedTicks;
        nextTick += period;
        long ms2 = System.Diagnostics.Stopwatch.Frequency / 500;
        while (nextTick - Clock.ElapsedTicks > ms2) System.Threading.Thread.Sleep(1);
        while (Clock.ElapsedTicks < nextTick) System.Threading.Thread.SpinWait(20);
        if (Clock.ElapsedTicks - nextTick > period * 2) nextTick = Clock.ElapsedTicks; // resincroniza tras un parón
    }

    // Logo (titus.conf:61 "logo titus/TITUS.SQZ") 4000 ms, luego TITRE.SQZ 6500 ms.
    // Devuelve false si se cerro la ventana.
    // fonts.c:533 viewintrotext: dos pantallas de texto, cada una espera Enter/Espacio.
    // ESC devuelve -1 y sale del programa (como el original).
    // Devuelve false si se cerro la ventana.
    static bool IntroText()
    {
        var buf = new byte[Fonts.W * Fonts.H];
        Array.Fill(buf, (byte)1);
        string year = $"YOU ARE STILL PLAYING TITUS IN {DateTime.Now.Year} !!";
        string[] second = { "REPROGRAMMED IN 2011 ON X86_64 2.40 GHZ." };
        string[] first = { " PROGRAMMED IN 1991 ON AT .286 12MHZ." };
        for (int page = 0; page < 2; page++)
        {
            Fonts.Print(buf, "     YEAAA . . .", 0, 5 * 12);
            Fonts.Print(buf, year, 0, 6 * 12);
            Fonts.Print(buf, page == 0 ? first[0] : second[0], 0, 12 * 12);
            Fonts.Print(buf, "   . . . ENJOY TITUS ADVENTURE !!", 0, 13 * 12);
            while (true)
            {
                if (Raylib.WindowShouldClose()) return false;
                if (Input.Confirm())
                    break;
                Begin();
                Raylib.ClearBackground(Color.Black);
                Render.Draw(OriginalTables.PaletteFontColour, buf);
                End();
            }
        }
        return true;
    }

    public static bool Run(string dir)
    {
        Fonts.Load(dir);
        if (!IntroText()) return false;
        string logo = Path.Combine(dir, "TITUS.bin");
        if (File.Exists(logo) && !View(logo, 4000)) return false;
        Audio.SelectMusic(15); // opentitus.c:90
        return View(Path.Combine(dir, "TITRE.bin"), 6500);
    }
}
