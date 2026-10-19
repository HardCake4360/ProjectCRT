using System.Collections.Generic;
using System.IO;
using System.Linq;
using NoSleep.Editor;
using NUnit.Framework;
using ProjectCRT.RPG.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace NoSleep.Tests
{
    public sealed class ProjectCrtImportTests
    {
        [Test]
        public void CrtMapsHaveDistinctIdsAndValidSpawnEventsAndConnections()
        {
            var office = AssetDatabase.LoadAssetAtPath<MapDefinition>(RpgPrototypeBuilder.DataPath + "/Maps/Office.asset");
            var street = AssetDatabase.LoadAssetAtPath<MapDefinition>(RpgPrototypeBuilder.DataPath + "/Maps/Street.asset");
            Assert.That(office, Is.Not.Null); Assert.That(street, Is.Not.Null);
            Assert.That(office.mapId, Is.EqualTo(RpgPrototypeBuilder.OfficeId));
            Assert.That(street.mapId, Is.EqualTo(RpgPrototypeBuilder.StreetId));
            Assert.That(office.Validate(), Is.Empty); Assert.That(street.Validate(), Is.Empty);
            var index = ContentReferenceIndex.Build();
            Assert.That(index.Maps, Has.Member(office)); Assert.That(index.Maps, Has.Member(street));
            Assert.That(index.GetDiagnostics().Where(row => row.Severity == ContentDiagnosticSeverity.Error), Is.Empty);
            Assert.That(MapConnectionAnalyzer.Collect(new[] { office, street }, index.Registries).Diagnostics
                .Where(row => row.Severity == ContentDiagnosticSeverity.Error), Is.Empty);
        }

        [Test]
        public void SavedCrtSceneUsesItsOwnTitleMapsAndSaveFolderWithSurvivalDisabled()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(RpgPrototypeBuilder.ScenePath);
            bool opened = !scene.IsValid() || !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(RpgPrototypeBuilder.ScenePath, OpenSceneMode.Additive);
            try
            {
                var bootstrap = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<NoSleepBootstrap>()).Single();
                Assert.That(bootstrap.gameTitle, Is.EqualTo("PROJECT CRT"));
                Assert.That(bootstrap.saveFolderName, Is.EqualTo(RpgPrototypeBuilder.SaveFolder));
                Assert.That(bootstrap.showTitleScreen, Is.True); Assert.That(bootstrap.survivalSettings, Is.Null);
                Assert.That(bootstrap.maps.Select(map => map.mapId), Is.EquivalentTo(new[] { RpgPrototypeBuilder.OfficeId, RpgPrototypeBuilder.StreetId }));
                Assert.That(bootstrap.startingMap.mapId, Is.EqualTo(RpgPrototypeBuilder.OfficeId));
            }
            finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
        }

        [Test]
        public void ReopeningPrototypePreservesAllAuthoredFilesAndExistingBuildOrder()
        {
            var paths = Directory.GetFiles(RpgPrototypeBuilder.DataPath, "*", SearchOption.AllDirectories)
                .Concat(Directory.GetFiles("Assets/Scenes", "*", SearchOption.AllDirectories))
                .Concat(new[] { RpgPrototypeBuilder.ScenePath, RpgPrototypeBuilder.ScenePath + ".meta" });
            var contents = paths.ToDictionary(path => path, File.ReadAllBytes);
            var buildScenes = EditorBuildSettings.scenes.Select(scene => (scene.path, scene.enabled)).ToArray();
            var setup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                RpgPrototypeBuilder.CreateOrOpen(); RpgPrototypeBuilder.CreateOrOpen();
                foreach (var pair in contents) Assert.That(File.ReadAllBytes(pair.Key), Is.EqualTo(pair.Value), pair.Key);
                Assert.That(EditorBuildSettings.scenes.Select(scene => (scene.path, scene.enabled)), Is.EqualTo(buildScenes));
            }
            finally
            {
                // Batch Test Runner starts with an unnamed empty scene, which Unity
                // cannot reconstruct with RestoreSceneManagerSetup.
                if (setup.Length == 0 || setup.Any(item => item.isLoaded && string.IsNullOrEmpty(item.path)))
                    EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                else EditorSceneManager.RestoreSceneManagerSetup(setup);
            }
        }
    }
}
