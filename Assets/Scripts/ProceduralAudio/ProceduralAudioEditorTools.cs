using UnityEngine;
using UnityEditor;
using View.Control;

namespace ProceduralAudio
{
    // Herramientas que solo existen dentro del editor de Unity.
    // Este archivo DEBE estar dentro de una carpeta llamada "Editor": Unity
    // no incluye esas carpetas al compilar el juego final, y por eso aqui se
    // puede usar UnityEditor (AssetDatabase, Undo...) sin directivas "#if".
    public static class ProceduralAudioEditorTools
    {
        // Agrega "Aleatorizar parametros" al menu de los 3 puntitos de cada
        // experimento. Undo.RecordObject permite deshacerlo con Ctrl+Z.
        [MenuItem("CONTEXT/SoundExperiment/Aleatorizar parametros (experimento)")]
        static void Randomize(MenuCommand command)
        {
            var experiment = (SoundExperiment)command.context;
            Undo.RecordObject(experiment, "Aleatorizar experimento");
            experiment.Aleatorizar();
            // SetDirty le avisa a Unity que el archivo cambio y debe guardarse.
            EditorUtility.SetDirty(experiment);
        }

        // Menu de los 3 puntitos del componente (fuera de Play).
        // Crea una fila de mapeo por cada evento del juego y un experimento
        // de PUNTO DE PARTIDA para cada evento que todavia no tenga uno. Los
        // eventos que ya tienen experimento NO se tocan, asi que se puede
        // ejecutar varias veces sin perder ajustes.
        //
        // Cada punto de partida usa una tecnica distinta del curso, para que
        // el conjunto sirva como catalogo de experimentos y no como copia de
        // los sonidos originales.
        [MenuItem("CONTEXT/ProceduralAudioManager/Crear experimentos faltantes (todos los eventos)")]
        static void CreateMissingExperiments(MenuCommand command)
        {
            var manager = (ProceduralAudioManager)command.context;

            const string folder = "Assets/ExperimentosSonido";
            if (!AssetDatabase.IsValidFolder(folder))
            {
                AssetDatabase.CreateFolder("Assets", "ExperimentosSonido");
            }

            Undo.RecordObject(manager, "Crear experimentos faltantes");

            foreach (GameClip clip in System.Enum.GetValues(typeof(GameClip)))
            {
                bool exists = false;
                foreach (var m in manager.mapeo)
                {
                    if (m != null && m.evento == clip) { exists = true; break; }
                }
                if (!exists)
                {
                    manager.mapeo.Add(new SoundMapping { evento = clip });
                }
            }

            int created = 0;

            // ---------------- MOVIMIENTO ----------------

            created += Make(manager, folder, GameClip.NodeRotate90, "Rotar_Exp01_TriangularAscendente",
                "Idea: pulso corto y ascendente que comunique 'se hizo'.\nTecnica: triangular + pitch envelope + ADSR percusiva.", e =>
                {
                    e.waveform = WaveformType.Triangle; e.frequency = 420f;
                    e.glideSemitonos = 3.7f; e.glideTime = 0.05f;
                    e.attack = 0.005f; e.decay = 0.06f; e.release = 0.08f;
                    e.amplitude = 0.3f;
                });

            created += Make(manager, folder, GameClip.InvalidRotate, "Invalido_Exp01_FMDescendente",
                "Idea: un 'no' que no castigue.\nTecnica: seno descendente + FM (ratio 2, indice 0.8).", e =>
                {
                    e.waveform = WaveformType.Sine; e.frequency = 260f;
                    e.glideSemitonos = -6.4f; e.glideTime = 0.09f;
                    e.attack = 0.003f; e.decay = 0.09f; e.release = 0.06f;
                    e.fmRatio = 2f; e.fmIndex = 0.8f;
                    e.amplitude = 0.25f;
                });

            // Varillas: tres alturas (High/Mid/Low) segun el largo de la varilla.
            // Push = encaje (sube); Pull = desencaje (baja, con FM inarmonica
            // de ratio 1.41 para que suene "metalico" y distinto del push).
            string[] heightNames = { "High", "Mid", "Low" };
            GameClip[] pushClips = { GameClip.MovePushHigh, GameClip.MovePushMid, GameClip.MovePushLow };
            GameClip[] pullClips = { GameClip.MovePullHigh, GameClip.MovePullMid, GameClip.MovePullLow };
            GameClip[] slideClips = { GameClip.ArcMoveHigh, GameClip.ArcMoveMid, GameClip.ArcMoveLow };
            float[] heightFreqs = { 660f, 523f, 392f };
            float[] slideCutoffs = { 3000f, 1600f, 800f };

            for (int i = 0; i < 3; i++)
            {
                float f = heightFreqs[i];
                float cutoff = slideCutoffs[i];

                created += Make(manager, folder, pushClips[i], "Encajar" + heightNames[i] + "_Exp01_FourierCuadrada",
                    "Idea: encaje de varilla; la altura depende del largo.\nTecnica: aditiva SquareFourier con 3 armonicos impares + barrido ascendente.", e =>
                    {
                        e.waveform = WaveformType.Additive; e.additiveMode = AdditiveMode.SquareFourier;
                        e.harmonicCount = 3; e.frequency = f;
                        e.glideSemitonos = 5f; e.glideTime = 0.06f;
                        e.attack = 0.004f; e.decay = 0.12f; e.release = 0.1f;
                        e.amplitude = 0.22f;
                    });

                created += Make(manager, folder, pullClips[i], "Soltar" + heightNames[i] + "_Exp01_FMInarmonica",
                    "Idea: desencaje, opuesto al encaje.\nTecnica: seno + FM con ratio no entero (1.41) + barrido descendente.", e =>
                    {
                        e.waveform = WaveformType.Sine; e.frequency = f;
                        e.glideSemitonos = -5f; e.glideTime = 0.08f;
                        e.attack = 0.004f; e.decay = 0.12f; e.release = 0.1f;
                        e.fmRatio = 1.41f; e.fmIndex = 1.2f;
                        e.amplitude = 0.2f;
                    });

                created += Make(manager, folder, slideClips[i], "Deslizar" + heightNames[i] + "_Exp01_RuidoTremolo",
                    "Idea: la varilla se desliza; textura en vez de tono.\nTecnica: ruido filtrado (pasa-bajos) + tremolo rapido que lo 'granula'.", e =>
                    {
                        e.waveform = WaveformType.Sine; e.frequency = f / 2f;
                        e.noiseMix = 0.7f; e.lowpassCutoff = cutoff;
                        e.attack = 0.02f; e.decay = 0.1f; e.sustainLevel = 0.4f;
                        e.sustainDuration = 0.08f; e.release = 0.12f;
                        e.tremoloRate = 22f; e.tremoloDepth = 0.7f;
                        e.amplitude = 0.15f;
                    });
            }

            created += Make(manager, folder, GameClip.NodeEnter, "AparecerNodo_Exp01_GotaSeno",
                "Idea: los nodos aparecen en ola; muchas gotas cortas.\nTecnica: seno muy corto + gran variacion de tono por disparo.", e =>
                {
                    e.waveform = WaveformType.Sine; e.frequency = 1200f;
                    e.glideSemitonos = 2f; e.glideTime = 0.02f;
                    e.attack = 0.002f; e.decay = 0.035f; e.release = 0.03f;
                    e.pitchVariation = 0.08f;
                    e.amplitude = 0.12f;
                });

            created += Make(manager, folder, GameClip.NodeLeave, "DesaparecerNodo_Exp01_CuadradaFiltrada",
                "Idea: espejo de la aparicion, mas opaco.\nTecnica: cuadrada con pasa-bajos + barrido descendente.", e =>
                {
                    e.waveform = WaveformType.Square; e.frequency = 900f;
                    e.lowpassCutoff = 1500f;
                    e.glideSemitonos = -5f; e.glideTime = 0.04f;
                    e.attack = 0.002f; e.decay = 0.04f; e.release = 0.04f;
                    e.pitchVariation = 0.06f;
                    e.amplitude = 0.1f;
                });

            created += Make(manager, folder, GameClip.WinBoard, "Victoria_Exp01_ArpegioTriangularFourier",
                "Idea: cierre de nivel con tonos ascendentes.\nTecnica: secuencia 0,4,7,12,16 (arpegio mayor) + aditiva TriangleFourier.", e =>
                {
                    e.waveform = WaveformType.Additive; e.additiveMode = AdditiveMode.TriangleFourier;
                    e.harmonicCount = 5;
                    e.usarNota = true; e.nota = "C"; e.octava = 5;
                    e.secuenciaSemitonos = new float[] { 0f, 4f, 7f, 12f, 16f };
                    e.intervaloSecuencia = 0.09f;
                    e.attack = 0.005f; e.decay = 0.2f; e.sustainLevel = 0.3f;
                    e.sustainDuration = 0.1f; e.release = 0.4f;
                    e.pitchVariation = 0f;
                    e.amplitude = 0.2f;
                });

            created += Make(manager, folder, GameClip.GameStart, "InicioNivel_Exp01_AditivaTremolo",
                "Idea: el tablero 'despierta'.\nTecnica: aditiva generica (6 armonicos) + barrido de una octava + tremolo.", e =>
                {
                    e.waveform = WaveformType.Additive; e.additiveMode = AdditiveMode.Generic;
                    e.harmonicCount = 6; e.frequency = 110f;
                    e.glideSemitonos = 12f; e.glideTime = 0.8f;
                    e.attack = 0.3f; e.decay = 0.2f; e.sustainLevel = 0.6f;
                    e.sustainDuration = 0.3f; e.release = 0.8f;
                    e.tremoloRate = 6f; e.tremoloDepth = 0.3f;
                    e.pitchVariation = 0f;
                    e.amplitude = 0.2f;
                });

            created += Make(manager, folder, GameClip.GameEnd, "FinNivel_Exp01_SierraFourierFiltrada",
                "Idea: el tablero se 'apaga', opuesto al inicio.\nTecnica: aditiva SawtoothFourier + pasa-bajos + barrido descendente de una octava.", e =>
                {
                    e.waveform = WaveformType.Additive; e.additiveMode = AdditiveMode.SawtoothFourier;
                    e.harmonicCount = 8; e.frequency = 330f;
                    e.lowpassCutoff = 1200f;
                    e.glideSemitonos = -12f; e.glideTime = 0.7f;
                    e.attack = 0.05f; e.decay = 0.3f; e.sustainLevel = 0.4f;
                    e.sustainDuration = 0.2f; e.release = 0.6f;
                    e.pitchVariation = 0f;
                    e.amplitude = 0.18f;
                });

            // ---------------- INTERFAZ ----------------

            created += Make(manager, folder, GameClip.LevelEnable, "NivelDesbloqueado_Exp01_WavetableVibrato",
                "Idea: algo nuevo esta disponible.\nTecnica: wavetable (tabla Triangle) + vibrato lento.", e =>
                {
                    e.waveform = WaveformType.Wavetable;
                    e.selectedWavetable = WavetableBank.WavetableName.Triangle;
                    e.frequency = 880f;
                    e.attack = 0.01f; e.decay = 0.1f; e.sustainLevel = 0.5f;
                    e.sustainDuration = 0.25f; e.release = 0.3f;
                    e.vibratoRate = 5f; e.vibratoSemitonos = 0.3f;
                    e.amplitude = 0.15f;
                });

            created += Make(manager, folder, GameClip.MenuSelect, "VolverSelector_Exp01_SenoDescendente",
                "Idea: abrir el selector de niveles = 'volver'.\nTecnica: seno corto con pitch envelope descendente.", e =>
                {
                    e.waveform = WaveformType.Sine; e.frequency = 700f;
                    e.glideSemitonos = -7f; e.glideTime = 0.08f;
                    e.attack = 0.003f; e.decay = 0.08f; e.release = 0.05f;
                    e.amplitude = 0.2f;
                });

            created += Make(manager, folder, GameClip.UiConfirm, "Confirmar_Exp01_DosTonosQuinta",
                "Idea: confirmar el nivel elegido.\nTecnica: secuencia de dos tonos ascendentes en quinta (0,7).", e =>
                {
                    e.waveform = WaveformType.Sine; e.frequency = 660f;
                    e.secuenciaSemitonos = new float[] { 0f, 7f };
                    e.intervaloSecuencia = 0.07f;
                    e.attack = 0.003f; e.decay = 0.07f; e.release = 0.06f;
                    e.pitchVariation = 0f;
                    e.amplitude = 0.18f;
                });

            created += Make(manager, folder, GameClip.UiPause, "Ajustes_Exp01_WavetableFiltroCerrado",
                "Idea: pulso neutro al abrir/cerrar ajustes (pausa).\nTecnica: wavetable Square con pasa-bajos muy cerrado.", e =>
                {
                    e.waveform = WaveformType.Wavetable;
                    e.selectedWavetable = WavetableBank.WavetableName.Square;
                    e.frequency = 220f; e.lowpassCutoff = 600f;
                    e.attack = 0.005f; e.decay = 0.12f; e.release = 0.08f;
                    e.amplitude = 0.3f;
                });

            created += Make(manager, folder, GameClip.UiRestart, "Reiniciar_Exp01_RuidoMasTono",
                "Idea: reinicio seco y claro, sin sonar a error.\nTecnica: ruido filtrado breve mezclado con un tono base.", e =>
                {
                    e.waveform = WaveformType.Triangle; e.frequency = 330f;
                    e.noiseMix = 0.5f; e.lowpassCutoff = 3000f;
                    e.attack = 0.002f; e.decay = 0.12f; e.release = 0.05f;
                    e.amplitude = 0.22f;
                });

            created += Make(manager, folder, GameClip.UiToggle, "Interruptor_Exp01_CampanaFM",
                "Idea: interruptor de musica/efectos con caracter propio.\nTecnica: FM con ratio 3.5 (timbre de campana) + decay largo.", e =>
                {
                    e.waveform = WaveformType.Sine; e.frequency = 1000f;
                    e.fmRatio = 3.5f; e.fmIndex = 2f;
                    e.attack = 0.002f; e.decay = 0.3f; e.release = 0.2f;
                    e.amplitude = 0.15f;
                });

            AssetDatabase.SaveAssets();
            EditorUtility.SetDirty(manager);
            Debug.Log("Se crearon " + created + " experimentos en " + folder + ". Guarda la escena (Ctrl+S) para conservar el mapeo.");
        }

        // Crea el experimento solo si el evento todavia no tiene uno.
        static int Make(ProceduralAudioManager manager, string folder, GameClip clip, string fileName, string notes, System.Action<SoundExperiment> setup)
        {
            if (manager.FindExperiment(clip) != null)
            {
                return 0;
            }

            var e = ScriptableObject.CreateInstance<SoundExperiment>();
            e.notas = notes + "\nQue se percibe:";
            setup(e);
            AssetDatabase.CreateAsset(e, AssetDatabase.GenerateUniqueAssetPath(folder + "/" + fileName + ".asset"));

            foreach (var m in manager.mapeo)
            {
                if (m != null && m.evento == clip)
                {
                    m.experimento = e;
                    return 1;
                }
            }
            manager.mapeo.Add(new SoundMapping { evento = clip, experimento = e });
            return 1;
        }
    }
}