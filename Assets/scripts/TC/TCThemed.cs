using UnityEngine;

/// polish3: marks a UI object the palette theme has already styled (baked by TCPolish3Bake or styled
/// when built at runtime), so the runtime theme pass never overwrites colours edited in the Unity editor.
[DisallowMultipleComponent]
public class TCThemed : MonoBehaviour
{
    public int version = TCTheme.Version;
    public string role;
}
