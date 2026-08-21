using System.Collections;
using TMPro;
using UnityEngine;

public class TerminalTyper : MonoBehaviour {
    [SerializeField] private TMP_Text terminalText;

    [TextArea(10, 100)]
    [SerializeField] private string consoleText;

    [Header("Typing")]
    [SerializeField] private float charactersPerSecond = 100f;
    [SerializeField] private float randomDelay = 0.015f;
    [SerializeField] private float newlineDelay = 0.12f;

    private void Start() {
        StartCoroutine(TypeConsole());
    }

    private IEnumerator TypeConsole() {
        terminalText.text = "";

        foreach (char c in consoleText) {
            terminalText.text += c;

            if (c == '\n') {
                yield return new WaitForSeconds(newlineDelay);
            }
            else {
                float delay =
                    (1f / charactersPerSecond) +
                    Random.Range(0f, randomDelay);

                yield return new WaitForSeconds(delay);
            }
        }
    }
}