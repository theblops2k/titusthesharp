namespace TitusTheFox;

// Lectura de TitusTheFox.cfg (junto al exe)
static class Config
{
    static bool? fullScreen;
    // True si el archivo contiene una linea FullScreen=T (cacheado; SetRaw lo invalida)
    public static bool FullScreen() => fullScreen ??= ReadFlag("FullScreen");

    static bool? showFps;
    // True si el archivo contiene una linea ShowFps=T (cacheado)
    public static bool ShowFps() => showFps ??= ReadFlag("ShowFps");

    static bool? wideScreen;
    // True si el archivo contiene una linea WideScreen=T (cacheado); solo tiene efecto con FullScreen=T
    public static bool WideScreen() => wideScreen ??= ReadFlag("WideScreen");

    static bool? bilinear, scanlines;
    // Bilinear=T: filtro bilineal al escalar a ventana (cacheado)
    public static bool Bilinear() => bilinear ??= ReadFlag("Bilinear");
    // Scanlines=T: lineas horizontales oscuras sobre el frame (cacheado)
    public static bool Scanlines() => scanlines ??= ReadFlag("Scanlines");

    static bool? sharpBilinear, xbr, fsr, scale3x;
    // SharpBilinear=T: shader de escalado nitido (cacheado)
    public static bool SharpBilinear() => sharpBilinear ??= ReadFlag("SharpBilinear");
    // XBR=T: shader xBR/Scale2x simplificado (cacheado); tiene prioridad sobre SharpBilinear
    public static bool XBR() => xbr ??= ReadFlag("XBR");
    // FSR=T: shader tipo FSR EASU simplificado (cacheado); prioridad XBR > SharpBilinear > FSR > Bilinear > Point
    public static bool FSR() => fsr ??= ReadFlag("FSR");
    // Scale3x=T: shader AdvMAME3x sobre toda la imagen (cacheado); prioridad XBR > Scale3x > SharpBilinear
    public static bool Scale3x() => scale3x ??= ReadFlag("Scale3x");

    static bool? vibrance;
    // Vibrance=T: vibrance 0.35 + contraste 1.05 tras el escalado (cacheado); combinable con cualquier filtro
    public static bool Vibrance() => vibrance ??= ReadFlag("Vibrance");

    static bool? finalExtraLevel;
    // FinalExtraLevel=T: agrega LEVELD al final de la lista de niveles (cacheado)
    public static bool FinalExtraLevel() => finalExtraLevel ??= ReadFlag("FinalExtraLevel");

    // Vidas iniciales (LivesAtStart, 1..30; invalido = 3)
    public static int LivesAtStart()
    {
        try
        {
            string path = Path.Combine(AppContext.BaseDirectory, "TitusTheFox.cfg");
            EnsureKeys(path);
            foreach (string line in File.ReadAllLines(path))
            {
                int eq = line.IndexOf('=');
                if (eq >= 0 && line[..eq].Trim().Equals("LivesAtStart", StringComparison.OrdinalIgnoreCase)
                    && int.TryParse(line[(eq + 1)..].Trim(), out int v) && v >= 1 && v <= 30) return v;
            }
        }
        catch { }
        return 3;
    }

    static int? fps;
    // Frecuencia de dibujo (Fps, 30..240; invalido = 60), cacheado
    public static int Fps()
    {
        if (fps.HasValue) return fps.Value;
        int r = 60;
        try
        {
            string path = Path.Combine(AppContext.BaseDirectory, "TitusTheFox.cfg");
            EnsureKeys(path);
            foreach (string line in File.ReadAllLines(path))
            {
                int eq = line.IndexOf('=');
                if (eq >= 0 && line[..eq].Trim().Equals("Fps", StringComparison.OrdinalIgnoreCase)
                    && int.TryParse(line[(eq + 1)..].Trim(), out int v) && v >= 30 && v <= 240) { r = v; break; }
            }
        }
        catch { }
        return (fps = r).Value;
    }

