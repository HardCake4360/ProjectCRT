using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace NoSleep
{
    public sealed partial class GameUI : MonoBehaviour, IEventPresentation
    {
        public bool IsModalOpen => dialogueOpen || (statusPanel != null && statusPanel.activeSelf) || ProgressMenuOpen;
        public bool IsDialogueOpen => dialogueOpen;
        public float charactersPerSecond = 35;
        public string gameTitle = "NO SLEEP";
        public string gameTagline = "잠들 수 없는 세상에서, 다음 아침까지.";
        GameSession session;
        Font font;
        RectTransform canvas;
        GameObject dialoguePanel, choicePanel, statusPanel;
        Text speakerText, bodyText, dialogueHint, hudText, interactionText, noticeText, statusText;
        readonly List<Button> choiceButtons = new List<Button>();
        readonly List<GameObject> choiceObjects = new List<GameObject>();
        readonly Color ink = new Color(0.9f, 0.94f, 0.94f);
        readonly Color accent = new Color(0.94f, 0.72f, 0.38f);
        bool dialogueOpen;
        int selectedChoice = -1;
        float noticeUntil;

        public void Configure(GameSession game)
        {
            if (session != null) session.SaveCompleted -= OnSaveCompleted;
            session = game;
            if (session != null) session.SaveCompleted += OnSaveCompleted;
            if (canvas == null) Build();
        }
        void OnDestroy() { if (session != null) session.SaveCompleted -= OnSaveCompleted; }
        void Build()
        {
            font = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "Apple SD Gothic Neo", "Noto Sans CJK KR", "Arial" }, 20);
            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var root = new GameObject("NoSleep Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            root.transform.SetParent(transform, false); canvas = root.GetComponent<RectTransform>();
            root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720); scaler.matchWidthOrHeight = 0.5f;
            if (FindAnyObjectByType<EventSystem>() == null)
            {
                var events = new GameObject("NoSleep Event System", typeof(EventSystem), typeof(InputSystemUIInputModule));
                events.transform.SetParent(transform, false);
            }
            var header = Panel("HUD", canvas, new Color(0.04f, 0.07f, 0.09f, 0.88f));
            Place(header, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -16), new Vector2(-32, 98));
            hudText = Label("Map", header, gameTitle, 20, TextAnchor.UpperLeft);
            Place(hudText.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-32, -20));
            var controls = Label("Controls", canvas, "WASD / 방향키 이동   Shift 달리기   E 상호작용   Tab 상태   J 일지   F5 저장   F9 불러오기", 16, TextAnchor.LowerCenter);
            Place(controls.rectTransform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, 14), new Vector2(-24, 26));
            interactionText = Label("Interaction", canvas, "", 21, TextAnchor.MiddleCenter); interactionText.color = accent;
            Place(interactionText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -85), new Vector2(500, 40));
            noticeText = Label("Notice", canvas, "", 18, TextAnchor.MiddleCenter); noticeText.color = accent;
            Place(noticeText.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -122), new Vector2(1080, 50));

            var dialogue = Panel("Dialogue", canvas, new Color(0.035f, 0.06f, 0.08f, 0.97f)); dialoguePanel = dialogue.gameObject;
            Place(dialogue, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, 56), new Vector2(-80, 198));
            speakerText = Label("Speaker", dialogue, "", 21, TextAnchor.UpperLeft); speakerText.color = accent;
            Place(speakerText.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -18), new Vector2(-48, 34));
            bodyText = Label("Body", dialogue, "", 21, TextAnchor.UpperLeft);
            Place(bodyText.rectTransform, new Vector2(0, 0), new Vector2(1, 1), new Vector2(0.5f, 0.5f), new Vector2(0, -5), new Vector2(-48, -92));
            dialogueHint = Label("Hint", dialogue, "E / Enter / Space — 계속", 15, TextAnchor.LowerRight); dialogueHint.color = accent;
            Place(dialogueHint.rectTransform, Vector2.zero, new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, 12), new Vector2(-48, 24));
            var choices = Panel("Choices", canvas, new Color(0.04f, 0.08f, 0.1f, 0.97f)); choicePanel = choices.gameObject;
            Place(choices, new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-40, 266), new Vector2(460, 120));
            var status = Panel("Status", canvas, new Color(0.035f, 0.06f, 0.08f, 0.98f)); statusPanel = status.gameObject;
            Place(status, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(720, 580));
            var title = Label("Title", status, "상태 / STATUS", 27, TextAnchor.UpperLeft); title.color = accent;
            Place(title.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -24), new Vector2(-56, 42));
            statusText = ScrollText(status, "Stats", 20, 76, 138);
            var save = MenuButton("Save", status, "저장 슬롯", () => ShowSaveSlots(true));
            Place(save.GetComponent<RectTransform>(), new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0), new Vector2(28, 24), new Vector2(160, 42));
            var load = MenuButton("Load", status, "불러오기", () => ShowSaveSlots(false));
            Place(load.GetComponent<RectTransform>(), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 24), new Vector2(160, 42));
            var close = MenuButton("Close", status, "닫기 (Tab)", () => statusPanel.SetActive(false));
            Place(close.GetComponent<RectTransform>(), new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-28, 24), new Vector2(160, 42));
            dialoguePanel.SetActive(false); choicePanel.SetActive(false); statusPanel.SetActive(false);
            BuildProgressMenus(status);
        }
        void Update()
        {
            if (session == null || !session.IsReady) return;
            var stats = session.State.player;
            hudText.text = gameTitle + "  /  " + session.Map.Map.displayName + "\nHP " + stats.health + "/" + stats.maxHealth + "    정신력 " + stats.sanity + "/" + stats.maxSanity + "    소지금 " + stats.gold;
            if (session.HorrorEnabled) hudText.text += "    " + ClockText(session.State) + "    피로 " + Mathf.RoundToInt(session.State.survival.fatigue) + " / 유효 " + Mathf.RoundToInt(SurvivalRules.EffectiveFatigue(session.State)) + "\n목표: " + session.State.journey.objectiveTitle;
            interactionText.text = !session.IsInputBlocked && session.Player.InteractionTarget != null ? "[ E ]  " + session.Player.InteractionTarget.DisplayName : "";
            if (Time.unscaledTime > noticeUntil) noticeText.text = "";
            if (!dialogueOpen && !session.IsGameplayBusy)
            {
                if (session.State.player.health <= 0 && !ProgressMenuOpen) ShowGameOver();
                if (GameInput.Cancel) BackFromMenu();
                if (!ProgressMenuOpen && session.State.player.health > 0)
                {
                    if (GameInput.Status) { if (statusPanel.activeSelf) statusPanel.SetActive(false); else ShowStatus(); }
                    if (GameInput.Journal) ShowJournal();
                    if (GameInput.QuickSave) RequestSave(0);
                }
                if (GameInput.QuickLoad) ShowSaveSlots(false);
            }
            SyncModalNavigation();
        }
        void RefreshStatus()
        {
            var stats = session.State.player;
            var text = new StringBuilder(); text.AppendLine(stats.name); text.AppendLine("체력   " + stats.health + " / " + stats.maxHealth);
            text.AppendLine("정신력   " + stats.sanity + " / " + stats.maxSanity); text.AppendLine("소지금   " + stats.gold);
            if (session.HorrorEnabled)
            {
                text.AppendLine("힘   " + stats.strength + "    " + ClockText(session.State));
                text.AppendLine("피로   " + Mathf.RoundToInt(session.State.survival.fatigue) + "   유효 피로   " + Mathf.RoundToInt(SurvivalRules.EffectiveFatigue(session.State)));
                text.AppendLine("동행   " + PartyText()); text.AppendLine("배운 행동   " + string.Join(", ", session.State.unlockedActions.ConvertAll(ActionLabel)));
            }
            text.AppendLine("\n소지품"); bool any = false;
            foreach (var item in session.State.inventory) if (item != null && item.value > 0) { text.AppendLine("  " + (session.Stimulant != null && item.key == session.Stimulant.itemId ? session.Stimulant.displayName : item.key) + " × " + item.value); any = true; }
            if (!any) text.AppendLine("  없음"); statusText.text = text.ToString();
        }
        public void Notice(string message) { if (noticeText != null) noticeText.text = message; noticeUntil = Time.unscaledTime + 5; }
        public IEnumerator ShowDialogue(string speaker, string text)
        {
            BeginDialogue(); speakerText.text = speaker ?? ""; bodyText.text = ""; dialogueHint.text = "E / Enter / Space — 빠르게 표시 / 계속";
            text ??= "";
            try
            {
                yield return null; // Do not consume the interaction press that opened this line.
                float visible = 0;
                while (visible < text.Length)
                {
                    if (GameInput.Confirm || charactersPerSecond <= 0) visible = text.Length;
                    else visible += Time.unscaledDeltaTime * charactersPerSecond;
                    bodyText.text = text.Substring(0, Mathf.Min(text.Length, Mathf.FloorToInt(visible)));
                    yield return null;
                }
                bodyText.text = text; dialogueHint.text = "E / Enter / Space — 계속";
                while (!GameInput.Confirm) yield return null;
                yield return null;
            }
            finally { CloseDialogue(); }
        }
        public IEnumerator ShowChoices(string prompt, List<string> choices, Action<int> onSelected)
        {
            if (choices == null || choices.Count == 0) throw new ArgumentException("Choices cannot be empty.");
            BeginDialogue(); speakerText.text = "선택"; bodyText.text = prompt ?? "";
            dialogueHint.text = "↑ / ↓ 선택   E / Enter 확인"; selectedChoice = -1;
            ClearChoices(); choicePanel.SetActive(true);
            int count = choices.Count;
            var rect = choicePanel.GetComponent<RectTransform>(); rect.sizeDelta = new Vector2(460, 16 + count * 48);
            for (int i = 0; i < count; i++)
            {
                int captured = i; var button = Button("Choice " + i, rect, choices[i], () => selectedChoice = captured);
                Place(button.GetComponent<RectTransform>(), new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -8 - i * 48), new Vector2(-16, 42));
                choiceButtons.Add(button); choiceObjects.Add(button.gameObject);
            }
            int focus = 0; Highlight(focus);
            try
            {
                yield return null;
                while (selectedChoice < 0)
                {
                    int delta = GameInput.ChoiceDelta;
                    if (delta != 0) { focus = (focus + delta + count) % count; Highlight(focus); }
                    if (GameInput.Confirm) selectedChoice = focus;
                    yield return null;
                }
                onSelected?.Invoke(selectedChoice);
            }
            finally { ClearChoices(); CloseDialogue(); }
        }
        void Highlight(int index)
        {
            for (int i = 0; i < choiceButtons.Count; i++) choiceButtons[i].GetComponent<Image>().color = i == index ? new Color(0.3f, 0.24f, 0.15f) : new Color(0.1f, 0.15f, 0.18f);
        }
        void BeginDialogue() { CloseMenus(); dialogueOpen = true; dialoguePanel.SetActive(true); interactionText.text = ""; }
        void CloseDialogue() { dialogueOpen = false; if (dialoguePanel != null) dialoguePanel.SetActive(false); if (choicePanel != null) choicePanel.SetActive(false); }
        void ClearChoices() { foreach (var obj in choiceObjects) if (obj != null) Destroy(obj); choiceObjects.Clear(); choiceButtons.Clear(); }
        public void CancelPresentation() { StopAllCoroutines(); ClearChoices(); CloseDialogue(); }
        void OnDisable() { if (session != null && session.Events != null) session.Events.Cancel(); CancelPresentation(); CloseMenus(); }
        RectTransform Panel(string name, Transform parent, Color color)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(Image)); obj.transform.SetParent(parent, false);
            obj.GetComponent<Image>().color = color; return obj.GetComponent<RectTransform>();
        }
        Text Label(string name, Transform parent, string value, int size, TextAnchor alignment)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(Text)); obj.transform.SetParent(parent, false);
            var text = obj.GetComponent<Text>(); text.font = font; text.fontSize = size; text.color = ink; text.text = value;
            text.alignment = alignment; text.raycastTarget = false; text.supportRichText = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap; text.verticalOverflow = VerticalWrapMode.Truncate; return text;
        }
        Button Button(string name, Transform parent, string label, Action clicked)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button)); obj.transform.SetParent(parent, false);
            var button = obj.GetComponent<Button>(); button.targetGraphic = obj.GetComponent<Image>(); button.navigation = new Navigation { mode = Navigation.Mode.None };
            obj.GetComponent<Image>().color = new Color(0.1f, 0.15f, 0.18f);
            var text = Label("Label", obj.transform, label, 18, TextAnchor.MiddleCenter);
            Place(text.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-12, 0));
            button.onClick.AddListener(() => clicked()); return button;
        }
        static void Place(RectTransform rect, Vector2 min, Vector2 max, Vector2 pivot, Vector2 position, Vector2 size)
        { rect.anchorMin = min; rect.anchorMax = max; rect.pivot = pivot; rect.anchoredPosition = position; rect.sizeDelta = size; }
    }
}
