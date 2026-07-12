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
        BuildWeaponList();
        SelectWeapon(0);
    }

    void BuildWeaponList() {
        // Clear existing buttons
        foreach (Transform child in weaponButtonContainer)
            Destroy(child.gameObject);

        _buttons = new Button[weaponRegistry.weapons.Length];

        for (int i = 0; i < weaponRegistry.weapons.Length; i++) {
            var entry = weaponRegistry.weapons[i];
            var btnObj = Instantiate(weaponButtonPrefab, weaponButtonContainer);
            var btn = btnObj.GetComponent<Button>();

            // Set icon
            var icon = btnObj.transform.Find("Icon")?.GetComponent<Image>();
            if (icon != null && entry.weaponIcon != null)
                icon.sprite = entry.weaponIcon;

            // Set name label
            var label = btnObj.transform.Find("Label")?.GetComponent<TextMeshProUGUI>();
            if (label != null)
                label.text = entry.weaponName;

            int index = i; // capture for lambda
            btn.onClick.AddListener(() => SelectWeapon(index));

            _buttons[i] = btn;
        }
    }

    void SelectWeapon(int index) {
        _selectedIndex = index;

        // Update button highlight colors
        for (int i = 0; i < _buttons.Length; i++) {
            var img = _buttons[i].GetComponent<Image>();
            if (img != null)
                img.color = i == index ? selectedColor : unselectedColor;
        }

        // Update info panel
        var weapon = weaponRegistry.weapons[index];
        if (selectedWeaponIcon != null && weapon.weaponIcon != null)
            selectedWeaponIcon.sprite = weapon.weaponIcon;
        if (selectedWeaponName != null)
            selectedWeaponName.text = weapon.weaponName;
        if (selectedWeaponDescription != null)
            selectedWeaponDescription.text = weapon.weaponDescription;

        // Tell the server our selection
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsConnectedClient) {
            if (PlayerWeaponSelection.Instance != null)
                PlayerWeaponSelection.Instance.SelectWeaponServerRpc(
                    index, NetworkManager.Singleton.LocalClientId);
        }
    }
}