using System.Collections.Generic;
using Raylib_cs;

// Filtro de pulsacion: Pressed devuelve true una sola vez por pulsacion fisica
// (consume el evento) y respeta un tiempo minimo entre pulsaciones de la misma tecla.
static class Input
{
    public const double MinMs = 80;
    public const double LevelKeyMs = 150; // P/ESC dentro del nivel (pausa)
    static readonly System.Diagnostics.Stopwatch clock = System.Diagnostics.Stopwatch.StartNew();
    static readonly Dictionary<KeyboardKey, double> last = new();

    public static bool Pressed(KeyboardKey k, double minMs = MinMs)
    {
        if (!Raylib.IsKeyPressed(k)) return false;
        double now = clock.Elapsed.TotalMilliseconds;
        if (last.TryGetValue(k, out double t) && now - t < minMs) return false;
        last[k] = now;
        return true;
    }

    // Enter, Enter del teclado numerico o Espacio (se evaluan todas para consumir cada una)
    public static bool Confirm()
    {
        bool a = Pressed(KeyboardKey.Enter), b = Pressed(KeyboardKey.KpEnter), c = Pressed(KeyboardKey.Space);
        return a || b || c;
    }

    // Marca la tecla como recien usada (p. ej. al entrar en pausa) sin leerla
    public static void Consume(KeyboardKey k) => last[k] = clock.Elapsed.TotalMilliseconds;

    // ---------------------------------------------------------------- joystick
    // Botones (raylib 6.0, segun su cabecera): 1-4 D-pad, 5 arriba (PS Triangle / Xbox Y),
    // 6 derecha (PS Circle / Xbox B), 7 abajo (PS Cross / Xbox A), 8 izquierda (PS Square / Xbox X),
    // 13 SELECT (PS Share), 14 GUIDE (boton PS / XBOX), 15 START (PS Options), 16 L3, 17 R3
    public const int PadUpBtn = 1, PadRightBtn = 2, PadDownBtn = 3, PadLeftBtn = 4;
    public const int PadY = 5, PadB = 6, PadA = 7, PadX = 8;
    public const int PadSelect = 13, PadGuide = 14, PadStart = 15;
    public const int PadL3 = 16, PadR3 = 17;
    const float AxisDead = 0.5f; // zona muerta del stick izquierdo

    static bool curUp, curDown, curLeft, curRight;        // estado del stick
    static bool edgeUp, edgeDown, edgeLeft, edgeRight;   // flanco del stick de este frame
    static int edgeButtons;                               // botones con flanco de este frame (bit a bit)
    static readonly bool[] padWasDown = new bool[32];     // estado de los botones en la llamada anterior

    // Se llama una vez por frame antes de leer el pad.
    // Detecta el flanco (pulsado ahora y no antes) del stick y de TODOS los botones: raylib's
    // IsGamepadButtonPressed no es fiable aqui (manteniendo el boton repetia la pulsacion).
    // Solo actua si el dispositivo elegido es JOYSTICK y hay pad conectado.
    // Se lee SIEMPRE el joystick configurado en CONTROLLER (Controller.Joystick): si hay varios
    // pads conectados, solo ese responde en los menus y en los saltos de pantalla.
    public static void UpdatePad()
    {
        edgeUp = edgeDown = edgeLeft = edgeRight = false;
        edgeButtons = 0;
        if (!PadActive())
        {
            curUp = curDown = curLeft = curRight = false;
            for (int i = 0; i < padWasDown.Length; i++) padWasDown[i] = false;
            return;
        }
        float ax = Raylib.GetGamepadAxisMovement(TitusTheFox.Controller.Joystick, GamepadAxis.LeftX);
        float ay = Raylib.GetGamepadAxisMovement(TitusTheFox.Controller.Joystick, GamepadAxis.LeftY);
        bool stickUp = ay < -AxisDead, stickDown = ay > AxisDead, stickLeft = ax < -AxisDead, stickRight = ax > AxisDead;
        edgeUp = stickUp && !curUp; curUp = stickUp;
        edgeDown = stickDown && !curDown; curDown = stickDown;
        edgeLeft = stickLeft && !curLeft; curLeft = stickLeft;
        edgeRight = stickRight && !curRight; curRight = stickRight;
        for (int b = 0; b < padWasDown.Length; b++)
        {
            bool pressed = Raylib.IsGamepadButtonDown(TitusTheFox.Controller.Joystick, (GamepadButton)b);
            if (pressed && !padWasDown[b]) edgeButtons |= 1 << b; // flanco: mantener no repite
            padWasDown[b] = pressed;
        }
    }

    // El pad solo cuenta si esta configurado como controlador (JOYSTICK) y hay uno conectado.
    // En los menus es adicional al teclado: se pueden usar los dos a la vez.
    public static bool PadActive() => TitusTheFox.Controller.UsingPad;

    // Direcciones: D-pad o stick izquierdo (solo el flanco)
    public static bool PadUp() => PadDir(PadUpBtn, edgeUp);
    public static bool PadDown() => PadDir(PadDownBtn, edgeDown);
    public static bool PadLeft() => PadDir(PadLeftBtn, edgeLeft);
    public static bool PadRight() => PadDir(PadRightBtn, edgeRight);

    static bool PadDir(int dpad, bool stickEdge)
    {
        if (!PadActive()) return false;
        return (edgeButtons & (1 << dpad)) != 0 || stickEdge;
    }

    // Boton A: confirmar
    public static bool PadConfirm() => PadActive() && (edgeButtons & (1 << PadA)) != 0;

    // Boton MENU (Xbox ☰ / PS Options, raylib 15) y boton PS/View (raylib 14): hacen lo mismo que
    // la tecla ESC (pausa en el juego, ventana de salir en el menu, atras en las demas pantallas).
    public static bool PadEscape() => PadActive() && (edgeButtons & ((1 << PadStart) | (1 << PadGuide))) != 0;

    // Cualquier boton del pad con flanco en este frame
    public static bool PadAnyButton() => PadActive() && edgeButtons != 0;

    // ---------------------------------------------------------------- saltar ventanas
    // Donde ENTER (o ESPACIO) salta de ventana: con joystick configurado, tambien el boton A.
    public static bool Skip() => Confirm() || PadConfirm();

    // Donde sirve cualquier tecla: con joystick configurado, tambien cualquier boton del pad.
    public static bool SkipAny() => Skip() || PadAnyButton();

    // Consume la pulsacion pendiente del pad (como Consume() para el teclado): se descarta el
    // boton que pudo saltar la pantalla anterior, para que no salte esta tambien.
    public static void ConsumePad()
    {
        UpdatePad();
        edgeButtons = 0;
        edgeUp = edgeDown = edgeLeft = edgeRight = false;
    }
}
