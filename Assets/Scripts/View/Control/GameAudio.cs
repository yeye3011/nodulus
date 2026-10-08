using UnityEngine;
using ProceduralAudio;

namespace View.Control
{
    public class GameAudio : MonoBehaviour
    {
        private const string MusicStatusKey = "music.status";
        private const string SfxStatusKey = "sfx.status";

        private const float MusicVolume = 0.2f;

        public AudioClip[] MusicClips;
        public AudioClip[] SfxClips;

        [Header("Rediseño procedural (pruebas)")]
        [Tooltip("Silencia los efectos .wav originales que aun no tienen version procedural.")]
        public bool muteOriginalSfx = true;

        [Tooltip("Silencia la musica original de fondo.")]
        public bool muteOriginalMusic = true;

        [Tooltip("Escribe en la Console cada evento de sonido que dispara el juego.")]
        public bool logSoundEvents = true;

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

                if (_musicSource != null)
                {
                    _musicSource.volume = value ? MusicVolume : 0f;
                }
                PlayerPrefs.SetInt(MusicStatusKey, value ? 0 : 1);

                if (_music != null)
                {
                    _music.SetEnabled(value);
                }
            }
        }

        private ProceduralMusicSystem _music;

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

        private void Awake()
        {
            _procedural = GetComponent<ProceduralAudioManager>();
            if (_procedural == null)
            {
                _procedural = gameObject.AddComponent<ProceduralAudioManager>();
            }

            _music = GetComponent<ProceduralMusicSystem>();
            if (_music == null)
            {
                _music = gameObject.AddComponent<ProceduralMusicSystem>();
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

        public void Play(GameClip clip, float delay = 0f, float volume = 1f, float startTime = 0f)
        {
            if (_music != null)
            {
                _music.OnGameEvent(clip);
            }

            if (!enabled || !SfxEnabled)
            {
                return;
            }

            if (_procedural != null && _procedural.Trigger(clip, volume, delay))
            {
                if (logSoundEvents)
                {
                    Debug.Log("[Audio] " + clip + " -> PROCEDURAL");
                }
                return;
            }

            if (logSoundEvents)
            {
                Debug.Log("[Audio] " + clip + (muteOriginalSfx ? " -> original SILENCIADO" : " -> original .wav"));
            }
            if (muteOriginalSfx)
            {
                return;
            }

            if ((int)clip >= SfxClips.Length)
            {
                return;
            }

            var audioClip = SfxClips[(uint)clip];
            LeanAudio.play(audioClip, volume, delay, time: startTime);
        }

        public void Play(MusicClip clip, float fadeTime = 0f, float delay = 0f, float volume = 1f, float startTime = 0f)
        {
            if (!enabled || !MusicEnabled)
            {
                return;
            }

            if (muteOriginalMusic)
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
            const float fadeTime = 3f;
            const float startTime = 32f;
            Play(MusicClip.Ambient02, fadeTime: fadeTime, volume: MusicVolume, startTime: startTime);
        }
    }

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
        ArcMoveLow,

        UiConfirm,
        UiPause,    
        UiRestart,  
        UiToggle
    }

    public enum MusicClip
    {
        Ambient01,
        Ambient02
    }
}