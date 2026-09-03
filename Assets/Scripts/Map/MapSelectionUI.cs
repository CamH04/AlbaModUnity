using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MapSelectionUI : MonoBehaviour {
    [Header("References")]
    public MapRegistry mapRegistry;
    public Transform mapButtonContainer;
    public GameObject mapButtonPrefab;

    [Header("Preview Panel")]
    public Image mapPreviewImage;
    public TextMeshProUGUI mapNameText;
    public TextMeshProUGUI mapDescriptionText;

    [Header("Random Button")]
    public Button randomMapButton;

    [Header("Highlight")]
    public Color selectedColor = new Color(0.3f, 0.7f, 1f);
    public Color unselectedColor = new Color(0.2f, 0.2f, 0.2f);

    private int _selectedIndex = 0;
    private Button[] _buttons;

    void Start() {


        if (mapRegistry == null) {
            Debug.LogError("MapSelectionUI: MapRegistry not assigned!");
            return;
        }

        if (mapRegistry.maps == null || mapRegistry.maps.Length == 0) {
            Debug.LogError("MapSelectionUI: MapRegistry has no maps!");
            return;
        }

        if (randomMapButton != null)
            randomMapButton.onClick.AddListener(SelectRandom);

        BuildMapList();
        SelectMap(0);
    }

    void BuildMapList() {
        foreach (Transform child in mapButtonContainer)
            Destroy(child.gameObject);

        _buttons = new Button[mapRegistry.maps.Length];

        for (int i = 0; i < mapRegistry.maps.Length; i++) {
            var def = mapRegistry.maps[i];
            var btnObj = Instantiate(mapButtonPrefab, mapButtonContainer);
            var btn = btnObj.GetComponent<Button>();

            var preview = btnObj.transform.Find("Preview")?.GetComponent<Image>();
            if (preview != null && def.mapPreviewImage != null)
                preview.sprite = def.mapPreviewImage;

            var label = btnObj.transform.Find("Label")?.GetComponent<TextMeshProUGUI>();
            if (label != null)
                label.text = def.mapName;

            int index = i;
            btn.onClick.AddListener(() => SelectMap(index));
            _buttons[i] = btn;
        }
    }

    void SelectMap(int index) {
        _selectedIndex = index;

        for (int i = 0; i < _buttons.Length; i++) {
            if (_buttons[i] == null) continue;
            var img = _buttons[i].GetComponent<Image>();
            if (img != null)
                img.color = i == index ? selectedColor : unselectedColor;
        }

        var def = mapRegistry.maps[index];

        if (mapPreviewImage != null)
            mapPreviewImage.sprite = def.mapPreviewImage != null
                ? def.mapPreviewImage : null;

        if (mapNameText != null)
            mapNameText.text = def.mapName;

        if (mapDescriptionText != null)
            mapDescriptionText.text = def.mapDescription;

        if (MapSelection.Instance != null)
            MapSelection.Instance.SelectMap(index);

        Debug.Log($"[AlbaMod] UI selected map: {def.mapName}");
    }

    void SelectRandom() {
        if (mapRegistry.maps.Length == 0) return;
        int index = Random.Range(0, mapRegistry.maps.Length);
        SelectMap(index);
    }
}