using UnityEngine;

[CreateAssetMenu(fileName = "NewCharacter", menuName = "Game/Character Definition")]
public class CharacterDefinition : ScriptableObject {
    [Header("Identity")]
    public string characterName;
    public Sprite characterPortrait;
    public string characterDescription;

    [Header("Model")]
    public GameObject characterModelPrefab; // swapped onto the player at spawn

    [Header("Death Voice Lines")]
    public AudioClip[] commonDeathLines;    // picked from normally
    public AudioClip[] rareDeathLines;      // picked rarely

    [Range(0f, 1f)]
    public float rareLineChance = 0.15f;    // 15% chance of rare line

    [Range(0f, 1f)]
    public float deathLineVolume = 1f;
}