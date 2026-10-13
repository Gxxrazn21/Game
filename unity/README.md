# Proyecto Unity — DORSAL (prototipo M2)

> **Estado (13-10-2026):** compila sin errores ni avisos y el partido se juega en el editor de Unity 6.3 LTS
> (6000.3.26f1, Windows, DX11). Pendiente: compilación Android y medición de FPS en dispositivos.

## Requisitos

- Unity **6.3 LTS** (probado para instalar: 6000.3.26f1; plantilla *Universal 3D*, URP).
  Módulos de Unity Hub: **Android Build Support** con **OpenJDK** y **Android SDK & NDK Tools**.
  iOS Build Support solo si se compilará desde un Mac (la exportación final a iOS exige Xcode en macOS).
- Paquetes (Window → Package Manager → *Add package by name*):
  - `com.unity.inputsystem` (suele venir en la plantilla).
  - `com.unity.nuget.newtonsoft-json` (necesario porque `Dorsal.Core.dll` usa Newtonsoft.Json).
- *Project Settings → Player → Active Input Handling*: **Input System Package (New)** o **Both**.
- .NET SDK 8 para compilar el núcleo.

## Puesta en marcha

1. En Unity Hub crea un proyecto **Universal 3D** en la carpeta `unity/Dorsal` de este repositorio
   (o crea el proyecto en otra ruta y copia dentro la carpeta `Assets/Dorsal`).
2. Compila el núcleo y cópialo al proyecto:
   - Linux/macOS: `tools/build-core-for-unity.sh`
   - Windows: `tools/build-core-for-unity.ps1`

   Esto deja `Assets/Plugins/Dorsal/Dorsal.Core.dll` (ignorado por git; se regenera).
3. Abre cualquier escena (la `SampleScene` vacía sirve) y pulsa **Play**. `DorsalBootstrap` se crea solo
   (`RuntimeInitializeOnLoadMethod`) y monta campo, jugadores, balón, cámara, controles y marcador.

## Controles

| Acción | Táctil | Teclado | Mando |
|---|---|---|---|
| Moverse | Joystick flotante (mitad izquierda) | WASD / flechas | Stick izquierdo |
| Sprint | Botón SPRINT (mantener) | Mayús izq. | Gatillo derecho |
| Pase / Entrada | Botón grande | J / Espacio | A (sur) |
| Tiro (mantener = potencia) | TIRO | K | B (este) |
| Pase al hueco | AL HUECO | L | Y (norte) |
| Centro | CENTRO | U | X (oeste) |
| Pase elevado | — | I | RB |

Los botones son contextuales: con balón muestran PASE / TIRO / AL HUECO / CENTRO; sin balón, ENTRADA.

## Estructura

| Archivo | Responsabilidad |
|---|---|
| `DorsalBootstrap.cs` | Arranque, bucle de paso fijo (`FixedUpdate` = `TickRate` del motor), modos Jugar/Ver, velocidad, simulación rápida, ajustes de FPS y calidad. |
| `MatchView.cs` | Campo, porterías, jugadores (cápsulas por ahora), balón e interpolación entre pasos. |
| `CameraRig.cs` | Cámaras de retransmisión, de jugador y táctica; conversión del joystick a dirección de campo. |
| `TouchControls.cs` | Joystick flotante, botones contextuales configurables, teclado y mando. |
| `MatchHud.cs` | Marcador, cronómetro con añadido, avisos y resumen final con notas. |
| `PitchSpace.cs` | Conversión de coordenadas motor ↔ Unity. |

Toda la lógica del partido vive en `Dorsal.Core` (probada con `dotnet test`); estos scripts solo presentan el
estado y envían la entrada.

## Rendimiento

No se ha medido en ningún dispositivo. El plan de medición (M2) es: compilación Android de desarrollo,
3 dispositivos (gama baja/media/alta), partido completo con el Profiler conectado y registro de FPS medios,
percentil 1 %, temperatura y consumo. Los resultados se documentarán en `docs/informes/`.
