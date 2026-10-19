using System;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace NoSleep
{
    public sealed partial class GameUI
    {
        GameObject titlePanel, slotsPanel, journalPanel, confirmPanel, gameOverPanel;
        RectTransform slotRows;
        Text slotsTitle, journalText, confirmText;
        Button continueButton, stimulantButton;
        Action confirmedAction;
        bool savingSlots;
        GameObject focusedMenu;
        public bool IsTitleOpen => titlePanel != null && titlePanel.activeSelf;
        bool ProgressMenuOpen => Active(titlePanel) || Active(slotsPanel) || Active(journalPanel) || Active(confirmPanel) || Active(gameOverPanel);
        static bool Active(GameObject obj) => obj != null && obj.activeSelf;

        Button MenuButton(string name, Transform parent, string text, Action action)
        { var button = Button(name, parent, text, action); button.navigation = new Navigation { mode = Navigation.Mode.Automatic }; return button; }
        RectTransform MenuPanel(string name, Vector2 size, string title)
        {
            var panel = Panel(name, canvas, new Color(0.025f, 0.045f, 0.065f, 0.99f));
            Place(panel, Vector2.one * .5f, Vector2.one * .5f, Vector2.one * .5f, Vector2.zero, size);
            var label = Label("Title", panel, title, 27, TextAnchor.UpperLeft); label.color = accent;
            Place(label.rectTransform, new Vector2(0, 1), Vector2.one, new Vector2(.5f, 1), new Vector2(0, -24), new Vector2(-48, 38));
            return panel;
        }
        void BuildProgressMenus(RectTransform status)
        {
            stimulantButton = MenuButton("Use Stimulant", status, "각성제 사용", () => { session.UseStimulant(); RefreshStatus(); });
            Place(stimulantButton.GetComponent<RectTransform>(), new Vector2(0, 0), new Vector2(0, 0), Vector2.zero, new Vector2(28, 78), new Vector2(220, 40));
            var journal = MenuButton("Journal", status, "여정 일지", ShowJournal);
            Place(journal.GetComponent<RectTransform>(), Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(266, 78), new Vector2(200, 40));

            var title = MenuPanel("Title Screen", new Vector2(620, 480), gameTitle); titlePanel = title.gameObject;
            var tagline = Label("Tagline", title, gameTagline, 22, TextAnchor.MiddleCenter);
            Place(tagline.rectTransform, new Vector2(0, 1), Vector2.one, new Vector2(.5f, 1), new Vector2(0, -110), new Vector2(-48, 70));
            var newGame = MenuButton("New Game", title, "새 게임", () => session.NewGame());
            Place(newGame.GetComponent<RectTransform>(), Vector2.one * .5f, Vector2.one * .5f, Vector2.one * .5f, new Vector2(0, 12), new Vector2(340, 48));
            continueButton = MenuButton("Continue", title, "이어하기", ContinueLatest);
            Place(continueButton.GetComponent<RectTransform>(), Vector2.one * .5f, Vector2.one * .5f, Vector2.one * .5f, new Vector2(0, -54), new Vector2(340, 48));
            var choose = MenuButton("Load Slots", title, "저장 선택", () => ShowSaveSlots(false));
            Place(choose.GetComponent<RectTransform>(), Vector2.one * .5f, Vector2.one * .5f, Vector2.one * .5f, new Vector2(0, -120), new Vector2(340, 48));

            var slots = MenuPanel("Save Slots", new Vector2(1000, 620), ""); slotsPanel = slots.gameObject;
            slotsTitle = slots.GetComponentInChildren<Text>();
            slotRows = new GameObject("Rows", typeof(RectTransform)).GetComponent<RectTransform>(); slotRows.SetParent(slots, false);
            Place(slotRows, Vector2.zero, Vector2.one, Vector2.one * .5f, new Vector2(0, -6), new Vector2(-40, -144));
            var back = MenuButton("Back", slots, "돌아가기", () => { slotsPanel.SetActive(false); if (IsTitleOpen) Focus(titlePanel); else if (Active(gameOverPanel)) Focus(gameOverPanel); });
            Place(back.GetComponent<RectTransform>(), new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-24, 22), new Vector2(180, 42));

            var book = MenuPanel("Journal", new Vector2(850, 590), "여정 · 단서 · 추억"); journalPanel = book.gameObject;
            journalText = ScrollText(book, "Entries", 20, 78, 84);
            var bookClose = MenuButton("Close", book, "닫기 (Esc)", () => journalPanel.SetActive(false));
            Place(bookClose.GetComponent<RectTransform>(), new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-24, 22), new Vector2(180, 42));

            var over = MenuPanel("Game Over", new Vector2(620, 370), "더 버틸 체력이 남아 있지 않다"); gameOverPanel = over.gameObject;
            var recover = MenuButton("Load", over, "저장 불러오기", () => ShowSaveSlots(false));
            Place(recover.GetComponent<RectTransform>(), Vector2.one * .5f, Vector2.one * .5f, Vector2.one * .5f, new Vector2(0, 12), new Vector2(340, 48));
            var restart = MenuButton("Restart", over, "새 게임", () => Confirm("현재 여정을 끝내고 새 게임을 시작할까요?", () => session.NewGame()));
            Place(restart.GetComponent<RectTransform>(), Vector2.one * .5f, Vector2.one * .5f, Vector2.one * .5f, new Vector2(0, -58), new Vector2(340, 48));

            var confirm = MenuPanel("Confirm", new Vector2(620, 300), "확인"); confirmPanel = confirm.gameObject;
            confirmText = Label("Question", confirm, "", 21, TextAnchor.MiddleCenter);
            Place(confirmText.rectTransform, new Vector2(0, 1), Vector2.one, new Vector2(.5f, 1), new Vector2(0, -80), new Vector2(-48, 90));
            var yes = MenuButton("Confirm", confirm, "확인", () => { var action = confirmedAction; confirmedAction = null; confirmPanel.SetActive(false); action?.Invoke(); });
            Place(yes.GetComponent<RectTransform>(), Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(50, 30), new Vector2(220, 42));
            var no = MenuButton("Cancel", confirm, "취소", () => { confirmedAction = null; confirmPanel.SetActive(false); });
            Place(no.GetComponent<RectTransform>(), new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-50, 30), new Vector2(220, 42));
            CloseMenus();
        }
        Text ScrollText(RectTransform parent, string name, int size, float top, float bottom)
        {
            var scroll = new GameObject(name + " Scroll", typeof(RectTransform), typeof(ScrollRect)).GetComponent<RectTransform>(); scroll.SetParent(parent, false);
            Place(scroll, Vector2.zero, Vector2.one, Vector2.one * .5f, new Vector2(0, (bottom - top) / 2), new Vector2(-56, -top - bottom));
            var viewport = Panel("Viewport", scroll, new Color(0, 0, 0, .05f)); viewport.gameObject.AddComponent<RectMask2D>();
            Place(viewport, Vector2.zero, Vector2.one, Vector2.one * .5f, Vector2.zero, Vector2.zero);
            var text = Label(name, viewport, "", size, TextAnchor.UpperLeft); text.verticalOverflow = VerticalWrapMode.Overflow;
            Place(text.rectTransform, new Vector2(0, 1), Vector2.one, new Vector2(0, 1), Vector2.zero, Vector2.zero);
            text.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var component = scroll.GetComponent<ScrollRect>(); component.viewport = viewport; component.content = text.rectTransform;
            component.horizontal = false; component.movementType = ScrollRect.MovementType.Clamped; component.scrollSensitivity = 24;
            return text;
        }
        public void ShowTitle()
        {
            CloseMenus(); titlePanel.SetActive(true);
            continueButton.interactable = session.SaveSlots.ListSlots().Any(x => x.valid && session.CanLoadMap(x.data.mapId));
            Focus(titlePanel);
        }
        public void ShowStatus()
        { if (session.IsGameplayBusy || IsTitleOpen || session.State.player.health <= 0) return; CloseMenus(); RefreshStatus(); stimulantButton.gameObject.SetActive(session.HorrorEnabled); statusPanel.SetActive(true); Focus(statusPanel); }
        public void ShowGameOver() { CloseMenus(); gameOverPanel.SetActive(true); Focus(gameOverPanel); }
        public void CloseMenus()
        {
            foreach (var panel in new[] { statusPanel, titlePanel, slotsPanel, journalPanel, confirmPanel, gameOverPanel }) if (panel != null) panel.SetActive(false);
            confirmedAction = null;
        }
        public void ShowSaveSlots(bool saving)
        {
            if (session.IsGameplayBusy || dialogueOpen || (saving && (IsTitleOpen || session.State.player.health <= 0))) return;
            statusPanel.SetActive(false); journalPanel.SetActive(false); confirmPanel.SetActive(false); savingSlots = saving;
            slotsPanel.SetActive(true); slotsPanel.transform.SetAsLastSibling(); slotsTitle.text = saving ? "저장 슬롯" : "불러오기 슬롯";
            RefreshSlots(); Focus(slotsPanel);
        }
        void RefreshSlots()
        {
            for (int i = slotRows.childCount - 1; i >= 0; i--) { var row = slotRows.GetChild(i); row.gameObject.SetActive(false); Destroy(row.gameObject); }
            foreach (var info in session.SaveSlots.ListSlots())
            {
                int slot = info.slot;
                string summary = "슬롯 " + (slot + 1) + "   ";
                if (info.valid)
                {
                    var data = info.data;
                    string stamp = data.savedUtcTicks > 0 ? new DateTime(data.savedUtcTicks, DateTimeKind.Utc).ToLocalTime().ToString("yyyy-MM-dd HH:mm") : "이전 버전 저장";
                    summary += stamp + " · " + (string.IsNullOrEmpty(data.mapDisplayName) ? data.mapId : data.mapDisplayName) + " · " + ClockText(data.state);
                    summary += "\n" + data.objectiveTitle;
                    if (!session.CanLoadMap(data.mapId)) summary += " (현재 게임에 없는 맵)";
                }
                else summary += info.exists || !string.IsNullOrEmpty(info.error) ? "저장 파일을 읽을 수 없음" : "빈 슬롯";
                var row = Panel("Slot " + slot, slotRows, new Color(.08f, .12f, .15f));
                Place(row, new Vector2(0, 1), Vector2.one, new Vector2(.5f, 1), new Vector2(0, -slot * 76), new Vector2(0, 68));
                var label = Label("Summary", row, summary, 17, TextAnchor.MiddleLeft);
                Place(label.rectTransform, new Vector2(0, 0), new Vector2(1, 1), new Vector2(.5f, .5f), new Vector2(-135, 0), new Vector2(-294, -8));
                var primary = MenuButton("Primary", row, savingSlots ? "저장" : "불러오기", () => { if (savingSlots) RequestSave(slot); else session.Load(slot); });
                Place(primary.GetComponent<RectTransform>(), new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(-126, 0), new Vector2(116, 46));
                primary.interactable = savingSlots || (info.valid && session.CanLoadMap(info.data.mapId));
                var backup = MenuButton("Backup", row, "이전 백업", () => session.Load(slot, true));
                Place(backup.GetComponent<RectTransform>(), new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(-6, 0), new Vector2(112, 46));
                backup.interactable = !savingSlots && info.backupValid;
            }
        }
        void RequestSave(int slot)
        {
            if (session.IsGameplayBusy || dialogueOpen || IsTitleOpen || session.State.player.health <= 0) return;
            Action save = () => { if (session.Save(slot) && !session.HasPendingSave && Active(slotsPanel)) RefreshSlots(); };
            var info = session.SaveSlots.Inspect(slot);
            if (info.exists || info.hasBackup) Confirm("슬롯 " + (slot + 1) + "의 저장을 덮어쓸까요?", save); else save();
        }
        void OnSaveCompleted(int slot, bool succeeded)
        {
            if (Active(slotsPanel)) { RefreshSlots(); Focus(slotsPanel); }
        }
        void Confirm(string message, Action action)
        { confirmText.text = message; confirmedAction = action; confirmPanel.SetActive(true); confirmPanel.transform.SetAsLastSibling(); SyncModalNavigation(); }
        void ContinueLatest()
        {
            var latest = session.SaveSlots.ListSlots().Where(x => x.valid && session.CanLoadMap(x.data.mapId)).OrderByDescending(x => x.data.savedUtcTicks).FirstOrDefault();
            if (latest != null) session.Load(latest.slot);
        }
        void ShowJournal()
        {
            if (session.IsGameplayBusy || dialogueOpen || IsTitleOpen) return;
            CloseMenus(); var state = session.State; var text = new StringBuilder();
            text.AppendLine("현재 목표\n" + state.journey.objectiveTitle + "\n"); text.AppendLine("동행\n" + PartyText() + "\n");
            text.AppendLine("단서와 기록"); foreach (var entry in state.journey.journal) text.AppendLine(entry.title + "\n" + entry.text + "\n");
            if (state.journey.journal.Count == 0) text.AppendLine("아직 기록이 없다.\n");
            text.AppendLine("좋은 추억"); foreach (var memory in state.journey.memories) text.AppendLine(memory.title + "\n" + memory.text + "\n");
            if (state.journey.memories.Count == 0) text.AppendLine("아직 함께 밤을 보낸 기억이 없다.");
            journalText.text = text.ToString(); journalPanel.SetActive(true); Focus(journalPanel);
        }
        void BackFromMenu()
        {
            if (Active(confirmPanel)) { confirmPanel.SetActive(false); confirmedAction = null; }
            else if (Active(slotsPanel)) { slotsPanel.SetActive(false); if (IsTitleOpen) Focus(titlePanel); else if (Active(gameOverPanel)) Focus(gameOverPanel); }
            else { statusPanel.SetActive(false); journalPanel.SetActive(false); }
        }
        static void Focus(GameObject panel)
        { if (EventSystem.current != null) { var button = panel.GetComponentsInChildren<Button>().FirstOrDefault(x => x.interactable); if (button != null) EventSystem.current.SetSelectedGameObject(button.gameObject); } }
        void SyncModalNavigation()
        {
            var panels = new[] { statusPanel, titlePanel, gameOverPanel, journalPanel, slotsPanel, confirmPanel };
            GameObject top = panels.LastOrDefault(Active);
            foreach (var panel in panels)
            {
                if (panel == null) continue;
                var group = panel.GetComponent<CanvasGroup>(); if (group == null) group = panel.AddComponent<CanvasGroup>();
                group.interactable = panel == top; group.blocksRaycasts = panel == top;
            }
            if (top != focusedMenu)
            {
                focusedMenu = top;
                if (top != null) Focus(top);
                else if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            }
        }
        static string ClockText(GameState state) => "Day " + SurvivalRules.Day(state) + "  " + SurvivalRules.Hour(state).ToString("00") + ":" + ((state.survival.startHour * 60L + state.survival.elapsedMinutes) % 60).ToString("00") + (SurvivalRules.IsNight(state) ? " 밤" : " 낮");
        string PartyText() => session.State.party.activeCompanions.Count == 0 ? "혼자" : string.Join(", ", session.State.party.activeCompanions.ConvertAll(session.CompanionName));
        static string ActionLabel(string id)
        { switch (id) { case "strike": return "공격"; case "guard": return "방어"; case "flee": return "물러나기"; case "aggressive": return "과격한 공격"; case "observe": return "관찰"; default: return id; } }
    }
}
