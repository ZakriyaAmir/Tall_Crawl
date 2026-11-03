using UnityEngine;

/// ScriptableObject holding all spider skins data and prefab reference for the skins menu.
[CreateAssetMenu(fileName = "SpiderSkinDatabase", menuName = "Tall Crawl/Spider Skin Database")]
public class SpiderSkinDatabase : ScriptableObject
{
    [Tooltip("The single prefab instantiated for skin items in the skins panel")]
    public GameObject spiderSkinListObjPrefab;

    [Tooltip("List of all spider skin definitions")]
    public SkinDef[] skins;

    static SpiderSkinDatabase _instance;
    public static SpiderSkinDatabase Instance
    {
        get
        {
            if (!_instance) _instance = Resources.Load<SpiderSkinDatabase>("SpiderSkinDatabase");
            if (!_instance && TCAssets.I) _instance = TCAssets.I.skinDatabase;
            return _instance;
        }
        set => _instance = value;
    }

    public SkinDef GetSkin(int index)
    {
        if (skins == null || skins.Length == 0) return null;
        return skins[Mathf.Clamp(index, 0, skins.Length - 1)];
    }

    public int Count => skins != null ? skins.Length : 0;
}

