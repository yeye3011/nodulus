using UnityEngine;
using System.Globalization;

/// <summary>
/// WavetableBank
/// --------------
/// Responsable UNICAMENTE de tener los datos de las wavetables listos para
/// leer: cargarlas desde archivo (.txt asignado como TextAsset), parsearlas,
/// generarlas matematicamente como alternativa, y remuestrearlas a otra
/// resolucion.
///
/// A proposito NO contiene logica de sintesis en tiempo real (fase,
/// interpolacion por muestra, ADSR, etc.). Esa logica vive en
/// SimpleAdditiveOscillator para mantener la separacion oscilador / datos
/// que pide la guia y que ya vienes respetando en los trabajos anteriores.
/// </summary>
public class WavetableBank : MonoBehaviour
{
    public enum WavetableName
    {
        Sine,
        Square,
        Triangle,
        Sawtooth,
        Custom1,
        Custom2,
        Custom3
    }

    // Formas basicas que se pueden generar por formula (Paso "generacion calculada")
    public enum WaveShape { Sine, Square, Sawtooth, Triangle }

    [Header("Arrastra aqui los wavetable_*.txt (como TextAsset)")]
    public TextAsset sineTxt;
    public TextAsset squareTxt;
    public TextAsset triangleTxt;
    public TextAsset sawtoothTxt;
    public TextAsset custom1Txt;
    public TextAsset custom2Txt;
    public TextAsset custom3Txt;

    // Tablas ya parseadas, listas para lectura en tiempo real
    private float[] sineTable;
    private float[] squareTable;
    private float[] triangleTable;
    private float[] sawtoothTable;
    private float[] custom1Table;
    private float[] custom2Table;
    private float[] custom3Table;

    void Awake()
    {
        sineTable = ParseTxt(sineTxt);
        squareTable = ParseTxt(squareTxt);
        triangleTable = ParseTxt(triangleTxt);
        sawtoothTable = ParseTxt(sawtoothTxt);
        custom1Table = ParseTxt(custom1Txt);
        custom2Table = ParseTxt(custom2Txt);
        custom3Table = ParseTxt(custom3Txt);
    }

    // ---------- CARGA DESDE ARCHIVO + PARSEO ----------

    /// <summary>
    /// Convierte el texto "0.0,0.1,0.2,..." en un arreglo de floats.
    /// Mismo parseo que ya usabas en WavetableTxtLoader, centralizado aqui.
    /// </summary>
    private float[] ParseTxt(TextAsset asset)
    {
        if (asset == null)
        {
            Debug.LogWarning("WavetableBank: falta asignar un TextAsset en el Inspector.");
            return new float[] { 0f };
        }

        string cleaned = asset.text.Replace("\n", "").Replace("\r", "").Trim();
        string[] parts = cleaned.Split(',');

        float[] result = new float[parts.Length];
        int count = 0;

        for (int i = 0; i < parts.Length; i++)
        {
            string token = parts[i].Trim();
            if (string.IsNullOrEmpty(token)) continue;

            if (float.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out float value))
            {
                result[count] = Mathf.Clamp(value, -1f, 1f);
                count++;
            }
            else
            {
                Debug.LogWarning("WavetableBank: token invalido en indice " + i + ": " + token);
            }
        }

        if (count != result.Length)
        {
            float[] resized = new float[count];
            System.Array.Copy(result, resized, count);
            result = resized;
        }

        return result;
    }

    public float[] GetTable(WavetableName name)
    {
        switch (name)
        {
            case WavetableName.Sine: return sineTable;
            case WavetableName.Square: return squareTable;
            case WavetableName.Triangle: return triangleTable;
            case WavetableName.Sawtooth: return sawtoothTable;
            case WavetableName.Custom1: return custom1Table;
            case WavetableName.Custom2: return custom2Table;
            case WavetableName.Custom3: return custom3Table;
            default: return sineTable;
        }
    }

    // ---------- GENERACION CALCULADA (alternativa a cargar desde archivo) ----------

    /// <summary>
    /// Genera una wavetable por formula matematica, del tamano que pidas.
    /// Sirve para comparar contra la version cargada desde .txt (deberian
    /// sonar identicas si el archivo fue generado con la misma formula) y
    /// para justificar en el video la diferencia entre "calcular en vivo"
    /// y "leer una tabla precalculada".
    /// </summary>
    public float[] GenerateTable(WaveShape shape, int size)
    {
        float[] t = new float[size];

        for (int i = 0; i < size; i++)
        {
            float phase = (float)i / size; // 0..1, un periodo completo

            switch (shape)
            {
                case WaveShape.Sine:
                    t[i] = Mathf.Sin(2f * Mathf.PI * phase);
                    break;

                case WaveShape.Square:
                    t[i] = phase < 0.5f ? 1f : -1f;
                    break;

                case WaveShape.Sawtooth:
                    t[i] = 2f * phase - 1f;
                    break;

                case WaveShape.Triangle:
                    t[i] = 1f - 4f * Mathf.Abs(phase - 0.5f);
                    break;
            }
        }

        return t;
    }

    // ---------- CONTROL DE RESOLUCION (remuestreo a otro tamano) ----------

    /// <summary>
    /// Genera una version de "original" con "newSize" muestras, interpolando
    /// linealmente entre las muestras vecinas de la tabla original.
    /// Bajar newSize (ej. 1024 -> 32) deja oir el efecto de menor resolucion
    /// (mas "escalones" / mas aliasing en armonicos altos).
    /// </summary>
    public float[] Resample(float[] original, int newSize)
    {
        if (original == null || original.Length == 0 || newSize <= 0)
            return original;

        float[] result = new float[newSize];

        for (int i = 0; i < newSize; i++)
        {
            float srcPos = (float)i / newSize * original.Length;
            int i0 = Mathf.FloorToInt(srcPos) % original.Length;
            int i1 = (i0 + 1) % original.Length;
            float frac = srcPos - Mathf.Floor(srcPos);

            result[i] = Mathf.Lerp(original[i0], original[i1], frac);
        }

        return result;
    }

    /// <summary>
    /// DIEZMADO (seccion 7 de la guia): toma 1 de cada "factor" muestras, SIN
    /// interpolar entre ellas. A diferencia de Resample() (que interpola para
    /// llegar exactamente al tamano pedido), aqui el tamano final depende del
    /// factor usado y el resultado puede sonar mas "escalonado", tal como
    /// describe la guia.
    /// </summary>
    public float[] Decimate(float[] source, int factor)
    {
        if (source == null || source.Length == 0 || factor <= 0)
            return source;

        int newSize = source.Length / factor;
        if (newSize <= 0) newSize = 1;

        float[] reduced = new float[newSize];
        for (int i = 0; i < newSize; i++)
            reduced[i] = source[i * factor];

        return reduced;
    }
}