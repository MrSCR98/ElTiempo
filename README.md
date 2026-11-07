# ElTiempo - Panel de hora y clima para Rust (Oxide)

Un plugin sencillo para **Rust** que abre un panel visual desde el chat para controlar la **hora** y el **clima** del servidor, **sin tener que escribir comandos de consola**.

Escribes `/tiempo`, tocas los botones y el servidor cambia al instante.

---

## 🚀 ¿Qué hace este plugin?

- Cambia la **hora del servidor** y la puede **congelar**
- Controla **18 parámetros del clima**: lluvia, viento, truenos, niebla, arcoíris, cielo y nubes
- Carga **ajustes rápidos** (Despejado, Tormenta, Niebla...) con un solo botón
- Muestra el **valor real** de cada parámetro y lo **actualiza cada segundo** mientras el panel está abierto
- Si pones un número que Rust no acepta, te enseña el valor que Rust dejó de verdad
- **Guarda tus ajustes**: al reiniciar el servidor se vuelven a aplicar solos
- Solo lo pueden usar **admins** o quien tenga el permiso

⚠️ Los cambios afectan a **todo el servidor**, no solo a ti.

---

## 📦 Requisitos

- Un servidor de Rust con **Oxide / uMod** (o Carbon)

---

## 📥 Instalación

1. Copia `ElTiempo.cs` en la carpeta de plugins del servidor:

```text
oxide/plugins/ElTiempo.cs
```

2. El servidor lo carga solo. Si quieres hacerlo a mano, desde la consola del servidor:

```text
oxide.reload ElTiempo
```

Los ajustes se guardan en `oxide/data/ElTiempo.json`.

---

## 🎮 Cómo se usa

Dentro del juego, escribe en el chat:

```text
/tiempo
```

Se abre el panel. Para cerrarlo pulsa **CERRAR**.

El panel tiene tres zonas: **Hora** (izquierda), **Clima** (centro y derecha) y **Ajustes rápidos** (abajo).

---

### 🕐 Hora

- **Atajos:** Amanecer (06:00), Mediodía (12:00), Atardecer (18:00) y Noche (00:00)
- **Cuadrícula de horas:** pulsa una hora exacta, de 00 a 23
- **Hora exacta (HH:MM):** escribe la hora con minutos y pulsa **Enter**
- **Congelar hora:** la hora deja de avanzar y se queda fija. Al reiniciar el servidor sigue congelada

**Cómo escribir la hora exacta:**

| Escribes | Resultado |
|----------|-----------|
| `14:30` | 14:30 |
| `14.30` o `14,30` | 14:30 |
| `7:05` | 07:05 |
| `18` | 18:00 |

Las horas van de 0 a 23 y los minutos de 0 a 59. Si escribes algo no válido, no cambia nada.

---

### 🌦️ Clima

El clima se divide en tres pestañas:

| Pestaña | Parámetros |
|---------|-----------|
| **Tiempo** | Lluvia, Viento, Truenos y rayos, Niebla, Arcoíris |
| **Cielo** | Rayleigh, Mie, Brillo del cielo, Contraste del cielo, Brillo del sol |
| **Nubes** | Tamaño, Opacidad, Cobertura, Nitidez, Coloración, Oscurecimiento, Dispersión de luz, Brillo de nubes |

Elige un parámetro de la lista y a la derecha aparece su detalle:

```text
[−1] [−0.1] [−0.01]     0.55     [+0.01] [+0.1] [+1]
```

- Los **botones** suben o bajan el valor
- **Escribir valor:** escribe un número exacto y pulsa **Enter**
- **AUTOMÁTICO:** devuelve ese parámetro al clima dinámico del servidor
- **CONGELAR VALOR ACTUAL:** fija el valor que tiene ahora mismo (solo en los parámetros que se pueden leer en vivo)

💡 No hay un máximo puesto por el plugin. Si pones un número más alto de lo que Rust acepta, Rust lo recorta y el panel te enseña el valor real que quedó.

---

### 🔘 Interruptores

