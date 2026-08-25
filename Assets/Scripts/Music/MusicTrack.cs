using UnityEngine;

[CreateAssetMenu(fileName = "NewTrack", menuName = "AlbaMod/Music Track")]
public class MusicTrack : ScriptableObject {
    public string trackName;
    public string artistName;
    public AudioClip clip;
}