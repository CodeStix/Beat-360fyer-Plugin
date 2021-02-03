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
                Plugin.Log.Info("level is null");
                return;
            }

            IBeatmapLevel level3 = level as IBeatmapLevel;
            if(level3 == null)
            {
                Plugin.Log.Info("IPreviewBeatmapLevel is not IBeatmapLevel? " + level.GetType().FullName);
                return;
            }

            if (level3.beatmapLevelData.difficultyBeatmapSets.Any((e) => e.beatmapCharacteristic.serializedName == GENERATED_GAME_MODE))
            {
                Plugin.Log.Info("already registered new gamemode");
                return;
            }

            IDifficultyBeatmapSet[] difficultySets = level3.beatmapLevelData.difficultyBeatmapSets;// Fields.Get<IDifficultyBeatmapSet[]>(level3.beatmapLevelData, "_difficultyBeatmapSets");
            if (difficultySets == null)
            {
                Plugin.Log.Info("difficultySets is null");
                return;
            }

            BeatmapCharacteristicSO customGameMode = GameModeHelper.GetGenerated360GameMode();

            IDifficultyBeatmapSet standard = level3.beatmapLevelData.difficultyBeatmapSets.FirstOrDefault((e) => e.beatmapCharacteristic.serializedName == "Standard");
            if (standard == null)
            {
                Plugin.Log.Info("standard is null");
                return;
            }

            CustomDifficultyBeatmapSet custom360DegreeSet = new CustomDifficultyBeatmapSet(customGameMode);

            CustomDifficultyBeatmap[] difficulties = standard.difficultyBeatmaps.Select((e) => new CustomDifficultyBeatmap(e.level, custom360DegreeSet, e.difficulty, e.difficultyRank, e.noteJumpMovementSpeed, e.noteJumpStartBeatOffset, e.beatmapData)).ToArray();
            custom360DegreeSet.SetCustomDifficultyBeatmaps(difficulties);

            difficultySets = difficultySets.AddToArray(custom360DegreeSet);
            FieldHelper.Set(level3.beatmapLevelData, "_difficultyBeatmapSets", difficultySets);

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
