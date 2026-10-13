# Informe de la entrega 1 — Fases 1 y 2 completas, inicio de la Fase 3 (hitos M0 y M1)

Fecha: 13-10-2026

## 1. Archivos creados

| Ruta | Contenido |
|---|---|
| `docs/00`–`09` | Resumen ejecutivo, motores, APIs/licencias, costes, GDD, arquitectura, hitos, riesgos, alcance, criterios. |
| `src/Dorsal.Core/Domain/` | Atributos (29, incluidos los de portero), posiciones y pesos de media, jugador, club, tácticas, formaciones (4-4-2, 4-3-3, 4-2-3-1, 3-5-2), competición, reglas, partidos de calendario. |
| `src/Dorsal.Core/Random/` | RNG determinista xoshiro128** con estado serializable. |
| `src/Dorsal.Core/Content/` | Paquetes de contenido, concesiones de derechos y `ContentRegistry` (activación por verificación, vigencia, territorio y plataforma). |
| `src/Dorsal.Core/World/` | Generador de nombres originales (con lista de reservados), generador de jugadores y del mundo ficticio, validador de invariantes. |
| `src/Dorsal.Core/Simulation/` | Calendario de liga (método del círculo), clasificación con desempates configurables, carga de reglas JSON, selección automática de once. |
| `src/Dorsal.Core/Match/` | Motor de partido determinista (ver §3). |
| `src/Dorsal.Cli/` | `match`, `batch`, `season` y traza de depuración (`--trace`). |
| `tests/Dorsal.Core.Tests/` | 68 pruebas xUnit. |
| `unity/Dorsal/Assets/Dorsal/Scripts/` | Arranque, vista 3D con primitivas, cámaras, controles táctiles/teclado/mando, HUD. |
| `content/rules/*.json` | Reglas de dos competiciones ficticias (datos, no código). |
| `content/third_party/ASSETS.md` | Registro obligatorio de recursos de terceros (vacío: no se usa ninguno). |
| `tools/build-core-for-unity.{sh,ps1}` | Compila el núcleo y lo copia al proyecto Unity. |

## 2. Pruebas realizadas

`dotnet test` → **68 superadas, 0 fallidas** (≈ 18 s, .NET 8, Linux x64).

| Área | Qué se verifica |
|---|---|
| RNG | Reproducibilidad, guardado/restauración de estado, rangos, momentos de la normal. |
| Atributos y medias | Límites, generación con media objetivo, jugadores fuera de posición, edad. |
| Mundo | Determinismo, invariantes (plantillas, dorsales, nombres únicos), detección de corrupción, nombres reservados, potencial por edad. |
| Calendario y clasificación | Doble vuelta correcta (12, 20 y 4 clubes; impares con descanso), equilibrio local/visitante, puntos y desempates configurables, reglas JSON y archivos del repositorio válidos. |
| Contenido y licencias | Paquete base activo; licenciados sin derechos, no verificados, caducados o fuera de territorio/plataforma **inactivos**; un proveedor de datos no habilita marcas ni nombres; el juego funciona con cero paquetes licenciados. |
| Motor de partido | Partido completo, marcador = eventos de gol, reloj ≥ 90', determinismo, límites del campo, coherencia estadística, orden cronológico, cambios (límite y reglas), control humano (moverse, pasar, tirar), salida de banda conduciendo, partido sin el jugador del usuario en el campo, pasos de 10/20/50 Hz, configuraciones inválidas. |
| Atributos → rendimiento | Equipo de nivel 78 contra 58: más del doble de puntos y 1,5× goles; definición 95 frente a 25: precisión de tiro claramente superior; velocidad 95 frente a 35: más distancia en sprint. |
| Notas | Goles suben y rojas bajan la nota; mínimo de minutos; límites 3–10; porteros; goleadores por encima de la media del equipo en partidos reales. |

## 3. Resultados comprobados (medidos en este entorno)

Lote de 200 partidos (`dorsal batch --n 200 --seed 1`, Release, mitades de 270 s de simulación, Intel Xeon 2,8 GHz):

| Métrica | Valor | Comentario |
|---|---|---|
| Tiempo por partido (sin gráficos) | 87,8 ms | Suficiente para "simular partido". Las ligas de fondo usarán un simulador estadístico (M4). |
| Goles por partido | 2,08 | Rango deseado 2–3. |
| Tiros / a puerta | 9,5 / 3,6 | Formato comprimido (≈ 9 min reales). |
| Precisión de pase | 70,5 % | — |
| Empates / victoria local | 28 % / 38 % | Ventaja de campo configurable (`HomeAdvantage`). |
| Faltas / amarillas / rojas | 9,1 / 1,99 / 0,23 | — |
| Cambios / lesiones | 3,1 / 0,34 | Cambios por cansancio desde el minuto 58. |
| Córners / penaltis | 0,8 / 0,04 | **Bajos**: pendiente de calibración. |

Temporada completa (132 partidos con el motor completo, `dorsal season --seed 7`): 10,7 s; la clasificación final se
correlaciona con la calidad de las plantillas.

**No medido:** rendimiento gráfico, FPS, memoria, batería o temperatura en dispositivos. No hay compilación de Android.

## 4. Errores y limitaciones pendientes

1. **Unity sin verificar**: los scripts no se han abierto en el editor ni compilado para Android. Solo se ha comprobado que compilan como C# contra el núcleo y contra *stubs* de la API de Unity.
2. Calibración: córners (0,8) y penaltis (0,04) por partido demasiado bajos; la conversión (22 %) es alta para fútbol real, aunque habitual en formatos comprimidos. Se ajustará con pruebas de juego en M3.
3. Física simplificada: sin postes/larguero, sin disputa aérea entre varios jugadores (gana el primero que llega), sin regla de ventaja, sin manos, fuera de juego evaluado solo en el momento del pase.
4. El entrenador IA solo cambia por cansancio o lesión; la alineación usa encaje × media × forma física (forma, confianza y necesidades tácticas llegan en M4).
5. Determinismo garantizado para la misma plataforma y versión; entre x64 y ARM puede variar (por eso los resultados se guardan, no se recalculan).
6. Algunos precios de proveedores (API-Football, football-data.org) son contradictorios entre sus propias páginas; hay que confirmarlos por escrito.
7. "DORSAL" es un título provisional sin búsqueda de marcas.

## 5. Siguiente hito (M2)

1. Abrir `unity/Dorsal` en Unity 6.3 LTS, corregir errores de compilación y validar los controles en el editor.
2. Compilación Android de desarrollo y **primera medición de FPS** en 3 dispositivos.
3. Sustituir cápsulas por modelos humanoides CC0 (Quaternius) registrados en `ASSETS.md`.
4. Calibrar córners, penaltis y volumen de tiros con pruebas de juego.
5. En paralelo en el núcleo (M4): creación de jugador, progresión, entrenamiento, confianza del entrenador, convocatorias y guardado versionado con checksum.
