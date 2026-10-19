using System.Collections.Generic;
using System.Linq;
using NoSleep.Editor;
using NUnit.Framework;
using UnityEngine;

namespace NoSleep.Tests
{
    public sealed class HorrorValidatorTests
    {
        readonly List<ScriptableObject> created = new List<ScriptableObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (var value in created) Object.DestroyImmediate(value);
            created.Clear();
        }

        [Test]
        public void ValidDefaultsAndOptionalDescriptionsProduceNoErrors()
        {
            var definitions = new ScriptableObject[]
            {
                Create<SurvivalSettings>(), Create<ItemEffectDefinition>(), Create<NightActivityDefinition>(),
                Create<CompanionDefinition>(), Create<EnemyDefinition>()
            };
            ((NightActivityDefinition)definitions[2]).description = null;
            ((CompanionDefinition)definitions[3]).description = null;
            foreach (var definition in definitions)
                Assert.That(HorrorContentValidator.ValidateDefinition(definition), Is.Empty, definition.GetType().Name);
        }

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        [TestCase(-1f)]
        public void InvalidFatigueParametersIdentifyTheAuthoredField(float invalid)
        {
            var settings = Create<SurvivalSettings>(); settings.fatiguePerMinute = invalid;
            var errors = HorrorContentValidator.ValidateDefinition(settings, "Rules/BadSettings");
            Assert.That(errors.Count, Is.EqualTo(1));
            Assert.That(errors[0], Does.Contain("Rules/BadSettings").And.Contain("fatiguePerMinute"));
        }

        [Test]
        public void SettingsReportAllInvalidIntervalsAndFloorsInOnePass()
        {
            var settings = Create<SurvivalSettings>();
            settings.timeMode = (TimeMode)999; settings.realSecondsPerGameMinute = 0;
            settings.initialHour = 24; settings.campaignDays = 0; settings.minStrength = 0;
            settings.minimumMovementMultiplier = 0; settings.minEnemyScale = 2;
            var errors = HorrorContentValidator.ValidateDefinition(settings);
            Assert.That(errors.Count, Is.EqualTo(7));
            foreach (string field in new[] { "timeMode", "realSecondsPerGameMinute", "initialHour", "campaignDays",
                "minStrength", "minimumMovementMultiplier", "minEnemyScale" })
                Assert.That(errors.Any(error => error.Contains(field)), Is.True, field);
        }

        [Test]
        public void ItemRejectsBlankIdNonPositiveDurationAndNegativeCosts()
        {
            var item = Create<ItemEffectDefinition>();
            item.itemId = " "; item.durationMinutes = 0; item.healthCost = -1; item.sanityCost = -1; item.awakeningRelief = float.NaN;
            var errors = HorrorContentValidator.ValidateDefinition(item);
            Assert.That(errors.Count, Is.EqualTo(5));
            foreach (string field in new[] { "itemId", "durationMinutes", "healthCost", "sanityCost", "awakeningRelief" })
                Assert.That(errors.Any(error => error.Contains(field)), Is.True, field);
        }

        [Test]
        public void NightCompanionAndEnemyReportInvalidAuthoredNumbers()
        {
            var night = Create<NightActivityDefinition>();
            night.dawnHour = -1; night.fatigueMultiplier = float.PositiveInfinity; night.healthRestore = -1;
            Assert.That(HorrorContentValidator.ValidateDefinition(night).Count, Is.EqualTo(3));
            var companion = Create<CompanionDefinition>(); companion.color = new Color(float.NaN, 0.5f, 0.5f, 1);
            Assert.That(HorrorContentValidator.ValidateDefinition(companion).Single(), Does.Contain("color.r"));
            var enemy = Create<EnemyDefinition>(); enemy.baseHealth = 0; enemy.baseAttack = -1;
            Assert.That(HorrorContentValidator.ValidateDefinition(enemy).Count, Is.EqualTo(2));
        }

        [Test]
        public void DuplicateIdentifiersAreReportedPerDefinitionTypeWithBothPaths()
        {
            var first = Create<ItemEffectDefinition>(); first.itemId = "shared";
            var second = Create<ItemEffectDefinition>(); second.itemId = "shared";
            var enemy = Create<EnemyDefinition>(); enemy.enemyId = "shared";
            var errors = HorrorContentValidator.ValidateDefinitions(new (ScriptableObject definition, string assetPath)[]
            {
                (first, "Items/First"), (second, "Items/Second"), (enemy, "Enemies/Shared")
            });
            Assert.That(errors.Count, Is.EqualTo(1));
            Assert.That(errors[0], Does.Contain("Items/First").And.Contain("Items/Second").And.Contain("ItemEffectDefinition"));
        }

        [Test]
        public void LeadingTrailingWhitespaceIsInvalidAndDoesNotHideDuplicateIds()
        {
            var first = Create<CompanionDefinition>(); first.companionId = " min ";
            var second = Create<CompanionDefinition>(); second.companionId = "min";
            var errors = HorrorContentValidator.ValidateDefinitions(new (ScriptableObject definition, string assetPath)[]
            {
                (first, "Companions/First"), (second, "Companions/Second")
            });
            Assert.That(errors.Count, Is.EqualTo(2));
            Assert.That(errors.Any(error => error.Contains("앞뒤 공백")), Is.True);
            Assert.That(errors.Any(error => error.Contains("중복")), Is.True);
        }

        [Test]
        public void MissingDefinitionIsReportedWithoutThrowing()
        {
            Assert.That(HorrorContentValidator.ValidateDefinition(null, "Missing/Definition").Single(), Does.Contain("Missing/Definition"));
        }

        T Create<T>() where T : ScriptableObject
        {
            var value = ScriptableObject.CreateInstance<T>(); created.Add(value); return value;
        }
    }
}
