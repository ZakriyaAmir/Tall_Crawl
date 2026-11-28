using UnityEditor;

/// <summary>
/// Ensures MCP for Unity is configured to auto-start whenever the project opens.
/// </summary>
[InitializeOnLoad]
public static class TCMcpAutoStart
{
    private const string AutoStartKey = "MCPForUnity.AutoStartOnLoad";

    static TCMcpAutoStart()
    {
        if (!EditorPrefs.GetBool(AutoStartKey, false))
        {
            EditorPrefs.SetBool(AutoStartKey, true);
        }
    }
}
