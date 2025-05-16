using OpenCvSharp;
using UnityEngine;

public class OpenCvFaceDetection : MonoBehaviour
{
    public int webcamIndex = 0;
    public string classifierFilePath;

    WebCamTexture camTexture;
    Color32[] pixelData;
    Mat frame;
    Mat gray;
    CascadeClassifier classifier;

    void Start()
    {
        print("Using WebCam device " + WebCamTexture.devices[webcamIndex].name + " with index " + webcamIndex);
        camTexture = new WebCamTexture(WebCamTexture.devices[webcamIndex].name);
        camTexture.Play();

        pixelData = new Color32[camTexture.height * camTexture.width];
        gray = new Mat();
        classifier = new CascadeClassifier(Application.dataPath + classifierFilePath);
    }

    void Update()
    {
        if(camTexture.isPlaying)
        {
            camTexture.GetPixels32(pixelData);
            frame = new Mat(camTexture.height, camTexture.width, MatType.CV_8UC4, pixelData);
            FaceTracking(frame);
        }
    }

    void FaceTracking(Mat img)
    {
        Cv2.Flip(img, img, FlipMode.X);
        Cv2.CvtColor(img, gray, ColorConversionCodes.RGBA2GRAY, 0);

        //Detect faces
        OpenCvSharp.Rect[] faces = classifier.DetectMultiScale(gray, 1.1, 3, 0);

        //Debug
        foreach (OpenCvSharp.Rect face in faces)
        {
            // Draw a rectangle around the detected face
            Cv2.Rectangle(img, face, new Scalar(0, 255, 0));
        }
        //Unity to OpenCV colors
        Cv2.CvtColor(img, img, ColorConversionCodes.RGBA2BGRA);
        Cv2.ImShow("Face Detection", img);
        //End Debug

        if (faces.Length == 0)
        {
            return;
        }

        // Keep the biggest face
        OpenCvSharp.Rect mainFace = faces[0];
        foreach (var face in faces)
        {
            if (face.Height * face.Width > mainFace.Height * face.Width)
            {
                mainFace = face;
            }
        }

        // Find the screen width and height
        float screenWidth = GetComponent<AsymFrustum>().width;
        float screenHeight = GetComponent<AsymFrustum>().height;

        // Convert face position to camera position
        transform.position = new Vector3(((((float)mainFace.X / camTexture.width) - 0.5f) * screenWidth) - 4.0f, -((mainFace.Y / (camTexture.height - 120.0f)) - 0.5f) * screenHeight, transform.position.z);
    }
}
