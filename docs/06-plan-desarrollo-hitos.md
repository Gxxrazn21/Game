# 06 — Plan de desarrollo por hitos

Cada hito termina con una versión ejecutable, pruebas automáticas en verde y un informe (archivos, pruebas, resultados, errores pendientes, siguiente hito). Duraciones orientativas para el Escenario 1 🔬.

| Hito | Fase | Contenido | Criterio de salida | Duración 🔬 / estado |
|---|---|---|---|---|
| **M0** | 1–2 | Investigación, GDD, arquitectura, riesgos, costes, alcance | Documentos `docs/00–09` | ✔ entrega 1 |
| **M1** | 3 (núcleo) | `Dorsal.Core`: dominio, RNG, contenido/licencias, mundo ficticio, calendario, **motor de partido determinista sin gráficos**, CLI, pruebas | `dotnet test` en verde; partido completo simulado con marcador, reloj, eventos y valoraciones | ✔ entrega 1–2 |
| **M2** | 3 (Unity) | Proyecto Unity: campo, 22 jugadores (cápsulas → modelos CC0), balón, cámara TV, joystick + botones, marcador y cronómetro; build Android de desarrollo | Partido jugable en dispositivo Android; **primera medición de FPS** | Editor ✔ (entrega 1); Android pendiente de compilar y medir |
| **M3** | 3 (calidad) | Animaciones humanoides con *retargeting*, porteros animados, faltas/saques visibles, tutorial de controles, ajustes de controles | Prueba de usabilidad con 5+ personas | Código entrega 2 (humanoides procedurales, modo portero); sin prueba de usabilidad |
| **M4** | 4 | Modo carrera: creación de jugador, entrenamiento, progresión, confianza del entrenador, convocatorias, evaluación, calendario, temporada completa, **guardado** | Temporada completa jugable y guardable; pruebas de progresión, calendario y guardado | Núcleo ✔ entrega 2 (pruebas); interfaz Unity sin abrir en el editor |
| **M5** | 4 | Contratos, ofertas, negociación, traspasos, cesiones, renovaciones, fin de temporada (ascensos, envejecimiento, retiradas), premios, historial | 10 temporadas simuladas sin violar invariantes | ✔ entrega 2 (prueba automática y CLI) |
| **M6** | 5 | Contenido ficticio completo (3 países ficticios × 2 divisiones), escudos y equipaciones originales, menús finales, presentación de partido | Revisión de identidad visual y de registro de recursos | 6–8 semanas |
| **M7** | 6 | Optimización, medición en 5+ dispositivos, corrección de errores, build de lanzamiento Android, ficha de tienda, política de privacidad | Objetivos de rendimiento medidos y documentados | 4–6 semanas |
| **M8** | 6 | Evaluación y port a iOS | Build TestFlight | 3–4 semanas |
| **M9+** | B | Backend proxy de datos, primer paquete licenciado (si hay acuerdo) | Concesión de derechos verificada | según acuerdos |
