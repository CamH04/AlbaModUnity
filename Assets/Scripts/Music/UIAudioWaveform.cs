using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasRenderer))]
public class UIAudioWaveform : Graphic {
    [Header("Waveform")]
    [SerializeField, Range(64, 1024)]
    private int waveformPoints = 256;

    [SerializeField, Range(0.1f, 2f)]
    private float amplitude = 1f;

    [SerializeField, Range(0.5f, 20f)]
    private float lineThickness = 2f;

    [Header("Scrolling")]
    [Tooltip("How many waveform points move across the screen per second.")]
    [SerializeField, Range(1f, 120f)]
    private float scrollSpeed = 50f;

    [Header("Smoothing")]
    [SerializeField, Range(0.01f, 1f)]
    private float smoothing = 0.15f;

    [Header("Appearance")]
    [SerializeField]
    private Color waveformColor = Color.white;

    [SerializeField]
    private bool fillWaveform = true;

    [SerializeField]
    private Color fillColor = new Color(1f, 1f, 1f, 0.25f);

    [Header("Playhead")]
    [SerializeField]
    private bool showPlayhead = true;

    [SerializeField, Range(1f, 10f)]
    private float playheadWidth = 2f;

    [SerializeField]
    private Color playheadColor = Color.white;

    [Header("Centre Line")]
    [SerializeField]
    private bool showCentreLine = false;

    [SerializeField, Range(0.5f, 5f)]
    private float centreLineThickness = 1f;

    [SerializeField]
    private Color centreLineColor =
        new Color(1f, 1f, 1f, 0.15f);


    // ---------------------------------------------------------------------
    // Audio
    // ---------------------------------------------------------------------

    private AudioSource audioSource;

    private const int AudioSampleCount = 1024;

    private float[] audioSamples;


    // ---------------------------------------------------------------------
    // Waveform
    // ---------------------------------------------------------------------

    private float[] topWaveform;
    private float[] bottomWaveform;

    private float scrollAccumulator;

    private AudioClip lastClip;


    // ---------------------------------------------------------------------
    // Unity
    // ---------------------------------------------------------------------

    protected override void Awake() {
        base.Awake();

        Initialise();
    }

    protected override void OnEnable() {
        base.OnEnable();

        FindMusicPlayer();

        ClearWaveform();

        SetVerticesDirty();
    }

    private void Initialise() {
        audioSamples = new float[AudioSampleCount];

        topWaveform = new float[waveformPoints];
        bottomWaveform = new float[waveformPoints];
    }


    private void Update() {
        // MusicPlayer creates the AudioSource dynamically.
        if (audioSource == null) {
            FindMusicPlayer();

            if (audioSource == null)
                return;
        }


        // Detect track changes.
        if (audioSource.clip != lastClip) {
            lastClip = audioSource.clip;

            ClearWaveform();
        }


        if (!audioSource.isPlaying) {
            FadeWaveform();

            SetVerticesDirty();

            return;
        }


        UpdateWaveform();

        SetVerticesDirty();
    }


    // ---------------------------------------------------------------------
    // Find MusicPlayer
    // ---------------------------------------------------------------------

    private void FindMusicPlayer() {
        if (MusicPlayer.Instance != null) {
            audioSource = MusicPlayer.Instance.AudioSource;
        }
    }


    // ---------------------------------------------------------------------
    // Waveform processing
    // ---------------------------------------------------------------------

