# 02 — APIs, recursos y licencias

Fecha de consulta: **13-10-2026**.

**Método y limitaciones.** En este entorno, el proxy de red bloquea la descarga directa de las webs de los proveedores. La información se obtuvo con un buscador restringido a los **dominios oficiales** de cada proveedor y se contrastó entre varias páginas del mismo dominio. Donde había contradicciones se indica. **Antes de contratar o publicar hay que releer los términos vigentes y pedir confirmación por escrito.** Esto no es asesoramiento jurídico.

### Leyenda de estado

| Código | Significado |
|---|---|
| ✅ **Confirmado** | Aparece en una página oficial del proveedor. |
| ⚠️ **Contradictorio** | Las páginas oficiales no coinciden; hay que preguntar. |
| 💬 **Consulta comercial** | No hay información pública; hay que negociarlo. |
| ⚖️ **Autorización legal** | Necesita un acuerdo con el titular de los derechos o revisión jurídica. |
| 🔬 **Hipótesis** | Evaluación propia, no verificada. |

### Tipos de derechos (no confundirlos)

| Tipo | Ejemplo | ¿Lo da una API de datos? |
|---|---|---|
| Datos estadísticos de partidos | Resultado 2-1, goleador, minuto | Se licencia el *acceso* al feed; el uso comercial depende del contrato. |
| Datos personales | Nombre, fecha de nacimiento, altura de un jugador real | Son datos personales (RGPD/UK GDPR). Riesgo propio aunque la API los entregue. |
| Nombre, imagen y semejanza (NIL) | Usar a un jugador real con su cara en el juego | **No.** FIFPRO-CE, sindicatos o el propio jugador. |
| Fotografías | Retratos de jugadores | **No.** Copyright del fotógrafo/agencia/club. |
| Marcas | Escudos, nombres de clubes y competiciones, patrocinadores, fabricantes | **No.** Club, liga, federación o marca. |
| Diseños | Camisetas, estadios | **No.** Club / fabricante / propietario del estadio. |

---

## A. Proveedores de datos deportivos

### A.1 Sportmonks Football API

| # | Punto | Información | Estado |
|---|---|---|---|
| 1 | Web oficial | <https://www.sportmonks.com/football-api/> | ✅ |
| 2 | Documentación | <https://docs.sportmonks.com/football/> (API 3.0) | ✅ |
| 3 | Endpoints y datos | Ligas, temporadas, partidos (*fixtures*), *livescores*, equipos, jugadores con estadísticas, clasificaciones, entrenadores, plantillas, traspasos, cuotas (complemento). Ejemplos en documentación oficial: `/v3/football/standings/season/{id}`, `/v3/football/players/{id}?include=statistics`, `/v3/football/teams/{id}`, `/v3/football/livescores/inplay`. | ✅ (lista completa a revisar en la documentación) |
| 4 | Precio y límites | Gratis: 2 ligas (Superliga danesa y Premiership escocesa). Starter 29 €/mes (5 ligas, 2.000 llamadas/entidad/hora). Growth 99 €/mes (30 ligas, 2.500/h). Pro 249 €/mes (120 ligas, 3.000/h). Enterprise a medida (todas las ligas, 5.000/h). Prueba de 14 días con tarjeta. Descuento anual de ~20 % (las páginas no coinciden en la cifra). Complementos: llamadas extra desde 29 €/mes, ligas extra desde 4 €/mes. | ✅ precios base (changelog oficial); ⚠️ descuento anual |
| 5 | Uso comercial | Los **Términos de servicio** dicen que el material es "strictly for personal and non-commercial use", mientras que el FAQ y la página *integrity-support* afirman que el uso comercial está permitido conforme a sus términos. | ⚠️ — pedir confirmación escrita |
| 6 | Almacenamiento y caché | La documentación recomienda cachear (entidades de referencia horas/días; datos en vivo segundos) y usar un proxy backend para no exponer el token. No se encontró una cláusula explícita sobre almacenamiento permanente. | ✅ recomendación técnica; 💬 permiso de almacenamiento permanente |
| 7 | Redistribución / uso en videojuego | Mostrar datos dentro del propio producto: aparentemente sí. Redistribuir el feed "como feed" requiere aprobación escrita; reventa prohibida sin consentimiento. Integrar una base de datos derivada dentro de un juego distribuido offline **no está cubierto expresamente**. | 💬 ⚖️ |
| 8 | Imágenes y logotipos | "All logos and profile photos are copyrighted by their legal owner" — el cliente debe acreditar sus propios derechos. | ✅ (no concede derechos) |
| 9 | Atribución | No localizada en las fuentes consultadas. | 💬 |
| 10 | Restricciones geográficas | No localizadas. | 💬 |
| 11 | Solicitud de licencia | Alta en la web; plan Enterprise y redistribución mediante contacto comercial. | ✅ |
| 12 | Limitaciones y alternativas | Proveedor de **datos**, no de derechos de imagen. Alternativas: API-Football, football-data.org, Stats Perform (Opta), Sportradar (enterprise, sin precios públicos). | ✅ |

