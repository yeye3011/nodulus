using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class SimpleAdditiveOscillator : MonoBehaviour
{

    public WaveformType waveform = WaveformType.Sine;

    [Range(0f, 1f)]
    public float amplitude = 0.2f;

    public float frequency = 440f;

    public float sampleRate = 44100f;

    [Header("Additive Synthesis")]

    public AdditiveMode additiveMode;

    [Range(1, 10)]
    public int harmonicCount = 10;

    public float[] harmonicLevels = new float[10]
    {
        1f,
        0.5f,
        0.33f,
        0.25f,
        0.20f,
        0.16f,
        0.14f,
        0.125f,
        0.11f,
        0.10f
    };

    // ==================== NUEVO: WAVETABLE ====================

    [Header("Wavetable")]
    public WavetableBank wavetableBank;
    public WavetableBank.WavetableName selectedWavetable = WavetableBank.WavetableName.Sine;

    [Tooltip("Cantidad de muestras a las que se remuestrea la tabla activa antes de reproducirla. Bajarlo deja oir el efecto de menor resolucion (mas aliasing).")]
    public int tableResolutionOverride = 1024;

    [Tooltip("Si esta activo, la resolucion se reduce por DIEZMADO (toma 1 de cada N muestras, sin interpolar, tal como lo describe la seccion 7 de la guia). Si esta apagado, se usa remuestreo con interpolacion lineal (mas suave).")]
    public bool useDecimation = false;

    private float[] activeTable;
    private WavetableBank.WavetableName lastLoadedTable;
    private int lastResolution = -1;
    private bool lastUseDecimation = false;

    // Fase acumulada 0..1 para lectura de wavetable. Es independiente de
    // "timeIndex" porque aqui necesitamos un puntero fraccionario dentro de
    // la tabla, no un contador de muestras absoluto.
    private double wavetablePhase = 0.0;

    // ============================================================

    void Awake()
    {
        if (harmonicLevels == null || harmonicLevels.Length != 10)
        {
            float[] fixedArray = new float[10];
            for (int i = 0; i < fixedArray.Length; i++)
            {
                fixedArray[i] = (harmonicLevels != null && i < harmonicLevels.Length)
                    ? harmonicLevels[i]
                    : 0f;
            }
            harmonicLevels = fixedArray;
        }
    }

    [Header("ADSR (tiempos en segundos)")]
    public float attack = 0.01f;

    public float decay = 0.15f;

    [Range(0f, 1f)]
    public float sustainLevel = 0.70f;

    // NUEVO: duraci�n de la fase sostenida (equivale a "Sustain" / "S" de la gu�a y de PianoProcedural)
    public float sustainDuration = 0.5f;

    public float release = 0.25f;

    enum AdsrStage { Idle, Attack, Decay, Sustain, Release }

    AdsrStage stage = AdsrStage.Idle;
    float envelopeValue = 0f;
    float releaseStartValue = 0f;

    // NUEVO: cron�metro interno para saber cu�nto tiempo llevamos en Sustain
    float sustainTimer = 0f;

    int timeIndex = 0;

    public enum WaveformType
    {
        Sine,
        Square,
        Triangle,
        Sawtooth,
        Additive,
        Wavetable // NUEVO
    }

    public enum AdditiveMode
    {
        Generic,
        SquareFourier,
        TriangleFourier,
        SawtoothFourier
    }

    public void NoteOn()
    {
        stage = AdsrStage.Attack;
        sustainTimer = 0f;
    }

    // Soltar la tecla ANTES de que termine el sustain_ms programado
    // sigue disparando el release de inmediato (comportamiento manual, secci�n 8.4 de la gu�a)
    public void NoteOff()
    {
        if (stage != AdsrStage.Idle && stage != AdsrStage.Release)
        {
            releaseStartValue = envelopeValue;
            stage = AdsrStage.Release;
        }
    }

    public void SetHarmonicLevel(int harmonicNumber1Based, float value)
    {
        int idx = harmonicNumber1Based - 1;
        if (idx >= 0 && idx < harmonicLevels.Length)
        {
            harmonicLevels[idx] = Mathf.Clamp01(value);
        }
    }

    float EvaluateAdsr(float dt)
    {
        switch (stage)
        {
            case AdsrStage.Attack:
                envelopeValue += dt / Mathf.Max(attack, 0.0001f);
                if (envelopeValue >= 1f)
                {
                    envelopeValue = 1f;
                    stage = AdsrStage.Decay;
                }
                break;

            case AdsrStage.Decay:
                envelopeValue -= dt * (1f - sustainLevel) / Mathf.Max(decay, 0.0001f);
                if (envelopeValue <= sustainLevel)
                {
                    envelopeValue = sustainLevel;
                    stage = AdsrStage.Sustain;
                    sustainTimer = 0f;
                }
                break;

            case AdsrStage.Sustain:
                // El volumen se mantiene fijo en sustainLevel durante sustainDuration segundos.
                // Si ese tiempo se cumple, pasa a Release autom�ticamente (nota se apaga sola,
                // igual que el "sustain_ms" medido de tus audios reales).
                sustainTimer += dt;
                if (sustainTimer >= sustainDuration)
                {
                    releaseStartValue = envelopeValue;
                    stage = AdsrStage.Release;
                }
                break;

            case AdsrStage.Release:
                envelopeValue -= dt * releaseStartValue / Mathf.Max(release, 0.0001f);
                if (envelopeValue <= 0f)
                {
                    envelopeValue = 0f;
                    stage = AdsrStage.Idle;
                }
                break;
        }

        return envelopeValue;
    }

    float SineWave(float f, int n)
    {
        return Mathf.Sin(2f * Mathf.PI * f * n / sampleRate);
    }

    float SquareWave(float f, int n)
    {
        return Mathf.Sign(SineWave(f, n));
    }

    float TriangleWave(float f, int n)
    {
        float period = sampleRate / f;

        float phase =
            (n % period) / period;

        return 1f -
               4f * Mathf.Abs(phase - 0.5f);
    }

    float SawtoothWave(float f, int n)
    {
        float period = sampleRate / f;

        float phase =
            (n % period) / period;

        return 1f - 2f * phase;
    }

    float AdditiveGeneric(float f, int n)
    {
        float sum = 0f;
        float totalLevel = 0f;

        for (int harmonic = 1; harmonic <= harmonicCount; harmonic++)
        {
            float level = harmonicLevels[harmonic - 1];

            sum += level * SineWave(harmonic * f, n);
            totalLevel += level;
        }

        return totalLevel > 0f ? sum / totalLevel : 0f;
    }

    float AdditiveSquareFourier(float f, int n)
    {
        float sum = 0f;

        for (int i = 0; i < harmonicCount; i++)
        {
            int k = 2 * i + 1;
            sum += (1f / k) * SineWave(k * f, n);
        }

        return sum;
    }

    float AdditiveTriangleFourier(float f, int n)
    {
        float sum = 0f;

        for (int i = 0; i < harmonicCount; i++)
        {
            int k = 2 * i + 1;
            float sign = (i % 2 == 0) ? 1f : -1f;

            sum += sign * (1f / (k * k)) * SineWave(k * f, n);
        }

        return sum;
    }

    float AdditiveSawtoothFourier(float f, int n)
    {
        float sum = 0f;

        for (int k = 1; k <= harmonicCount; k++)
        {
            float sign = (k % 2 == 0) ? -1f : 1f;

            sum += sign * (1f / k) * SineWave(k * f, n);
        }

        return sum;
    }

    float AdditiveWave(float f, int n)
    {
        switch (additiveMode)
        {
            case AdditiveMode.SquareFourier:
                return AdditiveSquareFourier(f, n);

            case AdditiveMode.TriangleFourier:
                return AdditiveTriangleFourier(f, n);

            case AdditiveMode.SawtoothFourier:
                return AdditiveSawtoothFourier(f, n);

            default:
                return AdditiveGeneric(f, n);
        }
    }

    // ==================== NUEVO: DEMOSTRACION DE GENERACION CALCULADA ====================

    /// <summary>
    /// Reemplaza la tabla activa por una calculada matematicamente en el momento
    /// (en vez de leida del .txt). Sirve para comparar por oido que ambas
    /// suenan igual. Se ejecuta desde el menu de los 3 puntitos (⋮) del
    /// componente en el Inspector, mientras estas en Play.
    /// </summary>
    [ContextMenu("DEBUG: Usar tabla GENERADA (no leida del archivo)")]
    void DebugUsarTablaGenerada()
    {
        if (wavetableBank == null)
        {
            Debug.LogWarning("Asigna primero el Wavetable Bank.");
            return;
        }

        WavetableBank.WaveShape shape;
        switch (selectedWavetable)
        {
            case WavetableBank.WavetableName.Square: shape = WavetableBank.WaveShape.Square; break;
            case WavetableBank.WavetableName.Triangle: shape = WavetableBank.WaveShape.Triangle; break;
            case WavetableBank.WavetableName.Sawtooth: shape = WavetableBank.WaveShape.Sawtooth; break;
            default: shape = WavetableBank.WaveShape.Sine; break;
        }

        activeTable = wavetableBank.GenerateTable(shape, Mathf.Max(4, tableResolutionOverride));

        Debug.Log("Ahora estas escuchando una tabla CALCULADA en el momento (formula), " +
                  "no la leida del archivo .txt. Toca una nota y compara con la version normal.");
    }

    // ==================== NUEVO: LECTURA DE WAVETABLE ====================

    /// <summary>
    /// Se asegura de que "activeTable" corresponda a la tabla seleccionada
    /// y a la resolucion pedida. Solo vuelve a remuestrear cuando algo
    /// cambio, para no hacerlo en cada muestra de audio.
    /// </summary>
    void EnsureTableLoaded()
    {
        if (wavetableBank == null) return;

        bool tableChanged = activeTable == null
            || selectedWavetable != lastLoadedTable
            || tableResolutionOverride != lastResolution
            || useDecimation != lastUseDecimation;

        if (!tableChanged) return;

        float[] baseTable = wavetableBank.GetTable(selectedWavetable);
        int targetResolution = Mathf.Max(4, tableResolutionOverride);

        if (useDecimation)
        {
            // Diezmado: se calcula el factor necesario para acercarse a la
            // resolucion pedida, y se descartan muestras sin interpolar.
            int factor = Mathf.Max(1, baseTable.Length / targetResolution);
            activeTable = wavetableBank.Decimate(baseTable, factor);
        }
        else
        {
            // Remuestreo con interpolacion lineal (mas suave).
            activeTable = wavetableBank.Resample(baseTable, targetResolution);
        }

        lastLoadedTable = selectedWavetable;
        lastResolution = tableResolutionOverride;
        lastUseDecimation = useDecimation;
    }

    /// <summary>
    /// Lee "activeTable" en una posicion de fase 0..1 con interpolacion
    /// lineal entre las dos muestras vecinas. Esta interpolacion ES el
    /// remuestreo: permite leer la misma tabla de N muestras a cualquier
    /// frecuencia sin que suene "cuantizada".
    /// </summary>
    float ReadWavetable(float phase01)
    {
        if (activeTable == null || activeTable.Length == 0) return 0f;

        float exactIndex = phase01 * activeTable.Length;
        int i0 = Mathf.FloorToInt(exactIndex) % activeTable.Length;
        int i1 = (i0 + 1) % activeTable.Length;
        float frac = exactIndex - Mathf.Floor(exactIndex);

        return Mathf.Lerp(activeTable[i0], activeTable[i1], frac);
    }

    /// <summary>
    /// Avanza el puntero de fase segun la frecuencia pedida y devuelve la
    /// muestra correspondiente. dt = 1/sampleRate.
    /// </summary>
    float WavetableWave(float f, float dt)
    {
        EnsureTableLoaded();

        float sample = ReadWavetable((float)wavetablePhase);

        wavetablePhase += f * dt;
        if (wavetablePhase >= 1.0) wavetablePhase -= 1.0;
        if (wavetablePhase < 0.0) wavetablePhase += 1.0;

        return sample;
    }

    // ========================================================================

    float GetSample(int n, float dt)
    {
        switch (waveform)
        {
            case WaveformType.Square:
                return SquareWave(frequency, n);

            case WaveformType.Triangle:
                return TriangleWave(frequency, n);

            case WaveformType.Sawtooth:
                return SawtoothWave(frequency, n);

            case WaveformType.Additive:
                return AdditiveWave(frequency, n);

            case WaveformType.Wavetable: // NUEVO
                return WavetableWave(frequency, dt);

            default:
                return SineWave(frequency, n);
        }
    }

    void OnAudioFilterRead(float[] data, int channels)
    {
        float dt = 1f / sampleRate;

        for (int i = 0; i < data.Length; i += channels)
        {
            float env = EvaluateAdsr(dt);
            float sample = amplitude * env * GetSample(timeIndex, dt);

            for (int ch = 0; ch < channels; ch++)
            {
                data[i + ch] = sample;
            }

            timeIndex++;
        }
    }
}