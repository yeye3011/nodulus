using UnityEngine;

namespace ProceduralAudio
{
    // Formas de onda disponibles para los efectos. Son las mismas del
    // SimpleAdditiveOscillator de la Guia 2; "Additive" suma parciales con
    // amplitud 1/n, igual que la serie de armonicos trabajada en clase.
    public enum SfxWaveform
    {
        Sine,
        Triangle,
        Square,
        Sawtooth,
        Additive
    }

    // Un "patch" describe COMO suena un evento: oscilador, envolvente,
    // barrido de tono, FM y ruido. El manager arma un patch por cada evento
    // del juego y se lo entrega a una voz libre.
    [System.Serializable]
    public struct SfxPatch
    {
        public SfxWaveform waveform;
        public int partials;        // numero de parciales (solo modo Additive)

        public float startFreq;     // Hz al inicio del sonido
        public float endFreq;       // Hz al final del barrido
        public float glideTime;     // segundos que tarda el barrido (pitch envelope)

        public float attack;        // ADSR en segundos (sustain es nivel 0-1)
        public float decay;
        public float sustain;
        public float release;
        public float hold;          // segundos desde el inicio hasta soltar la "tecla"

        public float fmRatio;       // relacion modulador/portadora (FM ligera)
        public float fmIndex;       // profundidad de la FM; 0 = sin FM

        public float noiseMix;      // 0 = solo oscilador, 1 = solo ruido blanco
        public float gain;          // amplitud final de este sonido
    }

    // Una voz = un AudioSource + este script generando muestras en
    // OnAudioFilterRead. Es la evolucion del SimpleAdditiveOscillator:
    // conserva la ADSR, pero ahora funciona en modo "one-shot" porque en el
    // juego nadie suelta una tecla; el sonido se apaga solo despues de "hold".
    [RequireComponent(typeof(AudioSource))]
    public class ProceduralSfxVoice : MonoBehaviour
    {
        enum Stage { Idle, Attack, Decay, Sustain, Release }

        // Se lee de AudioSettings en vez de fijar 44100, porque muchos
        // equipos y celulares trabajan a 48000 Hz y todo sonaria desafinado.
        float sampleRate = 48000f;

        // --- Datos que escribe el hilo principal (Unity) ---
        // OnAudioFilterRead corre en OTRO hilo (el de audio). Por eso el hilo
        // principal no toca directamente el estado del sonido: deja un patch
        // "pendiente" y levanta una bandera que el hilo de audio recoge.
        SfxPatch pendingPatch;
        int pendingDelaySamples;
        volatile bool hasPending;
        volatile bool busy;

        // --- Estado que solo usa el hilo de audio ---
        SfxPatch patch;
        Stage stage = Stage.Idle;
        float env;
        float releaseStep;
        int delaySamples;
        int sampleCount;
        int holdSamples;

        // Acumuladores de fase en lugar de un indice de tiempo (timeIndex).
        // Con timeIndex, cambiar la frecuencia a mitad del sonido produce
        // saltos en la onda; con fase acumulada el barrido de tono es continuo.
        double phase;
        double modPhase;

        uint noiseState = 22222u;

        // El manager consulta esto para saber si la voz esta libre.
        public bool IsBusy { get { return busy; } }
        public float StartedAt { get; private set; }

        void Awake()
        {
            sampleRate = AudioSettings.outputSampleRate;

            // AudioSource sin clip: solo sirve de "enchufe" para que Unity
            // llame a OnAudioFilterRead. Sonido 2D (sin espacializacion)
            // porque los efectos de interfaz no dependen de la camara.
            var source = GetComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.volume = 1f;
            source.Play();
        }

        // Llamado desde el manager (hilo principal).
        public void Play(SfxPatch newPatch, float delaySeconds)
        {
            pendingPatch = newPatch;
            // El retardo se cuenta en muestras: es exacto y no depende de
            // corrutinas ni de los cuadros por segundo del juego.
            pendingDelaySamples = Mathf.Max(0, Mathf.RoundToInt(delaySeconds * sampleRate));
            StartedAt = Time.time;
            busy = true;
            hasPending = true;
        }