### A.2 API-Football (API-SPORTS)

| # | Punto | Información | Estado |
|---|---|---|---|
| 1 | Web oficial | <https://www.api-football.com/> · <https://api-sports.io/sports/football> | ✅ |
| 2 | Documentación | <https://www.api-football.com/documentation-v3> | ✅ |
| 3 | Endpoints y datos | `/leagues` (con indicadores de cobertura por temporada), `/fixtures` (eventos, alineaciones, estadísticas, jugadores), `/fixtures/players`, `/standings`, `/players`, `/players/squads`, `/coachs`, `/transfers`, `/injuries`, `/sidelined`, entre otros. Logos sirven por CDN y no consumen cuota. | ✅ (blog oficial del proveedor; verificar parámetros en la referencia v3) |
| 4 | Precio y límites | Free: 100 peticiones/día, 10/min. Pro: 7.500/día, 300/min. Ultra: 75.000/día, 450/min. Mega: 150.000/día, 900/min. Planes a medida hasta 1.500.000/día. La cuota se reinicia a las 00:00 UTC. **Precios contradictorios** entre páginas oficiales (Pro 19 $; Ultra 29 $ o 39 $; Mega 39 $ o 29 $). | ✅ límites; ⚠️ precios |
| 5 | Uso comercial | Los términos indican que **no proporcionan una "licencia"** para el uso y publicación de los datos, y que apuestas, televisión, *fantasy* o distribución masiva pueden requerir licencias adicionales de los titulares. | ⚠️ ⚖️ |
| 6 | Almacenamiento y caché | Recomiendan descargar logos una vez y servirlos desde almacenamiento propio (técnico, no es una cesión de derechos). | ✅ técnico; 💬 legal |
| 7 | Redistribución | Reventa de datos sin permiso prohibida. Inclusión en un juego distribuido: no cubierta expresamente. | 💬 ⚖️ |
| 8 | Imágenes y logotipos | No reclaman propiedad de los logos; se ofrecen "solely for identification and descriptive purposes"; su uso "may require additional authorization or licensing from the respective rights holders". | ✅ (no concede derechos) |
| 9 | Atribución | No localizada. | 💬 |
| 10 | Restricciones geográficas | No localizadas. | 💬 |
| 11 | Solicitud | Alta directa en el *dashboard*; planes a medida por contacto. | ✅ |
| 12 | Limitaciones | Proveedor de datos sin derechos de imagen ni marcas. Página de términos con antigüedad (~570 días según el buscador): revisar versión vigente. | ✅ |

### A.3 football-data.org

| # | Punto | Información | Estado |
|---|---|---|---|
| 1 | Web oficial | <https://www.football-data.org/> | ✅ |
| 2 | Documentación | <https://docs.football-data.org/general/v4/index.html> | ✅ |
| 3 | Endpoints y datos | v4: `/v4/competitions`, `/v4/competitions/{code}/teams`, `/v4/competitions/{code}/matches?season=YYYY`, `/v4/matches`, `/v4/matches/{id}`, `/v4/teams/{id}` (plantilla), `/v4/teams/{id}/matches`, `/v4/persons/{id}` (jugadores, entrenadores, árbitros), `/v4/areas`. Autenticación con cabecera `X-Auth-Token`. | ✅ |
| 4 | Precio y límites | Plan gratuito "Free Forever": 12 competiciones, resultados con retraso, 10 llamadas/min. Política oficial de límites: Free 10/min, Standard 30/min, superiores 60/min. **Precios de pago contradictorios** entre la página de precios (12 €/mes *livescores*, 29 €/mes *deep data*, 15 €/mes complemento de estadísticas) y la página de cobertura (49/99/199 €). | ✅ plan gratuito y límites; ⚠️ precios de pago |
| 5 | Uso comercial | El operador indica en su blog que el uso gratuito es **no comercial** y que para uso comercial hay que contactar y acordar condiciones. | ✅ — 💬 obligatorio contactar |
| 6 | Almacenamiento | No localizado. | 💬 |
| 7 | Redistribución | Términos (actualizados 2018): cada clave ligada a una aplicación/dominio. Uso en juego distribuido: no cubierto. | 💬 ⚖️ |
| 8 | Imágenes y logos | No se encontraron cesiones de derechos. | 💬 (asumir que no) |
| 9 | Atribución | Los términos exigen atribución en la aplicación o web. | ✅ |
| 10 | Restricciones geográficas | No localizadas. | 💬 |
| 11 | Solicitud | Registro en <https://www.football-data.org/client/register>; comercial por contacto. | ✅ |
| 12 | Limitaciones | Proyecto pequeño; adecuado para prototipos y herramientas internas de diseño, no como base de contenido comercial sin acuerdo. | 🔬 |

