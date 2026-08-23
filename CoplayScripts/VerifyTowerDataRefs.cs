using System.Text;
using UnityEditor;
using UnityEngine;

// TowerData 등 ScriptableObject의 오브젝트 참조가 실제로 살아있는지 확인한다.
// GUID 충돌 사고 이후 검증용 - 필드 이름을 몰라도 되도록 SerializedObject로 훑는다.
public static class VerifyTowerDataRefs
{
    public static string Execute()
    {
        var report = new StringBuilder();
        int broken = 0;
        int checkedRefs = 0;

        string[] guids = AssetDatabase.FindAssets("t:ScriptableObject", new[] { "Assets/Data" });

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var so = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
            if (so == null)
            {
                report.AppendLine($"[LOAD FAIL] {path}");
                broken++;
                continue;
            }

            var serialized = new SerializedObject(so);
            SerializedProperty prop = serialized.GetIterator();

            while (prop.NextVisible(true))
            {
                if (prop.propertyType != SerializedPropertyType.ObjectReference)
                {
                    continue;
                }

                bool hasTarget = prop.objectReferenceInstanceIDValue != 0;
                if (!hasTarget)
                {
                    continue;
                }

                checkedRefs++;

                if (prop.objectReferenceValue == null)
                {
                    report.AppendLine($"[MISSING] {path} :: {prop.propertyPath}");
                    broken++;
                }
            }
        }

        report.Insert(0, $"검사한 참조 {checkedRefs}개 / 끊긴 참조 {broken}개\n");
        return report.ToString();
    }
}
