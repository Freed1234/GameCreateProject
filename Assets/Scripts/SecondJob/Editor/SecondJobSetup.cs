#if UNITY_EDITOR
using UnityEditor;

// Retained entry point now connects the new level-30 wind skills.
public static class SecondJobSetup
{
    [MenuItem("Tools/Game UI/Setup Archer Skills (Dash + 2nd Job)")]
    public static void Setup()
    {
        SecondArcherSetup.Setup();
    }
}
#endif
