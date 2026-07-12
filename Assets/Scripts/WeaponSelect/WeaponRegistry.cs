using UnityEngine;

[CreateAssetMenu(fileName = "WeaponRegistry", menuName = "Game/Weapon Registry")]
public class WeaponRegistry : ScriptableObject {
    [System.Serializable]
    public class WeaponEntry {
        public string weaponName;
        public Sprite weaponIcon;
        public string weaponDescription;
        public GameObject weaponPrefab;
    }

    public WeaponEntry[] weapons;
}