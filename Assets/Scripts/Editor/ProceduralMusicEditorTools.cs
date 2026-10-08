using UnityEngine;
using UnityEditor;

namespace ProceduralAudio
{
    // Herramienta solo de editor (carpeta Editor). Agrega al menu de los 3
    // puntitos del ProceduralMusicSystem la opcion "Crear musica inicial",
    // que genera un experimento de musica y los timbres de sus tres capas.
    public static class ProceduralMusicEditorTools
    {
        [MenuItem("CONTEXT/ProceduralMusicSystem/Crear musica inicial")]
        static void CreateStarterMusic(MenuCommand command)
        {
            var system = (ProceduralMusicSystem)command.context;

            const string folder = "Assets/ExperimentosMusica";
            if (!AssetDatabase.IsValidFolder(folder))
            {
                AssetDatabase.CreateFolder("Assets", "ExperimentosMusica");
            }

            // Capa 1: dron. Wavetable triangular muy filtrada (casi un seno
            // grave) con ataque y release largos, y un LFO de amplitud muy
            // lento (tremolo 0.2 Hz) que la hace "respirar".
            var dron = ScriptableObject.CreateInstance<SoundExperiment>();
            dron.notas = "Idea: pedal grave que da continuidad y espacio.\n" +
                         "Tecnica: wavetable Triangle + pasa-bajos cerrado + LFO lento de amplitud.\n" +
                         "La frecuencia la decide la musica (tonica o quinta).\nQue se percibe:";
            dron.waveform = WaveformType.Wavetable;
            dron.selectedWavetable = WavetableBank.WavetableName.Triangle;
            dron.lowpassCutoff = 500f;
            dron.attack = 1.5f;
            dron.decay = 0.5f;
            dron.sustainLevel = 0.8f;
            dron.sustainDuration = 1.5f; // la musica lo reemplaza por la duracion real
            dron.release = 2f;
            dron.tremoloRate = 0.2f;
            dron.tremoloDepth = 0.4f;
            dron.pitchVariation = 0f;
            dron.amplitude = 0.5f;
            Save(dron, folder, "Dron_Exp01_WavetableFiltrado");

            // Capa 2: motivo. Aditiva generica con 3 armonicos, filtro medio
            // (el progreso lo abre) y envolvente de nota pulsada.
            var motivo = ScriptableObject.CreateInstance<SoundExperiment>();
            motivo.notas = "Idea: pocas notas espaciadas que sugieran progreso.\n" +
                           "Tecnica: aditiva generica (3 armonicos) + pasa-bajos que se abre con la intensidad.\n" +
                           "Que se percibe:";
            motivo.waveform = WaveformType.Additive;
            motivo.additiveMode = AdditiveMode.Generic;
            motivo.harmonicCount = 3;
            motivo.lowpassCutoff = 1500f;
            motivo.attack = 0.01f;
            motivo.decay = 0.4f;
            motivo.sustainLevel = 0.2f;
            motivo.sustainDuration = 0.2f;
            motivo.release = 0.8f;
            motivo.pitchVariation = 0f;
            motivo.amplitude = 0.5f;
            Save(motivo, folder, "Motivo_Exp01_AditivaFiltrada");

            // Capa 3: textura alta. Aditiva con solo 2 parciales (brillo suave)
            // y tremolo lento, con ataque y release largos.
            var textura = ScriptableObject.CreateInstance<SoundExperiment>();
            textura.notas = "Idea: brillo y movimiento sutil cuando el jugador avanza.\n" +
                            "Tecnica: aditiva con parciales bajos + tremolo lento.\n" +
                            "Que se percibe:";
            textura.waveform = WaveformType.Additive;
            textura.additiveMode = AdditiveMode.Generic;
            textura.harmonicCount = 2;
            textura.attack = 0.8f;
            textura.decay = 0.5f;
            textura.sustainLevel = 0.5f;
            textura.sustainDuration = 1f;
            textura.release = 1.5f;
            textura.tremoloRate = 0.5f;
            textura.tremoloDepth = 0.6f;
            textura.pitchVariation = 0f;
            textura.amplitude = 0.35f;
            Save(textura, folder, "Textura_Exp01_AditivaTremolo");

            // Experimento de musica que une las tres capas.
            var musica = ScriptableObject.CreateInstance<MusicExperiment>();
            musica.notas = "Idea: musica minimalista y lenta que acompañe la concentracion.\n" +
                           "Estructura: D pentatonica menor, 66 BPM, dron + motivo + textura.\n" +
                           "Que se percibe:";
            musica.timbreDron = dron;
            musica.timbreMotivo = motivo;
            musica.timbreTextura = textura;
            Save(musica, folder, "Musica_Exp01_PentatonicaMenor");

            Undo.RecordObject(system, "Crear musica inicial");
            system.musica = musica;
            EditorUtility.SetDirty(system);
            AssetDatabase.SaveAssets();

            Debug.Log("Musica creada en " + folder + ". Guarda la escena (Ctrl+S) para conservar la asignacion.");
        }

        static void Save(Object asset, string folder, string fileName)
        {
            AssetDatabase.CreateAsset(asset, AssetDatabase.GenerateUniqueAssetPath(folder + "/" + fileName + ".asset"));
        }
    }
}