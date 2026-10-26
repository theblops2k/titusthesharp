namespace TitusTheFox;

// Editor.cfg junto a TitusTheFox.exe (misma carpeta que TitusTheFox.cfg y Controller.cfg).
// Es un archivo opcional que el usuario crea a mano: si existe y contiene MODE=ON se
// habilita el visor/editor de niveles. Sin el archivo, o con cualquier otro valor, el
// juego funciona exactamente igual que siempre.
static class EditorCfg
{
    // Archivo de configuracion del modo editor (no se crea solo)
    public static string CfgPath => System.IO.Path.Combine(AppContext.BaseDirectory, "Editor.cfg");

    // Niveles editados: el editor lee y guarda solamente aqui, junto a TitusTheFox.exe.
    public static string CustomDir => System.IO.Path.Combine(AppContext.BaseDirectory, "CustomLevels");

    // Orden canonico de los niveles originales. El editor usa este orden para listar
    // los .bin que haya en CustomLevels y para asignarles el levelid correcto.
    public static readonly string[] OriginalLevelOrder =
    {
        "LEVEL0", "LEVELJ", "LEVEL1", "LEVEL2", "LEVEL3", "LEVEL4", "LEVEL5", "LEVEL6",
        "LEVEL7", "LEVEL8", "LEVEL9", "LEVELB", "LEVELC", "LEVELE", "LEVELD", "LEVELG",
    };

    // Los originales viven siempre en FILES\; las ediciones en CustomLevels\.
    public static string OriginalLevelPath(string filesDir, string file)
        => System.IO.Path.Combine(filesDir, file + ".bin");

    public static string CustomLevelPath(string file)
        => System.IO.Path.Combine(CustomDir, file + ".bin");

    public static bool IsCustomLevel(string file) => File.Exists(CustomLevelPath(file));

    // Lista de niveles disponibles en CustomLevels, en el orden canonico del juego.
    // Solo se aceptan nombres originales y archivos con el tamano de un nivel valido.
    public static List<string> CustomLevelFiles()
    {
        var result = new List<string>();
        try
        {
            if (!Directory.Exists(CustomDir)) return result;
            foreach (string path in Directory.EnumerateFiles(CustomDir, "*.bin"))
            {
                string name = System.IO.Path.GetFileNameWithoutExtension(path);
                int order = KnownLevelIndex(name);
                if (order < 0) continue;
                try
                {
                    long len = new FileInfo(path).Length;
                    if (len < 35828 || (len - 35828) % 256 != 0) continue;
                }
                catch { continue; }
                if (!result.Contains(name, StringComparer.OrdinalIgnoreCase)) result.Add(name);
            }
        }
        catch { }
        result.Sort((a, b) =>
        {
            int oa = KnownLevelIndex(a), ob = KnownLevelIndex(b);
            if (oa != ob) return oa.CompareTo(ob);
            return StringComparer.OrdinalIgnoreCase.Compare(a, b);
        });
        return result;
    }

    public static int KnownLevelIndex(string file)
    {
        for (int i = 0; i < OriginalLevelOrder.Length; i++)
            if (OriginalLevelOrder[i].Equals(file, StringComparison.OrdinalIgnoreCase)) return i;
        return -1;
    }

    // levelid original para musica, paleta y comportamiento (LEVELD y LEVELG son especiales).
    public static short LevelIdForFile(string file) => file.ToUpperInvariant() switch
    {
        "LEVEL0" => 0,
        "LEVELJ" => 19,
        "LEVEL1" => 1,
        "LEVEL2" => 2,
        "LEVEL3" => 3,
        "LEVEL4" => 4,
        "LEVEL5" => 5,
        "LEVEL6" => 6,
        "LEVEL7" => 7,
        "LEVEL8" => 8,
        "LEVEL9" => 9,
        "LEVELB" => 11,
        "LEVELC" => 12,
        "LEVELE" => 14,
        "LEVELD" => 13,
        "LEVELG" => 16,
        _ => 0,
    };

    static bool? enabled;

    // True si Editor.cfg existe y tiene una linea MODE=ON (sin distinguir mayusculas)
    public static bool Enabled() => enabled ??= ReadMode();

    // Vuelve a leer el archivo (por si se edita con el juego abierto desde OPTIONS)
    public static void Reload() => enabled = null;

    static bool ReadMode()
    {
        try
        {
            if (!File.Exists(CfgPath)) return false;
            foreach (string line in File.ReadAllLines(CfgPath))
            {
                int eq = line.IndexOf('=');
                if (eq < 0) continue;
                if (!line[..eq].Trim().Equals("MODE", StringComparison.OrdinalIgnoreCase)) continue;
                return line[(eq + 1)..].Trim().Equals("ON", StringComparison.OrdinalIgnoreCase);
            }
        }
        catch { }
        return false;
    }
}
