using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ProjectCRT.Prologue.Editor
{
    public static class PrologueAssetPreparation
    {
        public const string Root="Assets/ThirdParty/Prologue";
        public static void Prepare()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play Mode first.");
            Directory.CreateDirectory("Logs/PrologueAssets");
            var paths=Directory.GetFiles(Root,"*.fbx",SearchOption.AllDirectories).Select(p=>p.Replace('\\','/')).ToArray();
            foreach(var path in paths)
            {
                var importer=AssetImporter.GetAtPath(path) as ModelImporter;
                if(importer==null)continue;
                importer.materialImportMode=ModelImporterMaterialImportMode.ImportStandard;
                importer.importCameras=false;importer.importLights=false;importer.isReadable=true;
                importer.generateSecondaryUV=false;
                // Only new third-party imports are changed; existing character/model importers are untouched.
                string textureFolder=Path.GetDirectoryName(path).Replace('\\','/')+"/EmbeddedTextures";
                Directory.CreateDirectory(textureFolder);
                importer.ExtractTextures(textureFolder);
                importer.SaveAndReimport();
            }
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            foreach(var path in Directory.GetFiles(Root,"*",SearchOption.AllDirectories).Where(p=>new[]{".png",".jpg",".jpeg",".tga"}.Contains(Path.GetExtension(p).ToLowerInvariant())))
            {
                var importer=AssetImporter.GetAtPath(path.Replace('\\','/')) as TextureImporter;if(importer==null)continue;
                importer.filterMode=FilterMode.Point;importer.mipmapEnabled=false;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();
            }
            Audit();
        }
        public static void Audit()
        {
            var report=new List<string>();
            var paths=Directory.GetFiles(Root,"*",SearchOption.AllDirectories).Where(p=>new[]{".fbx",".obj"}.Contains(Path.GetExtension(p).ToLowerInvariant())).Select(p=>p.Replace('\\','/'))
                .Concat(new[]{"Assets/resources/3Dmodels/maps/shangrila.fbx","Assets/resources/fbx/San/San.fbx","Assets/resources/fbx/Maps/room.fbx"});
            foreach(var path in paths)
            {
                var asset=AssetDatabase.LoadAssetAtPath<GameObject>(path);if(asset==null){report.Add("MISSING|"+path);continue;}
                report.Add("MODEL|"+path);
                foreach(var renderer in asset.GetComponentsInChildren<Renderer>(true))
                    report.Add($"{renderer.name}|center={renderer.bounds.center:F3}|size={renderer.bounds.size:F3}|materials="+
                        string.Join(";",renderer.sharedMaterials.Select(m=>m==null?"null":m.name+" texture="+(m.mainTexture!=null?AssetDatabase.GetAssetPath(m.mainTexture):"none"))));
            }
            File.WriteAllLines("Logs/PrologueAssets/model-audit.txt",report);
        }
    }
}