| Interruptor | Qué hace |
|-------------|----------|
| **Sin lluvia** | Deja la lluvia en 0 y no llueve |
| **Sin niebla** | Deja la niebla en 0 |
| **Congelar** | Fija el clima tal y como está ahora, para que el clima dinámico no lo cambie |

---

### ⚡ Ajustes rápidos

Cargan un clima completo de golpe:

`Despejado` · `Polvo` · `Niebla` · `Nublado` · `Lluvia leve` · `Lluvia fuerte` · `Tormenta`

**Dinámico (auto)** quita todo lo fijado y deja que Rust decida el clima solo.

⚠️ Al cargar un ajuste rápido o volver a Dinámico, se borran los valores que tenías fijados a mano.

---

## 🔑 Permisos (para gente que no es admin)

Por defecto, solo pueden usar `/tiempo` los **admins del servidor**.

Si quieres que lo use otra gente (moderadores, ayudantes...), dales el permiso:

```text
eltiempo.admin
```

Ejecuta estos comandos en la **consola del servidor o por RCON**.

### 👤 Dar el permiso a un jugador

```text
oxide.grant user NombreDelJugador eltiempo.admin
oxide.grant user 76561198000000000 eltiempo.admin
```

Puedes usar el nombre o el SteamID.

### 👥 Dar el permiso a un grupo

```text
oxide.group add moderadores
oxide.grant group moderadores eltiempo.admin
oxide.usergroup add NombreDelJugador moderadores
```

Así, todo el que esté en el grupo `moderadores` puede usar el panel.

### 🚫 Quitar el permiso

```text
oxide.revoke user NombreDelJugador eltiempo.admin
oxide.revoke group moderadores eltiempo.admin
```

### 🔎 Ver quién tiene el permiso

```text
oxide.show perm eltiempo.admin
```

⚠️ Dar este permiso es darle control total del hora y el clima del servidor. Dalo solo a gente de confianza.

---

## ⌨️ Comandos

### 💬 Chat

| Comando | Qué hace |
|---------|----------|
| `/tiempo` | Abre el panel de hora y clima |

Si no tienes permiso, el plugin te lo dice.

### 🖥️ Consola (para usuarios avanzados)

Los botones del panel usan internamente el comando `eltiempo.ui`. También puedes lanzarlo a mano, siempre que tengas permiso:

| Comando | Qué hace |
|---------|----------|
| `eltiempo.ui hora <0-24>` | Cambia la hora (puede llevar decimales, `14.5` = 14:30) |
| `eltiempo.ui horatxt <HH:MM>` | Cambia la hora escribiendo horas y minutos |
| `eltiempo.ui congelarhora` | Congela o descongela la hora |
| `eltiempo.ui set <nº parámetro> <valor>` | Fija un valor |
| `eltiempo.ui paso <nº parámetro> <cantidad>` | Suma o resta una cantidad al valor |
| `eltiempo.ui autoparam <nº parámetro>` | Pone un parámetro en automático |
| `eltiempo.ui frz <nº parámetro>` | Congela el valor actual de un parámetro |
| `eltiempo.ui nuncallueve` | Activa o desactiva «Sin lluvia» |
| `eltiempo.ui sinniebla` | Activa o desactiva «Sin niebla» |
| `eltiempo.ui congelarclima` | Activa o desactiva «Congelar» |
| `eltiempo.ui preset <nombre>` | Carga un ajuste rápido |
| `eltiempo.ui auto` | Vuelve al clima dinámico |
| `eltiempo.ui cerrar` | Cierra el panel |

Los nombres de ajuste rápido son: `Clear`, `Dust`, `Fog`, `Overcast`, `RainMild`, `RainHeavy` y `Storm`.

El número de parámetro va de `0` a `17`, en este orden: lluvia, viento, truenos, niebla, arcoíris, Rayleigh, Mie, brillo del cielo, contraste del cielo, brillo del sol, tamaño, opacidad, cobertura, nitidez, coloración, oscurecimiento, dispersión de luz y brillo de nubes.

---

## 🧩 Comandos de Rust que aplica el plugin

