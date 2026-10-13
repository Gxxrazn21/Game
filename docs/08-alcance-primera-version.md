# 08 — Alcance exacto de la primera versión jugable (v0.1, hitos M1–M2)

## Incluido

**Núcleo (`Dorsal.Core`) — M1**
- Modelo de dominio: atributos (incluidos porteros), jugador, club, tácticas, competición con reglas configurables, calendario.
- RNG determinista con semilla y estado serializable.
- Generador de mundo ficticio: 1 país ficticio, 1 liga de 12 clubes con nombres, colores y plantillas de 20 jugadores originales.
- Calendario de liga a doble vuelta (método del círculo).
- Sistema de paquetes de contenido con concesiones de derechos; paquete base `Original` activo; paquetes licenciados desactivados si no hay derechos verificados.
- **Motor de partido determinista** con: física del balón, locomoción por atributos, pase raso/elevado, tiro, centro, cabezazo, conducción/regate, entradas, intercepciones, despejes, control al recibir, portero con colocación/estirada/atrapar/despejar, faltas, tarjetas (incl. doble amarilla → expulsión), penaltis, fuera de juego, saques de banda/córner/puerta, tiros libres, marcador, cronómetro, descanso, tiempo añadido, cambios del entrenador IA, lesiones, 4 formaciones y tácticas básicas, estadísticas y valoración individual.
- Entrada humana para un agente controlado.
- CLI: simular un partido con resumen de eventos; simular lotes para estadísticas de ajuste.
- Pruebas automáticas.

**Unity — M2**
- Escena de partido con campo, porterías, 22 jugadores (cápsulas de color por equipo), balón, cámara TV y cámara de jugador.
- Joystick virtual + botones Pase / Tiro / Sprint / Entrada; teclado y mando para pruebas en editor.
- HUD con marcador, cronómetro y avisos (gol, falta, tarjeta, fuera de juego).

## Excluido de v0.1 (planificado)

Modo carrera completo (M4–M5), guardado (M4), animaciones humanoides (M3), menús finales (M6), audio, monetización, iOS, backend, contenido licenciado.

## Estado en esta entrega

- M1: **implementado y probado** con `dotnet test` (68 pruebas; ver `docs/informes/entrega-01.md`).
- M2: **código de Unity escrito pero no compilado** en este entorno (no hay editor Unity). Debe abrirse en Unity 6.3 LTS para verificarlo.
