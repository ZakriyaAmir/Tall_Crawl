using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Places GooglePlay and GooglePlayProfile into Settings/Card in MainMenu.unity
/// without overwriting any existing manual UI changes.
/// </summary>
[InitializeOnLoad]
public static class TCPlaceGooglePlayButton
{
    static TCPlaceGooglePlayButton()
    {
        EditorApplication.delayCall += PlaceIfMissing;
    }

    [MenuItem("Tools/Tall Crawl/Place Google Play Button In Settings")]
    public static void PlaceIfMissing()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;

        var active = SceneManager.GetActiveScene();
        if (active.name != "MainMenu") return;

        var menu = Object.FindObjectOfType<TCMenuUI>(true);
        if (!menu) return;

        RectTransform card = null;
        foreach (var rt in Resources.FindObjectsOfTypeAll<RectTransform>())
        {
            if (rt.gameObject.scene == active && rt.name == "Card" && rt.parent != null && rt.parent.name == "Settings")
            {
                card = rt;
                break;
            }
        }
        if (!card) return;

        bool changed = false;
        for (int i = card.childCount - 1; i >= 0; i--)
        {
            var ch = card.GetChild(i);
            if (ch.name == "Tutorial" || ch.name == "Shadow")
            {
                Object.DestroyImmediate(ch.gameObject);
                changed = true;
            }
        }

        if (card.Find("GooglePlay") == null || card.Find("GooglePlayProfile/SignOut") == null)
        {
            var oldProfile = card.Find("GooglePlayProfile");
            if (oldProfile != null && oldProfile.Find("SignOut") == null)
                Object.DestroyImmediate(oldProfile.gameObject);

            menu.BuildGpgsSettingsButton(card);
            changed = true;
        }

        if (!changed) return;
        EditorSceneManager.MarkSceneDirty(active);
        EditorSceneManager.SaveScene(active);
        Debug.Log("[TCPlaceGooglePlayButton] Cleaned Settings/Card and ensured GooglePlay + GooglePlayProfile/SignOut in MainMenu.unity.");
    }
}
