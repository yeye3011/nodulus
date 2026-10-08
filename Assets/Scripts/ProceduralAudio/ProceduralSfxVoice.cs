using UnityEngine;

namespace ProceduralAudio
{
    public struct SfxPatch
    {
        public WaveformType waveform;
        public AdditiveMode additiveMode;
        public int harmonicCount;
        public float[] harmonicLevels;
        public float[] wavetable;

        public float frequency;
        public float glideSemitones;
        public float glideTime;
        public float amplitude;

        public float attack;
        public float decay;
        public float sustainLevel;
        public float sustainDuration;
        public float release;

        public float fmRatio;
        public float fmIndex;

        public float tremoloRate;
        public float tremoloDepth;
        public float vibratoRate;
        public float vibratoSemitones;

        public float noiseMix;
        public float lowpassCutoff;
    }

    [RequireComponent(typeof(AudioSource))]
    public class ProceduralSfxVoice : MonoBehaviour
    {
        enum AdsrStage { Idle, Attack, Decay, Sustain, Release }

        float sampleRate = 48000f;

        SfxPatch pendingPatch;
        int pendingDelaySamples;
        volatile bool hasPending;
        volatile bool noteOffRequested;
        volatile bool busy;

        SfxPatch p;
        AdsrStage stage = AdsrStage.Idle;
        float envelopeValue = 0f;
        float releaseStartValue = 0f;
        float sustainTimer = 0f;
        float noteTime = 0f;
        int delaySamples;

        double phase;
        double modPhase;
        double tremoloPhase;
        double vibratoPhase;

        float filterState;
        float filterCoef = 1f;
        uint noiseState = 22222u;

        public bool IsBusy { get { return busy; } }
        public float StartedAt { get; private set; }

        void Awake()
        {
            sampleRate = AudioSettings.outputSampleRate;

            var source = GetComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.volume = 1f;
            source.Play();
        }

        public void NoteOn(SfxPatch patch, float delaySeconds)
        {
            pendingPatch = patch;
            pendingDelaySamples = Mathf.Max(0, Mathf.RoundToInt(delaySeconds * sampleRate));
            StartedAt = Time.time;
            busy = true;
            noteOffRequested = false;
            hasPending = true;
        }

        public void NoteOff()
        {
            noteOffRequested = true;
        }

        void StartPendingNote()
        {
            p = pendingPatch;
            delaySamples = pendingDelaySamples;
            noteTime = 0f;
            sustainTimer = 0f;

            stage = AdsrStage.Attack;

            filterCoef = p.lowpassCutoff >= 19999f
                ? 1f
                : 1f - Mathf.Exp(-2f * Mathf.PI * p.lowpassCutoff / sampleRate);

            hasPending = false;
        }

        float EvaluateAdsr(float dt)
        {
            switch (stage)
            {
                case AdsrStage.Attack:
                    envelopeValue += dt / Mathf.Max(p.attack, 0.0001f);
                    if (envelopeValue >= 1f)
                    {
                        envelopeValue = 1f;
                        stage = AdsrStage.Decay;
                    }
                    break;

                case AdsrStage.Decay:
                    envelopeValue -= dt * (1f - p.sustainLevel) / Mathf.Max(p.decay, 0.0001f);
                    if (envelopeValue <= p.sustainLevel)
                    {
                        envelopeValue = p.sustainLevel;
                        stage = AdsrStage.Sustain;
                        sustainTimer = 0f;
                    }
                    break;

                case AdsrStage.Sustain:
                    sustainTimer += dt;
                    if (sustainTimer >= p.sustainDuration)
                    {
                        releaseStartValue = envelopeValue;
                        stage = AdsrStage.Release;
                    }
                    break;

                case AdsrStage.Release:
                    envelopeValue -= dt * releaseStartValue / Mathf.Max(p.release, 0.0001f);
                    if (envelopeValue <= 0f)
                    {
                        envelopeValue = 0f;
                        stage = AdsrStage.Idle;
                        busy = false;
                    }
                    break;
            }

            return envelopeValue;
        }

        float SineWave(float ph)
        {
            return Mathf.Sin(2f * Mathf.PI * ph);
        }

        float SquareWave(float ph)
        {
            return Mathf.Sign(SineWave(ph));
        }

        float TriangleWave(float ph)
        {
            return 1f - 4f * Mathf.Abs(ph - 0.5f);
        }

        float SawtoothWave(float ph)
        {
            return 1f - 2f * ph;
        }

        float AdditiveGeneric(float ph)
        {
            int count = Mathf.Clamp(p.harmonicCount, 1, p.harmonicLevels != null ? p.harmonicLevels.Length : 1);
            if (p.harmonicLevels == null) return SineWave(ph);

            float sum = 0f;
            float totalLevel = 0f;

            for (int harmonic = 1; harmonic <= count; harmonic++)
            {
                float level = p.harmonicLevels[harmonic - 1];
                sum += level * SineWave(harmonic * ph);
                totalLevel += level;
            }

            return totalLevel > 0f ? sum / totalLevel : 0f;
        }

