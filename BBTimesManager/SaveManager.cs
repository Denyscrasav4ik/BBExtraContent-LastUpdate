using System;
using System.IO;
using UnityEngine;
using BepInEx;
using MTM101BaldAPI;
using MTM101BaldAPI.AssetTools;
using MTM101BaldAPI.Registers;
using MTM101BaldAPI.SaveSystem;
using UnityCipher;

namespace BBTimes.Manager
{
    internal class SaveManager
    {
        public static SaveManager Instance = new SaveManager();

        public bool secretEnding = false;

        private const string FILE_NAME = "SecretData.dat";

        public void Register(BaseUnityPlugin pluginInstance)
        {
            ModdedSaveSystem.AddSaveLoadAction(pluginInstance, SaveLoad);
        }

        private void SaveLoad(bool isSave, string path)
        {
            if (!Directory.Exists(path)) Directory.CreateDirectory(path);
            string filePath = Path.Combine(path, FILE_NAME);
            string key = "BBTimes_" + PlayerFileManager.Instance.fileName;

            BBTimesData data = new BBTimesData();

            if (File.Exists(filePath))
            {
                try
                {
                    string encrypted = File.ReadAllText(filePath);
                    string json = RijndaelEncryption.Decrypt(encrypted, key);
                    data = JsonUtility.FromJson<BBTimesData>(json);
                }
                catch (Exception e)
                {
                    Debug.LogWarning("BBTimes: Failed to load secret data, creating new record. " + e.Message);
                }
            }

            if (isSave)
            {
                data.secretEnding = secretEnding;
                string json = JsonUtility.ToJson(data);
                string encrypted = RijndaelEncryption.Encrypt(json, key);
                File.WriteAllText(filePath, encrypted);
            }
            else
            {
                secretEnding = data.secretEnding;
            }
        }

        public void SaveNow(BaseUnityPlugin pluginInstance)
        {
            ModdedSaveSystem.CallSaveLoadAction(
                pluginInstance,
                true,
                ModdedSaveSystem.GetCurrentSaveFolder(pluginInstance)
            );
        }

        [Serializable]
        private class BBTimesData
        {
            public bool secretEnding;
        }
    }
}
