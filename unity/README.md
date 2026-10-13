# Proyecto Unity — DORSAL

> **Estado (13-10-2026):** el prototipo M2 (partido con cápsulas) compiló y se jugó en el editor de Unity 6.3 LTS
> (6000.3.26f1, Windows). Esta versión (0.2) añade el modo carrera completo, menús, jugadores humanoides animados,
> estadio, repeticiones, modo portero, medidor de rendimiento y compilación Android. **Los scripts nuevos se han
> comprobado como C# contra el núcleo real y contra *stubs* de la API de Unity, pero aún no se han abierto en el
> editor**: si aparece algún error en la consola, envíalo y se corrige.

## Requisitos

- Unity **6.3 LTS** (6000.3.26f1 probado; plantilla *Universal 3D*, URP).
  Módulos: **Android Build Support** con **OpenJDK** y **Android SDK & NDK Tools**.
- Paquetes: `com.unity.inputsystem` (incluido en la plantilla) y `com.unity.nuget.newtonsoft-json`.
- .NET SDK 8 o superior para compilar el núcleo.

## Actualizar el proyecto tras cada cambio

```powershell
cd D:\Unity\Game
git pull
powershell -ExecutionPolicy Bypass -File tools\build-core-for-unity.ps1
```

Vuelve a Unity: recompila solo. Pulsa **Play** en cualquier escena: `DorsalApp` se crea automáticamente.

## Qué hay en el juego

| Pantalla | Contenido |
|---|---|
| Menú principal | Continuar, Nueva carrera, Cargar partida, Partido rápido, **Desafío portero**, Ajustes. |
| Crear futbolista | Nombre, nacionalidad, edad, altura, complexión, pie, pie débil, posición, estilo de juego, personalidad, piel, pelo y dorsal (con validación). |
| Elegir club | Ofertas juveniles y **pruebas** con cualquier club (el resultado depende de tu nivel y potencial). |
| Centro de carrera | Media, potencial estimado, forma física, forma, confianza del entrenador, popularidad, lesiones, próximo partido, objetivos con progreso y noticias. |
| Día de partido | Decisión del entrenador (titular, suplente, no convocado…), objetivo, instrucción táctica y opciones **Jugar / Ver / Simular**. |
| Partido 3D | Humanoides animados, estadio con público, cámaras (TV, jugador, táctica), repeticiones a cámara lenta de goles y paradas. |
| Tras el partido | Nota, estadísticas, cambio de confianza, objetivo cumplido, comentario del entrenador, ingresos. |
| Mercado | Contrato y valor de mercado, ofertas (traspaso, cesión, renovación, libre), negociación salarial, pedir salir. |
| Otras | Entrenamiento (enfoque e intensidad), calendario, clasificaciones (dos divisiones), perfil con atributos, historial y logros, fin de temporada con premios/ascensos, retirada. |

El guardado es automático (tras cada jornada, fichaje o cambio de temporada) en
`Application.persistentDataPath/partidas`, con copia de seguridad y comprobación de integridad.

## Modo portero

Si creas un portero (o eliges **Desafío portero** en el menú), controlas al guardameta:

- **Sin tocar el joystick**, el portero se coloca solo (colocación asistida).
- **ESTIRADA** (botón grande / J / A): se lanza hacia donde apuntes con el joystick; sin dirección hace un blocaje.
- **SALIR** (L / Y): sale a por el balón.
- Con el balón en las manos: **SAQUE CORTO** y **SAQUE LARGO**.
- La parada depende de tu posición real, de cuándo y hacia dónde te estiras, y de tus atributos de portero.
  Las paradas del usuario se repiten automáticamente a cámara lenta.

La cámara de jugador del portero se coloca detrás de su portería.

## Controles

| Acción | Táctil | Teclado | Mando |
|---|---|---|---|
| Moverse | Joystick flotante (mitad izquierda) | WASD / flechas | Stick izquierdo |
| Sprint | SPRINT (mantener) | Mayús izq. | Gatillo derecho |
| Pase / Entrada / Estirada | Botón grande | J / Espacio | A |
| Tiro (mantener = potencia) / Saque largo | TIRO | K | B |
| Al hueco / Salir (portero) | AL HUECO / SALIR | L | Y |
| Centro | CENTRO | U | X |
| Pase elevado | — | I | RB |

Tamaño, opacidad y modo zurdo de los botones se cambian en **Ajustes**.

## Compilar para Android

1. Menú **Dorsal → Configurar proyecto para Android** (IL2CPP, ARM64, API mínima 26, horizontal,
   identificador provisional `com.dorsal.footballcareer`: cámbialo en `Assets/Dorsal/Editor/DorsalBuild.cs` antes de publicar).
2. Asegúrate de que hay al menos una escena (la `SampleScene` vale) en *File → Build Profiles*; si no hay ninguna marcada, el script usa la primera que encuentre.
3. **Dorsal → Compilar APK de desarrollo (con Profiler)** → `Builds/Android/dorsal-0.2.0-dev.apk`.
4. Instala el APK en el móvil (cable USB con depuración activada, o copia el archivo y ábrelo).
5. Para Google Play: **Dorsal → Compilar AAB para Google Play** (requiere configurar un *keystore* de firma en
   *Player Settings → Publishing Settings*).

## Medir el rendimiento

Al terminar cada partido se añade una línea a `rendimiento.csv` (en `Application.persistentDataPath`) con
dispositivo, GPU, resolución, ajustes, FPS medio, FPS del 1 % más lento y mínimo. En Android la ruta es
`/Android/data/com.dorsal.footballcareer/files/rendimiento.csv`. Activa **Ajustes → Mostrar contador de FPS**
para verlo en pantalla. Con esos datos se completará el informe de rendimiento (criterio 14).

## Estructura

| Archivo | Responsabilidad |
|---|---|
| `DorsalApp.cs`, `AppScreens.*.cs` | Aplicación, flujo de pantallas, carrera, guardado, ajustes. |
| `MatchController.cs` | Bucle de paso fijo del motor, modos Jugar/Ver, velocidad, simulación, repeticiones. |
| `MatchView.cs`, `MatchFrame.cs` | Campo, porterías, figuras, balón; fotogramas e interpolación; sistema de repeticiones. |
| `PlayerFigure.cs` | Futbolista humanoide de primitivas con animación procedural (sin recursos de terceros). |
| `StadiumBuilder.cs` | Gradas con público procedural, vallas con marcas ficticias, torres de focos. |
| `CameraRig.cs` | Cámaras TV, jugador (portero detrás de su portería), táctica y repetición. |
| `TouchControls.cs` | Joystick, botones contextuales (incluido portero), teclado y mando. |
| `MatchHud.cs` | Marcador, avisos, botones de partido y resumen. |
| `PerformanceMonitor.cs` | Medición de FPS y registro CSV. |
| `UiKit.cs` | Estilo visual propio y ajustes persistentes. |
| `Editor/DorsalBuild.cs` | Menú de configuración y compilación Android. |
| `link.xml` | Evita que IL2CPP elimine código usado por el guardado. |
