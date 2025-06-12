using Mediapipe.Tasks.Vision.FaceLandmarker;
using Mediapipe.Unity.CoordinateSystem;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace Mediapipe.Unity.Tutorial
{
    public class FaceLandmarkerRunner : MonoBehaviour
    {
        [SerializeField] private RawImage screen;
        [SerializeField] private int width;
        [SerializeField] private int height;
        [SerializeField] private int fps;

        [SerializeField] private TextAsset modelAsset;

        private WebCamTexture webCamTexture;

        private IEnumerator Start()
        {
            if (WebCamTexture.devices.Length == 0)
            {
                throw new System.Exception("Web Camera devices are not found");
            }
            var webCamDevice = WebCamTexture.devices[0];
            webCamTexture = new WebCamTexture(webCamDevice.name, width, height, fps);
            webCamTexture.Play();

            // NOTE: On macOS, the contents of webCamTexture may not be readable immediately, so wait until it is readable
            yield return new WaitUntil(() => webCamTexture.width > 16);

            //for displaying webcamtexture on screen
            screen.rectTransform.sizeDelta = new Vector2(width, height);
            screen.texture = webCamTexture;

            //Generaate a task
            var options = new FaceLandmarkerOptions(
                baseOptions: new Tasks.Core.BaseOptions(
                     Tasks.Core.BaseOptions.Delegate.CPU,
                     modelAssetBuffer: modelAsset.bytes
                    ),
                      runningMode: Tasks.Vision.Core.RunningMode.VIDEO
                    );


            using var faceLandmarker = FaceLandmarker.CreateFromOptions(options);

            //Timestamp needed as second argument for running the task
            var stopwatch = new Stopwatch();
            stopwatch.Start();

            var waitForEndOfFrame = new WaitForEndOfFrame();
            var textureFrame = new Experimental.TextureFrame(webCamTexture.width, webCamTexture.height, TextureFormat.RGBA32);

            //Get screen transform
            var screenRect = screen.rectTransform.rect;

            //Create a sphere to display landmark
            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            //FaceLandmarkerResult.facelanmarks uses MediaPipe coordinate system
            //that differs from Unity -> to overlay it on screen, convert to screen local system
            sphere.transform.SetParent(screen.transform);
            sphere.transform.localPosition = new Vector3(0, 0, 0);
            sphere.transform.localScale = new Vector3(10f, 10f, 10f);
            sphere.SetActive(false);

            while (true)
            {
                //Prepare Data, FaceLandmark API needs image as input
                //Create image from WebCamTexture 
                textureFrame.ReadTextureOnCPU(webCamTexture, flipHorizontally: false, flipVertically: true);
                using var image = textureFrame.BuildCPUImage();

                //Run the api task
                //DectecForVideo returns infos about lamdmarks
                var result = faceLandmarker.DetectForVideo(image, stopwatch.ElapsedMilliseconds);

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

                    transform.position = new Vector3((((float)position.x / webCamTexture.width) * screenWidth), -((position.y / (webCamTexture.height))) * screenHeight, transform.position.z);
                }
                else
                {
                    sphere.SetActive(false);
                }

                yield return waitForEndOfFrame;


            }

        }

        private void OnDestroy()
        {
            if (webCamTexture != null)
            {
                webCamTexture.Stop();
            }
        }
    }
}
