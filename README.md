# DORSAL — Carrera de Futbolista *(título provisional)*

Juego de fútbol para móviles centrado en el **modo carrera de un futbolista**: crea tu jugador, gánate un puesto,
progresa, negocia contratos, cambia de club y construye tu legado. Partidos 3D jugables en los que controlas a tu
futbolista mientras compañeros y rivales actúan por IA.

Inspirado en la *filosofía* de los modos carrera clásicos; **sin** código, interfaces, marcas, animaciones ni
recursos de FIFA / EA Sports FC u otras franquicias. El lanzamiento usa contenido 100 % ficticio y no tiene
ninguna licencia oficial.

## Estado

| Hito | Contenido | Estado |
|---|---|---|
| M0 | Investigación y diseño (`docs/`) | ✅ |
| M1 | Núcleo C# + motor de partido determinista + pruebas | ✅ (67 pruebas en verde) |
| M2 | Prototipo Unity jugable en Android | 🟡 scripts escritos, **sin compilar en Unity** |
| M3+ | Ver `docs/06-plan-desarrollo-hitos.md` | ⏳ |

## Estructura

```
docs/                     Investigación, GDD, arquitectura, costes, riesgos, criterios de aceptación, informes
src/Dorsal.Core/          Núcleo independiente del motor (.NET Standard 2.1, C# 9)
src/Dorsal.Cli/           Herramienta de consola: partidos, lotes de calibración, temporadas
tests/Dorsal.Core.Tests/  Pruebas xUnit
unity/                    Proyecto Unity 6.3 LTS (presentación) — ver unity/README.md
content/                  Datos (reglas de competición) y registro de recursos de terceros
tools/                    Scripts de compilación del núcleo para Unity
```

## Uso rápido

Requiere .NET SDK 8.

```bash
dotnet test                                                    # pruebas
dotnet run --project src/Dorsal.Cli -c Release -- match --seed 3          # un partido con resumen
dotnet run --project src/Dorsal.Cli -c Release -- match --seed 3 --verbose # todos los eventos
dotnet run --project src/Dorsal.Cli -c Release -- batch --n 200           # estadísticas de calibración
dotnet run --project src/Dorsal.Cli -c Release -- season --seed 7         # temporada completa con el motor
tools/build-core-for-unity.sh                                  # copia Dorsal.Core.dll a Unity
```

## Documentación

- [Resumen ejecutivo](docs/00-resumen-ejecutivo.md)
- [Comparativa de motores](docs/01-comparativa-motores.md)
- [APIs, recursos y licencias](docs/02-apis-recursos-licencias.md)
- [Estimación de costes](docs/03-estimacion-costes.md)
- [Documento de diseño (GDD)](docs/04-documento-diseno-juego.md)
- [Arquitectura técnica](docs/05-arquitectura-tecnica.md)
- [Plan por hitos](docs/06-plan-desarrollo-hitos.md)
- [Riesgos](docs/07-riesgos.md)
- [Alcance v0.1](docs/08-alcance-primera-version.md)
- [Criterios de aceptación](docs/09-criterios-aceptacion.md)
- [Informe de la entrega 1](docs/informes/entrega-01.md)
