# 01 — Comparativa técnica de motores

Fecha: 13-10-2026. Las afirmaciones marcadas **[V]** están verificadas en fuentes oficiales (ver "Fuentes"). Las marcadas **[E]** son evaluación técnica del equipo, no mediciones. **No se ha medido rendimiento en dispositivos reales**; cualquier cifra de FPS será una hipótesis hasta la Fase 6.

## Requisitos que deciden la elección

1. Partido 3D con 22 jugadores animados + balón + cámara, a 30 FPS estables (60 opcional) en Android de gama media.
2. Animación humanoide con *retargeting* (para usar bibliotecas de animaciones con licencia comercial sobre modelos propios).
3. Controles táctiles y compatibilidad con mandos Bluetooth.
4. Paquetes de contenido descargables y desactivables (Configuración B).
5. Núcleo de simulación probable fuera del motor (pruebas automáticas de carrera, contratos, temporadas, guardado).
6. Tamaño de descarga razonable y coste asumible para un equipo pequeño.

## Tabla comparativa

| Criterio | Unity 6.3 LTS (C#) | Godot 4.6 (GDScript / C#) | Unreal Engine 5 (C++/Blueprints) |
|---|---|---|---|
| Estado / soporte | 6.3 LTS publicado el 04-12-2025, soporte hasta dic-2027 **[V]** | 4.6 estable **[V]**; C# en Android/iOS **experimental** según la documentación oficial 4.6 **[V]** | Maduro; enfoque en gama alta **[E]** |
| Rendimiento Android gama media | Bueno con URP + IL2CPP; muy usado en juegos deportivos móviles **[E]** | Renderer *Mobile* mejorando (4.6 corrigió *crashes* en GPU Mali/Adreno) **[V]**; menos casos de éxito 3D móvil exigentes **[E]** | Posible pero costoso: shaders, memoria y calor exigen mucho trabajo de optimización **[E]** |
| Calidad gráfica | Alta y escalable (URP, niveles de calidad) **[E]** | Buena; menos herramientas de escalado en móvil **[E]** | La más alta; infrautilizada en gama media **[E]** |
| Físicas | PhysX integrado; para el balón usaremos física propia determinista **[E]** | Jolt/Godot Physics; igual, física propia **[E]** | Chaos; igual, física propia **[E]** |
| Animación | Mecanim + Humanoid *retargeting*, *blend trees*, IK **[E]** | AnimationTree, *retargeting* disponible; flujo menos maduro para multitudes de humanoides **[E]** | Excelente (Control Rig, Motion Matching), pesado en móvil **[E]** |
| IA | Sin dependencia: IA propia en C# **[E]** | Igual **[E]** | Behaviour Trees integrados (no necesarios) **[E]** |
| Tamaño de descarga | APK/AAB base moderado; Addressables para contenido **[E]** | El más ligero **[E]** | El más pesado **[E]** |
| Entrada táctil / mando | Input System: táctil, *on-screen controls*, mandos **[E]** | Soporte táctil y mandos **[E]** | Soporte completo **[E]** |
| Contenido descargable | Addressables (catálogos remotos) **[E]** | Paquetes PCK **[E]** | Pak/Chunks **[E]** |
| Compartir núcleo C# con pruebas `dotnet test` | **Sí**, .NET Standard 2.1 nativo **[E]** | Solo con C#, que es experimental en móvil **[V]** | No (C++) **[E]** |
| Licencia / coste | Personal gratis hasta 200.000 $ de ingresos+financiación (12 meses); Pro 2.310 $/puesto/año; *Runtime Fee* cancelada **[V]** | MIT, gratis **[V-general]** | 5 % de ingresos brutos por encima del primer 1 M$ por producto (3,5 % con *Launch Everywhere with Epic*) **[V]** |
| Ecosistema de recursos | Asset Store con EULA estándar comercial **[V]** | Menor **[E]** | Fab/Marketplace amplio **[E]** |
| Coste de desarrollo | Medio **[E]** | Medio-bajo en motor, alto en herramientas que faltan **[E]** | Alto (C++, tiempos de compilación, optimización móvil) **[E]** |

## Decisión: Unity 6.3 LTS con C#

**Argumentos técnicos:**

1. **Un solo lenguaje para núcleo y presentación.** El núcleo (`Dorsal.Core`) es C# puro (.NET Standard 2.1, C# 9) que Unity consume como DLL y que se prueba fuera del motor. Con Godot habría que usar C# experimental en móvil o duplicar lógica en GDScript; con Unreal, C++.
2. **Animación humanoide con *retargeting*** madura: permite usar animaciones con licencia comercial (Mixamo, Quaternius, Asset Store) sobre modelos propios.
3. **Addressables** encaja con la Configuración B (paquetes de contenido que se activan o desactivan según derechos).
4. **Coste**: 0 € de licencia de motor mientras se esté por debajo del umbral Personal; coste predecible después.
5. **Riesgo controlado**: si Unity cambiara de nuevo su política comercial, el núcleo (la mayor parte de la lógica) es portable a cualquier motor con C# porque no depende de `UnityEngine`.

**Por qué no Godot:** excelente opción para 2D o 3D sencillo y su licencia MIT es ideal, pero el soporte C# en Android/iOS sigue marcado como experimental en la documentación oficial de 4.6; para un producto comercial con núcleo C# es un riesgo innecesario.

**Por qué no Unreal:** sus ventajas (gráficos de gama alta, Motion Matching) no compensan en gama media Android: mayor tamaño, mayor consumo, C++ y mayores tiempos de iteración. Además aplica *royalty* del 5 % por encima de 1 M$.

**No se desarrollará en paralelo en varios motores.**

## Consecuencias para la arquitectura

- `UnityEngine` **no** se referencia desde `Dorsal.Core`.
- Unity ejecuta el motor de partido en `FixedUpdate` con paso fijo e interpola posiciones para renderizar a 30/60 FPS.
- Calidad gráfica configurable (bajo/medio/alto), límite de FPS (30/60) y resolución de renderizado escalable desde ajustes.

## Fuentes

- Unity — precios y umbral Personal: <https://unity.com/products/pricing-updates>, <https://unity.com/runtime-fee>
- Unity — cancelación de la Runtime Fee: <https://unity.com/blog/unity-is-canceling-the-runtime-fee>
- Unity 6 — soporte LTS: <https://unity.com/releases/unity-6/support>, <https://unity.com/blog/unity-6-3-lts-is-now-available>
- Unity — splash screen en Unity 6: <https://docs.unity3d.com/Manual/class-PlayerSettingsSplashScreen.html> (la política anunciada la hace opcional en Personal; hay informes contradictorios en foros, verificar al compilar)
- Godot — exportar a Android (C# experimental): <https://docs.godotengine.org/en/4.6/tutorials/export/exporting_for_android.html>
- Godot 4.6: <https://godotengine.org/releases/4.6/>, <https://godotengine.org/article/godot-mobile-update-apr-2026/>
- Unreal — licencia y royalties: <https://www.unrealengine.com/license>, <https://www.unrealengine.com/eula/unreal>
