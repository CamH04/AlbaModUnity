using UnityEngine;

[CreateAssetMenu(fileName = "NewMap", menuName = "AlbaMod/Map Definition")]
public class MapDefinition : ScriptableObject {
    public string mapName;
    public Sprite mapPreviewImage;
    public string mapDescription;
    public GameObject mapPrefab;
}