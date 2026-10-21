# TitusTheSharp: Titus the Fox en C#

Port de *Titus the Fox* (1992) a C# con raylib, derivado de OpenTitus.
Requiere tu propia copia de *Titus the Fox* (1992). Este programa no incluye los datos del juego.
Si te gusta el juego, puedes comprarlo en:

Steam: https://store.steampowered.com/app/711230/Titus_the_Fox

Gog: https://www.gog.com/es/game/titus_the_fox_to_marrakech_and_back

## Primer inicio

1. Ejecuta el port, presiona en examinar y busca tu copia original.
2. El programa extrae los datos a la carpeta `FILES\` junto al ejecutable.

## Controles

| Tecla | Acción |
|---|---|
| Flechas | Mover, saltar (Arriba), agacharse (Abajo) |
| Espacio | Coger / soltar objetos |
| P o ESC | Pausa (dentro de la pausa, CONTINUE o EXIT) |
| Enter / Espacio | Elegir en menús |
| Ctrl + Alt + E | Pantalla final (en game over) |

Las teclas se pueden redefinir en **OPTIONS → CONTROLLER**. Ahí también se puede elegir joystick: las direcciones salen del D-pad o del stick izquierdo, y saltar/acción se asignan a los botones que quieras (por defecto **CRUZ** = saltar y **CUADRADO** = acción, que en un mando de Microsoft son **A** y **X**).

Con `Device=JOYSTICK` los menús también se manejan con el joystick, **además del teclado** (se pueden usar los dos a la vez): D-pad o stick izquierdo para moverse, **A** para confirmar. El botón **MENU** (Xbox ☰ / PS Options) y el botón **PS/View** hacen lo mismo que ESC: pausa el juego, abre la ventana de salir en el menú principal y vuelve atrás en las demás pantallas. El botón **A** también salta las pantallas de "pulsa una tecla" igual que ENTER, incluida la de estado de nivel (LEVEL / LIVES / EXTRA BONUS). La pausa (CONTINUE / EXIT) también se mueve con el joystick. Si hay varios joysticks conectados, solo responde el elegido en **OPTIONS → CONTROLLER**.

Adaptación Con joystick: para **subir escaleras** sirve tanto el botón de salto como el direccional arriba (D-pad o stick izquierdo), que es lo cómodo; el botón de salto sigue siendo el único que salta.

## Configuración (`TitusTheFox.cfg`)

Se crea solo al primer inicio. Cada clave tiene un comentario encima con su descripción.

| Clave | Valores | Descripción |
|---|---|---|
| FullScreen | T / F | Pantalla completa |
| WideScreen | T / F | Vista ampliada (solo con pantalla completa) |
| ShowFps | T / F | Mostrar FPS |
| Fps | 30 a 240 | Frecuencia de dibujo |
| LivesAtStart | 1 a 30 | Vidas iniciales |
| FinalExtraLevel | T / F | Nivel extra antes de la pantalla final |
| Volume | 0 a 100 | Volumen de la música |
| Bilinear | T / F | Filtro bilineal |
| SharpBilinear | T / F | Escalado nítido |
| XBR | T / F | Escalado de bordes (simplificado) |
| Scale3x | T / F | Escalado Scale3x |
| FSR | T / F | Reconstrucción tipo FSR |
| Vibrance | T / F | Colores más vivos |
| Scanlines | T / F | Líneas horizontales |

Los cambios desde OPTIONS se aplican al momento. Si editás el archivo a mano, reiniciá el juego.

## Controles (`Controller.cfg`)

Se crea solo al primer inicio, junto al ejecutable. Si en ese primer arranque hay un joystick conectado, se usa como controlador por defecto (el primero que se detecta); si no, arranca con el teclado. Se edita desde **OPTIONS → CONTROLLER**; si lo tocás a mano, reiniciá el juego.

| Clave | Valores | Descripción |
|---|---|---|
| Device | KEYBOARD / JOYSTICK | Dispositivo con el que se juega |
| Joystick | 0 a 3 | Índice del joystick (0 = primero conectado) |
| KeyLeft, KeyRight, KeyDown, KeyUp, KeyAction | Nombre de tecla (Left, Space...) | Teclas de mover, agacharse, saltar y acción |
| PadJump, PadAction | Número de botón | Botones de saltar y acción en el joystick |

Con joystick, las direcciones salen del D-pad o del stick izquierdo; solo se configuran saltar y acción. Si elegís un joystick y lo desconectás, se vuelve al teclado automáticamente.

La pantalla CONTROLLER detecta el tipo de mando a partir del nombre que reporta (PlayStation, Microsoft o compatible) y lo muestra en la fila DEVICE como `SONY 0`, `MICROSOFT 1` o `COMPATIBLE 2` (el número es el índice con el que el sistema identifica ese mando). Los botones se muestran con el nombre de cada familia: **A / B / X / Y** en Xbox, **CRUZ / CIRCULO / CUADRADO / TRIANGULO** en PlayStation, y el índice del sistema (**BTN 7**) en los compatibles, donde no se puede saber qué botón es cuál.

La misma tecla no puede controlar dos acciones, ni el mismo botón dos acciones del joystick (los botones del D-pad tampoco se pueden asignar a saltar o acción, porque son de las direcciones). Si al capturar una tecla o un botón que ya está en uso, se muestra `KEY ALREADY IN USE` / `BUTTON ALREADY IN USE` y no se asigna. Si editás el archivo a mano y dejas dos acciones con la misma tecla, al arrancar se descarta la repetida y esa acción recibe una tecla libre.

## Compilar

Requiere .NET 10 SDK.

```
dotnet build
dotnet run
```

Para generar un ejecutable para distribuir, copiá la carpeta `bin\...\net10.0-windows\` completa (el `.exe` necesita el `.dll` y `runtimeconfig.json` junto a él).

## Registro de errores

Si el juego se cierra por un error, se escribe `Log_error.txt` junto al ejecutable, con la excepción y el estado del juego en ese momento.

## Créditos y fuentes

- **OpenTitus** (GPL-3.0): base de la lógica y formatos.
- **Chocolate Doom / dbopl** (GPL-2.0+): emulador OPL2 para música y efectos.
- **Scale3x / AdvMAME3x** (Andrea Mazzoleni): escalado.
- **xBR** (Hyllian): escalado.
- **Empaquetado ZIV** (Eric Zmiro, 1991): descompresor LZW.
- **raylib 6.0** y **Raylib-cs 8.1.0** (zlib).
- **.NET 10** (MIT).

## Diferencias con el juego original

### Jugabilidad
- **Pausa**: P o ESC abren un menú de pausa centrado con CONTINUE y EXIT. En el original, P pausaba con un tile de pausa sin menú. ESC en el original cerraba el juego; acá no cierra en ninguna pantalla.
- **Autopause**: al perder el foco la ventana (Alt+Tab, minimizar) se abre el menú de pausa y no se reanuda solo al volver; hay que elegir CONTINUE.
- **Controles**: se agrega OPTIONS → CONTROLLER para elegir teclado o joystick y redefinir teclas/botones, con la configuración en `Controller.cfg`. El original solo usaba teclado.
- **Salida**: ESC en el menú principal abre un popup QUIT con YES y NO. En el original, ESC salía directo.
- **Vidas iniciales**: el original arranca con 2 vidas; el port usa 3 por defecto, configurable con `LivesAtStart` (1 a 30).
- **Nivel extra**: `FinalExtraLevel` agrega un nivel oculto antes de la pantalla final. Solo se puede abrir desde el menú con la contraseña `3B9C` si esa opción está activa (en el original no se aceptaba).
- **Pulsaciones**: se filtran las pulsaciones repetidas de P, ESC y los menús, para que una sola pulsación no cuente dos veces.
- **Menú principal**: se agrega un menú de OPTIONS que no existe en el juego original. Se pueden configurar todos los parámetros del `TitusTheFox.cfg` desde la interfaz del juego. El texto PASSWORD se pintó de un solo color, como START (el original tenía un degradé).
- **Velocidad**: la lógica sigue a 35 ticks por segundo, como el original, pero el dibujo va a 60 fps (configurable con `Fps`, hasta 240), con interpolación de sprites y fondo. Compatible con WideScreen.

### Presentación
- **Ventana**: se abre en 960x600 (escala 3x). Si el monitor no entra, usa la escala entera menor (2x o 1x) y la centra. Pantalla completa opcional, con vista ampliada (widescreen) que muestra más área del nivel, limitada a 1024 px de ancho (hasta ~48:9; más ancho queda con barras negras).
- **Filtros de imagen**: Bilinear, Sharp Bilinear, XBR, Scale3x, FSR, Vibrance y Scanlines. Ninguno está activo por defecto.

### Sonido y datos
- **Música**: el emulador OPL2 es un port de `dbopl.c`, y la música se toma de los datos del juego original.
- **Volumen**: opción VOLUME (0 a 100). La música se pausa al entrar en pausa y continúa desde donde quedó.
- **Datos**: los `.SQZ` se extraen una vez a `FILES\` y luego se leen solo de esa carpeta.

### Errores
- Si el juego se cierra por un error, escribe `Log_error.txt` junto al ejecutable.

## Desarrollo propio

- Descompresor LZW del empaquetado ZIV: reimplementado a partir del desensamblado de la rutina original.
- Extracción automática de los datos del juego, con ventana de progreso y búsqueda de la carpeta original.
- Ventana centrada, pantalla completa, modo widescreen y escalado entero.
- Filtros en shaders: Sharp Bilinear, XBR simplificado, FSR, Scale3x y Vibrance.
- Interpolación de sprites, con lógica del juego a 35 ticks por segundo.
- Menú de pausa, popup de salida, opciones gráficas y de volumen, con configuración en `TitusTheFox.cfg`.
- Pantalla CONTROLLER: teclado o joystick, con teclas y botones redefinibles en `Controller.cfg`.
- Autopause al perder el foco de la ventana, con el menú de pausa.
- Pausa de la música que continúa desde donde quedó.
- Registro de errores (`Log_error.txt`).
- Filtro de pulsaciones para evitar dobles registros de tecla.
- Nivel extra opcional y acceso por contraseña.
- Soporte de joystick en los menús y en los saltos de pantalla, con detección del tipo de mando.
- Múltiples fixes.
- Resto del port en C#, incluida la lógica de niveles, enemigos y pantallas, traducida de OpenTitus y adaptada.
