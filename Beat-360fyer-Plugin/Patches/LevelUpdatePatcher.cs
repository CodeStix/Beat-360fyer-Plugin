using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Beat_360fyer_Plugin.Patches
{
    public static class Fields
    {
        public static T Get<T>(object obj, string fieldName)
        {
            return (T)obj.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(obj);
        }

        public static void Set(object obj, string fieldName, object value)
        {
            obj.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(obj, value);
        }
    }

    [HarmonyPatch(typeof(LevelCollectionViewController))]
    [HarmonyPatch("HandleLevelCollectionTableViewDidSelectLevel", MethodType.Normal)]
    class LevelSelectPatcher
    {
        public const string GENERATED_GAME_MODE = "Generated360Degree";

        static void Prefix(LevelCollectionTableView tableView, IPreviewBeatmapLevel level) 
        {
            Plugin.Log.Info("HandleLevelCollectionTableViewDidSelectLevel");
            if(level == null)
            {
                Plugin.Log.Info("____previewBeatmapLevelToBeSelected is null");
                return;
            }

            Plugin.Log.Info("type: " + level.GetType().FullName);
            IBeatmapLevel level3 = level as IBeatmapLevel;
            if(level3 == null)
            {
                return;
            }

            if (level3.beatmapLevelData.difficultyBeatmapSets.Any((e) => e.beatmapCharacteristic.serializedName == GENERATED_GAME_MODE))
            {
                Plugin.Log.Info("Already registered new gamemode");
                return;
            }

            FieldInfo field = level3.beatmapLevelData.GetType().GetField("_difficultyBeatmapSets", BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null)
            {
                Plugin.Log.Info("field is null");
                return;
            }

            IDifficultyBeatmapSet[] set = (IDifficultyBeatmapSet[])field.GetValue(level3.beatmapLevelData);
            if (set == null)
            {
                Plugin.Log.Info("set is null");
                return;
            }

            CustomLevelLoader customLevelLoader = UnityEngine.Object.FindObjectOfType<CustomLevelLoader>();
            if (customLevelLoader == null)
            {
                Plugin.Log.Info("customLevelLoader is null");
                return;
            }

            FieldInfo customLevelField = customLevelLoader.GetType().GetField("_beatmapCharacteristicCollection", BindingFlags.Instance | BindingFlags.NonPublic);
            if (customLevelField == null)
            {
                Plugin.Log.Info("customLevelField is null");
                return;
            }

            BeatmapCharacteristicCollectionSO defaultGameModes = (BeatmapCharacteristicCollectionSO)customLevelField.GetValue(customLevelLoader);
            if (defaultGameModes == null)
            {
                Plugin.Log.Info("beatmapCharacteristicCollection is null");
                return;
            }

            Plugin.Log.Info("characteristics: " + string.Join(", ", defaultGameModes.beatmapCharacteristics.Select((e) => e.serializedName)));
            BeatmapCharacteristicSO default360GameMode = defaultGameModes.GetBeatmapCharacteristicBySerializedName("360Degree");
            if(default360GameMode == null)
            {
                Plugin.Log.Info("default360GameMode is null");
                return;
            }

            BeatmapCharacteristicSO customGameMode = BeatmapCharacteristicSO.CreateInstance<BeatmapCharacteristicSO>();
            Fields.Set(customGameMode, "_icon", default360GameMode.icon);
            Fields.Set(customGameMode, "_descriptionLocalizationKey", default360GameMode.descriptionLocalizationKey);
            Fields.Set(customGameMode, "_characteristicNameLocalizationKey", default360GameMode.characteristicNameLocalizationKey);
            Fields.Set(customGameMode, "_serializedName", GENERATED_GAME_MODE);
            Fields.Set(customGameMode, "_compoundIdPartName", GENERATED_GAME_MODE); // What is _compoundIdPartName?
            Fields.Set(customGameMode, "_sortingOrder", 100);
            Fields.Set(customGameMode, "_containsRotationEvents", true);
            Fields.Set(customGameMode, "_requires360Movement", true);
            Fields.Set(customGameMode, "_numberOfColors", 2);

            IDifficultyBeatmapSet standard = level3.beatmapLevelData.difficultyBeatmapSets.FirstOrDefault((e) => e.beatmapCharacteristic.serializedName == "Standard");
            if (standard == null)
            {
                Plugin.Log.Info("standard is null");
                return;
            }

            CustomDifficultyBeatmapSet custom360DegreeSet = new CustomDifficultyBeatmapSet(customGameMode);

            CustomDifficultyBeatmap[] difficulties = standard.difficultyBeatmaps.Select((e) => new CustomDifficultyBeatmap(e.level, custom360DegreeSet, e.difficulty, e.difficultyRank, e.noteJumpMovementSpeed, e.noteJumpStartBeatOffset, e.beatmapData)).ToArray();
            custom360DegreeSet.SetCustomDifficultyBeatmaps(difficulties);

            set = set.AddToArray(custom360DegreeSet);
            field.SetValue(level3.beatmapLevelData, set);

            Plugin.Log.Info("created generated 360 gamemode");
        }
    }

    [HarmonyPatch(typeof(StandardLevelDetailView))]
    [HarmonyPatch("RefreshContent", MethodType.Normal)]
    public class LevelUpdatePatcher
    {
        static void Prefix(StandardLevelDetailView __instance, ref IBeatmapLevel ____level, ref IDifficultyBeatmap ____selectedDifficultyBeatmap)
        {
            __instance.actionButtonText = ____level.songName;
        }
    }
}
