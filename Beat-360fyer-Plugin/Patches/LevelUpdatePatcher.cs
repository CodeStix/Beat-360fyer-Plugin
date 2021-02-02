using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Beat_360fyer_Plugin.Patches
{
    [HarmonyPatch(typeof(LevelCollectionViewController))]
    [HarmonyPatch("HandleLevelCollectionTableViewDidSelectLevel", MethodType.Normal)]
    class LevelSelectPatcher
    {
        static void Prefix(LevelCollectionTableView tableView, IPreviewBeatmapLevel level) 
        {
            Plugin.Log.Info("HandleLevelCollectionTableViewDidSelectLevel");
            if(level == null)
            {
                Plugin.Log.Info("____previewBeatmapLevelToBeSelected is null");
                return;
            }

            Plugin.Log.Info("type: " + level.GetType().FullName);
            IBeatmapLevel level3 = (IBeatmapLevel)level;
            if(level3 == null)
            {
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

            BeatmapCharacteristicCollectionSO beatmapCharacteristicCollection = (BeatmapCharacteristicCollectionSO)customLevelField.GetValue(customLevelLoader);
            if (beatmapCharacteristicCollection == null)
            {
                Plugin.Log.Info("beatmapCharacteristicCollection is null");
                return;
            }

            Plugin.Log.Info("characteristics: " + string.Join(", ", beatmapCharacteristicCollection.beatmapCharacteristics.Select((e) => e.serializedName)));

            BeatmapCharacteristicSO beatmapCharacteristicBySerializedName = beatmapCharacteristicCollection.GetBeatmapCharacteristicBySerializedName("360Degree");
            if (beatmapCharacteristicBySerializedName == null)
            {
                Plugin.Log.Info("beatmapCharacteristicBySerializedName is null");
                return;
            }

            IDifficultyBeatmapSet standard = level3.beatmapLevelData.difficultyBeatmapSets.FirstOrDefault((e) => e.beatmapCharacteristic.serializedName == "Standard");
            if (standard == null)
            {
                Plugin.Log.Info("standard is null");
                return;
            }

            Plugin.Log.Info("UPDATING");
            CustomDifficultyBeatmapSet degree360Set = new CustomDifficultyBeatmapSet(beatmapCharacteristicBySerializedName);

            CustomDifficultyBeatmap[] customs = standard.difficultyBeatmaps.Select((e) => new CustomDifficultyBeatmap(e.level, degree360Set, e.difficulty, e.difficultyRank, e.noteJumpMovementSpeed, e.noteJumpStartBeatOffset, e.beatmapData)).ToArray();
            degree360Set.SetCustomDifficultyBeatmaps(customs);

            set = set.AddToArray(degree360Set);
            field.SetValue(level3.beatmapLevelData, set);
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
