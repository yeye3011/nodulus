using UnityEngine;
using ProceduralAudio; // [NUEVO] acceso al sistema de audio procedural

namespace View.Control
{
    /// <summary>
    /// The main controller for the game's audio. Handles SFX along with looping music.
    /// </summary>
    public class GameAudio : MonoBehaviour
    {
        private const string MusicStatusKey = "music.status";
        private const string SfxStatusKey = "sfx.status";

        private const float MusicVolume = 0.2f;

        public AudioClip[] MusicClips;
        public AudioClip[] SfxClips;

        private AudioSource _musicSource;
        private int _musicVolumeTweenId;
        private bool _musicEnabled = true;
        public bool MusicEnabled
        {
            get { return _musicEnabled; }
            set
            {
                if (_musicEnabled != value)
                {
                    LeanTween.cancel(_musicVolumeTweenId);
                }

                _musicEnabled = value;
                _musicSource.volume = value ? MusicVolume : 0f;
                PlayerPrefs.SetInt(MusicStatusKey, value ? 0 : 1);
            }
        }

        // [NUEVO] Referencia al manager procedural que genera los efectos.
        private ProceduralAudioManager _procedural;

        private bool _sfxEnabled = true;
        public bool SfxEnabled
        {
            get { return _sfxEnabled; }
            set
            {
                _sfxEnabled = value;
                PlayerPrefs.SetInt(SfxStatusKey, value ? 0 : 1);
            }
        }

        // [NUEVO] Awake corre antes que cualquier Start, asi el manager existe
        // antes de que otro script pida un sonido. Si el manager ya fue
        // agregado a mano en la escena se usa ese (y sus valores del
        // Inspector); si no, se crea por codigo sin modificar la escena.
        private void Awake()
        {
            _procedural = GetComponent<ProceduralAudioManager>();
            if (_procedural == null)
            {
                _procedural = gameObject.AddComponent<ProceduralAudioManager>();
            }
        }

        private void Start()
        {
            StartMusic();

            if (!PlayerPrefs.HasKey(MusicStatusKey))
            {
                PlayerPrefs.SetInt(MusicStatusKey, 0);
            }
            if (!PlayerPrefs.HasKey(SfxStatusKey))
            {
                PlayerPrefs.SetInt(SfxStatusKey, 0);
            }

            MusicEnabled = PlayerPrefs.GetInt(MusicStatusKey) == 0;
            SfxEnabled = PlayerPrefs.GetInt(SfxStatusKey) == 0;
        }

        /// <summary>
        /// Plays the given sound clip with the specified parameters.
        /// </summary>
        public void Play(GameClip clip, float delay = 0f, float volume = 1f, float startTime = 0f)
        {
            if (!enabled || !SfxEnabled)
            {
                return;
            }

            // [NUEVO] Primero se intenta el sonido procedural. Si el evento ya
            // tiene patch, el manager lo genera y aqui termina. Si todavia no
            // tiene, se reproduce el .wav original como respaldo, para que el
            // juego nunca quede mudo mientras se rediseñan los eventos.
            if (_procedural != null && _procedural.Trigger(clip, volume, delay))
            {
                return;
            }

            var audioClip = SfxClips[(uint)clip];
            LeanAudio.play(audioClip, volume, delay, time: startTime);
        }

        /// <summary>
        /// Plays the given music clip with the specifide parameters.
        /// </summary>
        public void Play(MusicClip clip, float fadeTime = 0f, float delay = 0f, float volume = 1f, float startTime = 0f)
        {
            if (!enabled || !MusicEnabled)
            {
                return;
            }

            var audioClip = MusicClips[(uint)clip];

            _musicSource = LeanAudio.play(audioClip, 0f, delay, true, startTime);

            _musicVolumeTweenId = LeanTween.value(0f, volume, fadeTime)
                .setDelay(delay)
                .setEase(LeanTweenType.easeInOutSine)
                .setOnUpdate(v => {
                    _musicSource.volume = v;
                })
                .id;
        }

        private void StartMusic()
        {
            // TODO: make configurable
            const float fadeTime = 3f;
            const float startTime = 32f;
            Play(MusicClip.Ambient02, fadeTime: fadeTime, volume: MusicVolume, startTime: startTime);
        }
    }

    /// <summary>
    /// A one-to-one map of all sound clips
    /// </summary>
    public enum GameClip
    {
        GameStart,
        WinBoard,
        NodeEnter,
        NodeLeave,
        MovePushHigh,
        MovePullHigh,
        MovePullMid,
        MovePushMid,
        MovePullLow,
        MovePushLow,
        ArcMoveHigh,
        NodeRotate90,
        InvalidRotate,
        MenuSelect,
        GameEnd,
        LevelEnable,
        ArcMoveMid,
        ArcMoveLow
    }

    /// <summary>
    /// A one-to-one map of all music clips
    /// </summary>
    public enum MusicClip
    {
        Ambient01,
        Ambient02
    }
}