using UnityEngine;
using View.Control;

namespace ProceduralAudio
{
    // Recibe los eventos del juego (GameClip) y decide que sonido generar.
    // Es el unico lugar donde se definen los sonidos: los scripts del juego
    // no saben nada de sintesis, solo avisan "paso tal cosa".
    public class ProceduralAudioManager : MonoBehaviour
    {
        [Header("Mezcla")]
        // Volumen general de efectos. Se deja por debajo de 1 para conservar
        // margen (headroom) cuando suenan varias voces al tiempo.
        [Range(0f, 1f)]
        public float sfxVolume = 0.8f;

        [Header("Voces")]
        // Una voz solo puede sonar un evento a la vez, asi que se crea un
        // grupo (pool). 8 alcanza para rafagas como la aparicion del tablero.
        public int voiceCount = 8;

        [Header("Variacion")]
        // Pequeña variacion aleatoria de tono en cada disparo (±2 %), para
        // evitar la repeticion mecanica que menciona la guia.
        [Range(0f, 0.1f)]
        public float pitchVariation = 0.02f;

        ProceduralSfxVoice[] voices;
        int nextVoice;

        void Awake()
        {
            // Las voces se crean por codigo como hijos de este objeto, asi no
            // hay que modificar la escena ni los prefabs del juego original.
            voices = new ProceduralSfxVoice[voiceCount];
            for (int i = 0; i < voiceCount; i++)
            {
                var go = new GameObject("SfxVoice_" + i);
                go.transform.SetParent(transform, false);
                go.AddComponent<AudioSource>();
                voices[i] = go.AddComponent<ProceduralSfxVoice>();
            }
        }

        // Devuelve true si este evento ya tiene sonido procedural. Si
        // devuelve false, GameAudio usa el .wav original. Asi el juego sigue
        // sonando completo mientras se diseñan los eventos uno por uno.
        public bool Trigger(GameClip clip, float volume = 1f, float delay = 0f)
        {
            SfxPatch patch;
            if (!TryGetPatch(clip, out patch))
            {
                return false;
            }

            // UnityEngine.Random se usa aqui (hilo principal), no en la voz.
            float variation = 1f + Random.Range(-pitchVariation, pitchVariation);
            patch.startFreq *= variation;
            patch.endFreq *= variation;

            patch.gain *= volume * sfxVolume;

            GetFreeVoice().Play(patch, delay);
            return true;
        }

        ProceduralSfxVoice GetFreeVoice()
        {
            // Busca una voz libre empezando por la siguiente en la rotacion.
            for (int i = 0; i < voices.Length; i++)
            {
                int index = (nextVoice + i) % voices.Length;
                if (!voices[index].IsBusy)
                {
                    nextVoice = (index + 1) % voices.Length;
                    return voices[index];
                }
            }

            // Si todas estan ocupadas, se "roba" la que empezo hace mas tiempo:
            // es la que probablemente ya esta en su release y menos se nota.
            int oldest = 0;
            for (int i = 1; i < voices.Length; i++)
            {
                if (voices[i].StartedAt < voices[oldest].StartedAt)
                {
                    oldest = i;
                }
            }
            return voices[oldest];
        }

        bool TryGetPatch(GameClip clip, out SfxPatch patch)
        {
            switch (clip)
            {
                case GameClip.NodeRotate90:
                    patch = RotatePatch();
                    return true;

                case GameClip.InvalidRotate:
                    patch = InvalidPatch();
                    return true;

                // Los demas eventos se iran agregando aqui en las siguientes
                // fases. Mientras tanto siguen usando el audio original.
                default:
                    patch = default(SfxPatch);
                    return false;
            }
        }

        // ---------------- Patches de movimiento ----------------

        // Rotacion valida: pulso corto y brillante con leve barrido
        // ascendente. La onda triangular tiene armonicos suaves, asi que se
        // percibe "precisa" sin ser agresiva. Ataque de 5 ms para que el
        // sonido responda de inmediato al gesto del jugador.
        static SfxPatch RotatePatch()
        {
            return new SfxPatch
            {
                waveform = SfxWaveform.Triangle,
                startFreq = 420f,
                endFreq = 520f,
                glideTime = 0.05f,
                attack = 0.005f,
                decay = 0.06f,
                sustain = 0f,
                release = 0.08f,
                hold = 0f,
                gain = 0.30f
            };
        }

        // Movimiento invalido: mas grave, descendente y con FM ligera. El
        // barrido hacia abajo se asocia con "no" o "fallo", y la FM le da una
        // aspereza que lo diferencia claramente del giro valido, sin sonar a
        // error grave. Se deja un poco mas bajo de volumen que el giro.
        static SfxPatch InvalidPatch()
        {
            return new SfxPatch
            {
                waveform = SfxWaveform.Sine,
                startFreq = 260f,
                endFreq = 180f,
                glideTime = 0.09f,
                attack = 0.003f,
                decay = 0.09f,
                sustain = 0f,
                release = 0.06f,
                hold = 0f,
                fmRatio = 2f,
                fmIndex = 0.8f,
                gain = 0.25f
            };
        }
    }
}
