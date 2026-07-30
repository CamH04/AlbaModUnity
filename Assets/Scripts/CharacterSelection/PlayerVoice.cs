using Unity.Netcode;
using UnityEngine;

public class PlayerVoice : NetworkBehaviour {
    public CharacterRegistry characterRegistry;

    private AudioSource _audioSource;
    private CharacterDefinition _character;
    private PlayerHealth _health;

    void Awake() {
        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null)
            _audioSource = gameObject.AddComponent<AudioSource>();
    }

    public override void OnNetworkSpawn() {
        _health = GetComponent<PlayerHealth>();

        // Find which character this player picked
        if (characterRegistry != null && PlayerCharacterSelection.Instance != null) {
            int index = PlayerCharacterSelection.Instance.GetCharacterIndex(OwnerClientId);
            if (index < characterRegistry.characters.Length)
                _character = characterRegistry.characters[index];
        }

        // Hook into death event
        if (_health != null)
            _health.OnDied += HandleDeath;
    }

    public override void OnNetworkDespawn() {
        if (_health != null)
            _health.OnDied -= HandleDeath;
    }

    void HandleDeath() {
        if (_character == null) return;

        // Only the dying player hears/plays their own voice line
        // but we want everyone to hear it — server picks and broadcasts
        if (IsOwner)
            RequestDeathLineServerRpc();
    }

    [ServerRpc]
    void RequestDeathLineServerRpc() {
        if (_character == null) return;

        bool playRare = Random.value < _character.rareLineChance
                     && _character.rareDeathLines.Length > 0;

        AudioClip[] pool = playRare
            ? _character.rareDeathLines
            : _character.commonDeathLines;

        if (pool.Length == 0) return;

        int clipIndex = Random.Range(0, pool.Length);
        bool isRareLine = playRare;

        PlayDeathLineClientRpc(clipIndex, isRareLine);
    }

    [ClientRpc]
    void PlayDeathLineClientRpc(int clipIndex, bool isRare) {
        if (_character == null) return;

        AudioClip[] pool = isRare
            ? _character.rareDeathLines
            : _character.commonDeathLines;

        if (clipIndex >= pool.Length) return;

        AudioClip clip = pool[clipIndex];
        if (clip == null) return;

        _audioSource.spatialBlend = 1f; // 3D audio so it comes from the player's position
        _audioSource.PlayOneShot(clip, _character.deathLineVolume);

        if (isRare)
            Debug.Log($"{_character.characterName} played rare death line: {clip.name}");
    }
}