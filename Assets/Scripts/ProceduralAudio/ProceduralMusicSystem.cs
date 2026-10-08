using UnityEngine;
using View.Control;
using Core.Game;

namespace ProceduralAudio
{
    public class ProceduralMusicSystem : MonoBehaviour
    {
        [Header("Experimento de musica activo")]
        public MusicExperiment musica;

        [Header("Mezcla")]

        [Range(0f, 1f)]
        public float musicVolume = 0.35f;

        [Header("Voces de musica")]
        public int voiceCount = 10;

        [Header("Estado (solo para observar en Play)")]
        [SerializeField] int nivelActual;
        [SerializeField][Range(0f, 1f)] float intensidad;
        [SerializeField] float actividad;

        ProceduralSfxVoice[] voices;
        int nextVoice;
        WavetableBank bank;

        bool musicEnabled = true;
        double nextBarDsp;
        int barCount;
        int motifDegree;
        int silenceBars;
        bool warned;

        const double Lookahead = 0.25;

        void Awake()
        {
            bank = GetComponent<WavetableBank>();

            voices = new ProceduralSfxVoice[voiceCount];
            for (int i = 0; i < voiceCount; i++)
            {
                var go = new GameObject("MusicVoice_" + i);
                go.transform.SetParent(transform, false);
                go.AddComponent<AudioSource>();
                voices[i] = go.AddComponent<ProceduralSfxVoice>();
            }
        }

        void Start()
        {
            ReadLevel();
            nextBarDsp = AudioSettings.dspTime + 0.5;
        }

        void Update()
        {
            if (musica == null)
            {
                if (!warned)
                {
                    Debug.LogWarning("ProceduralMusicSystem: asigna un Experimento de musica (o usa 'Crear musica inicial').");
                    warned = true;
                }
                return;
            }

            actividad = Mathf.Max(0f, actividad - musica.caidaActividadPorSegundo * Time.deltaTime);

            float levelPart = Mathf.Clamp01(nivelActual / (float)Mathf.Max(1, musica.nivelesParaMaximo));
            intensidad = Mathf.Clamp01(levelPart + actividad);

            if (!musicEnabled) return;

            double now = AudioSettings.dspTime;

            if (nextBarDsp < now) nextBarDsp = now + 0.05;

            while (now + Lookahead >= nextBarDsp)
            {
                ScheduleBar(nextBarDsp);
                nextBarDsp += BarSeconds();
                barCount++;
            }
        }

        float BarSeconds()
        {
            return 60f / Mathf.Max(1f, musica.bpm) * musica.pulsosPorCompas;
        }

        void ScheduleBar(double barStartDsp)
        {
            float bar = BarSeconds();
            float delay = (float)(barStartDsp - AudioSettings.dspTime);

            int droneEvery = Mathf.Max(1, musica.compasesPorNotaDron);
            if (musica.dronActivo && musica.timbreDron != null && barCount % droneEvery == 0)
            {
                bool useFifth = musica.dronAlternaQuinta && (barCount / droneEvery) % 2 == 1;
                int degree = useFifth ? FifthDegree() : 0;
                PlayNote(musica.timbreDron, degree, 0, musica.volumenDron, delay, bar * droneEvery, 1f);
            }

            int textureEvery = Mathf.Max(1, musica.compasesPorNotaTextura);
            if (musica.texturaActiva && musica.timbreTextura != null &&
                intensidad >= musica.intensidadParaTextura && barCount % textureEvery == 0)
            {
                int[] chordDegrees = { 0, 2, 4 };
                int degree = chordDegrees[Random.Range(0, chordDegrees.Length)];
                PlayNote(musica.timbreTextura, degree, musica.octavaTextura, musica.volumenTextura, delay, bar * textureEvery, 1f);
            }

            if (silenceBars > 0)
            {
                silenceBars--;
                return;
            }

            if (musica.motivoActivo && musica.timbreMotivo != null)
            {
                int notes = musica.notasPorCompas + Mathf.RoundToInt(musica.notasExtraMaximo * intensidad);
                float step = bar / Mathf.Max(1, notes);
                float filterScale = Mathf.Lerp(1f, musica.aperturaFiltroMaxima, intensidad);

                for (int i = 0; i < notes; i++)
                {
                    if (Random.value > musica.probabilidadNota) continue;

                    int jump = Random.Range(-musica.saltoMaximo, musica.saltoMaximo + 1);
                    motifDegree = Mathf.Clamp(motifDegree + jump, 0, Mathf.Max(1, musica.rangoGrados) - 1);

                    float velocity = Random.Range(0.8f, 1f);
                    PlayNote(musica.timbreMotivo, motifDegree, musica.octavaMotivo,
                             musica.volumenMotivo * velocity, delay + i * step, -1f, filterScale);
                }
            }
        }

