using UnityEngine;

[ExecuteAlways]
public class ImageProjector : MonoBehaviour {
    [SerializeField] private Texture2D image;
    [SerializeField] private Renderer targetPlane;

    private MaterialPropertyBlock propertyBlock;

    private void OnEnable() {
        ApplyImage();
    }

    private void OnValidate() {
        ApplyImage();
    }

    private void ApplyImage() {
        if (targetPlane == null)
            targetPlane = GetComponentInChildren<Renderer>();

        if (targetPlane == null)
            return;

        if (propertyBlock == null)
            propertyBlock = new MaterialPropertyBlock();

        targetPlane.GetPropertyBlock(propertyBlock);

        if (image != null) {
            propertyBlock.SetTexture("_MainTex", image);
            propertyBlock.SetTexture("_BaseMap", image);
        }

        targetPlane.SetPropertyBlock(propertyBlock);
    }
}