using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEditor;
using View.Control;

namespace ProceduralAudio
{
    public static class ProceduralAudioExportTools
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        [MenuItem("CONTEXT/ProceduralAudioManager/Exportar tabla de sonidos")]
        static void Export(MenuCommand command)
        {
            var manager = (ProceduralAudioManager)command.context;
            var music = manager.GetComponent<ProceduralMusicSystem>();

            string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Documentacion"));
            Directory.CreateDirectory(folder);

            var md = new StringBuilder();
            var csv = new StringBuilder();

            md.AppendLine("Tabla de eventos sonoros - Nodulus procedural");
            md.AppendLine("=============================================");
            md.AppendLine();
            string header = "Generada: " + System.DateTime.Now.ToString("yyyy-MM-dd HH:mm") +
                            " | Volumen general de efectos: " + F(manager.sfxVolume);
            if (music != null) header += " | Volumen general de musica: " + F(music.musicVolume);
            md.AppendLine(header);
            md.AppendLine();
            md.AppendLine("Efectos de movimiento e interfaz");
            md.AppendLine("--------------------------------");
            md.AppendLine();
            md.AppendLine("| Familia | Evento | Experimento | Tecnica | Parametros principales | Bitacora |");
            md.AppendLine("|---|---|---|---|---|---|");

            csv.AppendLine("Familia;Evento;Experimento;Tecnica;Parametros principales;Bitacora");

            foreach (var m in manager.mapeo)
            {
                if (m == null) continue;

                string family = Family(m.evento);
                if (m.experimento == null)
                {
                    Row(md, csv, family, m.evento.ToString(), "(sin experimento)", "-", "-", "-");
                    continue;
                }

                var e = m.experimento;
                Row(md, csv, family, m.evento.ToString(), e.name, Technique(e), Parameters(e), Clean(e.notas));
            }

            if (music != null && music.musica != null)
            {
                var mu = music.musica;

                md.AppendLine();
                md.AppendLine("Musica de fondo");
                md.AppendLine("---------------");
                md.AppendLine();
                md.AppendLine("Experimento: " + mu.name);
                md.AppendLine();
                md.AppendLine("Estructura: " + F(mu.bpm) + " BPM, compas de " + mu.pulsosPorCompas + " pulsos, tonica " +
                              mu.tonica + mu.octavaBase + ", escala " + mu.escala + ".");
                md.AppendLine();
                md.AppendLine("Respuesta al progreso: intensidad maxima en el nivel " + mu.nivelesParaMaximo +
                              ", hasta " + mu.notasExtraMaximo + " notas extra por compas en el motivo, apertura de filtro x" +
                              F(mu.aperturaFiltroMaxima) + ", +" + F(mu.actividadPorMovimiento) +
                              " de actividad por movimiento (se pierde a " + F(mu.caidaActividadPorSegundo) +
                              " por segundo), cadencia de cierre " + (mu.cadenciaActiva ? "activa" : "inactiva") + ".");
                md.AppendLine();
                md.AppendLine("Bitacora: " + Clean(mu.notas));
                md.AppendLine();
                md.AppendLine("| Capa | Activa | Comportamiento de la capa | Timbre | Tecnica del timbre | Parametros del timbre | Bitacora del timbre |");
                md.AppendLine("|---|---|---|---|---|---|---|");

                csv.AppendLine();
                csv.AppendLine("Musica;" + Q(mu.name) + ";" + Q(F(mu.bpm) + " BPM, tonica " + mu.tonica + mu.octavaBase + ", escala " + mu.escala) + ";;;" + Q(Clean(mu.notas)));
                csv.AppendLine("Capa;Activa;Comportamiento de la capa;Timbre;Tecnica del timbre;Parametros del timbre;Bitacora del timbre");

                MusicRow(md, csv, "Dron", mu.dronActivo,
                    "Una nota cada " + mu.compasesPorNotaDron + " compases" +
                    (mu.dronAlternaQuinta ? ", alterna tonica y quinta" : ", siempre en la tonica") +
                    " / volumen de capa " + F(mu.volumenDron),
                    mu.timbreDron);

                MusicRow(md, csv, "Motivo", mu.motivoActivo,
                    mu.notasPorCompas + " notas por compas (probabilidad " + F(mu.probabilidadNota) +
                    "), caminata de hasta " + mu.saltoMaximo + " grados en un rango de " + mu.rangoGrados +
                    ", octava +" + mu.octavaMotivo + " / volumen de capa " + F(mu.volumenMotivo),
                    mu.timbreMotivo);

                MusicRow(md, csv, "Textura", mu.texturaActiva,
                    "Una nota cada " + mu.compasesPorNotaTextura + " compases desde intensidad " +
                    F(mu.intensidadParaTextura) + ", octava +" + mu.octavaTextura +
                    " / volumen de capa " + F(mu.volumenTextura),
                    mu.timbreTextura);
            }

            string mdPath = Path.Combine(folder, "TablaSonidos.md");
            string csvPath = Path.Combine(folder, "TablaSonidos.csv");

            File.WriteAllText(mdPath, md.ToString(), new UTF8Encoding(true));
            File.WriteAllText(csvPath, csv.ToString(), new UTF8Encoding(true));

            Debug.Log("Tabla exportada en: " + folder);
            EditorUtility.RevealInFinder(mdPath);
        }


