using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Firebase.Analytics;
using Firebase;

public class FireBaseInit : MonoBehaviour
{
    void Start()
    {
#if !UNITY_EDITOR
        // Initialize Firebase
        Firebase.FirebaseApp.CheckAndFixDependenciesAsync().ContinueWith(task =>
        {
            var dependencyStatus = task.Result;
            if (dependencyStatus == Firebase.DependencyStatus.Available)
            {
                Firebase.FirebaseApp app = Firebase.FirebaseApp.DefaultInstance;
                Debug.Log("FirebaseInitialized");
            }
            else
            {
                UnityEngine.Debug.LogError(System.String.Format(
                  "Could not resolve all Firebase dependencies: {0}", dependencyStatus));
            }
        });
#else
        Debug.Log("[FireBaseInit] Firebase disabled in Editor to prevent native runtime deadlock.");
#endif
    }

    public void SendCustomEvent(string logString)
    {
#if !UNITY_EDITOR
        FirebaseAnalytics.LogEvent(name: logString);
#endif
        Debug.Log("FireBase Log Sent: " + logString);
    }
}
