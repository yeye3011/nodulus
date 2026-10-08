using UnityEngine;

namespace ProceduralAudio
{
    // Mismas formas de onda y modos aditivos del SimpleAdditiveOscillator.
    // Se declaran aqui (fuera de la clase) para que las usen tanto el
    // experimento como la voz que lo reproduce.
    public enum WaveformType
    {
        Sine,
        Square,
        Triangle,
        Sawtooth,
        Additive,
        Wavetable
    }

    public enum AdditiveMode
    {
        Generic,
        SquareFourier,
        TriangleFourier,
        SawtoothFourier
    }

    // Un EXPERIMENTO de sonido guardado como archivo (.asset) dentro del
    // proyecto. Se eligio ScriptableObject en vez de variables en un script
    // por tres razones:
    //  1. Los cambios hechos en el Inspector DURANTE el Play se conservan al
    //     salir de Play (con un MonoBehaviour se perderian).
    //  2. Cada experimento es un archivo aparte: se puede duplicar (Ctrl+D),
    //     renombrar y comparar versiones sin borrar las anteriores.
    //  3. El campo "notas" funciona como bitacora del experimento, que es el
    //     registro de cambios que pide la guia.
    [CreateAssetMenu(fileName = "NuevoExperimento", menuName = "Audio Procedural/Experimento de sonido")]
    public class SoundExperiment : ScriptableObject
    {
        [Header("Bitacora del experimento")]
        [TextArea(3, 8)]
        public string notas = "Idea:\nCambio probado:\nQue se percibe:";

        [Header("Escucha en vivo")]
        [Tooltip("En Play, cada cambio en este Inspector dispara el sonido para escucharlo al instante.")]
        public bool escucharAlCambiar = true;

        [Header("Oscilador")]
        public WaveformType waveform = WaveformType.Sine;

        [Range(0f, 1f)]
        public float amplitude = 0.3f;

        [Header("Frecuencia")]
        [Tooltip("Si esta activo, la frecuencia sale de nota + octava con MusicUtils (como el teclado de la Guia 2).")]
        public bool usarNota = false;
        public string nota = "A";
        [Range(0, 8)]
        public int octava = 4;
        [Range(20f, 4000f)]
        public float frequency = 440f;

        [Header("Barrido de tono (pitch envelope)")]
        [Tooltip("Cuantos semitonos sube (+) o baja (-) el tono durante el sonido. 12 = una octava.")]
        [Range(-24f, 24f)]
        public float glideSemitonos = 0f;
        [Tooltip("Segundos que tarda el barrido. En 0 el tono salta de inmediato.")]
        [Range(0f, 1f)]
        public float glideTime = 0.05f;

        [Header("Sintesis aditiva (waveform = Additive)")]
        public AdditiveMode additiveMode = AdditiveMode.Generic;
        [Range(1, 10)]
        public int harmonicCount = 10;
        public float[] harmonicLevels = new float[10]
        {
            1f, 0.5f, 0.33f, 0.25f, 0.20f, 0.16f, 0.14f, 0.125f, 0.11f, 0.10f
        };

        [Header("Wavetable (waveform = Wavetable)")]
        public WavetableBank.WavetableName selectedWavetable = WavetableBank.WavetableName.Sine;

        [Header("ADSR (segundos, como en la Guia 2)")]
        [Range(0f, 0.5f)]
        public float attack = 0.005f;
        [Range(0f, 2f)]
        public float decay = 0.1f;
        [Range(0f, 1f)]
        public float sustainLevel = 0f;
        [Range(0f, 3f)]
        public float sustainDuration = 0f;
        [Range(0f, 3f)]
        public float release = 0.1f;

        [Header("FM (0 = sin FM)")]
        [Tooltip("Frecuencia del modulador como multiplo de la portadora. Enteros = timbre armonico; decimales = timbre metalico o inarmonico.")]
        [Range(0f, 8f)]
        public float fmRatio = 2f;
        [Tooltip("Profundidad de la modulacion. Mas alto = mas armonicos nuevos y mas aspereza.")]
        [Range(0f, 10f)]
        public float fmIndex = 0f;

        [Header("LFO")]
        [Tooltip("Tremolo: oscilacion lenta del VOLUMEN (Hz).")]
        [Range(0f, 30f)]
        public float tremoloRate = 0f;
        [Range(0f, 1f)]
        public float tremoloDepth = 0f;
        [Tooltip("Vibrato: oscilacion lenta del TONO (Hz).")]
        [Range(0f, 30f)]
        public float vibratoRate = 0f;
        [Range(0f, 2f)]
        public float vibratoSemitonos = 0f;

        [Header("Ruido y filtro")]
        [Range(0f, 1f)]
        public float noiseMix = 0f;
        [Tooltip("Filtro pasa-bajos. 20000 = abierto (sin filtro); valores bajos = sonido mas opaco.")]
        [Range(50f, 20000f)]
        public float lowpassCutoff = 20000f;

        [Header("Variacion por disparo")]
        [Tooltip("Variacion aleatoria de tono en cada disparo, para evitar repeticion mecanica.")]
        [Range(0f, 0.1f)]
        public float pitchVariation = 0.02f;

