# 09 — Criterios de aceptación y matriz de pruebas

Estado: ✅ automatizado y en verde · 🟡 parcialmente cubierto · ⏳ pendiente (hito indicado) · 🧪 requiere prueba manual o en dispositivo.

| # | Criterio | Cómo se verifica | Estado |
|---|---|---|---|
| 1 | Se puede crear un futbolista | `PlayerCreationTests`: creación válida, rechazo de datos no válidos, rango de potencial | ✅ |
| 2 | Se puede iniciar y guardar una carrera | `CareerFlowTests` + `SaveSystemTests`: inicio, firma, guardado/carga idénticos | ✅ |
| 3 | Se puede disputar un partido completo | `MatchEngineTests`: el partido termina, reloj ≥ 90', marcador = goles en eventos, sin estados inválidos | ✅ M1 |
| 4 | Los atributos afectan al rendimiento | `AttributeImpactTests`: equipo superior gana claramente más; mejor definición → más tiros a puerta; más velocidad → más distancia en sprint | ✅ M1 |
| 5 | La evaluación posterior es coherente | `RatingTests`: notas en [1,10]; el autor de un gol puntúa por encima de la media del equipo en promedio | ✅ M1 |
| 6 | Titularidad y convocatorias funcionan | `CoachAndContractTests`: encaje, disponibilidad (lesión), efecto de la confianza | ✅ |
| 7 | Se pueden recibir y negociar ofertas | `NegotiationAcceptsCountersAndWithdraws`, cesiones, renovaciones, agente libre | ✅ |
| 8 | Los fichajes actualizan las plantillas | `TransferKeepsSquads…`, `AcceptedTransferMovesPlayerAndMoney`, validación tras cada jornada | ✅ |
| 9 | Las temporadas avanzan sin inconsistencias | `TenSeasonsKeepTheWorldConsistent` (10 temporadas, invariantes, ascensos/descensos) | ✅ |
| 10 | La carrera funciona sin conexión | El núcleo no usa red; todas las pruebas de carrera se ejecutan sin conexión ni proveedores externos | ✅ |
| 11 | Los guardados se recuperan correctamente | Corrupción → copia de seguridad, escritura interrumpida, migraciones, exportar/importar, disco real, reproducibilidad | ✅ |
| 12 | No requiere licencias inexistentes | `ContentRegistryTests`: el juego funciona con cero paquetes licenciados; los no verificados no se activan | ✅ M1 |
| 13 | Recursos comerciales documentados y autorizados | Revisión de `content/third_party/ASSETS.md` antes de cada versión | 🧪 continuo |
| 14 | Rendimiento medido en dispositivos | Informe de medición con dispositivo, versión, escena y resultado | 🧪 M2/M7 |
| 15 | Determinismo | Misma semilla → mismo resultado y mismos eventos | ✅ M1 |
| 16 | Calendario correcto | Doble vuelta: cada pareja se enfrenta dos veces (local/visitante), cada club juega una vez por jornada | ✅ M1 |
| 17 | Reglas configurables por competición | Reglas cargadas desde JSON; puntos y desempates según configuración | ✅ M1 |

Pruebas automatizadas por sistema: progresión ✅, contratos y mercado ✅, simulación de temporadas ✅, calendario ✅, guardado ✅, motor de partido ✅, modo portero ✅, contenido/licencias ✅ (109 pruebas).