    private void UpdateWaveform() {
        /*
         * Get the actual samples currently being played.
         *
         * Unlike the previous version, we DON'T reduce the entire
         * buffer to one amplitude value.
         */
        audioSource.GetOutputData(audioSamples, 0);


        /*
         * Determine how much the waveform should move this frame.
         */
        scrollAccumulator +=
            scrollSpeed * Time.deltaTime;


        int pointsToAdd =
            Mathf.FloorToInt(scrollAccumulator);


        if (pointsToAdd <= 0)
            return;


        scrollAccumulator -= pointsToAdd;


        /*
         * Don't shift more points than exist.
         */
        pointsToAdd =
            Mathf.Min(pointsToAdd, waveformPoints);


        /*
         * Move existing waveform to the LEFT.
         */
        for (int i = 0;
             i < waveformPoints - pointsToAdd;
             i++) {
            topWaveform[i] =
                topWaveform[i + pointsToAdd];

            bottomWaveform[i] =
                bottomWaveform[i + pointsToAdd];
        }


        /*
         * We need new audio information for the RIGHT side.
         *
         * Divide the audio buffer into sections and use each
         * section to create one waveform point.
         */
        int samplesPerPoint =
            Mathf.Max(
                1,
                audioSamples.Length / pointsToAdd
            );


        for (int point = 0;
             point < pointsToAdd;
             point++) {
            int start =
                point * samplesPerPoint;

            int end =
                Mathf.Min(
                    start + samplesPerPoint,
                    audioSamples.Length
                );


            float positivePeak = 0f;
            float negativePeak = 0f;


            for (int i = start; i < end; i++) {
                float sample =
                    audioSamples[i];


                if (sample > positivePeak)
                    positivePeak = sample;


                if (sample < negativePeak)
                    negativePeak = sample;
            }


            int index =
                waveformPoints -
                pointsToAdd +
                point;


            /*
             * Smooth the new waveform point.
             */
            float smooth =
                1f -
                Mathf.Exp(
                    -smoothing *
                    60f *
                    Time.deltaTime
                );


            topWaveform[index] =
                Mathf.Lerp(
                    topWaveform[index],
                    positivePeak,
                    smooth
                );


            bottomWaveform[index] =
                Mathf.Lerp(
                    bottomWaveform[index],
                    negativePeak,
                    smooth
                );
        }
    }


    // ---------------------------------------------------------------------
    // Clear
    // ---------------------------------------------------------------------

    private void ClearWaveform() {
        if (topWaveform == null)
            return;


        for (int i = 0;
             i < waveformPoints;
             i++) {
            topWaveform[i] = 0f;
            bottomWaveform[i] = 0f;
        }


        scrollAccumulator = 0f;
    }


    // ---------------------------------------------------------------------
    // Fade out
    // ---------------------------------------------------------------------

    private void FadeWaveform() {
        float fade =
            1f -
            Mathf.Exp(
                -8f *
                Time.deltaTime
            );


        for (int i = 0;
             i < waveformPoints;
             i++) {
            topWaveform[i] =
                Mathf.Lerp(
                    topWaveform[i],
                    0f,
                    fade
                );

            bottomWaveform[i] =
                Mathf.Lerp(
                    bottomWaveform[i],
                    0f,
                    fade
                );
        }
    }


    // ---------------------------------------------------------------------
    // UI Mesh
    // ---------------------------------------------------------------------