### A.4 Proveedores enterprise (referencia)

| Proveedor | Información | Estado |
|---|---|---|
| **Stats Perform (Opta)** | Licencia datos a medios, apuestas y *fantasy*. EA licenció datos Opta para calibrar valoraciones en FC 24 (prensa 2023). Precios no públicos. <https://developers.statsperform.com/stats-perform-is-opta> | ✅ existencia; 💬 precio |
| **Sportradar** | Proveedor enterprise. No se encontró información específica para videojuegos. | 💬 |

> **Conclusión A:** las tres APIs analizadas son **proveedores de datos**, no soluciones de licenciamiento. Ninguna concede derechos sobre escudos, fotos, camisetas o identidades de jugadores. Para un juego distribuido (base de datos dentro del cliente) hay que negociar expresamente el derecho de **incorporación/redistribución**.

---

## B. Titulares de derechos (licencias oficiales)

### B.1 FIFPRO Commercial Enterprises (FIFPRO-CE)

| Punto | Información | Estado |
|---|---|---|
| Web | <https://www.fifpro.org/en/who-we-are/commercial> | ✅ |
| Qué licencia | Derechos **colectivos de nombre, imagen y semejanza (NIL)** de futbolistas, a través de sindicatos de jugadores de cerca de 60 países, principalmente para videojuegos. Licenciatarios públicos: Konami (eFootball, renovado), Gala Sports; Soccerverse anunció en dic-2025 derechos sobre más de 65.000 jugadores. | ✅ |
| Qué **no** licencia | Escudos, nombres de clubes, camisetas, competiciones ni marcas. No cubre a jugadores cuyos derechos estén cedidos a otros o no pertenezcan a sindicatos miembros; hay precedentes judiciales (Brasil) que limitan su capacidad de ceder derechos individuales. | ✅ (prensa y análisis jurídicos) |
| Proceso | Formulario para desarrolladores: <https://www.fifpro.org/en/who-we-are/contact/video-game-developers>. Pide: derechos solicitados, concepto, mercado objetivo, modelo de monetización, problemas de PI existentes y autorizaciones de plataformas (Apple, Google…). | ✅ |
| Precio | No público. | 💬 |

### B.2 Ligas, clubes y federaciones

| Punto | Información | Estado |
|---|---|---|
| Modelo | Las ligas suelen centralizar derechos de escudos, camisetas y nombres de sus clubes (sujetos a derechos propios de cada club) y sublicenciarlos; los clubes también firman acuerdos directos (p. ej. Konami–Juventus 2019). | ✅ (análisis Mondaq) |
| Ejemplo verificado | Premier League ↔ Football Manager: licencia de 4 años desde 2024/25 con logos, camisetas e imágenes de jugadores de los 20 clubes. | ✅ |
| Federaciones / selecciones | Escudos y nombres oficiales de selecciones son marcas de cada federación. Usar el **nombre del país** como nacionalidad es un dato; usar el escudo o la camiseta oficial no. | ⚖️ |
| Patrocinadores y fabricantes | Logos en camisetas y vallas: licencias aparte de cada marca. Una licencia de club **no** implica automáticamente las marcas de sus patrocinadores. | ⚖️ |
| Estadios | Nombres comerciales y diseños identificables pueden requerir licencia. | ⚖️ |
| Precio | No público en ningún caso. | 💬 |

### B.3 Riesgo de datos personales

En Reino Unido, el grupo *Project Red Card* (más de 850 futbolistas) envió en 2021 reclamaciones previas contra empresas de apuestas y juego por el uso de sus datos de rendimiento, alegando el RGPD/Data Protection Act 2018. No se encontró información actualizada sobre su resultado. **Implicación:** usar datos de jugadores reales, incluso solo estadísticas, en un producto comercial distribuido en la UE/Reino Unido requiere análisis jurídico. ⚖️

---

## C. Recursos gráficos y animaciones con licencia comercial

