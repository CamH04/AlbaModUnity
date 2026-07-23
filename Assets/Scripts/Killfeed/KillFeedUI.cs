using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;

public class KillFeedUI : MonoBehaviour {
    public static KillFeedUI Instance;

    [Header("References")]
    public Transform entryContainer;
    public GameObject entryPrefab;

    [Header("Settings")]
    public int maxEntries = 5;
    public float entryLifetime = 5f;
    public float fadeTime = 0.5f;

    [Header("Colors")]
    public Color localPlayerColor = new Color(0.3f, 0.8f, 1f);   // blue for you
    public Color enemyColor = new Color(1f, 0.3f, 0.3f); // red for enemies
    public Color defaultColor = Color.white;
    public Color weaponColor = new Color(0.9f, 0.75f, 0.2f); // gold for weapon

    private Queue<GameObject> _activeEntries = new Queue<GameObject>();

    void Awake() {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public void AddEntry(KillFeedEntry entry) {
        // Remove oldest if at max
        if (_activeEntries.Count >= maxEntries) {
            var oldest = _activeEntries.Dequeue();
            if (oldest != null) Destroy(oldest);
        }

        var entryObj = Instantiate(entryPrefab, entryContainer);
        _activeEntries.Enqueue(entryObj);

        // Get UI references from the prefab
        var killer = entryObj.transform.Find("KillerName")?.GetComponent<TextMeshProUGUI>();
        var weapon = entryObj.transform.Find("WeaponName")?.GetComponent<TextMeshProUGUI>();
        var victim = entryObj.transform.Find("VictimName")?.GetComponent<TextMeshProUGUI>();
        var weaponIcon = entryObj.transform.Find("WeaponIcon")?.GetComponent<Image>();

        ulong localId = NetworkManager.Singleton.LocalClientId;

        if (killer != null) {
            killer.text = entry.killerName;
            killer.color = entry.killerClientId == localId ? localPlayerColor : enemyColor;
        }

        if (victim != null) {
            victim.text = entry.victimName;
            victim.color = entry.victimClientId == localId ? localPlayerColor : enemyColor;
        }

        if (weapon != null) {
            weapon.text = entry.weaponName;
            weapon.color = weaponColor;
        }

        StartCoroutine(FadeAndRemove(entryObj, entryLifetime, fadeTime));
    }

    IEnumerator FadeAndRemove(GameObject entryObj, float lifetime, float fadeTime) {
        yield return new WaitForSeconds(lifetime);

        if (entryObj == null) yield break;

        // Fade out all text and images
        var texts = entryObj.GetComponentsInChildren<TextMeshProUGUI>();
        var images = entryObj.GetComponentsInChildren<Image>();

        float elapsed = 0f;
        while (elapsed < fadeTime) {
            elapsed += Time.deltaTime;
            float alpha = 1f - Mathf.Clamp01(elapsed / fadeTime);

            foreach (var t in texts) {
                var c = t.color;
                t.color = new Color(c.r, c.g, c.b, alpha);
            }
            foreach (var img in images) {
                var c = img.color;
                img.color = new Color(c.r, c.g, c.b, alpha);
            }

            yield return null;
        }

        if (_activeEntries.Count > 0 && _activeEntries.Peek() == entryObj)
            _activeEntries.Dequeue();

        Destroy(entryObj);
    }
}