        void OnAudioFilterRead(float[] data, int channels)
        {
            if (hasPending)
            {
                patch = pendingPatch;
                delaySamples = pendingDelaySamples;
                sampleCount = 0;
                holdSamples = Mathf.RoundToInt(patch.hold * sampleRate);
                // No se reinician env ni phase: si la voz estaba sonando, el
                // nuevo ataque parte del valor actual y se evita un "clic".
                stage = Stage.Attack;
                hasPending = false;
            }

            if (stage == Stage.Idle)
            {
                return; // el buffer ya viene en silencio (no hay clip)
            }

            for (int i = 0; i < data.Length; i += channels)
            {
                float s = 0f;

                if (delaySamples > 0)
                {
                    delaySamples--;
                }
                else if (stage != Stage.Idle)
                {
                    // Igual que en la Guia 2: señal * envolvente * amplitud.
                    s = NextSample() * NextEnvelope() * patch.gain;
                    sampleCount++;
                }

                for (int c = 0; c < channels; c++)
                {
                    data[i + c] = s;
                }
            }
        }

        float NextSample()
        {
            float t = sampleCount / sampleRate;

            // Pitch envelope exponencial: el oido percibe el tono de forma
            // logaritmica, asi que un barrido exponencial suena "parejo".
            float glide = patch.glideTime > 0f ? Mathf.Clamp01(t / patch.glideTime) : 1f;
            float start = Mathf.Max(1f, patch.startFreq);
            float end = Mathf.Max(1f, patch.endFreq);
            float freq = start * Mathf.Pow(end / start, glide);

            // FM ligera: un seno modulador desvia la frecuencia de la
            // portadora. Con indices bajos agrega aspereza sin volverse ruido.
            float mod = 0f;
            if (patch.fmIndex > 0f)
            {
                mod = Mathf.Sin((float)(2.0 * System.Math.PI * modPhase)) * patch.fmIndex * freq;
                modPhase += freq * patch.fmRatio / sampleRate;
                modPhase -= System.Math.Floor(modPhase);
            }

            phase += (freq + mod) / sampleRate;
            phase -= System.Math.Floor(phase);

            float osc = Oscillator((float)phase);

            if (patch.noiseMix > 0f)
            {
                osc = Mathf.Lerp(osc, Noise(), patch.noiseMix);
            }

            return osc;
        }

        // p es la fase normalizada entre 0 y 1.
        float Oscillator(float p)
        {
            switch (patch.waveform)
            {
                case SfxWaveform.Triangle:
                    return 1f - 4f * Mathf.Abs(p - 0.5f);
                case SfxWaveform.Square:
                    return p < 0.5f ? 1f : -1f;
                case SfxWaveform.Sawtooth:
                    return 2f * p - 1f;
                case SfxWaveform.Additive:
                    // Suma de senoidales con amplitud 1/n, normalizada para
                    // que agregar parciales no aumente el volumen (ni clipee).
                    int n = Mathf.Clamp(patch.partials, 1, 9);
                    float sum = 0f;
                    float norm = 0f;
                    for (int k = 1; k <= n; k++)
                    {
                        sum += Mathf.Sin(2f * Mathf.PI * k * p) / k;
                        norm += 1f / k;
                    }
                    return sum / norm;
                default:
                    return Mathf.Sin(2f * Mathf.PI * p);
            }
        }

        // Ruido blanco con xorshift: no se usa UnityEngine.Random porque
        // la API de Unity no debe llamarse desde el hilo de audio.
        float Noise()
        {
            noiseState ^= noiseState << 13;
            noiseState ^= noiseState >> 17;
            noiseState ^= noiseState << 5;
            return (noiseState / (float)uint.MaxValue) * 2f - 1f;
        }

        // ADSR lineal por muestra, la misma logica de EvaluateAdsr de la
        // Guia 2, con "hold" haciendo el papel de NoteOff automatico.
        float NextEnvelope()
        {
            switch (stage)
            {
                case Stage.Attack:
                    env += 1f / Mathf.Max(1f, patch.attack * sampleRate);
                    if (env >= 1f)
                    {
                        env = 1f;
                        stage = Stage.Decay;
                    }
                    break;

                case Stage.Decay:
                    env -= (1f - patch.sustain) / Mathf.Max(1f, patch.decay * sampleRate);
                    if (env <= patch.sustain)
                    {
                        env = patch.sustain;
                        stage = Stage.Sustain;
                    }
                    break;

                case Stage.Sustain:
                    if (sampleCount >= holdSamples)
                    {
                        stage = Stage.Release;
                        // El release parte del nivel actual, sea cual sea.
                        releaseStep = env / Mathf.Max(1f, patch.release * sampleRate);
                    }
                    break;

                case Stage.Release:
                    env -= releaseStep;
                    if (env <= 0f)
                    {
                        env = 0f;
                        stage = Stage.Idle;
                        busy = false; // la voz queda libre para otro evento
                    }
                    break;
            }

            return env;
        }
    }
}
