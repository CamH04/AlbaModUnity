using UnityEngine;
using TMPro;
using System.Collections.Generic;

public class RandomMenuText : MonoBehaviour {
    [SerializeField] private TMP_Text text;

    [SerializeField]
    private List<string> messages = new List<string>
    {
        "1",
        "2",
        "3"
    };

    [Header("Bobbing")]
    [SerializeField] private float bobAmount = 5f;
    [SerializeField] private float bobSpeed = 2f;

    private RectTransform rectTransform;
    private Vector2 startPosition;

    private void Start() {
        rectTransform = text.GetComponent<RectTransform>();
        startPosition = rectTransform.anchoredPosition;
        if (messages.Count > 0) {
            int randomIndex = Random.Range(0, messages.Count);
            text.text = messages[randomIndex];
        }
    }

    private void Update() {
        float offset = Mathf.Sin(Time.time * bobSpeed) * bobAmount;

        rectTransform.anchoredPosition = startPosition + Vector2.up * offset;
    }
}