using Unity.Netcode;
using UnityEngine;

public class KillFeedManager : NetworkBehaviour {
    public static KillFeedManager Instance;

    void Awake() {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    // Called by PlayerHealth when a player dies
    public void ReportKill(ulong killerClientId, ulong victimClientId, string weaponName) {
        if (!IsServer) return;

        string killerName = GetPlayerName(killerClientId);
        string victimName = GetPlayerName(victimClientId);

        BroadcastKillClientRpc(killerName, victimName, weaponName,
            killerClientId, victimClientId);
    }

    string GetPlayerName(ulong clientId) {
        // For now uses Client ID as name — hook into a player name system later
        return clientId == 0 ? "Host" : $"Player {clientId}";
    }

    [ClientRpc]
    void BroadcastKillClientRpc(string killerName, string victimName,
    string weaponName, ulong killerClientId, ulong victimClientId) {
        KillFeedUI.Instance?.AddEntry(new KillFeedEntry {
            killerName = killerName,
            victimName = victimName,
            weaponName = weaponName,
            killerClientId = killerClientId,
            victimClientId = victimClientId
        });
        if (killerClientId == NetworkManager.Singleton.LocalClientId
            && killerClientId != victimClientId)
        {
            LocalStatsTracker.Instance?.RegisterKill(weaponName);
        }
    }
}