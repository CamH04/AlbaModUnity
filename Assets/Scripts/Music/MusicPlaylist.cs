using UnityEngine;

[CreateAssetMenu(fileName = "MusicPlaylist", menuName = "AlbaMod/Music Playlist")]
public class MusicPlaylist : ScriptableObject {
    public MusicTrack[] tracks;
}