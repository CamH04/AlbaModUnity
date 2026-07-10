using UnityEngine;

public class DestroyTracker : MonoBehaviour {
    void OnDestroy() {
        if (!gameObject.scene.isLoaded) return; // ignore scene unload
        Debug.LogError($"PLAYER DESTROYED — Stack trace:\n{System.Environment.StackTrace}");
    }
}