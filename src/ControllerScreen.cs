using Raylib_cs;

namespace TitusTheFox;

// Pantalla CONTROLLER (dentro de OPTIONS): elige KEYBOARD o uno de los joysticks conectados.
// Con teclado se redefinen las 5 acciones; con joystick, solo JUMP y ACTION (el resto es el D-pad/stick).
static class ControllerScreen
{
    const int Y0 = 26, Step = 14;
    const int MaxLen = 29; // caracteres que entran en la columna de valores

    // Devuelve 0 al volver con ESC/BACK, -1 si se cierra la ventana
    public static int Run(ref Texture2D tex)
    {
        var buf = new byte[Fonts.W * Fonts.H];
        int sel = 0;
        while (Raylib.GetKeyPressed() != 0) { }
        while (true)
        {
            if (Raylib.WindowShouldClose()) return -1;
            int n = Rows().Length;
            Input.UpdatePad();
            if (Input.Pressed(KeyboardKey.Escape) || Input.PadEscape()) return 0;
            if (Input.Pressed(KeyboardKey.Up) || Input.PadUp()) sel = (sel + n) % (n + 1);
            if (Input.Pressed(KeyboardKey.Down) || Input.PadDown()) sel = (sel + 1) % (n + 1);

            bool ok = Input.Confirm() || Input.PadConfirm(); // se lee una sola vez (Pressed consume el evento)
            int d = 0;
            if (Input.Pressed(KeyboardKey.Right) || Input.PadRight()) d = 1;
            if (Input.Pressed(KeyboardKey.Left) || Input.PadLeft()) d = -1;

            if (sel == n)
            {
                if (ok) return 0;
            }
            else if (sel == 0)
            {
                // DEVICE: confirmar = pasar al joystick, izquierda = teclado, derecha = otro pad.
                // (Antes "confirmar" hacia de derecha y con un solo pad el ciclo daba la vuelta:
                //  pulsar Enter dos veces volvia a KEYBOARD y se guardaba asi.)
                if (ok) CycleDevice(0);
                else if (d != 0) CycleDevice(d);
            }
            else if (ok || d == 1)
            {
                Capture(ref tex, buf, sel);
            }
            Page(ref tex, buf, sel);
        }
    }

    // ---------------------------------------------------------------- filas
    static (string label, string value)[] Rows()
    {
        if (Controller.Device == Controller.Keyboard)
            return new[]
            {
                ("DEVICE", "KEYBOARD"),
                ("LEFT", KeyName(Controller.KeyLeft)),
                ("RIGHT", KeyName(Controller.KeyRight)),
                ("CROUCH", KeyName(Controller.KeyDown)),
                ("JUMP", KeyName(Controller.KeyUp)),
                ("ACTION", KeyName(Controller.KeyAction)),
            };
        return new[]
        {
            ("DEVICE", DeviceLabel()),
            // los nombres del boton dependen del mando: Xbox (A, B, X, Y) o PlayStation (CRUZ, CIRCULO...)
            ("JUMP", USBClass.ButtonName(Controller.PadJump, Controller.Joystick)),
            ("ACTION", USBClass.ButtonName(Controller.PadAction, Controller.Joystick)),
        };
    }

    // Tipo del mando y el indice con el que se identifica: "SONY 0", "MICROSOFT 1"...
    // OJO: la fuente del juego no tiene '#' (todo lo que no esta en 0-9 A-Z ! ? . $ _ cae en '?'),
    // asi que el indice va separado con un espacio.
    static string DeviceLabel()
    {
        if (Controller.Device != Controller.JoystickDev) return "KEYBOARD";
        int idx = Controller.Joystick;
        if (Controller.Pads().IndexOf(idx) < 0) return "NOT CONNECTED";
        return USBClass.Label(idx) + " " + idx;
    }

    static string KeyName(KeyboardKey k) => k.ToString().ToUpperInvariant();

