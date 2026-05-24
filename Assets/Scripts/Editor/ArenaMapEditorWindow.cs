#if UNITY_EDITOR
using SandBlast.Arena;
using UnityEditor;
using UnityEngine;

// 메뉴: SandBlast > Arena Map Editor
// 프리셋 에셋 생성 및 맵 브러시 유틸리티.
public class ArenaMapEditorWindow : EditorWindow
{
    [MenuItem("SandBlast/Arena Map Editor")]
    static void Open() => GetWindow<ArenaMapEditorWindow>("Arena Map Editor");

    // ── 프리셋 생성 ──────────────────────────────────────────────────

    void OnGUI()
    {
        GUILayout.Label("프리셋 에셋 생성", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Assets/MapTemplates/ 폴더에 MapTemplate 에셋을 저장합니다.", MessageType.Info);

        if (GUILayout.Button("Generate 1v1 Preset"))
            SavePreset(ArenaMapBuilder.CreateArena1v1Preset(), "Arena_1v1");

        if (GUILayout.Button("Generate 2v2 Preset"))
            SavePreset(ArenaMapBuilder.CreateArena2v2Preset(), "Arena_2v2");

        GUILayout.Space(12);
        GUILayout.Label("사용 방법", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "1. 위 버튼으로 에셋 생성\n" +
            "2. MapLoader 오브젝트의 MapTemplateAuthoring 에 에셋 할당\n" +
            "3. GamePrefabsAuthoring 에 WandItemPrefab 프리팹 할당",
            MessageType.None);
    }

    static void SavePreset(MapTemplate t, string assetName)
    {
        const string dir = "Assets/MapTemplates";
        if (!AssetDatabase.IsValidFolder(dir))
            AssetDatabase.CreateFolder("Assets", "MapTemplates");

        string path = $"{dir}/{assetName}.asset";
        var existing = AssetDatabase.LoadAssetAtPath<MapTemplate>(path);
        if (existing != null)
        {
            // 기존 에셋 덮어쓰기
            EditorUtility.CopySerialized(t, existing);
            EditorUtility.SetDirty(existing);
        }
        else
        {
            AssetDatabase.CreateAsset(t, path);
        }

        AssetDatabase.SaveAssets();
        EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<MapTemplate>(path));
        Debug.Log($"[ArenaMapEditor] Saved {path}");
    }
}
#endif