        static void Row(StringBuilder md, StringBuilder csv, string family, string evt, string exp,
                        string technique, string parameters, string notes)
        {
            md.AppendLine("| " + M(family) + " | " + M(evt) + " | " + M(exp) + " | " + M(technique) +
                          " | " + M(parameters) + " | " + M(notes) + " |");
            csv.AppendLine(Q(family) + ";" + Q(evt) + ";" + Q(exp) + ";" + Q(technique) + ";" +
                           Q(parameters) + ";" + Q(notes));
        }

        static void MusicRow(StringBuilder md, StringBuilder csv, string layer, bool active,
                             string behaviour, SoundExperiment timbre)
        {
            string name = timbre != null ? timbre.name : "(sin timbre)";
            string technique = timbre != null ? Technique(timbre) : "-";
            string parameters = timbre != null ? Parameters(timbre, false) : "-";
            string notes = timbre != null ? Clean(timbre.notas) : "-";
            string act = active ? "Si" : "No";

            md.AppendLine("| " + layer + " | " + act + " | " + M(behaviour) + " | " + M(name) + " | " +
                          M(technique) + " | " + M(parameters) + " | " + M(notes) + " |");
            csv.AppendLine(Q(layer) + ";" + Q(act) + ";" + Q(behaviour) + ";" + Q(name) + ";" +
                           Q(technique) + ";" + Q(parameters) + ";" + Q(notes));
        }

        static string Technique(SoundExperiment e)
        {
            var parts = new List<string>();

            switch (e.waveform)
            {
                case WaveformType.Additive:
                    parts.Add("Aditiva " + e.additiveMode + " (" + e.harmonicCount + " armonicos)");
                    break;
                case WaveformType.Wavetable:
                    parts.Add("Wavetable " + e.selectedWavetable);
                    break;
                case WaveformType.Square: parts.Add("Onda cuadrada"); break;
                case WaveformType.Triangle: parts.Add("Onda triangular"); break;
                case WaveformType.Sawtooth: parts.Add("Diente de sierra"); break;
                default: parts.Add("Onda seno"); break;
            }

            if (Mathf.Abs(e.glideSemitonos) > 0.01f)
                parts.Add("pitch envelope " + (e.glideSemitonos > 0 ? "ascendente" : "descendente"));
            if (e.fmIndex > 0f)
                parts.Add("FM");
            if (e.tremoloRate > 0f && e.tremoloDepth > 0f)
                parts.Add("tremolo (LFO de amplitud)");
            if (e.vibratoRate > 0f && e.vibratoSemitonos > 0f)
                parts.Add("vibrato (LFO de tono)");
            if (e.noiseMix > 0f)
                parts.Add("ruido blanco");
            if (e.lowpassCutoff < 19999f)
                parts.Add("filtro pasa-bajos");
            if (e.secuenciaSemitonos != null && e.secuenciaSemitonos.Length > 0)
                parts.Add("secuencia de " + e.secuenciaSemitonos.Length + " notas");

            parts.Add("ADSR");
            return string.Join(" + ", parts);
        }

