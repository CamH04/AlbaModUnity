using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class WeaponSelectionUI : MonoBehaviour {
    [Header("References")]
    public WeaponRegistry weaponRegistry;
    public Transform weaponButtonContainer;
    public GameObject weaponButtonPrefab;

    [Header("Selection Info")]
    public Image selectedWeaponIcon;
    public TextMeshProUGUI selectedWeaponName;
    public TextMeshProUGUI selectedWeaponDescription;

    [Header("Highlight")]
    public Color selectedColor = new Color(0.3f, 0.7f, 1f);
    public Color unselectedColor = new Color(0.2f, 0.2f, 0.2f);

    private int _selectedIndex = 0;
    private Button[] _buttons;

    void Start() {
        // Validate registry assigned
        if (weaponRegistry == null) {
            Debug.LogError("WeaponSelectionUI: WeaponRegistry not assigned in Inspector!");
            return;
        }

        if (weaponRegistry.weapons == null || weaponRegistry.weapons.Length == 0) {
            Debug.LogError("WeaponSelectionUI: WeaponRegistry has no weapons!");
            return;
        }

        Debug.Log($"WeaponSelectionUI: building list with {weaponRegistry.weapons.Length} weapons");
        BuildWeaponList();
        SelectWeapon(0);
    }

    void BuildWeaponList() {
        foreach (Transform child in weaponButtonContainer)
            Destroy(child.gameObject);

        _buttons = new Button[weaponRegistry.weapons.Length];

        for (int i = 0; i < weaponRegistry.weapons.Length; i++) {
            var entry = weaponRegistry.weapons[i];
            var btnObj = Instantiate(weaponButtonPrefab, weaponButtonContainer);
            var btn = btnObj.GetComponent<Button>();

            if (btn == null) {
                Debug.LogError($"WeaponSelectionUI: button prefab has no Button component!");
                continue;
            }

            var icon = btnObj.transform.Find("Icon")?.GetComponent<Image>();
            if (icon != null && entry.weaponIcon != null)
                icon.sprite = entry.weaponIcon;

            var label = btnObj.transform.Find("Label")?.GetComponent<TextMeshProUGUI>();
            if (label != null)
                label.text = entry.weaponName;
            else
                Debug.LogWarning($"WeaponSelectionUI: button prefab missing 'Label' child TextMeshPro");

            int index = i;
            btn.onClick.AddListener(() => SelectWeapon(index));
            _buttons[i] = btn;
        }
    }

    void SelectWeapon(int index) {
        _selectedIndex = index;

        for (int i = 0; i < _buttons.Length; i++) {
            if (_buttons[i] == null) continue;
            var img = _buttons[i].GetComponent<Image>();
            if (img != null)
                img.color = i == index ? selectedColor : unselectedColor;
        }

        var weapon = weaponRegistry.weapons[index];

        if (selectedWeaponIcon != null)
            selectedWeaponIcon.sprite = weapon.weaponIcon;
        if (selectedWeaponName != null)
            selectedWeaponName.text = weapon.weaponName;
        if (selectedWeaponDescription != null)
            selectedWeaponDescription.text = weapon.weaponDescription;

        // Only send to server if already connected
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsConnectedClient
            && PlayerWeaponSelection.Instance != null) {
            PlayerWeaponSelection.Instance.SelectWeaponServerRpc(
                index, NetworkManager.Singleton.LocalClientId);
        }
        else {
            // Store locally and send when connected
            _pendingSelection = index;
        }
    }

    private int _pendingSelection = 0;

    void Update() {
        // Send pending selection once connected
        if (_pendingSelection >= 0
            && NetworkManager.Singleton != null
            && NetworkManager.Singleton.IsConnectedClient
            && PlayerWeaponSelection.Instance != null) {
            PlayerWeaponSelection.Instance.SelectWeaponServerRpc(
                _pendingSelection, NetworkManager.Singleton.LocalClientId);
            _pendingSelection = -1;
        }
    }
}