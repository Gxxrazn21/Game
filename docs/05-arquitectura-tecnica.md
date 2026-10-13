# 05 — Arquitectura técnica

## 1. Visión general

```
┌──────────────────────────── Unity 6.3 LTS (presentación) ────────────────────────────┐
│  Escenas: Boot · Menús · Partido                                                     │
│  MatchView (render 3D + interpolación) · TouchControls / Gamepad · Cámaras · UI      │
│  Adaptadores: UnitySaveStorage (persistentDataPath) · AddressablesContentSource      │
└───────────────▲──────────────────────────────────────────────────────▲───────────────┘
                │ comandos de entrada / lectura de estado              │ datos de contenido
┌───────────────┴──────────── Dorsal.Core (C# puro, .NET Standard 2.1) ┴───────────────┐
│ Domain        Atributos, Jugador, Club, Contrato, Competición, Temporada, Partido    │
│ Random        RNG determinista con semilla (xoshiro128**)                            │
│ Match         Motor de partido determinista de paso fijo (física, IA, árbitro)       │
│ Simulation    Simulación rápida de partidos, calendario, clasificación, temporadas   │
│ Career        Creación, progresión, entrenamiento, confianza, contratos, mercado     │
│ Content       Paquetes de contenido + concesiones de derechos + registro             │
│ World         Generador de mundo ficticio, validador de invariantes                  │
│ Persistence   Guardado versionado, checksum, migraciones, escritura atómica          │
│ Data (puertos) ISportsDataProvider (opcional, Configuración B) con caché y fallback  │
└──────────────────────────────────────────────────────────────────────────────────────┘
                │ (solo Configuración B, opcional)
┌───────────────┴─────────── Backend propio (futuro) ───────────────────────────────────┐
│ Proxy de API deportiva: guarda las claves, cachea, limita peticiones, genera          │
│ instantáneas de datos versionadas y firmadas que el cliente descarga.                │
└──────────────────────────────────────────────────────────────────────────────────────┘
```

**Reglas:**
- `Dorsal.Core` no referencia `UnityEngine` ni hace E/S directa: la E/S pasa por interfaces (`ISaveStorage`, `IContentSource`, `IClock`).
- C# 9 como máximo (compatibilidad con Unity 6). Sin `record` ni `init` (requieren tipos ausentes en .NET Standard 2.1).
- El estado del juego es serializable (JSON con Newtonsoft.Json, que Unity incluye como paquete oficial `com.unity.nuget.newtonsoft-json`).
- Cada sistema expone servicios puros (`entrada → salida`) fáciles de probar.

## 2. Motor de partido

- **Paso fijo** configurable (por defecto 1/20 s en simulación sin gráficos; Unity puede usar 1/50 s). Determinista para una semilla, plataforma y versión dadas.
- **Coordenadas**: campo 105 × 68 m, origen en el centro, X a lo largo (el equipo local ataca hacia +X en la primera parte), Y a lo ancho, Z altura. Unity mapea `(x, z, y)`.
- **Balón**: posición/velocidad 3D, gravedad, rozamiento con el aire, rodadura, bote con restitución. Poseedor opcional (conducción).
- **Agentes**: posición, velocidad, orientación, energía, tarjetas, rol en la formación, atributos.
- **IA**: decisiones cada 0,2–0,3 s por agente (con reflejos como retardo).
  - Fase con balón: acompañar el bloque, ofrecer líneas de pase, desmarques al espacio respetando el fuera de juego.
  - Fase sin balón: presión del más cercano (intensidad táctica), cobertura, bloque compacto desplazado al balón, marcaje zonal.
  - Poseedor: evalúa tiro, pases (progresión, apertura de línea, dificultad, riesgo de fuera de juego), conducción, centro, despeje.
  - Portero: colocación en la bisectriz balón-portería, reacción y estirada ante tiros, salidas a balones sueltos en el área.
