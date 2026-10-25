using UnityEngine;
using TMPro;

/// Asset references for runtime-built UI. Lives at Assets/Resources/TCAssets.asset (made by Tools > Tall Crawl > Setup).
[CreateAssetMenu(menuName = "Tall Crawl/Assets", fileName = "TCAssets")]
public class TCAssets : ScriptableObject
{
    public TMP_FontAsset font;
    public Sprite logo;
    public GameObject spiderModel;
    public RuntimeAnimatorController spiderAnimator;
    public AudioClip click, coin, purchase, whoosh;
    public string privacyUrl = "https://sites.google.com/view/zakriya-production-privacy";

    static TCAssets _i;
    public static TCAssets I { get { if (!_i) _i = Resources.Load<TCAssets>("TCAssets"); if (!_i) _i = CreateInstance<TCAssets>(); return _i; } }
}
