# 00 — Resumen ejecutivo

> **Título de trabajo:** DORSAL — Carrera de Futbolista
> *(nombre provisional; requiere búsqueda de marcas en EUIPO, OEPM y USPTO antes de usarlo comercialmente)*
>
> **Fecha del informe:** 13-10-2026 · **Fase:** 1 (Investigación) y 2 (Diseño inicial)

## Qué es

Un juego de fútbol para móviles (Android primero, iOS después) centrado en el **modo carrera de un futbolista**. Inspirado en la *filosofía* de los modos carrera clásicos: crear un jugador, ganarse un puesto, progresar, negociar, cambiar de club y retirarse con un historial. **No** copia código, interfaces, marcas, animaciones ni recursos de FIFA / EA Sports FC ni de ninguna otra franquicia.

## Decisiones principales

| Decisión | Elección | Motivo resumido |
|---|---|---|
| Motor | **Unity 6.3 LTS + C#** (URP móvil) | Exportación a Android/iOS madura, animación humanoide con *retargeting*, Input System táctil + mando, Addressables para paquetes de contenido. Coste 0 € mientras ingresos + financiación < 200.000 $/año (plan Personal). Ver `01-comparativa-motores.md`. |
| Arquitectura | **Núcleo de simulación en C# puro** (`Dorsal.Core`, .NET Standard 2.1) independiente del motor | Partido, carrera, clubes, competiciones, contenido y guardado se prueban con `dotnet test` sin Unity; Unity solo presenta y lee la entrada. |
| Motor de partido | **Simulación determinista propia** (paso fijo, semilla) renderizada en 3D por Unity | La misma simulación sirve para el partido jugable, la vista televisiva desde el banquillo y la simulación rápida sin gráficos. |
| Contenido de lanzamiento | **Configuración A: 100 % ficticio** | Jugable sin APIs, sin licencias de terceros, sin conexión. |
| Contenido licenciado | **Configuración B: paquetes desacoplados** activados solo con derechos verificados, territorio y vigencia | Ruta realista hacia contenido oficial sin bloquear el lanzamiento. |
| Monetización | Prueba gratuita + **compra única** de la versión completa; cosméticos y expansiones después | Sin pay-to-win, sin anuncios en momentos críticos. No se integra monetización real hasta tener las cuentas y políticas de tienda listas. |

## Conclusiones de la investigación (resumen)

1. **Las APIs deportivas venden datos, no derechos de imagen.** Sportmonks, API-Football y football-data.org son *proveedores de datos*. Las tres dejan claro (en sus términos o documentación) que escudos y fotos pertenecen a sus titulares y que el cliente debe obtener sus propios permisos. Ninguna es una solución integral de licencias.
2. **Los derechos de nombre, imagen y semejanza (NIL) de jugadores** se licencian de forma colectiva a través de **FIFPRO Commercial Enterprises** (formulario para desarrolladores de videojuegos, sin precio público) o individualmente. No incluyen escudos, camisetas ni marcas de competiciones.
3. **Escudos, equipaciones y competiciones** requieren acuerdos con ligas, clubes o federaciones (ejemplo verificado: licencia de la Premier League a Football Manager desde 2024/25). Coste no público; requiere negociación.
4. **Incluso los datos estadísticos de jugadores reales tienen riesgo legal** en la UE/Reino Unido (protección de datos personales; caso *Project Red Card* en Reino Unido). Por eso la Configuración A no usa jugadores reales.
5. **Recursos gráficos legítimos existen**: Kenney y Quaternius (CC0), Poly Haven (CC0), Mixamo (uso comercial libre de royalties en juegos, sin redistribuir los archivos sueltos), Unity Asset Store (EULA estándar permite uso comercial integrado).

Detalle y fuentes en `02-apis-recursos-licencias.md`.

## Qué incluye esta entrega

- Documentos 00–09 en `docs/` (investigación, diseño, arquitectura, costes, riesgos, alcance, criterios de aceptación).
- Inicio de la implementación real:
  - `src/Dorsal.Core`: dominio (atributos, jugadores, clubes, competiciones), RNG determinista, generador de mundo ficticio, calendario, sistema de paquetes de contenido con control de licencias y **motor de partido determinista** (física de balón, IA de 22 jugadores, porteros, árbitro, faltas, tarjetas, fuera de juego, saques, marcador, cronómetro, tiempo añadido, cambios, lesiones, valoraciones).
  - `src/Dorsal.Cli`: ejecutor de consola para simular partidos y lotes sin Unity.
  - `tests/Dorsal.Core.Tests`: pruebas automatizadas (xUnit).
  - `unity/Dorsal`: esqueleto del proyecto Unity que visualiza el motor en 3D con controles táctiles. **No se ha compilado en Unity en este entorno** (no hay editor Unity disponible).

## Lo que NO se afirma

- No se afirma ninguna licencia oficial. El juego no tiene ninguna.
- No se afirma ninguna tasa de FPS: no se ha medido en dispositivos reales.
- Los precios citados proceden de páginas oficiales consultadas mediante buscador el 13-10-2026 y **deben reconfirmarse** antes de contratar (varias fuentes eran contradictorias; se indica en cada caso).