    // ---------------------------------------------------------------- dispositivo
    // d = 0: pasar al joystick (primer pad si no habia ninguno)
    // d = -1: al teclado (o al pad anterior)
    // d = +1: al siguiente pad (nunca vuelve al teclado: con un solo pad el ciclo daba la vuelta
    //        y pulsar Enter dos veces dejaba el dispositivo en KEYBOARD)
    static void CycleDevice(int d)
    {
        var pads = Controller.Pads();
        if (pads.Count == 0) { Controller.Device = Controller.Keyboard; Controller.Save(); return; }
        int cur = Controller.Device == Controller.JoystickDev ? pads.IndexOf(Controller.Joystick) : -1;
        if (d == 0)
        {
            Controller.Device = Controller.JoystickDev;
            if (cur < 0) Controller.Joystick = pads[0];
        }
        else if (d < 0)
        {
            if (cur <= 0) Controller.Device = Controller.Keyboard;
            else { Controller.Device = Controller.JoystickDev; Controller.Joystick = pads[cur - 1]; }
        }
        else
        {
            if (cur + 1 < pads.Count) { Controller.Device = Controller.JoystickDev; Controller.Joystick = pads[cur + 1]; }
            else if (cur < 0) { Controller.Device = Controller.JoystickDev; Controller.Joystick = pads[0]; }
        }
        Controller.Save();
    }

    // ---------------------------------------------------------------- captura
    static void Capture(ref Texture2D tex, byte[] buf, int row)
    {
        bool pad = Controller.Device == Controller.JoystickDev;
        int action = RowAction(row);
        string error = "";
        var prev = new bool[32];
        if (pad) for (int i = 0; i < prev.Length; i++) prev[i] = Raylib.IsGamepadButtonDown(Controller.Joystick, (GamepadButton)i);
        while (Raylib.GetKeyPressed() != 0) { } // descarta el ENTER/ESPACIO que abrio la captura (si no, se asigna solo)

        while (!Raylib.WindowShouldClose())
        {
            if (Raylib.IsKeyPressed(KeyboardKey.Escape)) break;
            if (pad)
            {
                int hit = -1;
                for (int i = 0; i < prev.Length; i++)
                {
                    bool down = Raylib.IsGamepadButtonDown(Controller.Joystick, (GamepadButton)i);
                    if (down && !prev[i]) hit = i;
                    prev[i] = down;
                }
                if (hit >= 0)
                {
                    // un boton no puede controlar dos acciones (ni ser del D-pad, que es de las direcciones)
                    if (Controller.AssignPad(action, hit)) { Controller.Save(); break; }
                    error = "BUTTON ALREADY IN USE";
                }
            }
            else
            {
                int k = Raylib.GetKeyPressed();
                if (k > 0)
                {
                    // una tecla no puede controlar dos acciones
                    if (Controller.AssignKey(action, (KeyboardKey)k)) { Controller.Save(); break; }
                    error = "KEY ALREADY IN USE";
                }
            }
            string msg = error.Length > 0 ? error : (pad ? "PRESS A BUTTON..." : "PRESS A KEY...");
            Page(ref tex, buf, row, msg);
        }
        while (Raylib.GetKeyPressed() != 0) { }
    }

    // Fila de la pantalla -> accion (segun el dispositivo)
    static int RowAction(int row)
    {
        if (Controller.Device == Controller.Keyboard)
            return row switch
            {
                1 => Controller.ActLeft,
                2 => Controller.ActRight,
                3 => Controller.ActCrouch,
                4 => Controller.ActJump,
                5 => Controller.ActAction,
                _ => -1,
            };
        return row switch
        {
            1 => Controller.ActJump,
            2 => Controller.ActAction,
            _ => -1,
        };
    }

    // ---------------------------------------------------------------- dibujo
    static void Page(ref Texture2D tex, byte[] buf, int sel, string? capture = null)
    {
        var rows = Rows();
        int n = rows.Length;
        Array.Fill(buf, (byte)1);
        const string title = "CONTROLLER";
        Fonts.Print(buf, title, 160 - title.Length * Fonts.CW / 2, 8);
        for (int i = 0; i < n; i++)
        {
            int y = Y0 + i * Step;
            Fonts.Print(buf, rows[i].label, 52, y);
            string v = capture != null && i == sel ? capture : rows[i].value;
            if (v.Length > MaxLen) v = v[..MaxLen];
            Fonts.Print(buf, v, 296 - v.Length * Fonts.CW, y);
        }
        int cy = Y0 + sel * Step + (Fonts.CH - 8) / 2;
        Texture2D t = tex; // copia: un parametro ref no se puede capturar en la lambda
        int backY = Y0 + n * Step;
        Menu.Present(buf, () =>
        {
            Menu.OverlayText(t, "BACK", 52, backY);
            Title.DrawRegion(t, Menu.Sel[0], 40, cy, Color.White);
        });
    }
}