- **Árbitro**: goles, fuera de banda/fondo, faltas (probabilidad en entradas según atributos y agresividad), tarjetas, penaltis, fuera de juego en el momento del pase, ventaja simple, tiempo añadido según interrupciones.
- **Entrada humana**: `PlayerInput` por tick para el agente controlado (dirección, sprint, acción). Si el futbolista no está en el campo, el partido funciona igual (vista TV).
- **Salida**: estado consultable (posiciones, balón, marcador, reloj), lista de eventos (`MatchEvent`) y estadísticas por jugador con valoración final.

## 3. Modelos de datos

Notación simplificada; los campos reales están en `src/Dorsal.Core`.

```text
UserProfile        { Id, DisplayName, Settings(Graphics, Controls, Audio), Slots[] }
SaveSlot           { SlotId, CareerId, LastSaved, GameVersion, SchemaVersion, Checksum }
SaveGame           { SchemaVersion, GameVersion, CreatedUtc, SavedUtc, Seed, RngState,
                     World, Career, ActiveContentPacks[] }

Player             { Id, FirstName, LastName, Nationality, BirthDate, HeightCm, WeightKg,
                     Build, PreferredFoot, WeakFoot, Position, SecondaryPositions[],
                     PlayStyle, Attributes, Potential, Personality, Appearance,
                     Condition{Fitness, Fatigue, Form, Morale},
                     Injury?, Suspension?, ContractId?, ClubId?, Reputation, IsUserPlayer,
                     SourcePackId }
Attributes         { Acceleration, SprintSpeed, Finishing, ShotPower, LongShots, Positioning,
                     ShortPassing, LongPassing, Crossing, Vision, Dribbling, BallControl,
                     Balance, Agility, Marking, Tackling, DefensiveAwareness, Strength,
                     Stamina, Jumping, Reactions, Composure, Aggression, Heading,
                     GkDiving, GkHandling, GkKicking, GkPositioning, GkReflexes }
AttributeHistory   { PlayerId, Date, Snapshot }               // para gráficos de progresión
Club               { Id, Name, ShortName, City, Colors{Primary,Secondary}, CrestSpec,
                     Reputation, Budget{Transfer, WageWeekly}, Stadium, LeagueId,
                     Tactics{Formation, Pressing, DefensiveLine, Width, Tempo},
                     SquadPlayerIds[], CoachId, SourcePackId }
Coach              { Id, Name, Philosophy, Formation, TrustByPlayer{PlayerId→0..100} }
Competition        { Id, Name, Type(League|Cup), RulesId, Tier, ClubIds[] }
CompetitionRules   { Id, Rounds(1|2), PointsWin/Draw/Loss, Tiebreakers[],
                     PromotionSlots, RelegationSlots, MaxSubs, SubWindows,
                     YellowCardBanThreshold, SquadSizeMin/Max, ForeignPlayerLimit? }
Season             { Id, Year, StartDate, EndDate, CompetitionSeasons[] }
CompetitionSeason  { CompetitionId, Fixtures[], Table[], Stats{Scorers, Assists} }
Fixture            { Id, CompetitionId, Round, Date, HomeClubId, AwayClubId, Result? }
MatchResult        { HomeGoals, AwayGoals, Events[], PlayerStats[], Seed, EngineVersion }
PlayerMatchStats   { PlayerId, ClubId, Minutes, Goals, Assists, Shots, ShotsOnTarget,
                     Passes, PassesCompleted, KeyPasses, Dribbles, Tackles, Interceptions,
                     Saves, Fouls, Yellow, Red, Rating }
Contract           { Id, PlayerId, ClubId, Start, End, WageWeekly, Bonuses{Appearance,Goal},
                     ReleaseClause?, Role, IsLoan, ParentClubId? }
TransferOffer      { Id, PlayerId, FromClubId, ToClubId, Fee, Type(Transfer|Loan|FreeAgent),
                     ProposedContract, Status, ExpiresOn, NegotiationRounds[] }
TransferRecord     { PlayerId, FromClubId, ToClubId, Date, Fee, Type }
TrainingSession    { Date, PlayerId, Focus, Intensity, Grade, AttributeDeltas, FatigueDelta }
Injury             { Type, Severity, StartDate, ExpectedReturn }
Suspension         { CompetitionId, MatchesRemaining, Reason }
ContentPackManifest{ Id, Name, Version, Kind(Original|Licensed), ContentTypes[],
                     RightsGrants[], DataVersion, MinGameVersion }
RightsGrant        { RightType(SportsData|PlayerNames|PlayerLikeness|ClubMarks|
                     CompetitionMarks|KitDesigns|SponsorMarks|StadiumMarks|Photos|Original),
                     Licensor, AgreementRef, Territories[], Platforms[], ValidFrom,
                     ValidUntil, VerifiedBy?, VerifiedOn? }
ExternalDataVersion{ ProviderId, Dataset, FetchedUtc, SourceVersion, Checksum, PackId }
```

