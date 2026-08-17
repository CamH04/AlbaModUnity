using UnityEngine;

public class ImageProjector : MonoBehaviour {
    [SerializeField] private Texture2D image;
    [SerializeField] private Renderer targetPlane;

    private Material materialInstance;

    private void Awake() {
        if (targetPlane == null)
            targetPlane = GetComponentInChildren<Renderer>();

        materialInstance = targetPlane.material;
        UpdateImage();
    }

    private void OnValidate() {
        if (targetPlane == null)
            targetPlane = GetComponentInChildren<Renderer>();

        if (targetPlane != null) {
            materialInstance = targetPlane.sharedMaterial;
            UpdateImage();
        }
    }

    private void UpdateImage() {
        if (materialInstance == null || image == null)
            return;

        materialInstance.mainTexture = image;
    }
}