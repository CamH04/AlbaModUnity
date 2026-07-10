using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;

public class RelayManager : MonoBehaviour {
    public static RelayManager Instance;

    void Awake() {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    // Host calls this — returns a join code to share
    public async Task<string> CreateRelay() {
        try {
            // Max 2 connections (1 host + 1 client)
            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(1);

            string joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            transport.SetHostRelayData(
                allocation.RelayServer.IpV4,
                (ushort)allocation.RelayServer.Port,
                allocation.AllocationIdBytes,
                allocation.Key,
                allocation.ConnectionData
            );

            Debug.Log($"Relay created. Join code: {joinCode}");
            return joinCode;
        }
        catch (RelayServiceException e) {
            Debug.LogError($"Relay creation failed: {e}");
            return null;
        }
    }

    // Client calls this with the code from host
    public async Task JoinRelay(string joinCode) {
        try {
            JoinAllocation allocation = await RelayService.Instance.JoinAllocationAsync(joinCode);

            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            transport.SetClientRelayData(
                allocation.RelayServer.IpV4,
                (ushort)allocation.RelayServer.Port,
                allocation.AllocationIdBytes,
                allocation.Key,
                allocation.ConnectionData,
                allocation.HostConnectionData
            );

            Debug.Log($"Joined relay with code: {joinCode}");
        }
        catch (RelayServiceException e) {
            Debug.LogError($"Relay join failed: {e}");
        }
    }
}