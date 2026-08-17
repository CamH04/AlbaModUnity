using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class ImageSlideshow : MonoBehaviour {
    public Image displayImage;

    public float slideDuration = 5f;
    public float fadeDuration = 1f;

    private Sprite[] slides;
    private int currentSlide = 0;

    void Start() {
        slides = Resources.LoadAll<Sprite>("Slides");

        if (slides.Length > 0) {
            displayImage.sprite = slides[0];
            StartCoroutine(Slideshow());
        }
    }

    IEnumerator Slideshow() {
        while (true) {
            yield return new WaitForSeconds(slideDuration);
            yield return StartCoroutine(Fade(1f, 0f));
            currentSlide = (currentSlide + 1) % slides.Length;
            displayImage.sprite = slides[currentSlide];
            yield return StartCoroutine(Fade(0f, 1f));
        }
    }

    IEnumerator Fade(float startAlpha, float endAlpha) {
        float elapsed = 0f;

        Color color = displayImage.color;

        while (elapsed < fadeDuration) {
            elapsed += Time.deltaTime;

            float alpha = Mathf.Lerp(startAlpha, endAlpha, elapsed / fadeDuration);

            displayImage.color = new Color(
                color.r,
                color.g,
                color.b,
                alpha
            );

            yield return null;
        }

        displayImage.color = new Color(
            color.r,
            color.g,
            color.b,
            endAlpha
        );
    }
}