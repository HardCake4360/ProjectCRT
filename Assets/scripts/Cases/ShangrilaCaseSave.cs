using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace ProjectCRT.Cases
{
    public enum ShangrilaCasePhase { StreetArrival, FreeInvestigation }
    public enum CaseLoadResult { Missing, Loaded, RecoveredBackup, Invalid }

    [Serializable]
    public sealed class ShangrilaCaseSave
    {
        public const string CaseId = "shangrila_arrival_001";
        public string schema;
        public int version;
        public string caseId;
        public string scenePath;
        public List<string> completedCutsceneIds = new();
        public InvestigationInventorySaveData inventory;
        public bool hasPlayerPose;
        public Vector3 playerPosition;
        public float playerYaw, playerPitch;

        public static ShangrilaCaseSave New(string scenePath) => new()
        {
            schema = "ProjectCRT.ShangrilaCase", version = 1, caseId = CaseId, scenePath = scenePath,
            inventory = new InvestigationInventorySaveData { caseId = CaseId }
        };
    }

    /// <summary>Validate before applying; replace a complete slot atomically and keep one previous copy.</summary>
    public sealed class ShangrilaCaseStore
    {
        public readonly string Path;
        public ShangrilaCaseStore(string path) { Path = path; }
        public CaseLoadResult Load(string scenePath, out ShangrilaCaseSave data, out string error)
        {
            data = null; error = null;
            if (!File.Exists(Path) && !File.Exists(Path + ".bak")) return CaseLoadResult.Missing;
            if (Read(Path, scenePath, out data, out error)) return CaseLoadResult.Loaded;
            string primaryError = error;
            if (Read(Path + ".bak", scenePath, out data, out error))
            { error = primaryError; return CaseLoadResult.RecoveredBackup; }
            error = primaryError + "; backup: " + error;
            return CaseLoadResult.Invalid;
        }

        public bool Save(ShangrilaCaseSave data, out string error)
        {
            error = null;
            try
            {
                Validate(data, data.scenePath);
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(Path)));
                string temporary = Path + ".tmp";
                using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    var bytes = System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(data, true));
                    stream.Write(bytes, 0, bytes.Length); stream.Flush(true);
                }
                if (File.Exists(Path)) File.Replace(temporary, Path, Path + ".bak");
                else File.Move(temporary, Path);
                return true;
            }
            catch (Exception e) { error = e.Message; return false; }
        }

        static bool Read(string path, string scene, out ShangrilaCaseSave data, out string error)
        {
            data = null; error = null;
            try
            {
                if (!File.Exists(path)) throw new IOException("File is missing.");
                if (new FileInfo(path).Length > 4 * 1024 * 1024) throw new IOException("Save file is too large.");
                var candidate = JsonUtility.FromJson<ShangrilaCaseSave>(File.ReadAllText(path));
                Validate(candidate, scene); data = candidate; return true;
            }
            catch (Exception e) { error = e.Message; return false; }
        }
        static void Validate(ShangrilaCaseSave data, string scene)
        {
            if (data == null || data.schema != "ProjectCRT.ShangrilaCase" || data.version != 1 ||
                data.caseId != ShangrilaCaseSave.CaseId || data.scenePath != scene ||
                data.completedCutsceneIds == null || data.inventory == null ||
                data.inventory.caseId != data.caseId || data.inventory.version != 1 ||
                data.inventory.topicIds == null || data.inventory.evidenceIds == null || data.inventory.informationIds == null)
                throw new InvalidDataException("Unsupported or incomplete case save.");
            if (data.hasPlayerPose && (!Finite(data.playerPosition.x) || !Finite(data.playerPosition.y) ||
                !Finite(data.playerPosition.z) || !Finite(data.playerYaw) || !Finite(data.playerPitch) ||
                data.playerPosition.sqrMagnitude > 1000000 || data.playerPosition.y < -4))
                throw new InvalidDataException("Invalid player pose.");
        }
        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