        float AdditiveSquareFourier(float ph)
        {
            float sum = 0f;
            for (int i = 0; i < p.harmonicCount; i++)
            {
                int k = 2 * i + 1;
                sum += (1f / k) * SineWave(k * ph);
            }
            return sum * 4f / Mathf.PI;
        }

        float AdditiveTriangleFourier(float ph)
        {
            float sum = 0f;
            for (int i = 0; i < p.harmonicCount; i++)
            {
                int k = 2 * i + 1;
                float sign = (i % 2 == 0) ? 1f : -1f;
                sum += sign * (1f / (k * k)) * SineWave(k * ph);
            }
            return sum * 8f / (Mathf.PI * Mathf.PI);
        }

        float AdditiveSawtoothFourier(float ph)
        {
            float sum = 0f;
            for (int k = 1; k <= p.harmonicCount; k++)
            {
                float sign = (k % 2 == 0) ? -1f : 1f;
                sum += sign * (1f / k) * SineWave(k * ph);
            }
            return sum * 2f / Mathf.PI;
        }

        float AdditiveWave(float ph)
        {
            switch (p.additiveMode)
            {
                case AdditiveMode.SquareFourier: return AdditiveSquareFourier(ph);
                case AdditiveMode.TriangleFourier: return AdditiveTriangleFourier(ph);
                case AdditiveMode.SawtoothFourier: return AdditiveSawtoothFourier(ph);
                default: return AdditiveGeneric(ph);
            }
        }

        float ReadWavetable(float phase01)
        {
            float[] table = p.wavetable;
            if (table == null || table.Length < 2) return SineWave(phase01);

            float exactIndex = phase01 * table.Length;
            int i0 = Mathf.FloorToInt(exactIndex) % table.Length;
            int i1 = (i0 + 1) % table.Length;
            float frac = exactIndex - Mathf.Floor(exactIndex);

            return Mathf.Lerp(table[i0], table[i1], frac);
        }

        float GetSample(float ph)
        {
            switch (p.waveform)
            {
                case WaveformType.Square: return SquareWave(ph);
                case WaveformType.Triangle: return TriangleWave(ph);
                case WaveformType.Sawtooth: return SawtoothWave(ph);
                case WaveformType.Additive: return AdditiveWave(ph);
                case WaveformType.Wavetable: return ReadWavetable(ph);
                default: return SineWave(ph);
            }
        }

        float CurrentFrequency(float dt)
        {
            float glide = p.glideTime > 0f ? Mathf.Clamp01(noteTime / p.glideTime) : 1f;
            float semitones = p.glideSemitones * glide;

            if (p.vibratoRate > 0f && p.vibratoSemitones > 0f)
            {
                semitones += p.vibratoSemitones * SineWave((float)vibratoPhase);
                vibratoPhase += p.vibratoRate * dt;
                vibratoPhase -= System.Math.Floor(vibratoPhase);
            }

            return p.frequency * Mathf.Pow(2f, semitones / 12f);
        }

        float Tremolo(float dt)
        {
            if (p.tremoloRate <= 0f || p.tremoloDepth <= 0f) return 1f;

            float lfo = 0.5f + 0.5f * SineWave((float)tremoloPhase);
            tremoloPhase += p.tremoloRate * dt;
            tremoloPhase -= System.Math.Floor(tremoloPhase);
            return 1f - p.tremoloDepth * lfo;
        }

        float Noise()
        {
            noiseState ^= noiseState << 13;
            noiseState ^= noiseState >> 17;
            noiseState ^= noiseState << 5;
            return (noiseState / (float)uint.MaxValue) * 2f - 1f;
        }

        float NextSample(float dt)
        {
            float freq = CurrentFrequency(dt);

            float mod = 0f;
            if (p.fmIndex > 0f)
            {
                mod = SineWave((float)modPhase) * p.fmIndex * freq;
                modPhase += freq * p.fmRatio * dt;
                modPhase -= System.Math.Floor(modPhase);
            }

            float s = GetSample((float)phase);

            phase += (freq + mod) * dt;
            phase -= System.Math.Floor(phase);

            if (p.noiseMix > 0f)
            {
                s = Mathf.Lerp(s, Noise(), p.noiseMix);
            }

            filterState += filterCoef * (s - filterState);
            return filterState;
        }

        void OnAudioFilterRead(float[] data, int channels)
        {
            if (hasPending)
            {
                StartPendingNote();
            }

            if (noteOffRequested)
            {
                noteOffRequested = false;
                if (stage != AdsrStage.Idle && stage != AdsrStage.Release)
                {
                    releaseStartValue = envelopeValue;
                    stage = AdsrStage.Release;
                }
            }

            if (stage == AdsrStage.Idle)
            {
                return;
            }

            float dt = 1f / sampleRate;

            for (int i = 0; i < data.Length; i += channels)
            {
                float sample = 0f;

                if (delaySamples > 0)
                {
                    delaySamples--;
                }
                else if (stage != AdsrStage.Idle)
                {
                    float env = EvaluateAdsr(dt);
                    sample = p.amplitude * env * Tremolo(dt) * NextSample(dt);
                    noteTime += dt;
                }

                sample = Mathf.Clamp(sample, -1f, 1f);

                for (int ch = 0; ch < channels; ch++)
                {
                    data[i + ch] = sample;
                }
            }
        }
    }
}