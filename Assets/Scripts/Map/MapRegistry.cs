using UnityEngine;

[CreateAssetMenu(fileName = "MapRegistry", menuName = "AlbaMod/Map Registry")]
public class MapRegistry : ScriptableObject {
    public MapDefinition[] maps;
}