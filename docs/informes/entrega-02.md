# Informe de la entrega 2 — Modo carrera completo, modo portero y cliente Unity 0.2

Fecha: 13-10-2026

## 1. Archivos creados y modificados

### Núcleo (`src/Dorsal.Core`)

| Ruta | Contenido |
|---|---|
| `Domain/Contract.cs` (nuevo) | Contratos (salario, primas, cláusula, rol en la plantilla, cesión), traspasos, lesiones, estadísticas por temporada. |
| `Domain/Player.cs`, `Domain/Club.cs` | Lesión, sanciones, acumulación de amarillas, forma reciente, retirada; nivel del club. |
| `World/GameWorld.cs` | Contratos, traspasos y estadísticas en el mundo; traspaso atómico (si falla no cambia nada); validación de plantillas, contratos, cesiones, dorsales y presupuestos. |
| `World/Economy.cs` (nuevo) | Valor de mercado, salario justo y rol esperado. |
| `World/WorldGenerator.cs` | Dos divisiones (Primera y Segunda Solaria, 12 clubes cada una), contratos iniciales, fechas de temporada. |
| `Career/PlayerCreator.cs` (nuevo) | Creación del futbolista: 17 estilos de juego, personalidad, validación, potencial oculto con rango visible. |
| `Career/Progression.cs` (nuevo) | Progresión semanal por edad, potencial, minutos, entrenamiento y profesionalidad; declive físico; entrenamiento con enfoque e intensidad, fatiga y riesgo de lesión. |
| `Career/CoachAI.cs` (nuevo) | Convocatoria por encaje, forma, estado físico, confianza y rol del contrato; objetivos de partido e instrucciones tácticas. |
| `Career/QuickMatchSimulator.cs` (nuevo) | Simulador estadístico (Poisson) del resto de partidos de la jornada. |
| `Career/CareerState.cs`, `CareerSession*.cs` (nuevos) | Máquina de estados de la carrera, jornadas, mercado (traspasos, cesiones, renovaciones, agente libre, pruebas, negociación en 3 rondas), IA de fichajes, fin de temporada (premios, ascensos/descensos, retiradas, cantera), objetivos, logros y noticias. |
| `Persistence/SaveSystem.cs` (nuevo) | Guardado con cabecera, carga útil gzip+base64, SHA-256, escritura atómica con copia `.bak`, recuperación automática, migraciones, ranuras, exportar/importar. |
| `Localization/Es.cs` (nuevo) | Textos en español de posiciones, roles, atributos, ofertas y moneda. |
| `Match/*` | **Modo portero** (ver §4), `SubstituteIn` para que el entrenador saque al usuario desde el banquillo. |
| `src/Dorsal.Cli/Program.cs` | Comando `career` (`--pos`, `--n`, `--seed`, `--full`). |

### Cliente Unity (`unity/Dorsal/Assets/Dorsal`)

| Ruta | Contenido |
|---|---|
| `Scripts/DorsalApp.cs`, `AppScreens.Menu.cs`, `AppScreens.Career.cs` (nuevos) | Aplicación completa: menú, crear futbolista, cargar, ajustes, elegir club/pruebas, centro de carrera, entrenamiento, calendario, clasificaciones, ofertas y negociación, perfil, historial, previa y crónica del partido, fin de temporada, retirada. Guardado automático. |
| `Scripts/PlayerFigure.cs` (nuevo) | Futbolista humanoide construido con primitivas y animación procedural (carrera, pase, tiro, entrada, estirada del portero). |
| `Scripts/StadiumBuilder.cs` (nuevo) | Gradas con público generado por código, vallas con marcas ficticias y torres de focos. |
| `Scripts/MatchFrame.cs` (nuevo) | Fotogramas interpolados y repeticiones (búfer de 8 s, cámara lenta). |
| `Scripts/MatchController.cs` (nuevo) | Bucle de paso fijo, modos Jugar/Ver, velocidad, simular hasta el final, repeticiones automáticas. |
| `Scripts/PerformanceMonitor.cs` (nuevo) | Contador de FPS y registro `rendimiento.csv`. |
| `Scripts/UiKit.cs` (nuevo) | Estilo visual propio y ajustes persistentes (calidad, FPS objetivo, duración, botones, zurdo, estadio). |
| `Scripts/MatchView.cs`, `CameraRig.cs`, `TouchControls.cs`, `MatchHud.cs` | Vista con humanoides y estadio, cámara del portero y de repetición, botones contextuales de portero, HUD con resumen. |
| `Scripts/DorsalBootstrap.cs` | **Eliminado** (sustituido por `DorsalApp`, que se crea solo). |
| `Editor/DorsalBuild.cs`, `Editor/Dorsal.Editor.asmdef` (nuevos) | Menú *Dorsal*: configurar Android (IL2CPP, ARM64, API 26, horizontal), APK de desarrollo, APK de lanzamiento, AAB. |
| `link.xml` (nuevo) | Evita que IL2CPP elimine `Dorsal.Core` (usado por reflexión en el guardado JSON). |

### Documentación

`README.md`, `unity/README.md` (reescrito), `docs/06` (estado de hitos), `docs/09` (criterios), este informe.

## 2. Pruebas realizadas

