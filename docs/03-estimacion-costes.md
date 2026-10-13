# 03 — Estimación de costes por escenarios

Todas las cifras de **desarrollo** y la mayoría de **recursos** son **hipótesis** 🔬 para planificar, no presupuestos. Las cifras de **licencias de software y tiendas** proceden de fuentes oficiales (ver `02` y `01`). Las licencias de contenido oficial no tienen precio público 💬.

## Supuestos

- Coste mensual cargado por persona (España, perfil medio-senior): **3.500–6.000 €** 🔬. Ajustar al país y modalidad real.
- Unity Personal es gratuito mientras ingresos + financiación < 200.000 $ en 12 meses; por encima, Unity Pro 2.310 $/puesto/año ✅.
- Configuración A no necesita backend para jugar.

## Escenario 1 — Indie mínimo (Configuración A)

Equipo: 1 programador generalista + 1 artista técnico a tiempo parcial; música/efectos y UI subcontratados. Duración: 14–18 meses hasta lanzamiento en Android.

| Partida | Estimación | Estado |
|---|---|---|
| Desarrollo (≈ 24–30 personas-mes) | 84.000–180.000 € | 🔬 |
| Motor (Unity Personal) | 0 € | ✅ (mientras se cumpla el umbral) |
| Recursos CC0 (Kenney, Quaternius, Poly Haven) | 0 € | ✅ |
| Animaciones de fútbol específicas (Asset Store o captura básica) | 300–3.000 € | 🔬 |
| Música y efectos con licencia comercial | 500–3.000 € | 🔬 |
| Dispositivos de prueba Android (3–5 de gama baja/media/alta) | 600–1.500 € | 🔬 |
| Google Play (registro) | 25 $ único | ✅ |
| Búsqueda y registro de marca (UE, 1 clase) + asesoría | 1.000–3.000 € | 🔬 (verificar tasas EUIPO vigentes) |
| Revisión legal de licencias y política de privacidad | 1.000–4.000 € | 🔬 |
| Infraestructura | 0–20 €/mes (analítica/crash reporting en planes gratuitos) | 🔬 |
| **Total aproximado** | **≈ 90.000–195.000 €** | 🔬 |

## Escenario 2 — Estudio pequeño (Configuración A con acabado comercial + iOS)

Equipo: 4–6 personas (2 programadores, 1 diseñador, 1–2 artistas 3D/animación, 1 UI/UX parcial, QA parcial). Duración: 18–24 meses.

| Partida | Estimación | Estado |
|---|---|---|
| Desarrollo (≈ 80–130 personas-mes) | 280.000–780.000 € | 🔬 |
| Motor | 0 € (Personal) o 2.310 $/puesto/año si se supera el umbral | ✅ |
| Captura de movimiento (estudio externo, 1–3 sesiones) | 5.000–30.000 € | 🔬 |
| Paquetes de animación / modelos de Asset Store | 1.000–5.000 € | 🔬 |
| Audio (música original + efectos + ambiente de estadio) | 5.000–20.000 € | 🔬 |
| Granja de dispositivos en la nube para pruebas | 0–300 €/mes | 🔬 |
| Apple Developer Program | 99 $/año | ✅ |
| Google Play | 25 $ único | ✅ |
| Legal (marca en varias jurisdicciones, contratos, privacidad) | 5.000–15.000 € | 🔬 |
| Backend opcional (guardado en la nube, telemetría) | 20–200 €/mes | 🔬 |
| **Total aproximado** | **≈ 300.000–870.000 €** | 🔬 |

## Escenario 3 — Expansión con licencias oficiales (Configuración B)

Se suma a 1 o 2 una vez que el juego base genere tracción.

| Partida | Estimación | Estado |
|---|---|---|
| Datos deportivos (feed) | Sportmonks 29–249 €/mes o Enterprise; API-Football 0 $ a ~39 $/mes; football-data.org gratuito no comercial / comercial por acuerdo | ✅ precios base, ⚠️ algunos contradictorios, 💬 derecho de incorporación al juego |
| Backend proxy para la API (claves fuera del cliente, caché, límites) | 20–150 €/mes | 🔬 |
| Derechos NIL colectivos (FIFPRO-CE) | Sin precio público; normalmente mínimo garantizado + royalties | 💬 |
| Licencia de una liga o club (escudos, camisetas, nombres) | Sin precio público | 💬 |
| Marcas de patrocinadores/fabricantes en equipaciones | Sin precio público, por marca | 💬 |
| Asesoría legal especializada en licencias deportivas | 10.000–50.000 € | 🔬 |
| Producción de contenido licenciado (modelos de camisetas, escudos, retratos) | 20.000–150.000 € según volumen | 🔬 |

> **Recomendación:** no comprometer gasto en el Escenario 3 hasta que el juego base demuestre retención. Mientras tanto, la arquitectura ya permite añadir paquetes licenciados sin reescribir el juego.
