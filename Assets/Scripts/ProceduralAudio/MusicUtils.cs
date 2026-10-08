using UnityEngine;

/// Conversion de nota musical (nombre + octava) a frecuencia en Hz.

public static class MusicUtils 
{ 

    /// Convierte una nota (ej. "C", "C#", "D") y una octava (ej. 4) a su
    /// frecuencia en Hz. Usa la misma tabla de referencia que ya usaba
    /// AdditiveKeyboardController.GetFrequencyFromOctave0.

    public static float NoteToFrequency(string note, int octave)
    {
        if (note == "C") return 16.3516f * Mathf.Pow(2f, octave);
        if (note == "C#") return 17.3239f * Mathf.Pow(2f, octave);
        if (note == "D") return 18.3540f * Mathf.Pow(2f, octave);
        if (note == "D#") return 19.4454f * Mathf.Pow(2f, octave);
        if (note == "E") return 20.6017f * Mathf.Pow(2f, octave);
        if (note == "F") return 21.8268f * Mathf.Pow(2f, octave);
        if (note == "F#") return 23.1246f * Mathf.Pow(2f, octave);
        if (note == "G") return 24.4997f * Mathf.Pow(2f, octave);
        if (note == "G#") return 25.9565f * Mathf.Pow(2f, octave);
        if (note == "A") return 27.5000f * Mathf.Pow(2f, octave);
        if (note == "A#") return 29.1353f * Mathf.Pow(2f, octave);

        return 30.8677f * Mathf.Pow(2f, octave); // B
    }
}