## 4. Contenido y licencias (Configuración A / B)

- Todo dato de mundo (club, jugador, competición) lleva `SourcePackId`.
- `ContentRegistry.Evaluate(pack, contexto{fecha, territorio, plataforma, buildRelease})` activa un paquete solo si **cada tipo de contenido** del paquete está cubierto por una concesión **verificada**, **vigente** y que incluya el **territorio** y la **plataforma**. Paquetes `Original` (creados por el estudio) siempre activos.
- Si un paquete licenciado caduca o no aplica en el territorio, sus entidades se sustituyen por equivalentes ficticios del paquete base (mapeo declarado en el paquete) o se ocultan; la partida sigue siendo jugable.
- El juego **arranca y se juega con cero paquetes licenciados** (prueba automática).

## 5. Datos externos (solo Configuración B)

- El cliente **nunca** contiene claves de API. El backend propio consulta al proveedor, cachea, aplica límites y genera **instantáneas versionadas** (`ExternalDataVersion`) que el cliente descarga como paquete de contenido.
- Fallback en cadena: instantánea descargada → última instantánea local válida → paquete base ficticio. Fallo de red o cuota agotada nunca bloquean el juego.
- Solo se activan datos para los que exista derecho de **incorporación** al juego (ver `02`).

## 6. Guardado y persistencia

- Formato: JSON (Newtonsoft) comprimido con GZip; cabecera con `SchemaVersion`, `GameVersion`, `Checksum` SHA-256 del cuerpo.
- **Escritura atómica**: escribir `slot.tmp` → verificar → mover `slot.sav` a `slot.bak` → renombrar `slot.tmp` a `slot.sav`.
- **Carga**: verificar checksum; si falla, intentar `slot.bak`; si falla, informar sin sobrescribir nada.
- **Migraciones**: cadena `ISaveMigration(from → to)` sobre el JSON antes de deserializar.
- **Autoguardado** en momentos seguros (tras partido, tras firmar contrato, al avanzar semana) y guardado manual.
- **Exportar/Importar** copia de seguridad (archivo `.dorsal`) mediante el selector de archivos del sistema.
- **Reproducibilidad**: el estado del RNG se guarda; los resultados ya jugados se almacenan, no se recalculan.

## 7. Rendimiento (objetivos, sin medir todavía)

- Objetivo: 30 FPS estables en gama media, 60 FPS opcional; niveles de calidad Bajo/Medio/Alto; límite de FPS configurable; escala de resolución de renderizado.
- Partido: 22 personajes con LOD (3 niveles), GPU *skinning*, animadores a menor frecuencia fuera de cámara, un material por equipo, sombras de *blob* en Bajo.
- Simulación de liga en segundo plano fuera del hilo principal entre pantallas.
- Medición con Unity Profiler + Android GPU Inspector / Perfetto en dispositivos reales (hito M6).

## 8. Estructura del repositorio

```
docs/                     Documentación de fases
src/Dorsal.Core/          Núcleo C# (.NET Standard 2.1)
src/Dorsal.Cli/           Herramienta de consola (simulaciones, ajuste)
tests/Dorsal.Core.Tests/  Pruebas xUnit
unity/Dorsal/             Proyecto Unity (presentación)
tools/                    Scripts (compilar núcleo para Unity, etc.)
content/                  Paquetes de contenido y registro de recursos de terceros
```
