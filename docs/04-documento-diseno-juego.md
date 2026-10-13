# 04 — Documento de diseño del juego (GDD inicial)

**DORSAL — Carrera de Futbolista** *(título provisional)* · Android primero, iOS después · Un jugador, sin conexión.

## 1. Pilares

1. **Tu carrera, tus decisiones.** Cada elección (club, contrato, entrenamiento, minutos) tiene consecuencias deportivas y económicas.
2. **Partidos jugables de verdad.** Partidos 3D en los que controlas a tu futbolista; el resto actúa por IA.
3. **Un mundo vivo.** Temporadas simuladas con clubes que mejoran o empeoran, jugadores que envejecen, fichajes coherentes.
4. **Respeto al jugador.** Sin pay-to-win, sin anuncios en momentos críticos, juego completo sin conexión.

## 2. Bucle principal

```
Semana de carrera
 ├─ Centro de carrera: noticias, objetivos, estado, rol en el equipo
 ├─ Entrenamiento (1–3 sesiones): enfoque, intensidad → progresión, fatiga, confianza del entrenador
 ├─ Mercado / contratos (si hay ofertas o ventana abierta)
 └─ Día de partido
      ├─ Convocatoria: titular / suplente / no convocado (decide el entrenador IA)
      ├─ Objetivos previos (p. ej. "2 pases clave", "nota ≥ 7,0")
      ├─ Jugar (control del propio futbolista) · Ver desde el banquillo · Simular
      └─ Evaluación: nota 1–10, estadísticas, XP, cambio de confianza y reputación
Fin de temporada: premios, renovaciones, cesiones, traspasos, ascensos/descensos, envejecimiento, retiradas.
```

## 3. Creación del futbolista

| Campo | Detalle |
|---|---|
| Nombre y apellido | Texto libre (filtro de longitud y caracteres). |
| Nacionalidad | Lista de países (dato geográfico; sin escudos de federación). |
| Fecha de nacimiento | Configurable; edad inicial 16–21 años. |
| Apariencia | Tono de piel, cara (preset), peinado, color de pelo, vello facial, botas (colores genéricos). |
| Altura / complexión | 160–205 cm; delgado, normal, robusto. Afectan a atributos base (salto/fuerza vs. agilidad/aceleración). |
| Pie dominante | Diestro / zurdo; "pie débil" 1–5 estrellas. |
| Posición principal | POR, DFC, LD, LI, MCD, MC, MCO, MD, MI, ED, EI, DC. |
| Posiciones secundarias | Hasta 2 (la competencia baja fuera de la principal). |
| Estilo de juego | Arquetipos por posición (p. ej. DC: Rematador, Delantero móvil, Referencia; MC: Organizador, Box-to-box, Recuperador). Ajusta la distribución inicial de atributos. |
| Nivel y potencial | Nivel inicial bajo (45–55 de media). Potencial oculto en rango (p. ej. "Alto: 78–86"), se estrecha con el tiempo. |
| Personalidad | Profesionalidad, ambición, temperamento, lealtad (1–20): afectan a progresión, disciplina, interés en traspasos. |
| Dorsal | Elegible entre los libres al firmar. |
| Inicio | (a) Elegir club de los que ofrecen contrato juvenil, o (b) **Pruebas**: 2–3 sesiones/partido de prueba; el rendimiento determina las ofertas. |

Varias carreras en espacios de guardado independientes.

## 4. Atributos (1–99)

| Grupo | Atributos | Efecto en el motor de partido |
|---|---|---|
| Ritmo | Aceleración, Velocidad máxima | Aceleración y velocidad punta del agente. |
| Tiro | Definición, Potencia, Tiro lejano, Colocación (*Positioning* ofensivo) | Error angular del disparo, velocidad del balón, rango de tiro, desmarques. |
| Pase | Pase corto, Pase largo, Centros, Visión | Error del pase, alcance, elección de opciones (visión amplía candidatos). |
| Regate | Regate, Control, Equilibrio, Agilidad | Pérdida en conducción, control al recibir, resistencia a entradas, giros. |
| Defensa | Marcaje, Entradas, Lectura defensiva | Distancia de marca, éxito de entrada, intercepciones. |
| Físico | Fuerza, Resistencia, Salto | Duelos, consumo de energía, alcance en balones aéreos. |
| Mental | Reflejos (*Reactions*), Compostura, Agresividad | Tiempo de reacción, error bajo presión, probabilidad de falta. |
| Cabeza | Remate de cabeza | Precisión de cabezazos. |
| Portero | Estirada, Paradas (*Handling*), Saque, Colocación, Reflejos | Alcance, retención del balón (atrapar vs. despejar), posición, tiempo de reacción. |

La **media (OVR)** se calcula por posición con pesos configurables; se muestra, pero la titularidad **no** depende solo de ella.

## 5. Progresión

- **XP** por partido (minutos, nota, acciones clave) y entrenamiento.
- **Crecimiento semanal** de cada atributo = `base(edad) × entrenamiento × profesionalidad × minutos × margen_al_potencial`.
  - `base(edad)`: alto hasta 21, medio 22–24, bajo 25–28, casi nulo 29–30, negativo después (primero físicos).
  - `margen_al_potencial`: decrece al acercarse al potencial; **nunca** supera el techo del potencial.
- **Entrenamiento**: el jugador elige *enfoque* (grupo de atributos) e *intensidad* (baja/normal/alta). Alta = más crecimiento, más fatiga y riesgo de lesión.
- **Forma** (racha de notas), **fatiga** (se acumula con minutos e intensidad; se recupera con descanso) y **riesgo de lesión** (fatiga + propensión + intensidad).
- Hitos de habilidades (*rasgos*): al cumplir requisitos (p. ej. regate ≥ 80 + 30 regates exitosos) se desbloquean rasgos que modifican decisiones del motor.