        [Header("Secuencia de notas (opcional)")]
        [Tooltip("Vacio = una sola nota. Con valores, el sonido se repite una vez por cada numero, transpuesto esos semitonos. Ej: 0,4,7,12 = arpegio mayor; 0,7 = dos tonos en quinta.")]
        public float[] secuenciaSemitonos = new float[0];
        [Tooltip("Segundos entre una nota y la siguiente de la secuencia.")]
        [Range(0.02f, 0.5f)]
        public float intervaloSecuencia = 0.1f;

        public float BaseFrequency()
        {
            if (usarNota)
            {
                return MusicUtils.NoteToFrequency(nota.Trim().ToUpper(), octava);
            }
            return frequency;
        }

        // Convierte el experimento en una "foto" de valores (SfxPatch) para la
        // voz. Se copia todo porque la voz lee en el hilo de audio y el
        // Inspector puede cambiar este asset en cualquier momento.
        public SfxPatch ToPatch(WavetableBank bank, float variation)
        {
            float[] levels = new float[10];
            if (harmonicLevels != null)
            {
                for (int i = 0; i < levels.Length && i < harmonicLevels.Length; i++)
                {
                    levels[i] = harmonicLevels[i];
                }
            }

            float[] table = null;
            if (waveform == WaveformType.Wavetable && bank != null)
            {
                table = bank.GetTable(selectedWavetable);
            }

            return new SfxPatch
            {
                waveform = waveform,
                additiveMode = additiveMode,
                harmonicCount = harmonicCount,
                harmonicLevels = levels,
                wavetable = table,
                frequency = BaseFrequency() * variation,
                glideSemitones = glideSemitonos,
                glideTime = glideTime,
                amplitude = amplitude,
                attack = attack,
                decay = decay,
                sustainLevel = sustainLevel,
                sustainDuration = sustainDuration,
                release = release,
                fmRatio = fmRatio,
                fmIndex = fmIndex,
                tremoloRate = tremoloRate,
                tremoloDepth = tremoloDepth,
                vibratoRate = vibratoRate,
                vibratoSemitones = vibratoSemitonos,
                noiseMix = noiseMix,
                lowpassCutoff = lowpassCutoff
            };
        }

        // Unity llama OnValidate cada vez que se cambia un valor en el
        // Inspector. Si el juego esta en Play, se pide escuchar el sonido:
        // asi se mueve un slider y se oye el resultado de inmediato.
        void OnValidate()
        {
            if (harmonicLevels == null || harmonicLevels.Length != 10)
            {
                float[] fixedArray = new float[10];
                for (int i = 0; i < fixedArray.Length; i++)
                {
                    fixedArray[i] = (harmonicLevels != null && i < harmonicLevels.Length) ? harmonicLevels[i] : 0f;
                }
                harmonicLevels = fixedArray;
            }

            if (Application.isPlaying && escucharAlCambiar && ProceduralAudioManager.Instance != null)
            {
                ProceduralAudioManager.Instance.RequestPreview(this);
            }
        }

        // Menu de los 3 puntitos del Inspector: escuchar sin tener que hacer
        // la accion dentro del juego.
        [ContextMenu("Probar sonido (en Play)")]
        void Probar()
        {
            if (ProceduralAudioManager.Instance == null)
            {
                Debug.LogWarning("Dale Play al juego para poder escuchar el experimento.");
                return;
            }
            ProceduralAudioManager.Instance.RequestPreview(this);
        }

        // Genera valores al azar dentro de rangos razonables. Sirve como punto
        // de partida para experimentos que no se nos ocurririan a mano; luego
        // se ajusta a oido lo que resulte interesante.
        // Se ejecuta desde el menu de los 3 puntitos del Inspector; ese menu
        // lo agrega el script ProceduralAudioEditorTools (carpeta Editor),
        // que ademas permite deshacer con Ctrl+Z.
        public void Aleatorizar()
        {
            waveform = (WaveformType)Random.Range(0, 6);
            additiveMode = (AdditiveMode)Random.Range(0, 4);
            harmonicCount = Random.Range(1, 11);
            usarNota = false;
            frequency = Random.Range(120f, 1500f);
            glideSemitonos = Random.Range(-12f, 12f);
            glideTime = Random.Range(0.01f, 0.3f);
            attack = Random.Range(0.001f, 0.05f);
            decay = Random.Range(0.03f, 0.4f);
            sustainLevel = Random.Range(0f, 0.6f);
            sustainDuration = Random.Range(0f, 0.2f);
            release = Random.Range(0.03f, 0.5f);
            float[] ratios = { 0.5f, 1f, 1.41f, 2f, 3f, 3.5f };
            fmRatio = ratios[Random.Range(0, ratios.Length)];
            fmIndex = Random.value < 0.5f ? 0f : Random.Range(0.2f, 4f);
            tremoloRate = Random.value < 0.5f ? 0f : Random.Range(2f, 20f);
            tremoloDepth = tremoloRate > 0f ? Random.Range(0.2f, 0.9f) : 0f;
            vibratoRate = Random.value < 0.7f ? 0f : Random.Range(3f, 12f);
            vibratoSemitonos = vibratoRate > 0f ? Random.Range(0.1f, 1f) : 0f;
            noiseMix = Random.value < 0.7f ? 0f : Random.Range(0.05f, 0.5f);
            lowpassCutoff = Random.value < 0.5f ? 20000f : Random.Range(400f, 6000f);
            notas += "\n[Aleatorizado]";
            if (Application.isPlaying && ProceduralAudioManager.Instance != null)
            {
                ProceduralAudioManager.Instance.RequestPreview(this);
            }
        }
    }
}