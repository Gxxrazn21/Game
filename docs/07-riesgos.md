# 07 — Riesgos técnicos, económicos y legales

Probabilidad (P) e impacto (I): Alta / Media / Baja.

## Técnicos

| # | Riesgo | P | I | Mitigación |
|---|---|---|---|---|
| T1 | El partido 3D no alcanza 30 FPS estables en gama media (22 personajes animados) | M | A | LOD de mallas y animación (actualizar animadores lejanos a menor frecuencia), GPU *skinning*, un material por equipo con atlas, sombras simples, resolución de renderizado escalable. Medir pronto en dispositivo real (hito M2). |
| T2 | La IA de fútbol resulta poco creíble o aburrida | A | A | Motor de partido propio y determinista con herramientas de consola para simular miles de partidos y ajustar estadísticas (goles, posesión, tiros) contra rangos reales de referencia. |
| T3 | Animaciones genéricas que no parecen fútbol | A | M | Presupuestar compra o captura de animaciones específicas; *root motion* desacoplado (el motor manda posición, la animación sigue). |
| T4 | Controles táctiles incómodos | M | A | Controles configurables, zona de joystick flotante, botones contextuales, pruebas con usuarios en M3. |
| T5 | Corrupción de partidas guardadas | M | A | Escritura atómica (temporal + renombrado), copia de seguridad del guardado anterior, *checksum* SHA-256, migraciones versionadas, pruebas automáticas. |
| T6 | Inconsistencias de simulación entre temporadas (jugadores duplicados, contratos rotos) | M | A | Invariantes verificables (`WorldValidator`) ejecutadas en pruebas y en modo desarrollo tras cada operación de mercado y cambio de temporada. |
| T7 | Determinismo de coma flotante entre arquitecturas (x64 vs ARM) | M | B | Los resultados se **guardan**, nunca se re-simulan al cargar. El determinismo se garantiza por plataforma y versión para pruebas y repeticiones. |
| T8 | Dependencia de Unity (cambios de política) | B | M | Núcleo en C# puro sin `UnityEngine`; capa de presentación delgada. |

## Económicos

| # | Riesgo | P | I | Mitigación |
|---|---|---|---|---|
| E1 | Alcance demasiado grande para el presupuesto | A | A | Hitos con versión jugable en cada uno; primera versión con alcance cerrado (`08`). |
| E2 | Mercado móvil saturado; descubrimiento difícil | A | A | Nicho claro (carrera individual profunda, sin pay-to-win, juego sin conexión), demo gratuita, comunidad temprana. |
| E3 | Las licencias oficiales resultan inasumibles | A | M | La Configuración A es el producto; las licencias son expansión opcional. |
| E4 | Superar el umbral de Unity Personal | B | B | Es una buena noticia; presupuestar 2.310 $/puesto/año. |

## Legales

| # | Riesgo | P | I | Mitigación |
|---|---|---|---|---|
| L1 | Similitud con FIFA / EA Sports FC (trade dress, UI, nombres de modos) | M | A | Identidad visual propia; no reutilizar terminología de marca (p. ej. "Ultimate Team", "FUT"); revisión de UI antes del lanzamiento. |
| L2 | Nombre del juego en conflicto con una marca existente | M | A | Búsqueda de marcas (EUIPO, OEPM, USPTO, tiendas) antes de comunicar el nombre. "DORSAL" es provisional. |
| L3 | Nombres generados que coincidan con futbolistas reales famosos | B | M | El generador admite una lista de nombres reservados; revisión de los nombres de la base ficticia. |
| L4 | Uso de datos de jugadores reales sin base legal (RGPD) | A si se usan | A | Configuración A no usa jugadores reales. Configuración B solo con acuerdo y análisis jurídico. |
| L5 | Asumir que una API concede derechos de imagen | — | A | Prohibido por diseño: el `ContentRegistry` no activa paquetes cuyas concesiones de derechos no estén verificadas, vigentes y cubran el territorio. |
| L6 | Recurso de terceros con licencia mal entendida | M | M | Registro obligatorio de recursos (`content/third_party/ASSETS.md`) con copia de la licencia. |
| L7 | Contenido creado por usuarios (editor de nombres, escudos) que infrinja marcas | M | M | Si se añade editor, solo almacenamiento local, sin compartir en línea; términos de uso claros. Evaluar antes de permitir compartir. |
| L8 | Privacidad (analítica, anuncios, menores) | M | A | Sin recogida de datos en la v0.x; política de privacidad y formulario de "Data safety" de Google Play antes del lanzamiento. |
