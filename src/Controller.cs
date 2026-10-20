using Raylib_cs;

namespace TitusTheFox;

// Controles del juego (Controller.cfg junto al exe): teclado o joystick.
// Teclado: se pueden redefinir las 5 acciones. Joystick: las direcciones salen del D-pad/stick
// izquierdo y solo se redefinen JUMP y ACTION.
static class Controller
{
    public const int MaxPads = 4;
    public const string Keyboard = "KEYBOARD", JoystickDev = "JOYSTICK";
    const float AxisDead = 0.5f; // zona muerta del stick izquierdo

    public static string Device = Keyboard;
    public static int Joystick; // indice raylib del pad elegido

    public static KeyboardKey KeyLeft = KeyboardKey.Left;
    public static KeyboardKey KeyRight = KeyboardKey.Right;
    public static KeyboardKey KeyDown = KeyboardKey.Down;     // agacharse
    public static KeyboardKey KeyUp = KeyboardKey.Up;         // saltar
    public static KeyboardKey KeyAction = KeyboardKey.Space;  // coger / soltar / lanzar

    public static int PadJump = 7;   // boton A (raylib: RIGHT_FACE_DOWN)
    public static int PadAction = 6; // boton B (raylib: RIGHT_FACE_RIGHT)

    public static string CfgPath => Path.Combine(AppContext.BaseDirectory, "Controller.cfg");

    public static bool HasPad => Raylib.IsGamepadAvailable(Joystick);

    // Con JOYSTICK elegido pero el pad desconectado se juega con el teclado (no queda sin control)
    public static bool UsingPad => Device == JoystickDev && HasPad;

    // ---------------------------------------------------------------- entrada
    public static bool Left()
    {
        if (!UsingPad) return Raylib.IsKeyDown(KeyLeft);
        return Raylib.IsGamepadButtonDown(Joystick, GamepadButton.LeftFaceLeft)
            || Raylib.GetGamepadAxisMovement(Joystick, GamepadAxis.LeftX) < -AxisDead;
    }

    public static bool Right()
    {
        if (!UsingPad) return Raylib.IsKeyDown(KeyRight);
        return Raylib.IsGamepadButtonDown(Joystick, GamepadButton.LeftFaceRight)
            || Raylib.GetGamepadAxisMovement(Joystick, GamepadAxis.LeftX) > AxisDead;
    }

    public static bool Crouch()
    {
        if (!UsingPad) return Raylib.IsKeyDown(KeyDown);
        return Raylib.IsGamepadButtonDown(Joystick, GamepadButton.LeftFaceDown)
            || Raylib.GetGamepadAxisMovement(Joystick, GamepadAxis.LeftY) > AxisDead;
    }

    public static bool Jump() => UsingPad
        ? Raylib.IsGamepadButtonDown(Joystick, (GamepadButton)PadJump)
        : Raylib.IsKeyDown(KeyUp);

    public static bool Action() => UsingPad
        ? Raylib.IsGamepadButtonDown(Joystick, (GamepadButton)PadAction)
        : Raylib.IsKeyDown(KeyAction);

    // ---------------------------------------------------------------- pads
    // Indices de los joysticks conectados (en orden)
    public static List<int> Pads()
    {
        var list = new List<int>();
        for (int i = 0; i < MaxPads; i++) if (Raylib.IsGamepadAvailable(i)) list.Add(i);
        return list;
    }

    public static string PadName(int index)
    {
        try { return Raylib.GetGamepadName_(index) ?? ""; } catch { return ""; }
    }

    // Nombre legible de un boton (raylib: 1-4 D-pad, 5 Y, 6 B, 7 A, 8 X, 9-12 gatillos, 13-17 varios)
    public static string ButtonName(int b) => b switch
    {
        1 => "UP", 2 => "RIGHT", 3 => "DOWN", 4 => "LEFT",
        5 => "Y", 6 => "B", 7 => "A", 8 => "X",
        9 => "LB", 10 => "LT", 11 => "RB", 12 => "RT",
        13 => "SELECT", 14 => "START", 15 => "HOME", 16 => "L3", 17 => "R3",
        _ => "BUTTON " + b,
    };

    // ---------------------------------------------------------------- archivo
    // Si Controller.cfg no existe lo crea con los valores por defecto del teclado
    public static void Ensure()
    {
        if (File.Exists(CfgPath)) Load();
        else Save();
    }

    static void Load()
    {
        try
        {
            foreach (string line in File.ReadAllLines(CfgPath))
            {
                int eq = line.IndexOf('=');
                if (eq <= 0) continue;
                string key = line[..eq].Trim(), val = line[(eq + 1)..].Trim();
                switch (key.ToLowerInvariant())
                {
                    case "device": Device = val.Equals(JoystickDev, StringComparison.OrdinalIgnoreCase) ? JoystickDev : Keyboard; break;
                    case "joystick": if (int.TryParse(val, out int j)) Joystick = Math.Clamp(j, 0, MaxPads - 1); break;
                    case "keyleft": KeyLeft = ParseKey(val, KeyLeft); break;
                    case "keyright": KeyRight = ParseKey(val, KeyRight); break;
                    case "keydown": KeyDown = ParseKey(val, KeyDown); break;
                    case "keyup": KeyUp = ParseKey(val, KeyUp); break;
                    case "keyaction": KeyAction = ParseKey(val, KeyAction); break;
                    case "padjump": if (int.TryParse(val, out int pj)) PadJump = Math.Clamp(pj, 0, 31); break;
                    case "padaction": if (int.TryParse(val, out int pa)) PadAction = Math.Clamp(pa, 0, 31); break;
                }
            }
        }
        catch { }
    }

    // Acepta el nombre del enum (Left, Space...) o el codigo numerico
    static KeyboardKey ParseKey(string val, KeyboardKey def)
    {
        if (int.TryParse(val, out int n) && Enum.IsDefined(typeof(KeyboardKey), n)) return (KeyboardKey)n;
        return Enum.TryParse(val, true, out KeyboardKey k) ? k : def;
    }

    public static void Save()
    {
        try
        {
            File.WriteAllLines(CfgPath, new[]
            {
                "# CONTROLLER (KEYBOARD or JOYSTICK)",
                "Device=" + Device,
                "# JOYSTICK INDEX (0 = first connected)",
                "Joystick=" + Joystick,
                "# KEYBOARD: LEFT / RIGHT / CROUCH / JUMP / ACTION",
                "KeyLeft=" + KeyLeft,
                "KeyRight=" + KeyRight,
                "KeyDown=" + KeyDown,
                "KeyUp=" + KeyUp,
                "KeyAction=" + KeyAction,
                "# JOYSTICK: JUMP / ACTION (directions come from the D-pad or left stick)",
                "PadJump=" + PadJump,
                "PadAction=" + PadAction,
            });
        }
        catch { }
    }
}