    static int? volume;
    // Volumen (Volume, 0..100; invalido = 80), cacheado
    public static int Volume()
    {
        if (volume.HasValue) return volume.Value;
        int r = 80;
        try
        {
            string path = Path.Combine(AppContext.BaseDirectory, "TitusTheFox.cfg");
            EnsureKeys(path);
            foreach (string line in File.ReadAllLines(path))
            {
                int eq = line.IndexOf('=');
                if (eq >= 0 && line[..eq].Trim().Equals("Volume", StringComparison.OrdinalIgnoreCase)
                    && int.TryParse(line[(eq + 1)..].Trim(), out int v) && v >= 0 && v <= 100) { r = v; break; }
            }
        }
        catch { }
        return (volume = r).Value;
    }

    static bool ensured;

    static string CfgPath => Path.Combine(AppContext.BaseDirectory, "TitusTheFox.cfg");

    // Valor crudo de una clave del cfg (null si no existe); lee el archivo sin cache
    public static string? GetRaw(string key)
    {
        try
        {
            EnsureKeys(CfgPath);
            foreach (string line in File.ReadAllLines(CfgPath))
            {
                int eq = line.IndexOf('=');
                if (eq >= 0 && line[..eq].Trim().Equals(key, StringComparison.OrdinalIgnoreCase)) return line[(eq + 1)..].Trim();
            }
        }
        catch { }
        return null;
    }

    // Reemplaza la linea de la clave (o la agrega) y reescribe el cfg; invalida los flags cacheados (Fps se lee al iniciar)
    public static void SetRaw(string key, string value)
    {
        try
        {
            EnsureKeys(CfgPath);
            var lines = new List<string>(File.ReadAllLines(CfgPath));
            bool done = false;
            for (int i = 0; i < lines.Count; i++)
            {
                int eq = lines[i].IndexOf('=');
                if (eq >= 0 && lines[i][..eq].Trim().Equals(key, StringComparison.OrdinalIgnoreCase)) { lines[i] = key + "=" + value; done = true; break; }
            }
            if (!done) lines.Add(key + "=" + value);
            File.WriteAllLines(CfgPath, lines);
            fullScreen = showFps = wideScreen = bilinear = scanlines = sharpBilinear = xbr = fsr = scale3x = vibrance = finalExtraLevel = null; fps = null; volume = null;
        }
        catch { }
    }

    // Descripciones por defecto (linea "# texto" encima de cada clave)
    static readonly Dictionary<string, string> DefaultDesc = new(StringComparer.OrdinalIgnoreCase)
    {
        ["FullScreen"] = "FULL SCREEN",
        ["WideScreen"] = "WIDE SCREEN",
        ["ShowFps"] = "SHOW FPS",
        ["Bilinear"] = "BILINEAR",
        ["Scanlines"] = "SCANLINES",
        ["SharpBilinear"] = "SHARP BILINEAR",
        ["XBR"] = "XBR",
        ["FSR"] = "FSR",
        ["Scale3x"] = "SCALE3X",
        ["Vibrance"] = "VIBRANCE",
        ["Fps"] = "MAX FPS",
        ["LivesAtStart"] = "LIVES",
        ["FinalExtraLevel"] = "EXTRA LEVEL",
        ["Volume"] = "VOLUME",
    };

    // Descripcion de la clave: texto de la linea "# ..." encima de ella; si no hay, el nombre de la clave
    public static string Description(string key)
    {
        try
        {
            EnsureKeys(CfgPath);
            string[] lines = File.ReadAllLines(CfgPath);
            for (int i = 1; i < lines.Length; i++)
            {
                int eq = lines[i].IndexOf('=');
                if (eq < 0 || !lines[i][..eq].Trim().Equals(key, StringComparison.OrdinalIgnoreCase)) continue;
                string prev = lines[i - 1].Trim();
                if (prev.StartsWith('#'))
                {
                    string t = prev[1..].Trim();
                    if (t.Length > 0) return t;
                }
                break;
            }
        }
        catch { }
        return key;
    }

