using UnityEngine;

public class HomeBaseMusicController : MonoBehaviour {
    private void Start() {
        StopGlobalMusic();
    }

    private void StopGlobalMusic() {
        if (MusicPlayer.Instance != null) {
            MusicPlayer.Instance.StopPlayback();
        }
    }
}