# 09 — Criterios de aceptación y matriz de pruebas

Estado: ✅ automatizado y en verde · 🟡 parcialmente cubierto · ⏳ pendiente (hito indicado) · 🧪 requiere prueba manual o en dispositivo.

| # | Criterio | Cómo se verifica | Estado |
|---|---|---|---|
| 1 | Se puede crear un futbolista | Prueba de creación con validación de campos y rangos | ⏳ M4 |
| 2 | Se puede iniciar y guardar una carrera | Prueba de guardado/carga de ida y vuelta con checksum | ⏳ M4 |
| 3 | Se puede disputar un partido completo | `MatchEngineTests`: el partido termina, reloj ≥ 90', marcador = goles en eventos, sin estados inválidos | ✅ M1 |
| 4 | Los atributos afectan al rendimiento | `AttributeImpactTests`: equipo superior gana claramente más; mejor definición → más tiros a puerta; más velocidad → más distancia en sprint | ✅ M1 |
| 5 | La evaluación posterior es coherente | `RatingTests`: notas en [1,10]; el autor de un gol puntúa por encima de la media del equipo en promedio | ✅ M1 |
| 6 | Titularidad y convocatorias funcionan | Prueba de selección de alineación por posición, forma, fatiga y confianza | ⏳ M4 |
| 7 | Se pueden recibir y negociar ofertas | Pruebas de generación y negociación de ofertas | ⏳ M5 |
| 8 | Los fichajes actualizan las plantillas | `WorldValidator` tras cada traspaso: un jugador en un solo club, contrato coherente | ⏳ M5 |
| 9 | Las temporadas avanzan sin inconsistencias | 10 temporadas simuladas + invariantes | ⏳ M5 |
| 10 | La carrera funciona sin conexión | El núcleo no tiene dependencias de red; `GameIsPlayableWithZeroLicensedPacks` genera el mundo y juega un partido sin proveedores externos | 🟡 mundo y partido ✅; carrera completa en M4 |
| 11 | Los guardados se recuperan correctamente | Pruebas de corrupción, backup y migraciones | ⏳ M4 |
| 12 | No requiere licencias inexistentes | `ContentRegistryTests`: el juego funciona con cero paquetes licenciados; los no verificados no se activan | ✅ M1 |
| 13 | Recursos comerciales documentados y autorizados | Revisión de `content/third_party/ASSETS.md` antes de cada versión | 🧪 continuo |
| 14 | Rendimiento medido en dispositivos | Informe de medición con dispositivo, versión, escena y resultado | 🧪 M2/M7 |
| 15 | Determinismo | Misma semilla → mismo resultado y mismos eventos | ✅ M1 |
| 16 | Calendario correcto | Doble vuelta: cada pareja se enfrenta dos veces (local/visitante), cada club juega una vez por jornada | ✅ M1 |
| 17 | Reglas configurables por competición | Reglas cargadas desde JSON; puntos y desempates según configuración | ✅ M1 |

Pruebas automatizadas previstas por sistema: progresión (M4), contratos (M5), simulación de temporada (M5), calendario (✅), guardado (M4), motor de partido (✅), contenido/licencias (✅).
