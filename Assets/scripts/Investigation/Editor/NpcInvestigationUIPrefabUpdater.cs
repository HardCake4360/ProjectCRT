using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class NpcInvestigationUIPrefabUpdater
{
    private const string PrefabPath = "Assets/resources/prefab/NpcInvestigationUI.prefab";

    [MenuItem("Tools/Investigation/Update NPC Investigation UI Prefab")]
    public static void UpdatePrefabFromMenu()
    {
        UpdatePrefab();
    }

    private static void UpdatePrefab()
    {
        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(PrefabPath);
        if (prefabRoot == null)
        {
            Debug.LogWarning($"NpcInvestigationUIPrefabUpdater could not load prefab at {PrefabPath}.");
            return;
        }

        bool changed = false;
        try
        {
            InvestigationInteractionUI ui = prefabRoot.GetComponent<InvestigationInteractionUI>();
            RectTransform panelRoot = FindRectTransform(prefabRoot.transform, "Panel");
            if (ui == null || panelRoot == null)
            {
                Debug.LogWarning("NpcInvestigationUIPrefabUpdater could not find InvestigationInteractionUI or Panel.");
                return;
            }

            RectTransform attachmentBar = FindRectTransform(panelRoot, "AttachmentBar");
            TMP_Text attachmentText = attachmentBar != null ? FindComponent<TMP_Text>(attachmentBar, "AttachmentSummaryText") : null;
            RectTransform speedDialRoot = FindRectTransform(panelRoot, "SpeedDialRoot");
            RectTransform selectionListRoot = FindRectTransform(panelRoot, "SelectionListRoot");
            Button floatingActionButton = FindComponent<Button>(panelRoot, "FloatingActionButton");
            Button sendButton = FindComponent<Button>(prefabRoot.transform, "SendButton") ?? FindComponent<Button>(prefabRoot.transform, "TalkButton");
            if (sendButton != null && sendButton.gameObject.name == "TalkButton")
            {
                sendButton.gameObject.name = "SendButton";
                changed = true;
            }

            Button askTopicButton = FindComponent<Button>(prefabRoot.transform, "AskTopicButton");
            Button presentEvidenceButton = FindComponent<Button>(prefabRoot.transform, "PresentEvidenceButton");
            Button closeButton = FindComponent<Button>(prefabRoot.transform, "CloseButton");

            changed |= SetButtonLabel(sendButton, "Send");
            changed |= SetButtonLabel(floatingActionButton, "+");

            SerializedObject serializedUi = new SerializedObject(ui);
            changed |= SetObjectReference(serializedUi, "sendButton", sendButton);
            changed |= SetObjectReference(serializedUi, "askTopicButton", askTopicButton);
            changed |= SetObjectReference(serializedUi, "presentEvidenceButton", presentEvidenceButton);
            changed |= SetObjectReference(serializedUi, "closeButton", closeButton);
            changed |= SetObjectReference(serializedUi, "floatingActionButton", floatingActionButton);
            changed |= SetObjectReference(serializedUi, "speedDialRoot", speedDialRoot);
            changed |= SetObjectReference(serializedUi, "selectionListRoot", selectionListRoot);
            changed |= SetObjectReference(serializedUi, "attachmentBarRoot", attachmentBar);
            changed |= SetObjectReference(serializedUi, "attachmentSummaryText", attachmentText);
            serializedUi.ApplyModifiedPropertiesWithoutUndo();

            if (changed)
            {
                PrefabUtility.SaveAsPrefabAsset(prefabRoot, PrefabPath);
                Debug.Log("NpcInvestigationUI prefab references updated without changing authored layout.");
            }
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    private static bool SetButtonLabel(Button button, string labelText)
    {
        if (button == null)
        {
            return false;
        }

        TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
        if (label == null || label.text == labelText)
        {
            return false;
        }

        label.text = labelText;
        EditorUtility.SetDirty(label);
        return true;
    }

    private static bool SetObjectReference(SerializedObject serializedObject, string propertyName, Object value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null || property.objectReferenceValue == value)
        {
            return false;
        }

        property.objectReferenceValue = value;
        return true;
    }

    private static RectTransform FindRectTransform(Transform root, string objectName)
    {
        return FindComponent<RectTransform>(root, objectName);
    }

    private static T FindComponent<T>(Transform root, string objectName) where T : Component
    {
        foreach (T component in root.GetComponentsInChildren<T>(true))
        {
            if (component.gameObject.name == objectName)
            {
                return component;
            }
        }

        return null;
    }
}
