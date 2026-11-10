#if UNITY_EDITOR
using UnityEditor;

// Retained menu and class for existing workflows. The former Lv30 setup is retired.
public static class SecondJobSetup
{
    [MenuItem("Tools/Game UI/Setup Archer Skills (Dash + 2nd Job)")]
    public static void Setup()
    {
        FirstArcherSetup.Setup();
    }
}
#endif
