using UnityEditor;

/// <summary>
/// Automatically applies the Android keystore and key alias passwords
/// whenever the Unity Editor opens or reloads scripts.
/// </summary>
[InitializeOnLoad]
public static class AndroidKeystoreAutoFill
{
    private const string KeystorePassword = "Mostwanted98";

    static AndroidKeystoreAutoFill()
    {
        ApplyPasswords();
        EditorApplication.delayCall += ApplyPasswords;
    }

    private static void ApplyPasswords()
    {
        PlayerSettings.Android.useCustomKeystore = true;
        PlayerSettings.Android.keystorePass = KeystorePassword;
        PlayerSettings.Android.keyaliasPass = KeystorePassword;
    }
}
