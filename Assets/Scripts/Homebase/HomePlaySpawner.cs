
using UnityEngine;

public class HomePlaySpawner : MonoBehaviour {
    public GameObject playerPrefab;
    public Transform spawnPoint;

    private void Start() {
        if (playerPrefab != null && spawnPoint != null) {
            Instantiate(
                playerPrefab,
                spawnPoint.position,
                spawnPoint.rotation
            );
        }
    }
}
