using System;
using Mediapipe.Tasks.Vision.FaceLandmarker;
using Mediapipe.Unity.CoordinateSystem;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using Stopwatch = System.Diagnostics.Stopwatch;
using UnityEngine.Android;

namespace Mediapipe.Unity.Tutorial
{
    public class FaceLandmarkerRunner : MonoBehaviour
    {
        [SerializeField] private RawImage screen;
        [SerializeField] private int width;
        [SerializeField] private int height;
        [SerializeField] private int fps;

        private string modelFileName = "face_landmarker_v2_with_blendshapes.bytes";
        private byte[] modelBytes;

        private WebCamTexture webCamTexture;

        public RawImage Screen { get => screen; set => screen = value; }
        public int Width { get => width; set => width = value; }
        public int Height { get => height; set => height = value; }
        public int Fps { get => fps; set => fps = value; }
        public string ModelFileName { get => modelFileName; set => modelFileName = value; }
        public byte[] ModelBytes { get => modelBytes; set => modelBytes = value; }
        public WebCamTexture WebCamTexture { get => webCamTexture; set => webCamTexture = value; }

        private IEnumerator Start()
        {
            
            if (WebCamTexture.devices.Length == 0)
            {
                throw new System.Exception("Web Camera devices are not found");
            }

            WebCamDevice frontCamera = WebCamTexture.devices[0];
            foreach (var device in WebCamTexture.devices)
            {
                if (device.isFrontFacing)
                {
                    frontCamera = device;
                    break;
                }
            }
            var webCamDevice = frontCamera;
            WebCamTexture = new WebCamTexture(webCamDevice.name, Width, Height, Fps);
            WebCamTexture.Play();

            // NOTE: On macOS, the contents of webCamTexture may not be readable immediately, so wait until it is readable
            yield return new WaitUntil(() => WebCamTexture.width > 16);
            
            //for displaying webcamtexture on screen
            Screen.rectTransform.sizeDelta = new Vector2(Width, Height);
            Screen.texture = WebCamTexture;
            //Read file out of StreamingAssets Folder
            yield return StartCoroutine(LoadModelBytes(result => ModelBytes = result));
            // Prüfen, ob das Laden erfolgreich war
            if (ModelBytes == null)
            {
                Debug.LogError("Model konnte nicht geladen werden.");
                yield break;
            }
            // Optionen erstellen
            var options = new FaceLandmarkerOptions(
                baseOptions: new Tasks.Core.BaseOptions(
                    Mediapipe.Tasks.Core.BaseOptions.Delegate.CPU,
                    modelAssetBuffer: ModelBytes
                ),
                runningMode: Tasks.Vision.Core.RunningMode.VIDEO
            );

            using var faceLandmarker = FaceLandmarker.CreateFromOptions(options);

            //Timestamp needed as second argument for running the task
            var stopwatch = new Stopwatch();
            stopwatch.Start();

            var waitForEndOfFrame = new WaitForEndOfFrame();
            var textureFrame = new Experimental.TextureFrame(WebCamTexture.width, WebCamTexture.height, TextureFormat.RGBA32);

            //Get screen transform
            var screenRect = Screen.rectTransform.rect;

            //Create a sphere to display landmark
            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            //FaceLandmarkerResult.facelanmarks uses MediaPipe coordinate system
            //that differs from Unity -> to overlay it on screen, convert to screen local system
            sphere.transform.SetParent(Screen.transform);
            sphere.transform.localPosition = new Vector3(0, 0, 0);
            sphere.transform.localScale = new Vector3(10f, 10f, 10f);
            sphere.SetActive(false);
            
            while (true)
            {
                //Prepare Data, FaceLandmark API needs image as input
                //Create image from WebCamTexture 
                //textureFrame.ReadTextureOnCPU(WebCamTexture, flipHorizontally: true, flipVertically: true);
                


                var imageTransformationOptions = Experimental.ImageTransformationOptions.Build(
                 shouldFlipHorizontally: webCamDevice.isFrontFacing,
                 isVerticallyFlipped: WebCamTexture.videoVerticallyMirrored,
                 rotation: (RotationAngle)WebCamTexture.videoRotationAngle
                );
                var flipHorizontally = imageTransformationOptions.flipHorizontally;
                var flipVertically = imageTransformationOptions.flipVertically;
                var imageProcessingOptions = new Tasks.Vision.Core.ImageProcessingOptions(rotationDegrees: (int)imageTransformationOptions.rotationAngle);

                textureFrame.ReadTextureOnCPU(WebCamTexture, flipHorizontally, flipVertically);
                using var image = textureFrame.BuildCPUImage();
                //Run the api task
                //DectecForVideo returns infos about lamdmarks
                var result = faceLandmarker.DetectForVideo(image, stopwatch.ElapsedMilliseconds,imageProcessingOptions);

                // Find the screen width and height
                float screenWidth = GetComponent<AsymFrustum>().width;
                float screenHeight = GetComponent<AsymFrustum>().height;

                if (result.faceLandmarks?.Count > 0)
                {
                    var landmarks = result.faceLandmarks[0].landmarks;
                    //position of top head is 11th element in landmark list
                    //468th element -> left eye, 473th element -> right eye
                    var righteye = landmarks[472];
                    var position = screenRect.GetPoint(in righteye);
                    position.z = 0; // ignore Z
                    sphere.transform.localPosition = position;
                    sphere.SetActive(true);

                    //Transform Camera with landmarkposition
                    //Version from old OpenCV script
                    //transform.position = new Vector3(((((float)position.x / webCamTexture.width) - 0.5f) * screenWidth) - 4.0f, -((position.y / (webCamTexture.height - 120.0f)) - 0.5f) * screenHeight, transform.position.z);

                    transform.position = new Vector3((((float)position.x / WebCamTexture.width) * screenWidth), ((position.y / (WebCamTexture.height))) * screenHeight, transform.position.z);
                }
                else
                {
                    sphere.SetActive(false);
                }

                yield return waitForEndOfFrame;


            }

        }
        public IEnumerator LoadModelBytes(System.Action<byte[]> onLoaded)
        {
            string streamingPath = Path.Combine(Application.streamingAssetsPath, ModelFileName);
            string cachedPath = Path.Combine(Application.persistentDataPath, ModelFileName);

            // Wenn schon lokal vorhanden: direkt laden
            if (File.Exists(cachedPath))
            {
                byte[] bytes = File.ReadAllBytes(cachedPath);
                onLoaded?.Invoke(bytes);
                yield break;
            }

#if UNITY_ANDROID && !UNITY_EDITOR

        // Android braucht UnityWebRequest für StreamingAssets
        using (UnityWebRequest request = UnityWebRequest.Get(streamingPath))
        {
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"Failed to load model: {request.error}");
                onLoaded?.Invoke(null);
                yield break;
            }