`dotnet test -c Release` → **109 superadas, 0 fallidas** (≈ 10 s, .NET 8, Linux x64). Además de las 68 de la entrega 1:

| Área | Qué se verifica |
|---|---|
| Traspasos | Plantillas, contratos y presupuestos coherentes tras un traspaso; traspasos imposibles rechazados sin efectos secundarios. |
| Creación de jugador | Validación de nombre, edad, altura, posiciones, estilo; porteros sin posiciones secundarias de campo; el rango visible de potencial se estrecha con la edad. |
| Flujo de carrera | Ofertas iniciales, pruebas, temporada completa coherente, **10 temporadas sin violar invariantes** y con población estable. |
| Progresión | Jóvenes mejoran más con minutos y entrenamiento y nunca superan el potencial; el enfoque orienta la mejora; la intensidad alta cansa y lesiona más; veteranos declinan empezando por lo físico. |
| Entrenador y contratos | Convocatoria coherente con confianza y rol; renovaciones, cesiones, negociación. |
| Guardado | Ida y vuelta exacta; **una partida cargada continúa de forma idéntica**; corrupción → copia de seguridad; escritura interrumpida sin perder la partida anterior; migración de versión; ranuras, exportar/importar; disco real. |
| Modo portero | Estirarse bien colocado para muchas más paradas que quedarse quieto (> 25 puntos); la estirada desplaza y el portero se recupera; colocación asistida; partido completo como portero. |

Compilación de los 15 scripts de Unity y del script de editor contra el núcleo real y *stubs* de la API de Unity:
**0 errores, 0 avisos**. Esto **no** equivale a compilar en Unity (ver §5).

## 3. Resultados comprobados (medidos en este entorno)

`dorsal career` (Release, Intel Xeon), carreras automáticas que aceptan ofertas:

| Prueba | Resultado |
|---|---|
| Portero, 5 temporadas (`--pos GK --n 5 --seed 3`) | 1,6 s. Debuta en Segunda, tres cesiones con 9–10 titularidades, media 53 → 72, traspaso a Primera. Mundo: 590 jugadores. Guardado: 122 KB. |
| Delantero, 10 temporadas (`--pos ST --n 10 --seed 11`) | 2,6 s. Cesiones, ascenso, más de 100 partidos y 25 goles. Mundo estable: 588 jugadores. Guardado: 124 KB. |
| Carreras largas (entre 8 y 25 temporadas, varias posiciones, durante el desarrollo) | Sin violaciones de invariantes; retirada forzosa a los 40 años. |

**No medido:** FPS, memoria, batería o temperatura en móviles; tiempo de carga en Android; tamaño del APK.

## 4. Modo portero

No se han usado recursos externos: la descarga de paquetes y animaciones de terceros está bloqueada en este entorno,
y así se evita además cualquier duda de licencia. Todo está programado en el núcleo y en `PlayerFigure`:

- Cuando el usuario es el portero, el tiro **no se resuelve al chutar**, sino cuando el balón cruza la línea de
  portería. La parada depende de la posición real del portero, del momento y la dirección de la estirada y de sus
  atributos (estirada, colocación, reflejos). Si roza el balón con la punta de los dedos puede desviarlo.
- Estirada con impulso (0,45 s) y caída hasta el suelo (1,25 s); blocaje sin dirección.
- Colocación asistida si no se toca el joystick; botón para salir a por el balón; saque corto/largo con las manos
  (límite de 6 s).
- Cámara detrás de la portería y repetición automática a cámara lenta de las paradas del usuario.
- Accesible como carrera de portero o desde **Desafío portero** en el menú principal.

## 5. Errores y limitaciones pendientes

1. **El cliente Unity 0.2 aún no se ha abierto en el editor.** Puede haber errores de compilación por diferencias
   entre los *stubs* y la API real de Unity 6.3. Hay que abrirlo, revisar la consola y corregir.
2. **No hay compilación de Android ni medición en dispositivo.** El criterio 14 (rendimiento) sigue abierto.
3. Interfaz con IMGUI (rápida de iterar, pero no es la definitiva para móvil): habrá que migrar a UI Toolkit en M6.
4. Humanoides de primitivas: aceptables como prototipo; los modelos y animaciones con *retargeting* (CC0 o propios)
   siguen pendientes (M3), igual que la prueba de usabilidad.
5. Equilibrio de carrera por calibrar. En la prueba del portero, varias temporadas empiezan en el banquillo del club
   propietario y la cesión llega en el mercado de invierno (9–10 partidos de 22).
6. Contenido: una sola federación ficticia con dos divisiones (el plan de M6 prevé tres).
7. Sin escudos ni equipaciones originales dibujados: se usan colores del club.

## 6. Siguiente hito

1. Abrir el proyecto en Unity 6.3 y corregir cualquier error de consola.
2. Probar en el editor: Desafío portero, Partido rápido y una carrera nueva hasta el primer partido.
3. Compilar el **APK de desarrollo**, instalarlo y registrar `rendimiento.csv` en al menos un móvil real.
   Primera medición de FPS documentada.
4. Calibrar con esos datos la calidad gráfica por defecto, el equilibrio de cesiones y la dificultad del modo portero.
5. Después, M3/M6: modelos y animaciones, UI Toolkit, más contenido ficticio y prueba de usabilidad.
