/// Representa una sola nota dentro de una melodia transcrita de una partitura.

[System.Serializable]
public struct NotaProgramada
{
    public string nombre;      
    public int octava;         
    public float inicioBeats;  // momento de inicio, medido en pulsos (no en segundos)
    public float duracionBeats; // negra = 1, blanca = 2, corchea = 0.5, etc.
    public float velocity;     // intensidad opcional entre 0 y 1 (puedes dejarlo en 1 si no la usas)
}