using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(AffectPulseGraphic))]
public class AffectPulseGraphicEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Effect Preview", EditorStyles.boldLabel);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Pending Dark"))
            {
                ForEachTarget(graphic => graphic.PreviewPendingState());
            }

            if (GUILayout.Button("Arrival Flash"))
            {
                ForEachTarget(graphic => graphic.PreviewArrivalFlash());
            }

            if (GUILayout.Button("Reset"))
            {
                ForEachTarget(graphic => graphic.PreviewResetState());
            }
        }

        EditorGUILayout.HelpBox(
            "Use these buttons to preview the affect waiting color and value-arrival flash directly in the editor.",
            MessageType.Info);
    }

    private void ForEachTarget(System.Action<AffectPulseGraphic> action)
    {
        foreach (Object targetObject in targets)
        {
            AffectPulseGraphic graphic = targetObject as AffectPulseGraphic;
            if (graphic == null)
            {
                continue;
            }

            Undo.RecordObject(graphic, "Preview Affect Pulse Effect");
            action(graphic);
            EditorUtility.SetDirty(graphic);
            graphic.SetVerticesDirty();
        }

        SceneView.RepaintAll();
    }
}
