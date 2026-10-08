using System.Collections.Generic;
using UnityEngine;
using View.Control;

namespace ProceduralAudio
{
    [System.Serializable]
    public class SoundMapping
    {
        public GameClip evento;
        public SoundExperiment experimento;
    }

    public class ProceduralAudioManager : MonoBehaviour
    {
        public static ProceduralAudioManager Instance { get; private set; }

        [Header("Mapeo: evento del juego -> experimento")]
        [Tooltip("Si un evento no tiene experimento asignado, GameAudio lo silencia o usa el .wav original segun 'Mute Original Sfx'.")]
        public List<SoundMapping> mapeo = new List<SoundMapping>();

        [Header("Wavetables (opcional)")]
        [Tooltip("Tu WavetableBank de la Guia 3. Si queda vacio, se busca en este mismo objeto; si no hay, los experimentos Wavetable suenan como seno.")]
        public WavetableBank wavetableBank;

        [Header("Mezcla")]

        [Range(0f, 1f)]
        public float sfxVolume = 0.8f;

        [Header("Voces")]

        public int voiceCount = 8;

        [Header("Escucha en vivo")]
        [Tooltip("Tiempo minimo entre repeticiones al mover sliders, para que no se amontonen los sonidos.")]
        [Range(0.05f, 1f)]
        public float previewInterval = 0.25f;

        ProceduralSfxVoice[] voices;
        int nextVoice;
        SoundExperiment pendingPreview;
        float lastPreviewTime = -10f;

        void Awake()
        {
            Instance = this;

            if (wavetableBank == null)
            {
                wavetableBank = GetComponent<WavetableBank>();
            }

            voices = new ProceduralSfxVoice[voiceCount];
            for (int i = 0; i < voiceCount; i++)
            {
                var go = new GameObject("SfxVoice_" + i);
                go.transform.SetParent(transform, false);
                go.AddComponent<AudioSource>();
                voices[i] = go.AddComponent<ProceduralSfxVoice>();
            }
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            if (pendingPreview != null && Time.unscaledTime - lastPreviewTime >= previewInterval)
            {
                PlayExperiment(pendingPreview, 1f, 0f);
                pendingPreview = null;
                lastPreviewTime = Time.unscaledTime;
            }
        }

        public bool Trigger(GameClip clip, float volume = 1f, float delay = 0f)
        {
            SoundExperiment experiment = FindExperiment(clip);
            if (experiment == null)
            {
                return false;
            }

            PlayExperiment(experiment, volume, delay);
            return true;
        }

        public void RequestPreview(SoundExperiment experiment)
        {
            pendingPreview = experiment;
        }

        public SoundExperiment FindExperiment(GameClip clip)
        {
            for (int i = 0; i < mapeo.Count; i++)
            {
                if (mapeo[i] != null && mapeo[i].evento == clip && mapeo[i].experimento != null)
                {
                    return mapeo[i].experimento;
                }
            }
            return null;
        }

        void PlayExperiment(SoundExperiment experiment, float volume, float delay)
        {
            if (voices == null) return;

            float variation = 1f + Random.Range(-experiment.pitchVariation, experiment.pitchVariation);

            float[] steps = experiment.secuenciaSemitonos;
            int count = (steps != null && steps.Length > 0) ? steps.Length : 1;

            for (int i = 0; i < count; i++)
            {
                float semitones = (steps != null && steps.Length > 0) ? steps[i] : 0f;
                float transpose = Mathf.Pow(2f, semitones / 12f);

                SfxPatch patch = experiment.ToPatch(wavetableBank, variation * transpose);
                patch.amplitude *= volume * sfxVolume;

                GetFreeVoice().NoteOn(patch, delay + i * experiment.intervaloSecuencia);
            }
        }

        ProceduralSfxVoice GetFreeVoice()
        {
            for (int i = 0; i < voices.Length; i++)
            {
                int index = (nextVoice + i) % voices.Length;
                if (!voices[index].IsBusy)
                {
                    nextVoice = (index + 1) % voices.Length;
                    return voices[index];
                }
            }

            int oldest = 0;
            for (int i = 1; i < voices.Length; i++)
            {
                if (voices[i].StartedAt < voices[oldest].StartedAt) oldest = i;
            }
            return voices[oldest];
        }

    }
}