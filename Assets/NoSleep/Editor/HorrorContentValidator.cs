using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace NoSleep.Editor
{
    public static class HorrorContentValidator
    {
        public static List<string> CollectErrors()
        {
            var definitions = new List<(ScriptableObject definition, string assetPath)>();
            if (!AssetDatabase.IsValidFolder("Assets/NoSleep")) return new List<string>();
            Scan<SurvivalSettings>(definitions);
            Scan<ItemEffectDefinition>(definitions);
            Scan<NightActivityDefinition>(definitions);
            Scan<CompanionDefinition>(definitions);
            Scan<EnemyDefinition>(definitions);
            return ValidateDefinitions(definitions);
        }

        // A public pure validation entry point also permits testing unsaved authoring data.
        public static List<string> ValidateDefinition(ScriptableObject definition, string assetPath = null)
        {
            var errors = new List<string>();
            string path = string.IsNullOrWhiteSpace(assetPath) ? definition != null ? definition.GetType().Name : "Horror content" : assetPath;
            if (definition == null) { errors.Add(path + ": 에셋 데이터가 없습니다."); return errors; }
            switch (definition)
            {
                case SurvivalSettings settings:
                    Id(settings.profileId, "profileId", path, errors);
                    if (!Enum.IsDefined(typeof(TimeMode), settings.timeMode)) Error("timeMode", "정의된 시간 진행 방식을 선택해야 합니다", path, errors);
                    Range(settings.initialHour, 0, 23, "initialHour", path, errors);
                    Range(settings.campaignDays, 1, 100, "campaignDays", path, errors);
                    FiniteRange(settings.realSecondsPerGameMinute, 0.1f, float.MaxValue, "realSecondsPerGameMinute", path, errors);
                    FiniteRange(settings.fatiguePerMinute, 0, 100, "fatiguePerMinute", path, errors);
                    FiniteRange(settings.nightMultiplier, 0, 100, "nightMultiplier", path, errors);
                    FiniteRange(settings.criticalFatigue, 1, 100, "criticalFatigue", path, errors);
                    FiniteRange(settings.minimumMovementMultiplier, 0.1f, 1, "minimumMovementMultiplier", path, errors);
                    Range(settings.healthLossPerDay, 0, int.MaxValue, "healthLossPerDay", path, errors);
                    Range(settings.strengthLossPerDay, 0, int.MaxValue, "strengthLossPerDay", path, errors);
                    Range(settings.minMaxHealth, 1, int.MaxValue, "minMaxHealth", path, errors);
                    Range(settings.minStrength, 1, int.MaxValue, "minStrength", path, errors);
                    FiniteRange(settings.enemyDeclinePerDay, 0, 1, "enemyDeclinePerDay", path, errors);
                    FiniteRange(settings.minEnemyScale, 0.01f, 1, "minEnemyScale", path, errors);
                    break;
                case ItemEffectDefinition item:
                    Id(item.itemId, "itemId", path, errors); Name(item.displayName, path, errors);
                    Range(item.healthCost, 0, int.MaxValue, "healthCost", path, errors);
                    Range(item.sanityCost, 0, int.MaxValue, "sanityCost", path, errors);
                    FiniteRange(item.awakeningRelief, 0, 100, "awakeningRelief", path, errors);
                    Range(item.durationMinutes, 1, int.MaxValue, "durationMinutes", path, errors);
                    break;
                case NightActivityDefinition activity:
                    Id(activity.activityId, "activityId", path, errors); Name(activity.displayName, path, errors);
                    Range(activity.dawnHour, 0, 23, "dawnHour", path, errors);
                    FiniteRange(activity.fatigueMultiplier, 0, 100, "fatigueMultiplier", path, errors);
                    Range(activity.healthRestore, 0, int.MaxValue, "healthRestore", path, errors);
                    Range(activity.sanityRestore, 0, int.MaxValue, "sanityRestore", path, errors);
                    break;
                case CompanionDefinition companion:
                    Id(companion.companionId, "companionId", path, errors); Name(companion.displayName, path, errors);
                    FiniteRange(companion.color.r, 0, 1, "color.r", path, errors);
                    FiniteRange(companion.color.g, 0, 1, "color.g", path, errors);
                    FiniteRange(companion.color.b, 0, 1, "color.b", path, errors);
                    FiniteRange(companion.color.a, 0, 1, "color.a", path, errors);
                    break;
                case EnemyDefinition enemy:
                    Id(enemy.enemyId, "enemyId", path, errors); Name(enemy.displayName, path, errors);
                    Range(enemy.baseHealth, 1, int.MaxValue, "baseHealth", path, errors);
                    Range(enemy.baseAttack, 1, int.MaxValue, "baseAttack", path, errors);
                    break;
                default:
                    Error("type", "호러 RPG 제작 데이터 형식이 아닙니다", path, errors);
                    break;
            }
            return errors;
        }

        public static List<string> ValidateDefinitions(IEnumerable<(ScriptableObject definition, string assetPath)> definitions)
        {
            var errors = new List<string>();
            var byType = new Dictionary<Type, Dictionary<string, string>>();
            if (definitions == null) { errors.Add("Horror content: 제작 데이터 목록이 없습니다."); return errors; }
            foreach (var entry in definitions)
            {
                var value = entry.definition;
                string path = entry.assetPath ?? (value != null ? value.GetType().Name : "Horror content");
                errors.AddRange(ValidateDefinition(value, path));
                if (value == null) continue;
                string id = value switch
                {
                    SurvivalSettings settings => settings.profileId,
                    ItemEffectDefinition item => item.itemId,
                    NightActivityDefinition activity => activity.activityId,
                    CompanionDefinition companion => companion.companionId,
                    EnemyDefinition enemy => enemy.enemyId,
                    _ => null
                };
                if (string.IsNullOrWhiteSpace(id)) continue;
                id = id.Trim();
                Type type = value.GetType();
                if (!byType.TryGetValue(type, out var ids))
                    byType.Add(type, ids = new Dictionary<string, string>(StringComparer.Ordinal));
                if (ids.TryGetValue(id, out string previous))
                    errors.Add(path + ": " + type.Name + " ID '" + id + "'가 중복입니다. 기존 에셋: " + previous);
                else ids.Add(id, path);
            }
            return errors;
        }
        static void Scan<T>(List<(ScriptableObject definition, string assetPath)> definitions) where T : ScriptableObject
        {
            foreach (string guid in AssetDatabase.FindAssets("t:" + typeof(T).Name, new[] { "Assets/NoSleep" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                definitions.Add((AssetDatabase.LoadAssetAtPath<T>(path), path));
            }
        }
        static void Id(string value, string field, string path, List<string> errors)
        {
            if (string.IsNullOrWhiteSpace(value)) Error(field, "저장 가능한 고유 ID가 필요합니다", path, errors);
            else if (value != value.Trim()) Error(field, "ID의 앞뒤 공백을 제거해야 합니다", path, errors);
        }
        static void Name(string value, string path, List<string> errors)
        {
            if (string.IsNullOrWhiteSpace(value)) Error("displayName", "플레이어에게 표시할 이름이 필요합니다", path, errors);
        }
        static void Range(int value, int minimum, int maximum, string field, string path, List<string> errors)
        {
            if (value < minimum || value > maximum) Error(field, minimum + "~" + maximum + " 범위여야 합니다", path, errors);
        }
        static void FiniteRange(float value, float minimum, float maximum, string field, string path, List<string> errors)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) Error(field, "유한한 수를 입력해야 합니다", path, errors);
            else if (value < minimum || value > maximum) Error(field, minimum + "~" + maximum + " 범위여야 합니다", path, errors);
        }
        static void Error(string field, string message, string path, List<string> errors) => errors.Add(path + ": " + field + " / " + message + ".");
    }
}