            byte[] modelBytes = request.downloadHandler.data;

            // In den Cache schreiben für späteren direkten Zugriff
            File.WriteAllBytes(cachedPath, modelBytes);
            onLoaded?.Invoke(modelBytes);
        }
#else
            // Auf PC/macOS kann direkt gelesen werden
            if (File.Exists(streamingPath))
            {
                byte[] modelBytes = File.ReadAllBytes(streamingPath);
                File.WriteAllBytes(cachedPath, modelBytes); // Optional: cachen
                onLoaded?.Invoke(modelBytes);
            }
            else
            {
                Debug.LogError($"Model file not found at {streamingPath}");
                onLoaded?.Invoke(null);
            }
#endif
        }
        private void OnModelLoaded(byte[] modelBytes)
        {
            if (modelBytes == null)
            {
                Debug.LogError("Model loading failed.");
                return;
            }

            var options = new Mediapipe.Tasks.Vision.FaceLandmarker.FaceLandmarkerOptions(
                baseOptions: new Mediapipe.Tasks.Core.BaseOptions(
                    Mediapipe.Tasks.Core.BaseOptions.Delegate.CPU,
                    modelAssetBuffer: modelBytes
                ),
                runningMode: Mediapipe.Tasks.Vision.Core.RunningMode.VIDEO
            );
            
            Debug.Log("Model successfully loaded and ready.");
        }

        private void OnDestroy()
        {
            if (WebCamTexture != null)
            {
                WebCamTexture.Stop();
            }
        }
    }
}
