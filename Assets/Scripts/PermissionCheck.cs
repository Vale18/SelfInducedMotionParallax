
using Mediapipe.Unity.Tutorial;
using UnityEngine;
using UnityEngine.Android;
using UnityEngine.Events;
using UnityEngine.Rendering;
public class PermissionCheck : MonoBehaviour
{
    private FaceLandmarkerRunner faceLandmarkerRunner;
    void Awake()
    {
        faceLandmarkerRunner = GetComponent<FaceLandmarkerRunner>();
        faceLandmarkerRunner.enabled = false;
    }


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
#if !UNITY_Editor
        if (!Permission.HasUserAuthorizedPermission(Permission.Camera)) {
            var callbacks = new PermissionCallbacks();
            callbacks.PermissionDenied += PermissionCallbacks_PermissionDenied;
            callbacks.PermissionGranted += PermissionCallbacks_PermissionGranted;
            Permission.RequestUserPermission(Permission.Camera,callbacks );
        }
        else
        {
            cameraAccessGranted();
        }
#endif
#if UNITY_Editor
        cameraAccessGranted();
        
#endif
    }


    void cameraAccessGranted()
    {
        faceLandmarkerRunner.enabled = true;
    }
    internal void PermissionCallbacks_PermissionGranted(string permissionName)
    {
        cameraAccessGranted();
    }

    internal void PermissionCallbacks_PermissionDenied(string permissionName)
    {
        var callbacks = new PermissionCallbacks();
        callbacks.PermissionDenied += PermissionCallbacks_PermissionDenied;
        callbacks.PermissionGranted += PermissionCallbacks_PermissionGranted;
        Permission.RequestUserPermission(Permission.Camera, callbacks);
    }
}
