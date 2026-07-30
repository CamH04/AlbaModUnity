using UnityEngine;

[CreateAssetMenu(fileName = "CharacterRegistry", menuName = "Game/Character Registry")]
public class CharacterRegistry : ScriptableObject {
    public CharacterDefinition[] characters;
}