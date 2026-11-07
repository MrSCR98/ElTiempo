using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using Oxide.Core;
using Oxide.Game.Rust.Cui;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("ElTiempo", "SCR98", "2.0.0")]
    [Description("Panel visual para controlar la hora y el clima del servidor")]
    class ElTiempo : RustPlugin
    {
        #region Constantes

        private const string PermAdmin = "eltiempo.admin";
        private const string UiFondo = "ElTiempo.Fondo";
        private const string UiContenido = "ElTiempo.Contenido";
        private const string Prefijo = "<color=#f78a1f>[ElTiempo]</color> ";

        private const string FuenteNormal = "robotocondensed-regular.ttf";
        private const string FuenteNegrita = "robotocondensed-bold.ttf";

        // Paleta: tema claro y naranja
        private const string ColFondo = "0.10 0.07 0.04 0.55";
        private const string ColPanel = "0.992 0.972 0.945 1";
        private const string ColTarjeta = "1 1 1 1";
        private const string ColNaranja = "0.97 0.52 0.12 1";
        private const string ColNaranjaOsc = "0.78 0.36 0.04 1";
        private const string ColNaranjaClaro = "1 0.925 0.83 1";
        private const string ColNaranjaSuave = "0.97 0.52 0.12 0.35";
        private const string ColTexto = "0.22 0.18 0.15 1";
        private const string ColSuave = "0.55 0.48 0.42 1";
        private const string ColApagado = "0.94 0.92 0.90 1";
        private const string ColVacio = "0.90 0.87 0.84 1";
        private const string ColBlanco = "1 1 1 1";
        private const string ColBlancoSuave = "1 1 1 0.85";

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        #endregion

        #region Parametros del clima

        private class Param
        {
            public string Conv;
            public string Nombre;
            public string Desc;
            public float Min;
            public float Max;

            public Param(string conv, string nombre, string desc, float min, float max)
            {
                Conv = conv; Nombre = nombre; Desc = desc; Min = min; Max = max;
            }
        }

        // Min/Max ya no limitan nada: el plugin manda el valor a Rust y muestra lo que Rust acepto.
        private static readonly Param[] P =
        {
            // 0-4: Tiempo
            new Param("weather.rain", "Lluvia", "Cuánta lluvia cae.", 0f, 1f),
            new Param("weather.wind", "Viento", "Cuánto viento hay (sobre todo visual, mueve la vegetación).", 0f, 1f),
            new Param("weather.thunder", "Truenos y rayos", "Cuántos truenos y relámpagos hay.", 0f, 1f),
            new Param("weather.fog", "Niebla", "Cuánta niebla hay.", 0f, 1f),
            new Param("weather.rainbow", "Arcoíris", "Fuerza la aparición de un arcoíris.", 0f, 1f),

            // 5-9: Cielo
            new Param("weather.atmosphere_rayleigh", "Rayleigh", "Dispersión Rayleigh. Determina el color del cielo, prueba a jugar con ella.", 0f, 5f),
            new Param("weather.atmosphere_mie", "Mie", "Dispersión Mie. Determina el color del cielo, prueba a jugar con ella.", 0f, 5f),
            new Param("weather.atmosphere_brightness", "Brillo del cielo", "Brillo general del cielo.", 0f, 5f),
            new Param("weather.atmosphere_contrast", "Contraste del cielo", "Contraste general del cielo.", 0f, 5f),
            new Param("weather.atmosphere_directionality", "Brillo del sol", "Tamaño del resplandor alrededor del sol.", 0f, 1f),

            // 10-17: Nubes
            new Param("weather.cloud_size", "Tamaño", "Escala de las capas de nubes (no afecta a los billboards).", 0f, 5f),
            new Param("weather.cloud_opacity", "Opacidad", "Opacidad de las capas de nubes y los billboards.", 0f, 1f),
            new Param("weather.cloud_coverage", "Cobertura", "Cuánto cielo cubren las nubes.", 0f, 1f),
            new Param("weather.cloud_sharpness", "Nitidez", "Lo definidas que son las nubes.", 0f, 1f),
            new Param("weather.cloud_coloring", "Coloración", "Mezcla entre la luz indirecta del cielo y la luz directa del sol.", 0f, 1f),
            new Param("weather.cloud_attenuation", "Oscurecimiento", "Cuánto se oscurecen las nubes cuando son más gruesas.", 0f, 1f),
            new Param("weather.cloud_scattering", "Dispersión de luz", "Cuánta luz solar dispersan las nubes (las hace brillar).", 0f, 5f),
            new Param("weather.cloud_brightness", "Brillo de nubes", "Brillo general de las nubes.", 0f, 5f)
        };

        private static readonly string[] TabNombres = { "Tiempo", "Cielo", "Nubes" };
        private static readonly int[][] TabRangos = { new[] { 0, 5 }, new[] { 5, 10 }, new[] { 10, 18 } };

        // Ajustes rapidos: weather.load
        private static readonly string[] PresetClaves = { "Clear", "Dust", "Fog", "Overcast", "RainMild", "RainHeavy", "Storm" };
        private static readonly string[] PresetNombres = { "Despejado", "Polvo", "Niebla", "Nublado", "Lluvia leve", "Lluvia fuerte", "Tormenta" };

        #endregion

        #region Datos

        private class Datos
        {
            // -1 = automatico (el servidor/clima dinamico decide)
            public float[] Valores;
            public bool[] Bloqueados;
            public bool NuncaLluvia;
            public bool SinNiebla;
            public bool ClimaCongelado;
            public bool HoraCongelada;
            public float HoraFija = 12f;
            // Ultimo weather.load aplicado ("" = clima dinamico)
            public string Preset = "";
        }

        private Datos datos;

        // Ultimo valor enviado al servidor por convar (evita reenviar los que no cambian)
        private float[] enviados;

        // Estado de la interfaz por jugador: [0] pestaña, [1] parametro seleccionado
        private readonly Dictionary<ulong, int[]> estadoUi = new Dictionary<ulong, int[]>();

        // Jugadores con el panel abierto (el fondo con cursor solo se crea una vez)
        private readonly HashSet<ulong> abiertos = new HashSet<ulong>();

        // Etiquetas que se actualizan solas mientras el panel esta abierto
        private class Viva
        {
            public string Nombre, Padre, Texto, Color;
            public int Tam;
            public TextAnchor Align;
            public bool Negrita;
            public float X0, Y0, X1, Y1;
            public Func<string> FTexto;
            public Func<string> FColor;
        }

        private readonly Dictionary<ulong, List<Viva>> vivas = new Dictionary<ulong, List<Viva>>();

        // Solo existe mientras haya algun panel abierto
        private Timer tickTimer;

        private static float[] Automaticos()
        {
            var v = new float[P.Length];
            for (int i = 0; i < v.Length; i++) v[i] = -1f;
            return v;
        }

        private void CargarDatos()
        {
            try { datos = Interface.Oxide.DataFileSystem.ReadObject<Datos>("ElTiempo"); }
            catch { datos = null; }

            if (datos == null) datos = new Datos();
            if (datos.Valores == null || datos.Valores.Length != P.Length) datos.Valores = Automaticos();
            if (datos.Bloqueados == null || datos.Bloqueados.Length != P.Length) datos.Bloqueados = new bool[P.Length];
            if (datos.Preset == null) datos.Preset = "";

            enviados = new float[P.Length];
            for (int i = 0; i < enviados.Length; i++) enviados[i] = -99f;
        }

        private Timer guardarTimer;

        private void GuardarDiferido()
        {
            if (guardarTimer != null && !guardarTimer.Destroyed) return;
            guardarTimer = timer.Once(3f, GuardarDatos);
        }

        private void GuardarDatos()
        {
            Interface.Oxide.DataFileSystem.WriteObject("ElTiempo", datos);
        }

        #endregion

        #region Hooks

        private void Init()
        {
            permission.RegisterPermission(PermAdmin, this);
            CargarDatos();
        }

        private void OnServerInitialized()
        {
            timer.Once(5f, () =>
            {
                if (datos.HoraCongelada)
                {
                    PonerHora(datos.HoraFija);
                    EjecutarComando("env.progresstime", "false");
                }

                if (!string.IsNullOrEmpty(datos.Preset))
                    EjecutarComando("weather.load", datos.Preset);

                AplicarClima(false);
            });

            // Reaplica lo fijado por si el servidor lo cambia
            timer.Every(60f, () => AplicarClima(false, true));
        }

        private void Unload()
        {
            foreach (var jugador in BasePlayer.activePlayerList)
                CuiHelper.DestroyUi(jugador, UiFondo);

            GuardarDatos();
        }

        private void OnPlayerDisconnected(BasePlayer player)
        {
            if (player == null) return;
            abiertos.Remove(player.userID);
            vivas.Remove(player.userID);
            AjustarTick();
        }

        #endregion

        #region Comandos

        [ChatCommand("tiempo")]
        private void CmdTiempo(BasePlayer player, string command, string[] args)
        {
            if (!TienePermiso(player))
            {
                SendReply(player, Prefijo + "No tienes permiso para usar este comando.");
                return;
            }
            MostrarUi(player, true);
        }

        [ConsoleCommand("eltiempo.ui")]
        private void CmdUi(ConsoleSystem.Arg arg)
        {
            var jugador = arg.Player();
            if (jugador == null || !TienePermiso(jugador)) return;

            string accion = arg.GetString(0, "").ToLower();
            string a1 = arg.GetString(1, "");
            string a2 = arg.GetString(2, "");

            float num;
            bool tieneNum = float.TryParse(a1, NumberStyles.Float, Inv, out num);

            int idx;
            bool idxValido = int.TryParse(a1, out idx) && idx >= 0 && idx < P.Length;

            var estado = Estado(jugador);

            switch (accion)
            {
                case "cerrar":
                    CuiHelper.DestroyUi(jugador, UiFondo);
                    abiertos.Remove(jugador.userID);
                    vivas.Remove(jugador.userID);
                    AjustarTick();
                    return;

                case "refrescar":
                    break;

                case "hora":
                    if (tieneNum)
                    {
                        PonerHora(num);
                        if (datos.HoraCongelada) datos.HoraFija = Mathf.Repeat(num, 24f);
                    }
                    break;

                case "horatxt":
                    {
                        float nueva;
                        if (ParsearHora(a1, out nueva))
                        {
                            PonerHora(nueva);
                            if (datos.HoraCongelada) datos.HoraFija = Mathf.Repeat(nueva, 24f);
                        }
                    }
                    break;

                case "horah":
                case "horam":
                    {
                        int valorEntero;
                        if (int.TryParse((a1 ?? "").Trim(), NumberStyles.None, Inv, out valorEntero))
                        {
                            int hAct, mAct;
                            HoraMinutosActual(out hAct, out mAct);

                            bool esHora = accion == "horah";
                            int hNueva = esHora ? valorEntero : hAct;
                            int mNueva = esHora ? mAct : valorEntero;

                            if (hNueva >= 0 && hNueva <= 23 && mNueva >= 0 && mNueva <= 59)
                            {
                                float nueva = hNueva + mNueva / 60f;
                                PonerHora(nueva);
                                if (datos.HoraCongelada) datos.HoraFija = nueva;
                            }
                        }
                    }
                    break;

                case "congelarhora":
                    if (!datos.HoraCongelada)
                    {
                        datos.HoraFija = HoraActual();
                        datos.HoraCongelada = true;
                        EjecutarComando("env.progresstime", "false");
                    }
                    else
                    {
                        datos.HoraCongelada = false;
                        EjecutarComando("env.progresstime", "true");
                    }
                    break;

                case "tab":
                    if (tieneNum && num >= 0 && num < TabRangos.Length)
                    {
                        estado[0] = (int)num;
                        estado[1] = TabRangos[(int)num][0];
                    }
                    break;

                case "sel":
                    if (idxValido) estado[1] = idx;
                    break;

                case "set":
                    {
                        float v;
                        if (idxValido && float.TryParse(a2, NumberStyles.Float, Inv, out v))
                            FijarValor(idx, v);
                    }
                    break;

                case "txt":
                    {
                        float v;
                        string texto = (a2 ?? "").Trim().Replace(',', '.');
                        if (idxValido && float.TryParse(texto, NumberStyles.Float, Inv, out v))
                            FijarValor(idx, v);
                    }
                    break;

                case "paso":
                    if (idxValido)
                    {
                        bool fijo, hayVivo;
                        float actual = ValorMostrado(idx, jugador.transform.position, out fijo, out hayVivo);
                        float delta;
                        if (!float.TryParse(a2, NumberStyles.Float, Inv, out delta)) break;
                        float nuevo = Mathf.Max(0f, actual + delta);
                        FijarValor(idx, Mathf.Round(nuevo * 1000f) / 1000f);
                    }
                    break;

                case "autoparam":
                    if (idxValido)
                    {
                        datos.Valores[idx] = -1f;
                        datos.Bloqueados[idx] = false;
                        if (idx == 0) datos.NuncaLluvia = false;
                        if (idx == 3) datos.SinNiebla = false;
                        AplicarUno(idx);
                    }
                    break;

                case "frz":
                    if (idxValido)
                    {
                        float vivo;
                        if (datos.Valores[idx] < 0f && Vivo(idx, jugador.transform.position, out vivo))
                        {
                            datos.Valores[idx] = Redondear(vivo);
                            datos.Bloqueados[idx] = false;
                            AplicarUno(idx);
                        }
                    }
                    break;

                case "nuncallueve":
                    datos.NuncaLluvia = !datos.NuncaLluvia;
                    AplicarUno(0);
                    break;

                case "sinniebla":
                    datos.SinNiebla = !datos.SinNiebla;
                    AplicarUno(3);
                    break;

                case "congelarclima":
                    AlternarCongelarClima(jugador);
                    AplicarClima(true);
                    break;

                case "preset":
                    CargarPreset(a1);
                    break;

                case "auto":
                    ClimaDinamico();
                    break;
            }

            GuardarDiferido();
            MostrarUi(jugador);

            // Tras cargar un clima, Rust tarda un momento en reflejarlo: se vuelve a dibujar
            // una vez a los 1 s para que las cajas muestren el valor real y no el anterior
            if (accion == "preset" || accion == "auto")
            {
                timer.Once(1f, () =>
                {
                    if (jugador != null && jugador.IsConnected && abiertos.Contains(jugador.userID))
                        MostrarUi(jugador);
                });
            }
        }

        #endregion

        #region Logica del tiempo y clima

        private bool TienePermiso(BasePlayer jugador)
        {
            return jugador.IsAdmin || permission.UserHasPermission(jugador.UserIDString, PermAdmin);
        }

        private int[] Estado(BasePlayer jugador)
        {
            int[] e;
            if (!estadoUi.TryGetValue(jugador.userID, out e))
            {
                e = new[] { 0, 0 };
                estadoUi[jugador.userID] = e;
            }
            return e;
        }

        private float HoraActual()
        {
            return TOD_Sky.Instance != null ? TOD_Sky.Instance.Cycle.Hour : 12f;
        }

        private void PonerHora(float hora)
        {
            if (TOD_Sky.Instance == null) return;
            TOD_Sky.Instance.Cycle.Hour = Mathf.Repeat(hora, 24f);
        }

        private float Redondear(float v)
        {
            return Mathf.Round(v * 100f) / 100f;
        }

        // Lectura en vivo del clima dinamico (solo existe para algunos parametros)
        private bool Vivo(int i, Vector3 pos, out float v)
        {
            switch (i)
            {
                case 0: v = Climate.GetRain(pos); return true;
                case 1: v = Climate.GetWind(pos); return true;
                case 2: v = Climate.GetThunder(pos); return true;
                case 3: v = Climate.GetFog(pos); return true;
                case 12: v = Climate.GetClouds(pos); return true;
            }
            v = 0f;
            return false;
        }

        private float Efectivo(int i)
        {
            if (i == 0 && datos.NuncaLluvia) return 0f;
            if (i == 3 && datos.SinNiebla) return 0f;
            return datos.Valores[i];
        }

        // Valor que se enseña: el fijado o, si es automatico, la lectura en vivo (si la hay)
        private float ValorMostrado(int i, Vector3 pos, out bool fijo, out bool hayVivo)
        {
            float e = Efectivo(i);
            float vivo;
            hayVivo = Vivo(i, pos, out vivo);

            if (e >= 0f)
            {
                fijo = true;
                return e;
            }

            fijo = false;
            return hayVivo ? Redondear(vivo) : P[i].Min;
        }

        private void EjecutarComando(string comando, string valor)
        {
            // Quiet: no escribe la respuesta en la consola del servidor
            ConsoleSystem.Run(ConsoleSystem.Option.Server.Quiet(), (comando + " " + valor).Trim());
        }

        private static readonly Regex NumeroRx = new Regex(@"-?\d+(?:[.,]\d+)?(?:[eE][-+]?\d+)?", RegexOptions.Compiled);

        // Lee el valor real que tiene Rust en un convar (null si no se pudo leer)
        private float? LeerConvar(string conv)
        {
            try
            {
                string r = ConsoleSystem.Run(ConsoleSystem.Option.Server.Quiet(), conv);
                if (string.IsNullOrEmpty(r)) return null;

                var m = NumeroRx.Match(r.Replace(conv, ""));
                float v;
                if (m.Success && float.TryParse(m.Value.Replace(',', '.'), NumberStyles.Float, Inv, out v))
                    return v;
            }
            catch { }
            return null;
        }

        // Envia un solo convar. Solo lo manda si su valor cambio (o si se fuerza).
        private void AplicarUno(int i, bool forzar = false)
        {
            float v = Efectivo(i);
            if (!forzar && Math.Abs(enviados[i] - v) < 0.0001f) return;

            enviados[i] = v;
            EjecutarComando(P[i].Conv, v.ToString("0.###", Inv));

            // Si Rust limito el valor (ej. pusiste 50 y el maximo es 10), se guarda y muestra el real
            bool porInterruptor = (i == 0 && datos.NuncaLluvia) || (i == 3 && datos.SinNiebla);
            if (v >= 0f && !porInterruptor)
            {
                float? real = LeerConvar(P[i].Conv);
                if (real.HasValue && real.Value >= 0f && Math.Abs(real.Value - v) > 0.0005f)
                {
                    float r = Mathf.Round(real.Value * 1000f) / 1000f;
                    datos.Valores[i] = r;
                    enviados[i] = r;
                }
            }
        }

        private void AplicarClima(bool incluirAutomaticos, bool forzar = false)
        {
            for (int i = 0; i < P.Length; i++)
            {
                if (Efectivo(i) < 0f && !incluirAutomaticos) continue;
                AplicarUno(i, forzar);
            }
        }

        private void FijarValor(int indice, float valor)
        {
            // Un valor negativo significaria "automatico", asi que se limita a 0
            valor = Mathf.Max(0f, valor);

            datos.Valores[indice] = valor;
            datos.Bloqueados[indice] = false;

            if (indice == 0 && valor > 0.01f) datos.NuncaLluvia = false;
            if (indice == 3 && valor > 0.01f) datos.SinNiebla = false;

            AplicarUno(indice);
        }

        private void AlternarCongelarClima(BasePlayer jugador)
        {
            if (!datos.ClimaCongelado)
            {
                // Fija en su valor actual los parametros automaticos que se pueden leer
                Vector3 pos = jugador.transform.position;
                for (int i = 0; i < P.Length; i++)
                {
                    float vivo;
                    if (datos.Valores[i] < 0f && Vivo(i, pos, out vivo))
                    {
                        datos.Valores[i] = Redondear(Mathf.Clamp01(vivo));
                        datos.Bloqueados[i] = true;
                    }
                }
                datos.ClimaCongelado = true;
            }
            else
            {
                for (int i = 0; i < P.Length; i++)
                {
                    if (datos.Bloqueados[i])
                    {
                        datos.Valores[i] = -1f;
                        datos.Bloqueados[i] = false;
                    }
                }
                datos.ClimaCongelado = false;
            }
        }

        // Quita todos los valores fijados y manda -1 al servidor
        private void LimpiarFijados()
        {
            for (int i = 0; i < P.Length; i++)
            {
                datos.Valores[i] = -1f;
                datos.Bloqueados[i] = false;
            }
            datos.NuncaLluvia = false;
            datos.SinNiebla = false;
            datos.ClimaCongelado = false;

            AplicarClima(true, true);
        }

        private void CargarPreset(string clave)
        {
            if (Array.IndexOf(PresetClaves, clave) < 0) return;

            // Los valores fijados taparian el preset, asi que se quitan antes
            LimpiarFijados();
            datos.Preset = clave;
            EjecutarComando("weather.load", clave);
        }

        private void ClimaDinamico()
        {
            LimpiarFijados();
            datos.Preset = "";
            EjecutarComando("weather.reset", "");
        }

        #endregion

        #region Actualizacion en vivo

        // Arranca el refresco de 1 s si hay paneles abiertos y lo para cuando no queda ninguno
        private void AjustarTick()
        {
            if (abiertos.Count > 0)
            {
                if (tickTimer == null || tickTimer.Destroyed)
                    tickTimer = timer.Every(1f, TickVivo);
            }
            else if (tickTimer != null)
            {
                tickTimer.Destroy();
                tickTimer = null;
            }
        }

        private void TickVivo()
        {
            if (abiertos.Count == 0)
            {
                AjustarTick();
                return;
            }

            foreach (var id in new List<ulong>(abiertos))
            {
                var jugador = BasePlayer.FindByID(id);
                List<Viva> lista;
                if (jugador == null || !jugador.IsConnected || !vivas.TryGetValue(id, out lista))
                {
                    abiertos.Remove(id);
                    vivas.Remove(id);
                    continue;
                }

                CuiElementContainer c = null;
                foreach (var v in lista)
                {
                    string t = v.FTexto();
                    string col = v.FColor();
                    if (t == v.Texto && col == v.Color) continue; // sin cambios: no se envia nada

                    v.Texto = t;
                    v.Color = col;
                    if (c == null) c = new CuiElementContainer();
                    c.Add(CrearViva(v, true));
                }

                if (c != null) CuiHelper.AddUi(jugador, c);
            }

            AjustarTick();
        }

        private CuiElement CrearViva(Viva v, bool reemplazar)
        {
            return new CuiElement
            {
                Name = v.Nombre,
                Parent = v.Padre,
                DestroyUi = reemplazar ? v.Nombre : null,
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = v.Texto,
                        FontSize = v.Tam,
                        Align = v.Align,
                        Color = v.Color,
                        Font = v.Negrita ? FuenteNegrita : FuenteNormal
                    },
                    new CuiRectTransformComponent { AnchorMin = Anc(v.X0, v.Y0), AnchorMax = Anc(v.X1, v.Y1) }
                }
            };
        }

        private void EtiquetaViva(CuiElementContainer c, List<Viva> lista, ulong uid, string padre, string id, int tam,
            TextAnchor alineado, float x0, float y0, float x1, float y1, bool negrita,
            Func<string> fTexto, Func<string> fColor)
        {
            var v = new Viva
            {
                Nombre = "ElTiempo.v." + uid + "." + id,
                Padre = padre,
                Tam = tam,
                Align = alineado,
                Negrita = negrita,
                X0 = x0, Y0 = y0, X1 = x1, Y1 = y1,
                FTexto = fTexto,
                FColor = fColor
            };
            v.Texto = fTexto();
            v.Color = fColor();
            lista.Add(v);
            c.Add(CrearViva(v, false));
        }

        // Acepta "14:30", "14.30", "14,30" o solo "14". Minutos 0-59, horas 0-23.
        private bool ParsearHora(string texto, out float hora)
        {
            hora = 0f;
            string t = (texto ?? "").Trim().Replace('.', ':').Replace(',', ':');
            string[] partes = t.Split(':');
            if (partes.Length < 1 || partes.Length > 2) return false;

            int h, m = 0;
            if (!int.TryParse(partes[0], NumberStyles.None, Inv, out h)) return false;
            if (partes.Length == 2 && !int.TryParse(partes[1], NumberStyles.None, Inv, out m)) return false;
            if (h < 0 || h > 23 || m < 0 || m > 59) return false;

            hora = h + m / 60f;
            return true;
        }

        // Se cuentan minutos enteros (con un margen minimo) para que 14:07 no salga como 14:06
        private void HoraMinutosActual(out int hh, out int mm)
        {
            int total = (int)Mathf.Floor(HoraActual() * 60f + 0.01f);
            hh = (total / 60) % 24;
            mm = total % 60;
        }

        private string TextoHora()
        {
            int hh, mm;
            HoraMinutosActual(out hh, out mm);
            return hh.ToString("00") + ":" + mm.ToString("00");
        }

        private string TextoGrande(int i, Vector3 pos)
        {
            bool fijo, hayVivo;
            float v = ValorMostrado(i, pos, out fijo, out hayVivo);
            return (!fijo && !hayVivo) ? "AUTO" : Fmt(v);
        }

        private string ColorValor(int i, Vector3 pos)
        {
            bool fijo, hayVivo;
            ValorMostrado(i, pos, out fijo, out hayVivo);
            return fijo ? ColNaranjaOsc : ColSuave;
        }

        private string TextoEstado(int i, Vector3 pos)
        {
            bool fijo, hayVivo;
            ValorMostrado(i, pos, out fijo, out hayVivo);

            if (fijo && datos.NuncaLluvia && i == 0) return "Fijado por «Sin lluvia»";
            if (fijo && datos.SinNiebla && i == 3) return "Fijado por «Sin niebla»";
            if (fijo) return "Valor fijado";
            if (hayVivo) return "Automático · lectura actual del clima dinámico";
            return "Automático · sin lectura en vivo";
        }

        #endregion

        #region Interfaz

        private string Num(float v)
        {
            return v.ToString("0.####", Inv);
        }

        private string Fmt(float v)
        {
            return v.ToString("0.##", Inv);
        }

        private string Anc(float x, float y)
        {
            return Num(x) + " " + Num(y);
        }

        private string Panel(CuiElementContainer c, string parent, string color, float x0, float y0, float x1, float y1)
        {
            return c.Add(new CuiPanel
            {
                Image = { Color = color },
                RectTransform = { AnchorMin = Anc(x0, y0), AnchorMax = Anc(x1, y1) }
            }, parent);
        }

        private void Etiqueta(CuiElementContainer c, string parent, string texto, int tam, string color,
            TextAnchor alineado, float x0, float y0, float x1, float y1, bool negrita = false)
        {
            c.Add(new CuiLabel
            {
                Text =
                {
                    Text = texto,
                    FontSize = tam,
                    Align = alineado,
                    Color = color,
                    Font = negrita ? FuenteNegrita : FuenteNormal
                },
                RectTransform = { AnchorMin = Anc(x0, y0), AnchorMax = Anc(x1, y1) }
            }, parent);
        }

        private string Boton(CuiElementContainer c, string parent, string texto, string comando, bool activo,
            float x0, float y0, float x1, float y1, int tam = 12,
            string fondoOff = ColNaranjaClaro, string textoOff = ColNaranjaOsc)
        {
            return c.Add(new CuiButton
            {
                Button = { Command = comando, Color = activo ? ColNaranja : fondoOff },
                RectTransform = { AnchorMin = Anc(x0, y0), AnchorMax = Anc(x1, y1) },
                Text =
                {
                    Text = texto,
                    FontSize = tam,
                    Align = TextAnchor.MiddleCenter,
                    Color = activo ? ColBlanco : textoOff,
                    Font = FuenteNegrita
                }
            }, parent);
        }

        private void Interruptor(CuiElementContainer c, string parent, string texto, string comando, bool activo,
            float x0, float y0, float x1, float y1, int tam = 12)
        {
            Boton(c, parent, texto + (activo ? ": SÍ" : ": NO"), comando, activo,
                x0, y0, x1, y1, tam, ColApagado, ColTexto);
        }

        private void CajaHora(CuiElementContainer c, string parent, string texto, string comando, float x0, float x1)
        {
            string caja = Panel(c, parent, ColApagado, x0, 0.45f, x1, 0.515f);
            c.Add(new CuiElement
            {
                Parent = caja,
                Components =
                {
                    new CuiInputFieldComponent
                    {
                        Text = texto,
                        FontSize = 14,
                        Align = TextAnchor.MiddleCenter,
                        Color = ColTexto,
                        CharsLimit = 2,
                        Command = comando,
                        Font = FuenteNegrita
                    },
                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1" }
                }
            });
        }

        private string TextoValor(int i, Vector3 pos, out bool fijo)
        {
            bool hayVivo;
            float v = ValorMostrado(i, pos, out fijo, out hayVivo);
            if (fijo) return Fmt(v);
            return hayVivo ? "Auto (" + Fmt(v) + ")" : "Auto";
        }

        private void MostrarUi(BasePlayer jugador, bool abrir = false)
        {
            // Al refrescar solo se recrea el contenido: si se recreara el fondo con
            // CursorEnabled, Rust volveria a centrar el raton en cada pulsacion.
            bool yaAbierta = !abrir && abiertos.Contains(jugador.userID);
            if (!yaAbierta) CuiHelper.DestroyUi(jugador, UiFondo);

            var c = new CuiElementContainer();
            var estado = Estado(jugador);
            int tab = Mathf.Clamp(estado[0], 0, TabRangos.Length - 1);
            int sel = Mathf.Clamp(estado[1], 0, P.Length - 1);
            if (sel < TabRangos[tab][0] || sel >= TabRangos[tab][1]) sel = TabRangos[tab][0];
            estado[0] = tab;
            estado[1] = sel;

            Vector3 pos = jugador.transform.position;
            ulong uid = jugador.userID;
            var lista = new List<Viva>();
            vivas[uid] = lista;

            // Fondo oscuro con cursor (solo la primera vez)
            if (!yaAbierta)
            {
                c.Add(new CuiPanel
                {
                    Image = { Color = ColFondo },
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                    CursorEnabled = true
                }, "Overlay", UiFondo);
                abiertos.Add(jugador.userID);
            }
            AjustarTick();

            // DestroyUi borra el contenido anterior en este mismo mensaje: no hay
            // ningun frame intermedio sin panel (era lo que parpadeaba en negro).
            string main = UiContenido;
            c.Add(new CuiElement
            {
                Name = UiContenido,
                Parent = UiFondo,
                DestroyUi = yaAbierta ? UiContenido : null,
                Components =
                {
                    new CuiImageComponent { Color = ColPanel },
                    new CuiRectTransformComponent { AnchorMin = "0.08 0.06", AnchorMax = "0.92 0.94" }
                }
            });

            // ---------- Cabecera ----------
            string cab = Panel(c, main, ColNaranja, 0f, 0.9f, 1f, 1f);
            Etiqueta(c, cab, "ElTiempo", 30, ColBlanco, TextAnchor.LowerLeft, 0.025f, 0.38f, 0.5f, 1f, true);
            Etiqueta(c, cab, "Control de la hora y el clima del servidor  ·  Hecho por SCR98", 12, ColBlancoSuave,
                TextAnchor.UpperLeft, 0.027f, 0.0f, 0.6f, 0.4f);

            Boton(c, cab, "ACTUALIZAR", "eltiempo.ui refrescar", false, 0.74f, 0.27f, 0.86f, 0.73f, 12, ColBlanco, ColNaranjaOsc);
            Boton(c, cab, "CERRAR", "eltiempo.ui cerrar", false, 0.875f, 0.27f, 0.975f, 0.73f, 12, ColBlanco, ColNaranjaOsc);

            // ---------- Tarjeta HORA ----------
            string hora = Panel(c, main, ColTarjeta, 0.02f, 0.19f, 0.28f, 0.87f);
            float h = HoraActual();
            int hh = (int)Mathf.Floor(h);

            Etiqueta(c, hora, "HORA DEL SERVIDOR", 13, ColNaranja, TextAnchor.MiddleLeft, 0.05f, 0.92f, 0.95f, 0.99f, true);
            EtiquetaViva(c, lista, uid, hora, "hora", 52, TextAnchor.MiddleCenter, 0.05f, 0.73f, 0.95f, 0.91f, true,
                TextoHora, () => ColTexto);
            Etiqueta(c, hora, datos.HoraCongelada ? "La hora está congelada" : "La hora avanza con normalidad", 12,
                datos.HoraCongelada ? ColNaranjaOsc : ColSuave, TextAnchor.MiddleCenter, 0.05f, 0.67f, 0.95f, 0.73f);

            // Atajos
            string[] atajoNombre = { "Amanecer\n06:00", "Mediodía\n12:00", "Atardecer\n18:00", "Noche\n00:00" };
            float[] atajoHora = { 6f, 12f, 18f, 0f };
            float aw = (0.9f - 3 * 0.02f) / 4f;
            for (int i = 0; i < 4; i++)
            {
                float a = 0.05f + i * (aw + 0.02f);
                Boton(c, hora, atajoNombre[i], "eltiempo.ui hora " + Num(atajoHora[i]), false, a, 0.54f, a + aw, 0.65f, 10);
            }

            // Cuadricula de horas
            int hAhora, mAhora;
            HoraMinutosActual(out hAhora, out mAhora);

            Etiqueta(c, hora, "Hora exacta", 12, ColSuave, TextAnchor.MiddleLeft, 0.05f, 0.45f, 0.40f, 0.515f, true);
            CajaHora(c, hora, hAhora.ToString("00"), "eltiempo.ui horah", 0.42f, 0.66f);
            Etiqueta(c, hora, ":", 16, ColTexto, TextAnchor.MiddleCenter, 0.66f, 0.45f, 0.71f, 0.515f, true);
            CajaHora(c, hora, mAhora.ToString("00"), "eltiempo.ui horam", 0.71f, 0.95f);
            float gw = (0.9f - 7 * 0.012f) / 8f;
            for (int i = 0; i < 24; i++)
            {
                int fila = i / 8;
                int col = i % 8;
                float x = 0.05f + col * (gw + 0.012f);
                float yTop = 0.425f - fila * 0.098f;
                Boton(c, hora, i.ToString("00"), "eltiempo.ui hora " + i, i == hh, x, yTop - 0.09f, x + gw, yTop, 11);
            }

            Interruptor(c, hora, "Congelar hora", "eltiempo.ui congelarhora", datos.HoraCongelada, 0.05f, 0.03f, 0.95f, 0.12f);

            // ---------- Tarjeta CLIMA ----------
            string clima = Panel(c, main, ColTarjeta, 0.29f, 0.19f, 0.98f, 0.87f);
            Etiqueta(c, clima, "CLIMA", 13, ColNaranja, TextAnchor.MiddleLeft, 0.03f, 0.93f, 0.3f, 0.99f, true);
            Etiqueta(c, clima, "Auto = el clima dinámico del servidor decide solo", 11, ColSuave,
                TextAnchor.MiddleRight, 0.3f, 0.93f, 0.97f, 0.99f);

            // Pestañas
            float tabW = (0.37f - 2 * 0.01f) / 3f;
            for (int t = 0; t < TabNombres.Length; t++)
            {
                float a = 0.03f + t * (tabW + 0.01f);
                Boton(c, clima, TabNombres[t], "eltiempo.ui tab " + t, t == tab, a, 0.86f, a + tabW, 0.92f, 12);
            }

            // Lista de parametros de la pestaña
            int ini = TabRangos[tab][0];
            int fin = TabRangos[tab][1];
            for (int i = ini; i < fin; i++)
            {
                int r = i - ini;
                float y1 = 0.83f - r * 0.088f;
                float y0 = y1 - 0.08f;
                bool seleccionado = i == sel;

                string fila = c.Add(new CuiButton
                {
                    Button = { Command = "eltiempo.ui sel " + i, Color = seleccionado ? ColNaranjaClaro : ColApagado },
                    RectTransform = { AnchorMin = Anc(0.03f, y0), AnchorMax = Anc(0.40f, y1) },
                    Text = { Text = "", FontSize = 1 }
                }, clima);

                int ii = i;
                Etiqueta(c, fila, P[i].Nombre, 13, ColTexto, TextAnchor.MiddleLeft, 0.05f, 0f, 0.55f, 1f, seleccionado);
                EtiquetaViva(c, lista, uid, fila, "fila" + ii, 13, TextAnchor.MiddleRight, 0.5f, 0f, 0.95f, 1f, true,
                    () => { bool f; return TextoValor(ii, jugador.transform.position, out f); },
                    () => ColorValor(ii, jugador.transform.position));
            }

            // Interruptores
            float tw = (0.37f - 2 * 0.01f) / 3f;
            Interruptor(c, clima, "Sin lluvia", "eltiempo.ui nuncallueve", datos.NuncaLluvia,
                0.03f, 0.03f, 0.03f + tw, 0.11f, 10);
            Interruptor(c, clima, "Sin niebla", "eltiempo.ui sinniebla", datos.SinNiebla,
                0.03f + tw + 0.01f, 0.03f, 0.03f + 2 * tw + 0.01f, 0.11f, 10);
            Interruptor(c, clima, "Congelar", "eltiempo.ui congelarclima", datos.ClimaCongelado,
                0.03f + 2 * (tw + 0.01f), 0.03f, 0.03f + 3 * tw + 0.02f, 0.11f, 10);

            // ---------- Detalle del parametro seleccionado ----------
            string det = Panel(c, clima, ColPanel, 0.42f, 0.03f, 0.97f, 0.92f);
            var par = P[sel];

            bool esFijo, hayVivo;
            float valor = ValorMostrado(sel, pos, out esFijo, out hayVivo);
            bool auto = !esFijo;

            Etiqueta(c, det, par.Nombre, 24, ColTexto, TextAnchor.MiddleLeft, 0.04f, 0.88f, 0.96f, 0.98f, true);
            Etiqueta(c, det, par.Conv, 11, ColSuave, TextAnchor.MiddleLeft, 0.04f, 0.82f, 0.96f, 0.88f);
            Etiqueta(c, det, par.Desc, 12, ColSuave, TextAnchor.UpperLeft, 0.04f, 0.68f, 0.96f, 0.81f);

            // Controles: -1 -0.1 -0.01 [ numero ] +0.01 +0.1 +1
            string[] pasoTxt = { "−1", "−0.1", "−0.01", "+0.01", "+0.1", "+1" };
            string[] pasoVal = { "-1", "-0.1", "-0.01", "0.01", "0.1", "1" };
            float[] pasoX = { 0.04f, 0.155f, 0.27f, 0.625f, 0.74f, 0.855f };
            for (int k = 0; k < 6; k++)
                Boton(c, det, pasoTxt[k], "eltiempo.ui paso " + sel + " " + pasoVal[k], false,
                    pasoX[k], 0.50f, pasoX[k] + 0.105f, 0.66f, 14);

            int sl = sel;
            EtiquetaViva(c, lista, uid, det, "grande", 44, TextAnchor.MiddleCenter, 0.385f, 0.50f, 0.615f, 0.66f, true,
                () => TextoGrande(sl, jugador.transform.position),
                () => ColorValor(sl, jugador.transform.position) == ColNaranjaOsc ? ColNaranjaOsc : ColSuave);

            EtiquetaViva(c, lista, uid, det, "estado", 11, TextAnchor.MiddleCenter, 0.05f, 0.43f, 0.95f, 0.49f, false,
                () => TextoEstado(sl, jugador.transform.position),
                () => ColorValor(sl, jugador.transform.position));

            // Entrada de texto
            Etiqueta(c, det, "Escribir valor:", 13, ColTexto, TextAnchor.MiddleLeft, 0.04f, 0.22f, 0.34f, 0.34f, true);
            string caja = Panel(c, det, ColTarjeta, 0.35f, 0.22f, 0.63f, 0.34f);
            c.Add(new CuiElement
            {
                Parent = caja,
                Components =
                {
                    new CuiInputFieldComponent
                    {
                        Text = (esFijo || hayVivo) ? Fmt(valor) : "",
                        FontSize = 16,
                        Align = TextAnchor.MiddleCenter,
                        Color = ColTexto,
                        CharsLimit = 8,
                        Command = "eltiempo.ui txt " + sel,
                        Font = FuenteNegrita
                    },
                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1" }
                }
            });
            Etiqueta(c, det, "Escribe y pulsa Enter", 11, ColSuave, TextAnchor.MiddleLeft, 0.65f, 0.22f, 0.97f, 0.34f);

            // Auto / Congelar
            Boton(c, det, "AUTOMÁTICO", "eltiempo.ui autoparam " + sel, auto, 0.04f, 0.04f, 0.48f, 0.15f, 12);
            if (hayVivo)
                Boton(c, det, "CONGELAR VALOR ACTUAL", "eltiempo.ui frz " + sel, false, 0.52f, 0.04f, 0.96f, 0.15f, 12);
            else
                Boton(c, det, "SIN LECTURA EN VIVO", "eltiempo.ui refrescar", false, 0.52f, 0.04f, 0.96f, 0.15f, 11,
                    ColApagado, ColSuave);

            // ---------- Ajustes rapidos (weather.load) ----------
            string rapidos = Panel(c, main, ColTarjeta, 0.02f, 0.03f, 0.98f, 0.16f);
            Etiqueta(c, rapidos, "Ajustes rápidos", 13, ColTexto, TextAnchor.MiddleLeft, 0.02f, 0.1f, 0.12f, 0.9f, true);

            int total = PresetClaves.Length + 1;
            float pw = (0.855f - (total - 1) * 0.01f) / total;
            for (int i = 0; i < total; i++)
            {
                float a = 0.135f + i * (pw + 0.01f);
                if (i < PresetClaves.Length)
                {
                    Boton(c, rapidos, PresetNombres[i], "eltiempo.ui preset " + PresetClaves[i],
                        datos.Preset == PresetClaves[i], a, 0.2f, a + pw, 0.8f, 12);
                }
                else
                {
                    Boton(c, rapidos, "Dinámico (auto)", "eltiempo.ui auto", datos.Preset == "",
                        a, 0.2f, a + pw, 0.8f, 12, ColApagado, ColTexto);
                }
            }

            CuiHelper.AddUi(jugador, c);
        }

        #endregion
    }
}
