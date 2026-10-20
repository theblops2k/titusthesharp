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
            if (Input.Pressed(KeyboardKey.Escape)) return 0;
            if (Input.Pressed(KeyboardKey.Up)) sel = (sel + n) % (n + 1);
            if (Input.Pressed(KeyboardKey.Down)) sel = (sel + 1) % (n + 1);

            bool ok = Input.Confirm(); // se lee una sola vez (Pressed consume el evento)
            int d = 0;
            if (ok || Input.Pressed(KeyboardKey.Right)) d = 1;
            if (Input.Pressed(KeyboardKey.Left)) d = -1;

            if (sel == n)
            {
                if (ok) return 0;
            }
            else if (sel == 0)
            {
                if (d != 0) CycleDevice(d);
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
            ("JUMP", Controller.ButtonName(Controller.PadJump)),
            ("ACTION", Controller.ButtonName(Controller.PadAction)),
        };
    }

    // "PAD 1", "PAD 2"... segun el orden de los joysticks conectados
    static string DeviceLabel()
    {
        int ord = Controller.Pads().IndexOf(Controller.Joystick);
        return ord >= 0 ? "PAD " + (ord + 1) : "PAD ?";
    }

    static string KeyName(KeyboardKey k) => k.ToString().ToUpperInvariant();

    // ---------------------------------------------------------------- dispositivo
    // Cicla KEYBOARD -> PAD 1 -> PAD 2 -> ... -> KEYBOARD
    static void CycleDevice(int d)
    {
        var pads = Controller.Pads();
        if (pads.Count == 0) { Controller.Device = Controller.Keyboard; Controller.Save(); return; }
        int cur = Controller.Device == Controller.JoystickDev ? pads.IndexOf(Controller.Joystick) : -1;
        int next = cur + d;
        if (next < -1) next = pads.Count - 1;
        if (next >= pads.Count) next = -1;
        if (next < 0) Controller.Device = Controller.Keyboard;
        else { Controller.Device = Controller.JoystickDev; Controller.Joystick = pads[next]; }
        Controller.Save();
    }

    // ---------------------------------------------------------------- captura
    static void Capture(ref Texture2D tex, byte[] buf, int row)
    {
        bool pad = Controller.Device == Controller.JoystickDev;
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
                if (hit >= 0) { Assign(row, hit); Controller.Save(); break; }
            }
            else
            {
                int k = Raylib.GetKeyPressed();
                if (k > 0) { Assign(row, k); Controller.Save(); break; }
            }
            Page(ref tex, buf, row, pad ? "PRESS A BUTTON..." : "PRESS A KEY...");
        }
        while (Raylib.GetKeyPressed() != 0) { }
    }

    static void Assign(int row, int code)
    {
        if (Controller.Device == Controller.Keyboard)
        {
            var k = (KeyboardKey)code;
            switch (row)
            {
                case 1: Controller.KeyLeft = k; break;
                case 2: Controller.KeyRight = k; break;
                case 3: Controller.KeyDown = k; break;
                case 4: Controller.KeyUp = k; break;
                case 5: Controller.KeyAction = k; break;
            }
        }
        else if (row == 1) Controller.PadJump = code;
        else if (row == 2) Controller.PadAction = code;
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
        string info = Controller.Device == Controller.Keyboard ? "" : PadInfo();
        Menu.Present(buf, () =>
        {
            Menu.OverlayText(t, "BACK", 52, backY);
            if (info.Length > 0) Menu.OverlayText(t, info, 52, 184);
            Title.DrawRegion(t, Menu.Sel[0], 40, cy, Color.White);
        });
    }

    // Nombre + indice del pad elegido (para distinguir dos joysticks iguales)
    static string PadInfo()
    {
        if (Controller.Pads().IndexOf(Controller.Joystick) < 0)
            return "PAD " + (Controller.Joystick + 1) + " NOT CONNECTED";
        string name = Controller.PadName(Controller.Joystick).ToUpperInvariant();
        string s = (name.Length > 0 ? name : "JOYSTICK") + " [ID " + Controller.Joystick + "]";
        return s.Length > MaxLen ? s[..MaxLen] : s;
    }
}
