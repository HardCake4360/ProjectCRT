using System;
using System.Collections.Generic;
using UnityEngine;

namespace NoSleep.Editor
{
    public static class EventTemplates
    {
        // Factories return unsaved editor assets. Applying a template copies its
        // pages through EventEditOperations.InsertPages and never replaces data.
        public static EventTemplateDefinition Create(EventTemplateKind kind, EventTemplateReferences references = null)
        {
            if (!Enum.IsDefined(typeof(EventTemplateKind), kind)) throw new ArgumentOutOfRangeException(nameof(kind));
            var values = references ?? new EventTemplateReferences();
            var result = ScriptableObject.CreateInstance<EventTemplateDefinition>();
            var page = new EventPage { pageId = Guid.NewGuid().ToString("N"), legacyPageIndex = -1 };
            result.pages.Add(page);
            EventCommand Dialogue(string text) => new EventCommand { type = EventCommandType.ShowDialogue, speaker = values.speaker, text = text };
            switch (kind)
            {
                case EventTemplateKind.NpcDialogue:
                    result.displayName = "NPC 대화"; result.description = "상호작용하면 대사를 표시합니다. 화자와 문장을 편집하세요.";
                    page.name = "대화"; page.commands.Add(Dialogue(values.text)); break;
                case EventTemplateKind.OneTimeReward:
                    result.displayName = "일회 보상"; result.description = "지급은 GrantOnce의 거래 ID로 보호하며 대사까지 마치면 페이지가 한 번만 완료됩니다.";
                    page.name = "보상"; page.runOnce = true;
                    var effects = new List<StateMutation> { new StateMutation { kind = StateMutationKind.AddGold, intValue = values.rewardGold } };
                    if (values.rewardItemAmount != 0) effects.Add(new StateMutation { kind = StateMutationKind.AddItem, key = values.rewardItemId, intValue = values.rewardItemAmount });
                    page.commands.Add(new EventCommand { type = EventCommandType.GrantOnce, mutations = effects });
                    page.commands.Add(Dialogue(values.text)); break;
                case EventTemplateKind.ConditionalBranch:
                    result.displayName = "조건부 문"; result.description = "조건이 참이면 목적지로 이동하고 거짓이면 잠긴 응답을 표시합니다. 조건 키와 맵/타일을 연결하세요.";
                    page.name = "조건부 이동"; page.blocksMovement = false;
                    page.commands.Add(new EventCommand { type = EventCommandType.IfCondition, condition = EventEditOperations.Clone(values.condition), targetLabel = "locked" });
                    page.commands.Add(new EventCommand { type = EventCommandType.TransferPlayer, map = values.destinationMap, cell = values.destinationCell });
                    page.commands.Add(new EventCommand { type = EventCommandType.Jump, targetLabel = "done" });
                    page.commands.Add(new EventCommand { type = EventCommandType.Label, label = "locked" });
                    page.commands.Add(Dialogue("아직 지나갈 수 없습니다."));
                    page.commands.Add(new EventCommand { type = EventCommandType.Label, label = "done" }); break;
                case EventTemplateKind.StayAwake:
                    result.displayName = "밤샘 활동"; result.description = "연결한 밤샘 활동을 시도합니다. 동료/시간 조건이 실패하면 후속 대사와 완료를 진행하지 않습니다.";
                    page.name = "밤샘";
                    page.commands.Add(new EventCommand { type = EventCommandType.StayAwake, nightActivity = values.nightActivity });
                    page.commands.Add(Dialogue(values.text)); break;
                case EventTemplateKind.ContactCombat:
                    result.displayName = "접촉 전투"; result.description = "보이는 적과 접촉해 전투합니다. 승리만 defeated 셀프 스위치와 페이지 완료를 기록합니다.";
                    page.name = "접촉 전투"; page.trigger = EventTrigger.PlayerTouch; page.blocksMovement = false; page.runOnce = true;
                    page.commands.Add(new EventCommand { type = EventCommandType.StartEncounter, enemy = values.enemy, key = "defeated" }); break;
                case EventTemplateKind.CommonCall:
                    result.displayName = "공통 이벤트 호출"; result.description = "공통 에셋의 매개변수 기본값으로 타입 인자를 생성합니다. 에셋을 연결한 뒤 실제 값을 편집하세요.";
                    page.name = "공통 호출";
                    var call = new EventCommand { type = EventCommandType.CallCommon, commonEvent = values.commonEvent };
                    if (values.commonEvent?.parameters != null) foreach (var parameter in values.commonEvent.parameters)
                        if (parameter != null) call.arguments.Add(new EventArgument { name = parameter.name, type = parameter.type,
                            intValue = parameter.intValue, boolValue = parameter.boolValue, stringValue = parameter.stringValue });
                    page.commands.Add(call); break;
            }
            page.commands.Add(new EventCommand { type = EventCommandType.End });
            EventProgram.EnsureCommandIds(page.commands);
            return result;
        }
    }
}
