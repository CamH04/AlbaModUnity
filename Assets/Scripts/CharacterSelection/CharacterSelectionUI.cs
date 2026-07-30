using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CharacterSelectionUI : MonoBehaviour {
    [Header("References")]
    public CharacterRegistry characterRegistry;
    public Transform characterButtonContainer;
    public GameObject characterButtonPrefab;

    [Header("Preview Panel")]
    public Image characterPortrait;
    public TextMeshProUGUI characterName;
    public TextMeshProUGUI characterDescription;

    [Header("Highlight")]
    public Color selectedColor = new Color(0.3f, 0.7f, 1f);
    public Color unselectedColor = new Color(0.2f, 0.2f, 0.2f);

    private int _selectedIndex = 0;
    private Button[] _buttons;
    private int _pendingSelection = 0;

    void Start() {
        if (characterRegistry == null) {
            Debug.LogError("CharacterSelectionUI: CharacterRegistry not assigned!");
            return;
        }

        if (characterRegistry.characters == null
            || characterRegistry.characters.Length == 0) {
            Debug.LogError("CharacterSelectionUI: CharacterRegistry has no characters!");
            return;
        }

        BuildCharacterList();
        SelectCharacter(0);
    }

    void BuildCharacterList() {
        foreach (Transform child in characterButtonContainer)
            Destroy(child.gameObject);

        _buttons = new Button[characterRegistry.characters.Length];

        for (int i = 0; i < characterRegistry.characters.Length; i++) {
            var def = characterRegistry.characters[i];
            var btnObj = Instantiate(characterButtonPrefab, characterButtonContainer);
            var btn = btnObj.GetComponent<Button>();

            var portrait = btnObj.transform.Find("Portrait")?.GetComponent<Image>();
            if (portrait != null && def.characterPortrait != null)
                portrait.sprite = def.characterPortrait;

            var label = btnObj.transform.Find("Label")?.GetComponent<TextMeshProUGUI>();
            if (label != null)
                label.text = def.characterName;

            int index = i;
            btn.onClick.AddListener(() => SelectCharacter(index));
            _buttons[i] = btn;
        }
    }

    void SelectCharacter(int index) {
        _selectedIndex = index;

        for (int i = 0; i < _buttons.Length; i++) {
            if (_buttons[i] == null) continue;
            var img = _buttons[i].GetComponent<Image>();
            if (img != null)
                img.color = i == index ? selectedColor : unselectedColor;
        }

        var def = characterRegistry.characters[index];
        if (characterPortrait != null && def.characterPortrait != null)
            characterPortrait.sprite = def.characterPortrait;
        if (characterName != null)
            characterName.text = def.characterName;
        if (characterDescription != null)
            characterDescription.text = def.characterDescription;

        // Store locally — no RPC needed, server reads this at spawn time
        if (PlayerCharacterSelection.Instance != null) {
            ulong clientId = Unity.Netcode.NetworkManager.Singleton != null
                ? Unity.Netcode.NetworkManager.Singleton.LocalClientId
                : 0;

            PlayerCharacterSelection.Instance.SelectCharacter(index, clientId);
        }

        _pendingSelection = -1; // clear pending since we handle it directly now
    }
}