using System;
using System.Collections.Generic;
using System.IO;

namespace NoSleep
{
    public sealed class SaveSlotInfo
    {
        public int slot;
        public bool exists;
        public bool valid;
        public bool hasBackup;
        public bool backupValid;
        public string error;
        public SaveData data;
    }

    /// <summary>Owns save slot paths and metadata without changing the running game state.</summary>
    public sealed class SaveSlotRepository
    {
        public const int VisibleSlotCount = 6;
        readonly string directoryPath;

        public SaveSlotRepository(string directoryPath)
        {
            if (string.IsNullOrWhiteSpace(directoryPath))
                throw new ArgumentException("Save directory cannot be empty.", nameof(directoryPath));
            this.directoryPath = Path.GetFullPath(directoryPath);
        }

        public string GetPath(int slot)
        {
            if (slot < 0 || slot > 99) throw new ArgumentOutOfRangeException(nameof(slot));
            return Path.Combine(directoryPath, "slot-" + slot + ".json");
        }

        public List<SaveSlotInfo> ListSlots(int count = VisibleSlotCount)
        {
            if (count < 0 || count > 100) throw new ArgumentOutOfRangeException(nameof(count));
            var result = new List<SaveSlotInfo>(count);
            for (int slot = 0; slot < count; slot++) result.Add(Inspect(slot));
            return result;
        }

        public SaveSlotInfo Inspect(int slot)
        {
            string path = GetPath(slot);
            var info = new SaveSlotInfo { slot = slot, error = string.Empty };
            InspectFile(path, false, info);
            InspectFile(path + ".bak", true, info);
            return info;
        }

        public void Write(int slot, SaveData data)
        {
            string path = GetPath(slot);
            // Validate and detach first. Stamping must not mutate caller metadata or state.
            SaveData snapshot = SaveService.Deserialize(SaveService.Serialize(data));
            if (snapshot.savedUtcTicks == 0) snapshot.savedUtcTicks = DateTime.UtcNow.Ticks;
            if (string.IsNullOrWhiteSpace(snapshot.mapDisplayName)) snapshot.mapDisplayName = snapshot.mapId;
            SaveService.Write(path, snapshot);
        }

        public SaveData Read(int slot, bool backup = false)
        {
            string path = GetPath(slot);
            // Recovery reads only: the primary file is replaced only by an explicit save.
            return SaveService.Read(backup ? path + ".bak" : path);
        }

        static void InspectFile(string path, bool backup, SaveSlotInfo info)
        {
            try
            {
                if (File.Exists(Path.GetDirectoryName(path)))
                    throw new InvalidDataException("Save directory is a file.");
                // File.Exists hides access errors. Attributes distinguish an empty slot
                // from an inaccessible slot without letting one failure abort listing.
                FileAttributes attributes;
                try { attributes = File.GetAttributes(path); }
                catch (FileNotFoundException) { return; }
                catch (DirectoryNotFoundException) { return; }
                if (backup) info.hasBackup = true;
                else info.exists = true;
                if ((attributes & FileAttributes.Directory) != 0)
                    throw new InvalidDataException("Save path is a directory.");
                SaveData loaded = SaveService.Read(path);
                if (backup) info.backupValid = true;
                else { info.valid = true; info.data = loaded; }
            }
            catch (Exception ex) when (ex is IOException || ex is InvalidDataException || ex is UnauthorizedAccessException || ex is System.Security.SecurityException || ex is ArgumentException)
            {
                string failure = (backup ? "Backup: " : "Save: ") + ex.Message;
                info.error = string.IsNullOrEmpty(info.error) ? failure : info.error + "\n" + failure;
            }
        }
    }
}