    // Agrega las claves que falten con su descripcion (crea el archivo si no existe)
    static void EnsureKeys(string path)
    {
        if (ensured) return;
        ensured = true;
        try
        {
            var lines = File.Exists(path) ? new List<string>(File.ReadAllLines(path)) : new List<string>();
            bool changed = !File.Exists(path);
            foreach (string key in new[] { "FullScreen", "ShowFps", "WideScreen", "Bilinear", "Scanlines", "SharpBilinear", "XBR", "FSR", "Scale3x", "Vibrance", "FinalExtraLevel" })
            {
                bool found = false;
                foreach (string line in lines)
                {
                    int eq = line.IndexOf('=');
                    if (eq >= 0 && line[..eq].Trim().Equals(key, StringComparison.OrdinalIgnoreCase)) { found = true; break; }
                }
                if (!found) { lines.Add(key + "=F"); changed = true; }
            }
            bool livesOk = false;
            for (int i = 0; i < lines.Count; i++)
            {
                int eq = lines[i].IndexOf('=');
                if (eq >= 0 && lines[i][..eq].Trim().Equals("LivesAtStart", StringComparison.OrdinalIgnoreCase))
                {
                    livesOk = int.TryParse(lines[i][(eq + 1)..].Trim(), out int v) && v >= 1 && v <= 30;
                    if (!livesOk)
                    {
                        lines.RemoveAt(i);
                        if (i > 0 && lines[i - 1].TrimStart().StartsWith('#')) lines.RemoveAt(i - 1);
                    }
                    break;
                }
            }
            if (!livesOk) { lines.Add("LivesAtStart=3"); changed = true; }
            bool fpsOk = false;
            for (int i = 0; i < lines.Count; i++)
            {
                int eq = lines[i].IndexOf('=');
                if (eq >= 0 && lines[i][..eq].Trim().Equals("Fps", StringComparison.OrdinalIgnoreCase))
                {
                    fpsOk = int.TryParse(lines[i][(eq + 1)..].Trim(), out int v) && v >= 30 && v <= 240;
                    if (!fpsOk) lines[i] = "Fps=60";
                    changed |= !fpsOk;
                    fpsOk = true;
                    break;
                }
            }
            if (!fpsOk) { lines.Add("Fps=60"); changed = true; }
            bool volOk = false;
            for (int i = 0; i < lines.Count; i++)
            {
                int eq = lines[i].IndexOf('=');
                if (eq >= 0 && lines[i][..eq].Trim().Equals("Volume", StringComparison.OrdinalIgnoreCase))
                {
                    bool valid = int.TryParse(lines[i][(eq + 1)..].Trim(), out int v) && v >= 0 && v <= 100;
                    if (!valid) { lines[i] = "Volume=80"; changed = true; }
                    volOk = true;
                    break;
                }
            }
            if (!volOk) { lines.Add("Volume=80"); changed = true; }
            // comentario descriptivo antes de cada clave (sin tocar valores)
            for (int i = 0; i < lines.Count; i++)
            {
                int eq = lines[i].IndexOf('=');
                if (eq < 0 || lines[i].TrimStart().StartsWith('#')) continue;
                if (!DefaultDesc.TryGetValue(lines[i][..eq].Trim(), out string? text)) continue;
                if (i > 0 && lines[i - 1].TrimStart().StartsWith('#')) continue;
                lines.Insert(i, "# " + text);
                changed = true;
                i++;
            }
            if (changed) File.WriteAllLines(path, lines);
        }
        catch { }
    }

    static bool ReadFlag(string key)
    {
        try
        {
            string path = Path.Combine(AppContext.BaseDirectory, "TitusTheFox.cfg");
            EnsureKeys(path);
            if (!File.Exists(path)) return false;
            foreach (string line in File.ReadAllLines(path))
            {
                int eq = line.IndexOf('=');
                if (eq < 0) continue;
                if (line[..eq].Trim().Equals(key, StringComparison.OrdinalIgnoreCase))
                    return line[(eq + 1)..].Trim().Equals("T", StringComparison.OrdinalIgnoreCase);
            }
        }
        catch { }
        return false;
    }
}
