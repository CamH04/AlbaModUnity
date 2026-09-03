using UnityEngine;

public class MapSelection : MonoBehaviour {
    public static MapSelection Instance;

    public MapRegistry mapRegistry;
    private int _selectedIndex = 0;

    public int SelectedIndex => _selectedIndex;
    public MapDefinition SelectedMap => mapRegistry != null
        && _selectedIndex < mapRegistry.maps.Length
        ? mapRegistry.maps[_selectedIndex] : null;

    void Awake() {
        if (Instance == null) {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else Destroy(gameObject);
    }

    public void SelectMap(int index) {
        if (mapRegistry == null || index >= mapRegistry.maps.Length) return;
        _selectedIndex = index;
        Debug.Log($"[AlbaMod] Map selected: {mapRegistry.maps[index].mapName}");
    }

    public void SelectRandom() {
        if (mapRegistry == null || mapRegistry.maps.Length == 0) return;
        SelectMap(Random.Range(0, mapRegistry.maps.Length));
    }
}