    protected override void OnPopulateMesh(VertexHelper vh) {
        vh.Clear();


        if (topWaveform == null ||
            bottomWaveform == null ||
            waveformPoints < 2) {
            return;
        }


        Rect rect =
            rectTransform.rect;


        float centerY =
            rect.center.y;


        float halfHeight =
            rect.height * 0.5f;


        /*
         * Draw waveform.
         */
        for (int i = 0;
             i < waveformPoints - 1;
             i++) {
            float t1 =
                i /
                (float)(waveformPoints - 1);


            float t2 =
                (i + 1) /
                (float)(waveformPoints - 1);


            float x1 =
                Mathf.Lerp(
                    rect.xMin,
                    rect.xMax,
                    t1
                );


            float x2 =
                Mathf.Lerp(
                    rect.xMin,
                    rect.xMax,
                    t2
                );


            float top1 =
                centerY +
                topWaveform[i] *
                halfHeight *
                amplitude;


            float top2 =
                centerY +
                topWaveform[i + 1] *
                halfHeight *
                amplitude;


            float bottom1 =
                centerY +
                bottomWaveform[i] *
                halfHeight *
                amplitude;


            float bottom2 =
                centerY +
                bottomWaveform[i + 1] *
                halfHeight *
                amplitude;


            /*
             * Filled waveform.
             */
            if (fillWaveform) {
                AddFilledQuad(
                    vh,
                    new Vector2(x1, bottom1),
                    new Vector2(x1, top1),
                    new Vector2(x2, top2),
                    new Vector2(x2, bottom2),
                    fillColor
                );
            }


            /*
             * Top edge.
             */
            AddLine(
                vh,
                new Vector2(x1, top1),
                new Vector2(x2, top2),
                lineThickness,
                waveformColor
            );


            /*
             * Bottom edge.
             */
            AddLine(
                vh,
                new Vector2(x1, bottom1),
                new Vector2(x2, bottom2),
                lineThickness,
                waveformColor
            );
        }


        /*
         * Centre line.
         */
        if (showCentreLine) {
            AddLine(
                vh,
                new Vector2(
                    rect.xMin,
                    centerY
                ),
                new Vector2(
                    rect.xMax,
                    centerY
                ),
                centreLineThickness,
                centreLineColor
            );
        }


        /*
         * Fixed centre playhead.
         */
        if (showPlayhead) {
            AddLine(
                vh,
                new Vector2(
                    rect.center.x,
                    rect.yMin
                ),
                new Vector2(
                    rect.center.x,
                    rect.yMax
                ),
                playheadWidth,
                playheadColor
            );
        }
    }


    // ---------------------------------------------------------------------
    // Mesh helpers
    // ---------------------------------------------------------------------

    private void AddLine(
        VertexHelper vh,
        Vector2 start,
        Vector2 end,
        float thickness,
        Color color) {
        Vector2 direction =
            (end - start).normalized;


        Vector2 perpendicular =
            new Vector2(
                -direction.y,
                direction.x
            );


        Vector2 offset =
            perpendicular *
            thickness *
            0.5f;


        int index =
            vh.currentVertCount;


        UIVertex vertex =
            UIVertex.simpleVert;


        vertex.color = color;


        vertex.position =
            start - offset;

        vh.AddVert(vertex);


        vertex.position =
            start + offset;

        vh.AddVert(vertex);


        vertex.position =
            end + offset;

        vh.AddVert(vertex);


        vertex.position =
            end - offset;

        vh.AddVert(vertex);


        vh.AddTriangle(
            index,
            index + 1,
            index + 2
        );


        vh.AddTriangle(
            index,
            index + 2,
            index + 3
        );
    }


    private void AddFilledQuad(
        VertexHelper vh,
        Vector2 bottomLeft,
        Vector2 topLeft,
        Vector2 topRight,
        Vector2 bottomRight,
        Color color) {
        int index =
            vh.currentVertCount;


        UIVertex vertex =
            UIVertex.simpleVert;


        vertex.color = color;


        vertex.position =
            bottomLeft;

        vh.AddVert(vertex);


        vertex.position =
            topLeft;

        vh.AddVert(vertex);


        vertex.position =
            topRight;

        vh.AddVert(vertex);


        vertex.position =
            bottomRight;

        vh.AddVert(vertex);


        vh.AddTriangle(
            index,
            index + 1,
            index + 2
        );


        vh.AddTriangle(
            index,
            index + 2,
            index + 3
        );
    }


    // ---------------------------------------------------------------------
    // Public controls
    // ---------------------------------------------------------------------

    public void SetWaveformColor(Color color) {
        waveformColor = color;
        SetVerticesDirty();
    }


    public void SetFillColor(Color color) {
        fillColor = color;
        SetVerticesDirty();
    }


    public void SetPlayheadColor(Color color) {
        playheadColor = color;
        SetVerticesDirty();
    }
}