        static string Parameters(SoundExperiment e, bool includeFrequency = true)
        {
            var parts = new List<string>();

            if (includeFrequency)
            {
                if (e.usarNota)
                    parts.Add("Nota " + e.nota + e.octava + " (" + F(e.BaseFrequency()) + " Hz)");
                else
                    parts.Add("Frecuencia " + F(e.frequency) + " Hz");
            }

            parts.Add("Amplitud " + F(e.amplitude));
            parts.Add("ADSR A " + Ms(e.attack) + " / D " + Ms(e.decay) + " / S " + F(e.sustainLevel) +
                      " por " + Ms(e.sustainDuration) + " / R " + Ms(e.release));

            if (e.waveform == WaveformType.Additive && e.additiveMode == AdditiveMode.Generic && e.harmonicLevels != null)
            {
                var levels = new List<string>();
                for (int i = 0; i < e.harmonicCount && i < e.harmonicLevels.Length; i++)
                    levels.Add(F(e.harmonicLevels[i]));
                parts.Add("Niveles de armonicos [" + string.Join(", ", levels) + "]");
            }
            if (Mathf.Abs(e.glideSemitonos) > 0.01f)
                parts.Add("Barrido " + (e.glideSemitonos > 0 ? "+" : "") + F(e.glideSemitonos) + " semitonos en " + Ms(e.glideTime));
            if (e.fmIndex > 0f)
                parts.Add("FM ratio " + F(e.fmRatio) + ", indice " + F(e.fmIndex));
            if (e.tremoloRate > 0f && e.tremoloDepth > 0f)
                parts.Add("Tremolo " + F(e.tremoloRate) + " Hz, profundidad " + F(e.tremoloDepth));
            if (e.vibratoRate > 0f && e.vibratoSemitonos > 0f)
                parts.Add("Vibrato " + F(e.vibratoRate) + " Hz, " + F(e.vibratoSemitonos) + " semitonos");
            if (e.noiseMix > 0f)
                parts.Add("Ruido " + Mathf.RoundToInt(e.noiseMix * 100f) + " %");
            if (e.lowpassCutoff < 19999f)
                parts.Add("Corte pasa-bajos " + F(e.lowpassCutoff) + " Hz");
            if (e.secuenciaSemitonos != null && e.secuenciaSemitonos.Length > 0)
            {
                var steps = new List<string>();
                foreach (var s in e.secuenciaSemitonos) steps.Add(F(s));
                parts.Add("Secuencia [" + string.Join(", ", steps) + "] semitonos cada " + Ms(e.intervaloSecuencia));
            }
            if (includeFrequency && e.pitchVariation > 0f)
                parts.Add("Variacion de tono +/-" + F(e.pitchVariation * 100f) + " %");

            return string.Join(" / ", parts);
        }

        static string Family(GameClip clip)
        {
            switch (clip)
            {
                case GameClip.MenuSelect:
                case GameClip.LevelEnable:
                case GameClip.UiConfirm:
                case GameClip.UiPause:
                case GameClip.UiRestart:
                case GameClip.UiToggle:
                    return "Interfaz";
                case GameClip.GameStart:
                case GameClip.GameEnd:
                case GameClip.WinBoard:
                case GameClip.NodeEnter:
                case GameClip.NodeLeave:
                    return "Tablero y progreso";
                default:
                    return "Movimiento";
            }
        }


        static string F(float value)
        {
            return value.ToString("0.###", Inv);
        }

        static string Ms(float seconds)
        {
            return Mathf.RoundToInt(seconds * 1000f) + " ms";
        }

        static string Clean(string text)
        {
            if (string.IsNullOrEmpty(text)) return "-";
            return text.Replace("\r", "").Replace("\n", " / ").Trim();
        }

        static string M(string text)
        {
            return text.Replace("|", "/");
        }

        static string Q(string text)
        {
            return "\"" + text.Replace("\"", "\"\"") + "\"";
        }
    }
}