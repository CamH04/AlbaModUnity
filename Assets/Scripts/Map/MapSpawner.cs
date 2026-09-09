using UnityEngine;

public class MapSpawner : MonoBehaviour {
    private GameObject _spawnedMap;

    public static MapSpawner Instance;

    void Awake() {
        Instance = this;
    }

    void Start() {
        SpawnSelectedMap();
    }

    public void SpawnSelectedMap() {
        if (_spawnedMap != null)
            Destroy(_spawnedMap);

        if (MapSelection.Instance == null) {
            Debug.LogWarning("[AlbaMod] MapSelection instance not found");
            return;
        }

        var map = MapSelection.Instance.SelectedMap;
        if (map == null || map.mapPrefab == null) {
            Debug.LogWarning("[AlbaMod] No map selected or prefab is null");
            return;
        }

        _spawnedMap = Instantiate(
            map.mapPrefab,
            transform.position,
            transform.rotation,
            transform);

        Debug.Log($"[AlbaMod] Spawned map: {map.mapName}");
    }
}