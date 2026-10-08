using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// Recorre una lista de NotaProgramada y va llamando NoteOn/NoteOff del
/// oscilador en el momento correcto, segun el BPM.

public class MelodySequencer : MonoBehaviour
{
    [Header("Oscilador que va a reproducir la melodia")]
    public SimpleAdditiveOscillator oscillator;

    [Header("Tempo de la melodia (negras por minuto)")]
    public float bpm = 100f;

    private Coroutine currentPlayback;

    float SegundosPorBeat()
    {
        return 60f / bpm;
    }

    /// Punto de entrada publico: reproduce la lista de notas dada.
    /// Si ya habia una melodia sonando, la corta primero.

    public void PlayMelody(List<NotaProgramada> notas)
    {
        StopMelody();
        currentPlayback = StartCoroutine(ReproducirMelodia(notas));
    }

    public void StopMelody()
    {
        if (currentPlayback != null)
        {
            StopCoroutine(currentPlayback);
            currentPlayback = null;
        }
        oscillator.NoteOff();
    }

    /// Recorre la lista en orden
    /// Los SILENCIOS se representan con nombre = "-" 
 
    IEnumerator ReproducirMelodia(List<NotaProgramada> notas)
    {
        float ultimoBeat = 0f;

        foreach (var nota in notas)
        {
            float espera = Mathf.Max(0f, nota.inicioBeats - ultimoBeat) * SegundosPorBeat();
            if (espera > 0f)
                yield return new WaitForSeconds(espera);

            bool esSilencio = string.IsNullOrEmpty(nota.nombre) || nota.nombre == "-";

            if (!esSilencio)
            {
                oscillator.frequency = MusicUtils.NoteToFrequency(nota.nombre, nota.octava);
                oscillator.NoteOn();
            }

            float duracionSegundos = nota.duracionBeats * SegundosPorBeat();
            yield return new WaitForSeconds(duracionSegundos);

            if (!esSilencio)
            {
                oscillator.NoteOff();
            }

            ultimoBeat = nota.inicioBeats + nota.duracionBeats;
        }

        currentPlayback = null;
    }

    [ContextMenu("DEBUG: Tocar escala de Do mayor")]
    void DebugPlayScale()
    {
        List<NotaProgramada> escala = new List<NotaProgramada>
        {
            new NotaProgramada { nombre = "C", octava = 4, inicioBeats = 0f, duracionBeats = 1f },
            new NotaProgramada { nombre = "D", octava = 4, inicioBeats = 1f, duracionBeats = 1f },
            new NotaProgramada { nombre = "E", octava = 4, inicioBeats = 2f, duracionBeats = 1f },
            new NotaProgramada { nombre = "F", octava = 4, inicioBeats = 3f, duracionBeats = 1f },
            new NotaProgramada { nombre = "G", octava = 4, inicioBeats = 4f, duracionBeats = 1f },
            new NotaProgramada { nombre = "A", octava = 4, inicioBeats = 5f, duracionBeats = 1f },
            new NotaProgramada { nombre = "B", octava = 4, inicioBeats = 6f, duracionBeats = 1f },
            new NotaProgramada { nombre = "C", octava = 5, inicioBeats = 7f, duracionBeats = 1f },
        };

        PlayMelody(escala);
    }
}