using UnityEngine;

public class MapSpawner : MonoBehaviour {
    private GameObject _spawnedMap;

    void Start() {
        SpawnSelectedMap();
    }

    void SpawnSelectedMap() {
        // Clean up any existing map
        if (_spawnedMap != null)
            Destroy(_spawnedMap);

        if (MapSelection.Instance == null) {
            Debug.LogWarning("[AlbaMod] MapSelection instance not found — no map spawned");
            return;
        }

        var map = MapSelection.Instance.SelectedMap;
        if (map == null || map.mapPrefab == null) {
            Debug.LogWarning("[AlbaMod] No map selected or map prefab is null");
            return;
        }

        _spawnedMap = Instantiate(map.mapPrefab, transform.position, transform.rotation, transform);
        Debug.Log($"[AlbaMod] Spawned map: {map.mapName}");
    }
}