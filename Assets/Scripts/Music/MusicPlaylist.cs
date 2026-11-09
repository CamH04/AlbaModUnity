using UnityEngine;

[CreateAssetMenu(fileName = "MusicPlaylist", menuName = "AlbaMod/Music Playlist")]
public class MusicPlaylist : ScriptableObject {
    [Tooltip("Standard songs shown in the main playlist.")]
    public MusicTrack[] tracks;

    [Tooltip("Exclusive songs shown only in the Lighthouse menu. " +
             "Selected songs from here share the same live queue as the standard playlist.")]
    public MusicTrack[] lighthouseTracks;
}
