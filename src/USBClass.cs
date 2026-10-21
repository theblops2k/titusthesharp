using Raylib_cs;

namespace TitusTheFox;

/// Tipo de mando. Raylib no expone el VID/PID del dispositivo, asi que el fabricante se deduce
/// del nombre que reporta: los de PlayStation se llaman "Wireless Controller" (PS4/PS5 por
/// bluetooth), "DualShock", "DualSense"... y los de Xbox "Xbox Controller", "Xbox One Controller"...
public enum PadType
{
    Compatible, // generico, sin fabricante reconocible
    Sony,       // PlayStation
    Microsoft,  // Xbox
}

/// Deteccion del tipo de mando (Sony / Microsoft / Compatible) a partir del nombre del device.
static class USBClass
{
    // trozos de nombre que delatan al fabricante
    static readonly string[] SonyNames =
    {
        "wireless controller", // PS4/PS5 por bluetooth (nombre por defecto del DualShock/DualSense)
        "playstation", "dualshock", "dualsense", "ps3", "ps4", "ps5", "054c",
    };
    static readonly string[] MicrosoftNames =
    {
        "xbox", "x-box", "xinput", "045e",
    };

    // Nombre que reporta el pad (vacio si no se puede leer)
    public static string Name(int index) => Controller.PadName(index);

    // Tipo de un pad por su indice de raylib
    public static PadType Detect(int index) => Detect(Name(index));

    // Tipo de un pad por su nombre
    public static PadType Detect(string name)
    {
        string n = (name ?? "").ToLowerInvariant();
        if (n.Length == 0) return PadType.Compatible;
        foreach (string s in SonyNames) if (n.Contains(s)) return PadType.Sony;
        foreach (string s in MicrosoftNames) if (n.Contains(s)) return PadType.Microsoft;
        return PadType.Compatible;
    }

    // Nombre corto del tipo, para mostrarlo en pantalla
    public static string Label(PadType t) => t switch
    {
        PadType.Sony => "SONY",
        PadType.Microsoft => "MICROSOFT",
        _ => "COMPATIBLE",
    };

    public static string Label(int index) => Label(Detect(index));

    // ---------------------------------------------------------------- nombres de boton
    // Cada familia los llama distinto: Xbox usa A, B, X, Y; PlayStation usa CRUZ (X),
    // CIRCULO (B), CUADRADO (X) y TRIANGULO (Y). En los mandos compatibles no se sabe que
    // boton es cada uno, asi que se muestra el indice que usa el sistema (raylib).
    public static string ButtonName(int button, PadType type) => type switch
    {
        PadType.Sony => SonyButtonName(button),
        PadType.Microsoft => XButtonName(button),
        _ => GenericButtonName(button),
    };

    public static string ButtonName(int button, int padIndex) => ButtonName(button, Detect(padIndex));

    // Nombres de Xbox (y de los mandos genericos conocidos)
    public static string XButtonName(int b) => b switch
    {
        1 => "UP", 2 => "RIGHT", 3 => "DOWN", 4 => "LEFT",
        5 => "Y", 6 => "B", 7 => "A", 8 => "X",
        9 => "LB", 10 => "LT", 11 => "RB", 12 => "RT",
        13 => "BACK", 14 => "VIEW", 15 => "MENU",
        16 => "L3", 17 => "R3",
        _ => "BUTTON " + b,
    };

    // Nombres de PlayStation (misma posicion, otro dibujo)
    public static string SonyButtonName(int b) => b switch
    {
        1 => "UP", 2 => "RIGHT", 3 => "DOWN", 4 => "LEFT",
        5 => "TRIANGLE", 6 => "CIRCLE", 7 => "CROSS", 8 => "SQUARE",
        9 => "L1", 10 => "L2", 11 => "R1", 12 => "R2",
        13 => "SHARE", 14 => "PS", 15 => "OPTIONS",
        16 => "L3", 17 => "R3",
        _ => "BUTTON " + b,
    };

    // Mandos compatibles: se muestra el indice del boton (raylib)
    public static string GenericButtonName(int b) => "BTN " + b;
}