| Fuente | Licencia | Uso comercial | Modificación | Atribución | Redistribución | Estado |
|---|---|---|---|---|---|---|
| **Kenney** (<https://kenney.nl/support>) | CC0 | Sí | Sí | Opcional | Sí (dominio público) | ✅ (revisar el archivo de licencia de cada paquete descargado) |
| **Quaternius** (p. ej. *Universal Base Characters*, *Universal Animation Library 2*) | CC0 por paquete | Sí | Sí | No requerida | Sí | ✅ por página de paquete |
| **Poly Haven** (texturas, HDRI, modelos) | CC0 | Sí | Sí | No requerida | Sí | ✅ (fuente secundaria; comprobar en polyhaven.com) |
| **Mixamo** (Adobe) | Términos propios | Sí, libre de royalties en videojuegos | Sí | No requerida | **No** se pueden redistribuir los archivos sueltos (ni paquetes para motores); excluido el entrenamiento de IA | ✅ FAQ oficial |
| **Unity Asset Store** | EULA estándar (salvo "Restricted"/no estándar) | Sí, integrado en el producto | Sí | Según el activo | No como activos sueltos ni extraíbles | ✅ |

**Política del proyecto:**
1. Cada recurso de terceros se registra en `content/third_party/ASSETS.md` con: nombre, URL, autor, licencia, fecha de descarga, copia del archivo de licencia y uso.
2. No se incorporan recursos con licencia "Restricted", no estándar sin revisar, ni de sitios sin condiciones claras.
3. **Nunca**: imágenes de Google, recursos extraídos de otros juegos, escudos de webs sin licencia.
4. Animaciones de fútbol específicas (regate, tiro, parada) probablemente requieran **captura propia o compra** en Asset Store: las bibliotecas CC0 son genéricas. 🔬

---

## D. Plataformas de distribución

| Concepto | Dato | Estado |
|---|---|---|
| Google Play — registro | 25 $ pago único | ✅ <https://support.google.com/googleplay/android-developer/answer/6112435> |
| Google Play — comisión | Las páginas de 2026 describen un nuevo modelo con 10 % sobre el primer 1 M$ anual (vigente desde el 30-06-2026 en ciertos programas/regiones); el modelo 2021 era 15 % / 30 %. | ⚠️ revisar página "Service fees" |
| Apple Developer Program | 99 $/año | ✅ <https://developer.apple.com/programs/whats-included/> |

---

## Fuentes principales

- Sportmonks: <https://docs.sportmonks.com/v3/changelog/changelog>, <https://www.sportmonks.com/terms-of-service/>, <https://www.sportmonks.com/faq/>, <https://www.sportmonks.com/integrity-support/>, <https://docs.sportmonks.com/football/welcome/best-practices>
- API-Football: <https://www.api-football.com/pricing/>, <https://api-sports.io/terms>, <https://www.api-football.com/news/post/how-ratelimit-works>, <https://www.api-football.com/news/post/list-of-all-available-endpoints>, <https://www.api-football.com/documentation-v3>
- football-data.org: <https://www.football-data.org/pricing>, <https://docs.football-data.org/general/v4/policies.html>, <https://www.football-data.org/coverage>, <https://www.football-data.org/blog>, <https://docs.football-data.org/general/v4/resources.html>
- FIFPRO: <https://www.fifpro.org/en/who-we-are/commercial>, <https://www.fifpro.org/en/who-we-are/contact/video-game-developers>, <https://fifpro.org/en/who-we-are/our-organisation/fifpro-and-konami-extend-e-football-agreement-renewing-commitment-to-innovation-and-welfare>, <https://soccerverse.com/news/soccerverse-secures-fifpro-license>
- Licencias de ligas: <https://www.gamedeveloper.com/press-release/the-premier-league-is-coming-to-football-manager>, <https://webiis05.mondaq.com/uk/licensing-syndication/831992/konami-juventus-and-the-licensing-of-ip-for-use-in-sports-video-game-content>
- Datos personales: <https://sportsinsights.lewissilkin.com/post/102gcsz/project-red-card-footballers-to-sue-over-use-of-their-player-data-and-match-stat>
- Stats Perform: <https://developers.statsperform.com/stats-perform-is-opta>, <https://insidersport.com/2023/10/02/stats-perform-ea-data-partnership/>
- Recursos: <https://kenney.nl/support>, <https://quaternius.com/packs/universalbasecharacters.html>, <https://quaternius.com/packs/universalanimationlibrary2.html>, <https://helpx.adobe.com/creative-cloud/faq/mixamo-faq.html>, <https://unity.com/legal/as-terms>, <https://assetstore.unity.com/browse/eula-faq>
- Tiendas: <https://support.google.com/googleplay/android-developer/answer/112622>, <https://developer.apple.com/programs/whats-included/>