        public void OnGameEvent(GameClip clip)
        {
            if (musica == null) return;

            switch (clip)
            {
                case GameClip.NodeRotate90:
                case GameClip.MovePushHigh:
                case GameClip.MovePushMid:
                case GameClip.MovePushLow:
                case GameClip.MovePullHigh:
                case GameClip.MovePullMid:
                case GameClip.MovePullLow:
                    actividad = Mathf.Min(1f, actividad + musica.actividadPorMovimiento);
                    break;

                case GameClip.GameStart:
                    ReadLevel();
                    actividad = 0f;
                    break;

                case GameClip.WinBoard:
                    if (musicEnabled && musica.cadenciaActiva && musica.timbreMotivo != null)
                    {
                        float beat = 60f / Mathf.Max(1f, musica.bpm);
                        int octaveUp = musica.Intervalos().Length;
                        PlayNote(musica.timbreMotivo, FifthDegree(), musica.octavaMotivo, musica.volumenMotivo, 0.05f, -1f, musica.aperturaFiltroMaxima);
                        PlayNote(musica.timbreMotivo, octaveUp, musica.octavaMotivo, musica.volumenMotivo, 0.05f + beat, beat * 2f, musica.aperturaFiltroMaxima);
                        silenceBars = 1;
                    }
                    break;
            }
        }

        public void SetEnabled(bool on)
        {
            musicEnabled = on;

            if (!on && voices != null)
            {
                foreach (var v in voices) v.NoteOff();
            }
            else
            {
                nextBarDsp = AudioSettings.dspTime + 0.1;
            }
        }

        void ReadLevel()
        {
            try
            {
                nivelActual = Levels.CurrentLevelNum;
            }
            catch
            {

            }
        }

        float DegreeToFrequency(int degree, int octaveOffset)
        {
            int[] intervals = musica.Intervalos();
            int len = intervals.Length;
            int octave = Mathf.FloorToInt(degree / (float)len);
            int index = degree - octave * len;
            int semitones = 12 * (octave + octaveOffset) + intervals[index];

            float tonic = MusicUtils.NoteToFrequency(musica.tonica.Trim().ToUpper(), musica.octavaBase);
            return tonic * Mathf.Pow(2f, semitones / 12f);
        }

        int FifthDegree()
        {
            int[] intervals = musica.Intervalos();
            for (int i = 0; i < intervals.Length; i++)
            {
                if (intervals[i] == 7) return i;
            }
            return intervals.Length / 2; 
        }

        void PlayNote(SoundExperiment timbre, int degree, int octaveOffset, float volume,
                      float delay, float length, float filterScale)
        {
            SfxPatch patch = timbre.ToPatch(bank, 1f);
            patch.frequency = DegreeToFrequency(degree, octaveOffset);
            patch.amplitude *= volume * musicVolume;

            if (filterScale != 1f && patch.lowpassCutoff < 20000f)
            {
                patch.lowpassCutoff = Mathf.Min(20000f, patch.lowpassCutoff * filterScale);
            }

            if (length > 0f)
            {
                patch.sustainDuration = Mathf.Max(0f, length - patch.attack - patch.decay);
            }

            GetFreeVoice().NoteOn(patch, Mathf.Max(0f, delay));
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