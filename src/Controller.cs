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

    // Por defecto: salto = CRUZ (raylib 7, RIGHT_FACE_DOWN) y accion = CUADRADO (raylib 8,
    // RIGHT_FACE_LEFT). En un mando de Microsoft son los botones A y X respectivamente.
    public static int PadJump = 7;   // boton CRUZ / A
    public static int PadAction = 8; // boton CUADRADO / X

    // ---------------------------------------------------------------- acciones
    // Acciones del teclado. Con joystick solo se usan JUMP y ACTION (las direcciones salen del D-pad/stick).
    public const int ActLeft = 0, ActRight = 1, ActCrouch = 2, ActJump = 3, ActAction = 4;
    public const int ActCount = 5;

    // Tecla asignada a una accion
    public static KeyboardKey KeyOf(int action) => action switch
    {
        ActLeft => KeyLeft,
        ActRight => KeyRight,
        ActCrouch => KeyDown,
        ActJump => KeyUp,
        ActAction => KeyAction,
        _ => KeyboardKey.Null,
    };

    public static void SetKey(int action, KeyboardKey k)
    {
        switch (action)
        {
            case ActLeft: KeyLeft = k; break;
            case ActRight: KeyRight = k; break;
            case ActCrouch: KeyDown = k; break;
            case ActJump: KeyUp = k; break;
            case ActAction: KeyAction = k; break;
        }
    }

    // Boton del pad asignado a una accion (solo JUMP y ACTION); -1 si la accion no usa boton
    public static int PadOf(int action) => action switch
    {
        ActJump => PadJump,
        ActAction => PadAction,
        _ => -1,
    };

    public static void SetPad(int action, int button)
    {
        if (action == ActJump) PadJump = button;
        else if (action == ActAction) PadAction = button;
    }

    // Tecla por defecto de cada accion
    public static KeyboardKey DefaultKey(int action) => action switch
    {
        ActLeft => KeyboardKey.Left,
        ActRight => KeyboardKey.Right,
        ActCrouch => KeyboardKey.Down,
        ActJump => KeyboardKey.Up,
        ActAction => KeyboardKey.Space,
        _ => KeyboardKey.Null,
    };

    // El D-pad (botones 1-4) es de las direcciones: no se puede asignar a JUMP/ACTION
    public static bool IsDpad(int button) => button >= 1 && button <= 4;

    // True si la tecla ya controla otra accion: una tecla no puede estar en dos acciones
    public static bool KeyInUse(KeyboardKey k, int action)
    {
        if (k == KeyboardKey.Null) return false;
        for (int a = 0; a < ActCount; a++)
            if (a != action && KeyOf(a) == k) return true;
        return false;
    }

    // True si el boton ya controla otra accion (o es del D-pad, que es de las direcciones)
    public static bool PadInUse(int button, int action)
    {
        if (button <= 0) return false;
        if (IsDpad(button)) return true;
        for (int a = ActJump; a <= ActAction; a++)
            if (a != action && PadOf(a) == button) return true;
        return false;
    }

    // Asigna la tecla a la accion; false si ya la usa otra accion (no se cambia nada)
    public static bool AssignKey(int action, KeyboardKey k)
    {
        if (action < 0 || action >= ActCount) return false;
        if (k == KeyboardKey.Null) return false;
        if (KeyInUse(k, action)) return false;
        SetKey(action, k);
        return true;
    }

    // Asigna el boton del pad a la accion; false si ya lo usa otra accion (no se cambia nada)
    public static bool AssignPad(int action, int button)
    {
        if (action != ActJump && action != ActAction) return false;
        if (button <= 0) return false;
        if (IsDpad(button)) return false;
        if (button == PadOf(action)) return true; // ya era el suyo
        if (PadInUse(button, action)) return false;
        SetPad(action, button);
        return true;
    }

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

    // Direccion "arriba": con joystick se puede usar el boton de salto (el que tiene el dedo arriba
    // al saltar) o el direccional arriba (D-pad o stick izquierdo), que es lo comodo para subir
    // escaleras. Con teclado es la tecla de arriba.
    public static bool Up()
    {
        if (!UsingPad) return Raylib.IsKeyDown(KeyUp);
        return Raylib.IsGamepadButtonDown(Joystick, (GamepadButton)PadJump)
            || Raylib.IsGamepadButtonDown(Joystick, GamepadButton.LeftFaceUp)
            || Raylib.GetGamepadAxisMovement(Joystick, GamepadAxis.LeftY) < -AxisDead;
    }

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

    // Nombre legible de un boton (raylib 6.0: 1-4 D-pad, 5 Y, 6 B, 7 A, 8 X, 13 BACK,
    // 14 VIEW, 15 MENU, 16 L3, 17 R3). Para el mando que se esta configurando
    // usar USBClass.ButtonName(b, indice), que usa los nombres de Xbox o de PlayStation.
    public static string ButtonName(int b) => USBClass.XButtonName(b);

    // ---------------------------------------------------------------- archivo
    // Si Controller.cfg no existe lo crea con los valores por defecto del teclado
    public static void Ensure()
    {
        if (File.Exists(CfgPath)) { Load(); return; }
        firstRun = true;
        Save(); // se crea con los defaults; si hay joystick se actualiza despues (ApplyDefaultPad)
    }

    static bool firstRun; // true si el cfg no existia (primera vez que se arranca el juego)

    // Primera vez: si hay un joystick conectado se usa como controlador por defecto (el primero
    // que se detecta). Se llama cuando la ventana ya esta creada, que es cuando raylib detecta
    // los joysticks (hacen falta los eventos del sistema).
    public static void ApplyDefaultPad()
    {
        if (!firstRun) return;
        firstRun = false;
        var pads = Pads();
        if (pads.Count > 0)
        {
            Device = JoystickDev;
            Joystick = pads[0];
            Save();
        }
    }

    static void Load()
    {
        try
        {
            var kb = new KeyboardKey?[ActCount]; // valor del cfg por accion
            int? pJump = null, pAction = null;
            foreach (string line in File.ReadAllLines(CfgPath))
            {
                int eq = line.IndexOf('=');
                if (eq <= 0) continue;
                string key = line[..eq].Trim(), val = line[(eq + 1)..].Trim();
                switch (key.ToLowerInvariant())
                {
                    case "device": Device = val.Equals(JoystickDev, StringComparison.OrdinalIgnoreCase) ? JoystickDev : Keyboard; break;
                    case "joystick": if (int.TryParse(val, out int j)) Joystick = Math.Clamp(j, 0, MaxPads - 1); break;
                    case "keyleft": kb[ActLeft] = ParseKey(val, KeyLeft); break;
                    case "keyright": kb[ActRight] = ParseKey(val, KeyRight); break;
                    case "keydown": kb[ActCrouch] = ParseKey(val, KeyDown); break;
                    case "keyup": kb[ActJump] = ParseKey(val, KeyUp); break;
                    case "keyaction": kb[ActAction] = ParseKey(val, KeyAction); break;
                    case "padjump": if (int.TryParse(val, out int pj)) pJump = Math.Clamp(pj, 0, 31); break;
                    case "padaction": if (int.TryParse(val, out int pa)) pAction = Math.Clamp(pa, 0, 31); break;
                }
            }
            ApplyKeys(kb);
            ApplyPad(pJump, pAction);
        }
        catch { }
    }

    // Una tecla no puede controlar dos acciones: si el cfg esta mal, la repetida se descarta
    // y la accion se queda con una tecla libre (por defecto, o la primera que quede).
    static void ApplyKeys(KeyboardKey?[] cfgValues)
    {
        var res = new KeyboardKey[ActCount];
        var used = new List<KeyboardKey>();
        var pending = new List<int>();
        for (int a = 0; a < ActCount; a++)
        {
            var k = cfgValues[a];
            if (k == null || k == KeyboardKey.Null || used.Contains(k.Value)) { pending.Add(a); continue; }
            used.Add(k.Value);
            res[a] = k.Value;
        }
        foreach (int a in pending)
        {
            foreach (var cand in SpareKeys(a))
            {
                if (used.Contains(cand)) continue;
                used.Add(cand);
                res[a] = cand;
                break;
            }
        }
        for (int a = 0; a < ActCount; a++)
            if (res[a] != KeyboardKey.Null) SetKey(a, res[a]);
    }

    // Teclas candidatas para una accion: la suya por defecto y despues algunas de repuesto
    static IEnumerable<KeyboardKey> SpareKeys(int action)
    {
        yield return DefaultKey(action);
        foreach (var k in new[]
        {
            KeyboardKey.Left, KeyboardKey.Right, KeyboardKey.Down, KeyboardKey.Up, KeyboardKey.Space,
            KeyboardKey.Enter, KeyboardKey.KpEnter, KeyboardKey.Z, KeyboardKey.X, KeyboardKey.C,
            KeyboardKey.V, KeyboardKey.A, KeyboardKey.S, KeyboardKey.D, KeyboardKey.W, KeyboardKey.Q,
            KeyboardKey.E, KeyboardKey.R, KeyboardKey.T, KeyboardKey.G,
        })
            yield return k;
    }

    // JUMP y ACTION con botones distintos y ninguno del D-pad (1-4), que es de las direcciones
    static void ApplyPad(int? jump, int? action)
    {
        int j = PadJump, a = PadAction;
        if (jump != null && jump > 0 && jump != a && !IsDpad(jump.Value)) j = jump.Value;
        if (action != null && action > 0 && action != j && !IsDpad(action.Value)) a = action.Value;
        PadJump = j;
        PadAction = a;
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
                "# JOYSTICK INDEX (indice de raylib del pad elegido)",
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