## 6. Carrera profesional

- **Contratos**: salario semanal, duración (1–5 años), prima por partido/gol, cláusula de rescisión opcional, rol prometido (Promesa, Rotación, Titular, Estrella).
- **Negociación**: el jugador pide salario/duración/rol; el club responde según presupuesto, valor y necesidad; 3 rondas máximo.
- **Interés de clubes**: basado en reputación, OVR, potencial, edad, minutos y nota media; sube con buenas actuaciones vistas por ojeadores.
- **Traspasos y cesiones**: ofertas de clubes a tu club; el club acepta/rechaza según valor y necesidad; tú aceptas o pides quedarte. Cesiones para ganar minutos.
- **Fin de contrato**: renovar, salir libre, o quedarse sin club (pruebas).
- **Competencia por el puesto**: la plantilla tiene jugadores ficticios con su propia progresión en tu posición.
- **Objetivos**: individuales (goles, minutos, nota media) y colectivos (posición en liga, copa).
- **Reputación y popularidad**: afectan a ofertas, salario y premios.
- **Premios**: jugador del mes, mejor joven, máximo goleador, equipo de la temporada, mejor jugador de la liga; logros de carrera.
- **Selección nacional**: con selecciones **genéricas** (nombre del país, escudo y camiseta originales no oficiales) — opcional, posterior a la v1.
- **Retiro**: voluntario desde los 33 años u obligado por edad/forma; pantalla de legado con estadísticas históricas.

## 7. Entrenador y equipo

- **Confianza del entrenador (0–100)**: sube con buenas notas respecto a lo esperado, entrenamientos con buena evaluación, objetivos cumplidos; baja con tarjetas, bajo rendimiento, quejas públicas.
- **Selección de alineación** (IA del entrenador): para cada puesto de la formación calcula
  `puntuación = encaje_posicional × OVR_en_puesto × w1 + forma × w2 + estado_físico × w3 + confianza × w4 + necesidad_táctica × w5`
  y asigna puestos de forma voraz por importancia. La titularidad depende de posición, táctica, forma, fatiga y competencia interna.
- **Previa del partido**: objetivo personal + instrucción táctica (p. ej. "presiona al central", "quédate abierto").
- **Evaluación posterior**: nota, comentario del entrenador, cambio de confianza.

## 8. Partido

**Modos:** Jugar (controlas a tu futbolista), Ver (cámara TV, también desde el banquillo y entrar al ser sustituido), Simular (resultado inmediato con estadísticas).

**Sistemas del motor** (implementados en `Dorsal.Core.Match`): locomoción con aceleración/velocidad por atributos; pase raso y elevado; tiro; centros; cabezazos; regate y conducción; entradas, intercepciones y despejes; control al recibir; porteros (colocación, reacción, estirada, atrapar/despejar); árbitro con faltas, tarjetas (incl. doble amarilla), penaltis y fuera de juego; saques de banda, córners, saques de puerta y tiros libres; marcador, cronómetro y tiempo añadido; cambios del entrenador IA y lesiones; formaciones (4-4-2, 4-3-3, 4-2-3-1, 3-5-2) y tácticas (presión, línea defensiva, amplitud, ritmo); evaluación individual.

**Duración:** mitades de 3–10 minutos reales (configurable); el reloj de partido se escala a 45'.

## 9. Controles táctiles

- **Joystick virtual flotante** (mitad izquierda): aparece donde se toca.
- **Botones contextuales** (mitad derecha):
  - Con balón: Pase, Pase al hueco, Tiro (mantener = potencia), Centro (aparece en banda), Sprint, Regate/Protección.
  - Sin balón: Presión, Entrada, Pedir balón / Desmarque, Sprint.
- **Cámara**: Dinámica, TV, Jugador; botón de cambio.
- **Ajustes**: tamaño y opacidad de botones, posición zurda/diestra, sensibilidad, zona muerta, vibración, asistencia de pase/tiro.
- **Tutorial** inicial interactivo (moverse, pasar, tirar, defender).
- **Mando Bluetooth** mediante el Input System de Unity (mapeo estándar).

## 10. Interfaz

Pantallas: Menú principal · Crear carrera · Perfil del futbolista · **Centro de carrera** · Calendario · Próximo partido · Vestuario/Alineación · Entrenamiento · Clasificaciones · Mercado · Contratos y ofertas · Estadísticas de temporada · Historial · Premios y logros · Ajustes (gráficos, controles, audio).

**Centro de carrera** (pantalla principal): tarjeta del próximo partido (rival, competición, fecha, tu rol previsto), estado del jugador (forma, fatiga, lesiones, confianza), objetivos activos, noticias, accesos rápidos (Entrenar, Avanzar, Mercado).

**Identidad visual propia**: tipografía legible, alto contraste, paleta propia (no azul/verde ni estilos de franquicias existentes), iconografía original, menús de dos niveles máximo.

## 11. Monetización (análisis; sin implementar)

| Modelo | Valoración |
|---|---|
| **Compra única** (demo gratuita con la primera temporada + desbloqueo) | ✅ Recomendado como base. |
| Gratis con anuncios *opcionales* (recompensa no esencial) | Posible; nunca durante partidos ni negociaciones. |
| Cosméticos (botas, peinados, celebraciones) | ✅ Sin efecto en atributos. |
| Expansiones de contenido (nuevas ligas ficticias, modo mánager) | ✅ |
| Paquetes de ligas licenciadas | Solo con licencias verificadas (Configuración B). |
| Cajas de botín / mejoras de atributos de pago | ❌ Excluido (pay-to-win). |

No se integra ningún SDK de pago o anuncios hasta tener cuentas de tienda, política de privacidad y formularios de seguridad de datos.
