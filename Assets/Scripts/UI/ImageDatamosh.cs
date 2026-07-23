using UnityEngine;
using UnityEngine.UI;

public class ImageDatamosh : MonoBehaviour {
    public Material datamoshMaterial;
    public Image image;
    public float moshDuration = 3f;

    private float timer = 0f;

    private void OnEnable() {
        timer = 0f;

        datamoshMaterial.SetFloat("_MoshAmount", 0f);

        Color c = image.color;
        c.a = 0.5f;
        image.color = c;
    }

    private void Update() {
        timer += Time.deltaTime;

        float progress = Mathf.Clamp01(timer / moshDuration);

        datamoshMaterial.SetFloat("_MoshAmount", progress);
    }

    private void OnDisable() {
        timer = 0f;
        datamoshMaterial.SetFloat("_MoshAmount", 0f);
    }
}