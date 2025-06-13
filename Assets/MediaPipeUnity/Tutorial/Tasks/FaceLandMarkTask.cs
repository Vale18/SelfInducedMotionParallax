using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Mediapipe;
using Mediapipe.Tasks.Core;
using Mediapipe.Tasks.Vision.Core;
using UnityEngine;
using Mediapipe.Tasks.Vision.FaceLandmarker;
using Mediapipe.Unity.Tutorial;
using System.Diagnostics;
using Mediapipe.Unity.CoordinateSystem;
using Mediapipe.Unity.Experimental;
using UnityEngine.UI;
using Debug = UnityEngine.Debug;

public class FaceLandMarkTask : MonoBehaviour
{
    public FaceLandmarkerRunner faceLandmarkerRunner;
    public TextAsset modelAsset;
    [SerializeField] private RectTransform eyeRectOverlay;
    [SerializeField] private RawImage screen;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    private IEnumerator Start()
    {
        yield return new WaitForEndOfFrame();
        //create Task
        var options = new FaceLandmarkerOptions(
            baseOptions: new BaseOptions(
                BaseOptions.Delegate.CPU,
                modelAssetBuffer: modelAsset.bytes),
            runningMode: RunningMode.VIDEO);
        using var faceLandmarker = FaceLandmarker.CreateFromOptions(options);
        //prepare Data
        var tmpTexture = new Texture2D(faceLandmarkerRunner.webCamTexture.width, faceLandmarkerRunner.webCamTexture.height, TextureFormat.RGBA32, false);

        
        //run Task
        //createTimestamp
        var stopwatch = new Stopwatch();
        stopwatch.Start();
        
        var waitForEndOfFrame = new WaitForEndOfFrame();
        using var textureFrame = new TextureFrame(faceLandmarkerRunner.webCamTexture.width, faceLandmarkerRunner.webCamTexture.height, TextureFormat.RGBA32);
        var screenRect = screen.rectTransform.rect;

        var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.transform.SetParent(screen.transform);
        sphere.transform.localPosition = new Vector3(0, 0, 0);
        sphere.transform.localScale = new Vector3(10f, 10f, 10f);
        sphere.SetActive(false);
        
        while (true)
        {
            textureFrame.ReadTextureOnCPU(faceLandmarkerRunner.webCamTexture, flipHorizontally: false, flipVertically: true);
            using var image = textureFrame.BuildCPUImage();
            var result = faceLandmarker.DetectForVideo(image, stopwatch.ElapsedMilliseconds);
            if (result.faceLandmarks?.Count > 0)
            {
                var landmarks = result.faceLandmarks[0].landmarks;

                // Augenpunkte extrahieren (je nach Modellversion anpassen)
                var l33 = landmarks[33];
                var l133 = landmarks[133];
                var l362 = landmarks[362];
                var l263 = landmarks[263];

                var leftEyeOuter = screenRect.GetPoint(in l33);
                var leftEyeInner = screenRect.GetPoint(in l133);
                var rightEyeOuter = screenRect.GetPoint(in l362);
                var rightEyeInner = screenRect.GetPoint(in l263);


                // Min/Max für Rechteck berechnen
                float minX = Mathf.Min(leftEyeOuter.x, leftEyeInner.x, rightEyeOuter.x, rightEyeInner.x);
                float maxX = Mathf.Max(leftEyeOuter.x, leftEyeInner.x, rightEyeOuter.x, rightEyeInner.x);
                float minY = Mathf.Min(leftEyeOuter.y, leftEyeInner.y, rightEyeOuter.y, rightEyeInner.y);
                float maxY = Mathf.Max(leftEyeOuter.y, leftEyeInner.y, rightEyeOuter.y, rightEyeInner.y);

                // Rechteck-Position und Größe setzen
                var rectCenter = new Vector2((minX + maxX) / 2f, (minY + maxY) / 2f);
                var rectSize = new Vector2((maxX - minX), (maxY - minY));

                eyeRectOverlay.anchoredPosition = rectCenter;
                eyeRectOverlay.sizeDelta = rectSize * 1.5f; // etwas größer für Puffer
                eyeRectOverlay.gameObject.SetActive(true);
            }
            else
            {
                eyeRectOverlay.gameObject.SetActive(false);
            }
            
            yield return waitForEndOfFrame;
        }

    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void CreateAndRunTask()
    {
    }

}
