using UnityEngine;

namespace ProceduralAudio
{
    // Escalas disponibles para la musica generativa. Todas se construyen
    // sobre la tonica elegida, usando MusicUtils para la frecuencia base.
    public enum MusicScale
    {
        Mayor,
        Menor,
        PentatonicaMayor,
        PentatonicaMenor,
        Dorica,
        Lidia,
        TonosEnteros
    }

    // Un EXPERIMENTO de musica: define la estructura (tempo, escala, capas y
    // como responde al progreso). El timbre de cada capa NO se define aqui,
    // sino con un SoundExperiment, asi que el dron, el motivo y la textura se
    // diseñan con los mismos controles que los efectos (onda, aditiva,
    // wavetable, ADSR, FM, LFO, filtro).
    //
    // Igual que los efectos, se edita en vivo durante el Play: los cambios
    // se oyen a partir del siguiente compas y se conservan al salir de Play.
    [CreateAssetMenu(fileName = "NuevaMusica", menuName = "Audio Procedural/Experimento de musica")]
    public class MusicExperiment : ScriptableObject
    {
        [Header("Bitacora del experimento")]
        [TextArea(3, 8)]
        public string notas = "Idea:\nCambio probado:\nQue se percibe:";

        [Header("Tempo y tonalidad")]
        [Range(30f, 160f)]
        public float bpm = 66f;
        [Range(2, 8)]
        public int pulsosPorCompas = 4;
        [Tooltip("Nota tonica, como en tu teclado: C, C#, D, D#, E, F, F#, G, G#, A, A#, B")]
        public string tonica = "D";
        [Range(1, 5)]
        public int octavaBase = 2;
        public MusicScale escala = MusicScale.PentatonicaMenor;

        [Header("Capa 1: dron (pedal grave continuo)")]
        public bool dronActivo = true;
        public SoundExperiment timbreDron;
        [Tooltip("Cuantos compases dura cada nota del dron. Las notas se encadenan: el release de una se mezcla con el ataque de la siguiente.")]
        [Range(1, 8)]
        public int compasesPorNotaDron = 2;
        [Tooltip("Si esta activo, el dron alterna entre la tonica y la quinta.")]
        public bool dronAlternaQuinta = true;
        [Range(0f, 1f)]
        public float volumenDron = 0.6f;

        [Header("Capa 2: motivo (notas espaciadas en la escala)")]
        public bool motivoActivo = true;
        public SoundExperiment timbreMotivo;
        [Tooltip("Notas por compas con progreso cero.")]
        [Range(1, 8)]
        public int notasPorCompas = 2;
        [Tooltip("Probabilidad de que cada nota posible realmente suene. Menos = mas silencios.")]
        [Range(0f, 1f)]
        public float probabilidadNota = 0.6f;
        [Tooltip("Octavas por encima de la octava base.")]
        [Range(0, 4)]
        public int octavaMotivo = 2;
        [Tooltip("Cuantos grados de la escala puede recorrer el motivo.")]
        [Range(2, 14)]
        public int rangoGrados = 7;
        [Tooltip("Salto maximo entre una nota y la siguiente, en grados de la escala.")]
        [Range(1, 4)]
        public int saltoMaximo = 2;
        [Range(0f, 1f)]
        public float volumenMotivo = 0.5f;

        [Header("Capa 3: textura alta")]
        public bool texturaActiva = true;
        public SoundExperiment timbreTextura;
        [Range(1, 8)]
        public int compasesPorNotaTextura = 1;
        [Range(0, 5)]
        public int octavaTextura = 3;
        [Tooltip("La textura solo entra cuando la intensidad supera este valor (respuesta al progreso).")]
        [Range(0f, 1f)]
        public float intensidadParaTextura = 0.3f;
        [Range(0f, 1f)]
        public float volumenTextura = 0.3f;

        [Header("Respuesta al progreso")]
        [Tooltip("En que nivel la musica alcanza su intensidad maxima.")]
        [Range(1, 40)]
        public int nivelesParaMaximo = 12;
        [Tooltip("Notas extra por compas en el motivo cuando la intensidad es maxima.")]
        [Range(0, 6)]
        public int notasExtraMaximo = 3;
        [Tooltip("Cuanto se abre el filtro del motivo con intensidad maxima (multiplica la frecuencia de corte).")]
        [Range(1f, 8f)]
        public float aperturaFiltroMaxima = 3f;
        [Tooltip("Cuanta intensidad suma cada movimiento del jugador.")]
        [Range(0f, 0.5f)]
        public float actividadPorMovimiento = 0.08f;
        [Tooltip("Que tan rapido se pierde esa intensidad cuando el jugador no se mueve.")]
        [Range(0f, 1f)]
        public float caidaActividadPorSegundo = 0.1f;

        [Header("Cierre de nivel")]
        [Tooltip("Al ganar un nivel, el motivo toca una cadencia V - I y guarda un compas de silencio.")]
        public bool cadenciaActiva = true;

        // Intervalos de cada escala en semitonos desde la tonica.
        public int[] Intervalos()
        {
            switch (escala)
            {
                case MusicScale.Mayor: return new int[] { 0, 2, 4, 5, 7, 9, 11 };
                case MusicScale.Menor: return new int[] { 0, 2, 3, 5, 7, 8, 10 };
                case MusicScale.PentatonicaMayor: return new int[] { 0, 2, 4, 7, 9 };
                case MusicScale.Dorica: return new int[] { 0, 2, 3, 5, 7, 9, 10 };
                case MusicScale.Lidia: return new int[] { 0, 2, 4, 6, 7, 9, 11 };
                case MusicScale.TonosEnteros: return new int[] { 0, 2, 4, 6, 8, 10 };
                default: return new int[] { 0, 3, 5, 7, 10 }; // pentatonica menor
            }
        }
    }
}