El plugin no inventa nada: por debajo usa los comandos normales de Rust. Si quieres, también puedes ponerlos tú a mano en la consola del servidor, por ejemplo `weather.rain 0.5`. El valor `-1` en cualquiera de ellos significa **automático**.

### 🌦️ Tiempo

| Comando de Rust | Parámetro del panel | Qué hace |
|-----------------|--------------------|----------|
| `weather.rain` | Lluvia | Cuánta lluvia cae |
| `weather.wind` | Viento | Cuánto viento hay (sobre todo visual, mueve la vegetación) |
| `weather.thunder` | Truenos y rayos | Cuántos truenos y relámpagos hay |
| `weather.fog` | Niebla | Cuánta niebla hay |
| `weather.rainbow` | Arcoíris | Fuerza la aparición de un arcoíris |

### 🌌 Cielo

| Comando de Rust | Parámetro del panel | Qué hace |
|-----------------|--------------------|----------|
| `weather.atmosphere_rayleigh` | Rayleigh | Dispersión Rayleigh. Determina el color del cielo, prueba a jugar con ella |
| `weather.atmosphere_mie` | Mie | Dispersión Mie. Determina el color del cielo, prueba a jugar con ella |
| `weather.atmosphere_brightness` | Brillo del cielo | Brillo general del cielo |
| `weather.atmosphere_contrast` | Contraste del cielo | Contraste general del cielo |
| `weather.atmosphere_directionality` | Brillo del sol | Tamaño del resplandor alrededor del sol |

### ☁️ Nubes

| Comando de Rust | Parámetro del panel | Qué hace |
|-----------------|--------------------|----------|
| `weather.cloud_size` | Tamaño | Escala de las capas de nubes (no afecta a los billboards) |
| `weather.cloud_opacity` | Opacidad | Opacidad de las capas de nubes y los billboards |
| `weather.cloud_coverage` | Cobertura | Cuánto cielo cubren las nubes |
| `weather.cloud_sharpness` | Nitidez | Lo definidas que son las nubes |
| `weather.cloud_coloring` | Coloración | Mezcla entre la luz indirecta del cielo y la luz directa del sol |
| `weather.cloud_attenuation` | Oscurecimiento | Cuánto se oscurecen las nubes cuando son más gruesas |
| `weather.cloud_scattering` | Dispersión de luz | Cuánta luz solar dispersan las nubes (las hace brillar) |
| `weather.cloud_brightness` | Brillo de nubes | Brillo general de las nubes |

### ⚡ Ajustes rápidos y hora

| Comando de Rust | Qué hace |
|-----------------|----------|
| `weather.load <preset>` | Carga un clima completo: `Clear`, `Dust`, `Fog`, `Overcast`, `RainMild`, `RainHeavy` o `Storm` |
| `weather.reset` | Vuelve al clima dinámico |
| `env.progresstime true/false` | Hace que la hora avance o se quede congelada |

Ejemplos de uso a mano:

```text
weather.rain 0.5
weather.fog 0
weather.load Storm
weather.reset
```

---

## 🛡️ Cómo funciona por dentro

- Cada parámetro usa un comando de Rust (por ejemplo `weather.rain` o `weather.fog`)
- Un parámetro en **Automático** lo decide el clima dinámico del servidor
- Los parámetros que fijas se **reaplican cada 60 segundos**, por si el servidor los cambia
- Si todo está en automático, el plugin no hace nada
- El panel solo se actualiza cada segundo **mientras está abierto**. Al cerrarlo no se comprueba nada

---

## 🚧 Limitaciones

- Solo se pueden leer en vivo: lluvia, viento, truenos, niebla y cobertura de nubes. Los parámetros del cielo no tienen lectura en vivo
- No se pueden poner valores negativos (un valor negativo significa «automático» en Rust)
- La hora resaltada en la cuadrícula se actualiza al pulsar cualquier botón o con **ACTUALIZAR**
- Los cambios son para todo el servidor, no por jugador

---

## ⚖️ Aviso

Plugin no oficial. No está afiliado a Facepunch ni a Oxide/uMod.

Hecho por **SCR98**.
