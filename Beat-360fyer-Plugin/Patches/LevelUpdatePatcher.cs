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
    [HarmonyPatch(typeof(SinglePlayerLevelSelectionFlowCoordinator))]
    [HarmonyPatch("StartLevel")]
    class PlayButtonPatcher
    {
        static void Prefix(Action beforeSceneSwitchCallback, bool practice, ref LevelSelectionNavigationController ___levelSelectionNavigationController)
        {
            IDifficultyBeatmap bm = ___levelSelectionNavigationController.selectedDifficultyBeatmap;

            Plugin.Log.Info("starting level: " + bm.SerializedName());

            if (bm.parentDifficultyBeatmapSet.beatmapCharacteristic.serializedName == GameModeHelper.GENERATED_360DEGREE_MODE)
            {
                Plugin.Log.Info("starting generated 360");
                bm.beatmapData.AddBeatmapEventData(new BeatmapEventData(1f, BeatmapEventType.Event15, 1));
                bm.beatmapData.AddBeatmapEventData(new BeatmapEventData(2f, BeatmapEventType.Event15, 2));
                bm.beatmapData.AddBeatmapEventData(new BeatmapEventData(3f, BeatmapEventType.Event15, 3));
                bm.beatmapData.AddBeatmapEventData(new BeatmapEventData(4f, BeatmapEventType.Event15, 4));
            }
        }
    }

    [HarmonyPatch(typeof(StandardLevelDetailView))]
    [HarmonyPatch("SetContent", MethodType.Normal)]
    public class LevelUpdatePatcher
    {
        static void Prefix(StandardLevelDetailView __instance, IBeatmapLevel level, BeatmapDifficulty defaultDifficulty, BeatmapCharacteristicSO defaultBeatmapCharacteristic, PlayerData playerData, bool showPlayerStats)
        {
            Plugin.Log.Info("[SetContent] song " + level.songName);
            Plugin.Log.Info("[SetContent] type " + level.GetType().FullName);

            if (level.beatmapLevelData.difficultyBeatmapSets.Any((e) => e.beatmapCharacteristic.serializedName == GameModeHelper.GENERATED_360DEGREE_MODE))
            {
                Plugin.Log.Info("[SetContent] already registered new gamemode");
                return;
            }

            IDifficultyBeatmapSet standard = level.beatmapLevelData.difficultyBeatmapSets.FirstOrDefault((e) => e.beatmapCharacteristic.serializedName == "Standard");
            if (standard == null)
            {
                Plugin.Log.Info("[SetContent] standard is null");
                return;
            }

            BeatmapCharacteristicSO customGameMode = GameModeHelper.GetGenerated360GameMode();
            CustomDifficultyBeatmapSet custom360DegreeSet = new CustomDifficultyBeatmapSet(customGameMode);
            CustomDifficultyBeatmap[] difficulties = standard.difficultyBeatmaps.Select((e) => new CustomDifficultyBeatmap(e.level, custom360DegreeSet, e.difficulty, e.difficultyRank, e.noteJumpMovementSpeed, e.noteJumpStartBeatOffset, e.beatmapData)).ToArray();
            custom360DegreeSet.SetCustomDifficultyBeatmaps(difficulties);

            IDifficultyBeatmapSet[] newSets = level.beatmapLevelData.difficultyBeatmapSets.AddItem(custom360DegreeSet).ToArray();
            
            if (level.beatmapLevelData is BeatmapLevelData data)
            {
                if (!FieldHelper.Set(data, "_difficultyBeatmapSets", newSets))
                {
                    Plugin.Log.Info("[SetContent] could not set new difficulty sets");
                    return;
                }
            }
            else
            {
                Plugin.Log.Info("[SetContent] unsupported data: " + (level.beatmapLevelData?.GetType().FullName ?? "null"));
            }
        }
    }

    /*[HarmonyPatch(typeof(LevelCollectionViewController))]
    [HarmonyPatch("HandleLevelCollectionTableViewDidSelectLevel", MethodType.Normal)]
    class LevelSelectPatcher
    {
        public const string GENERATED_GAME_MODE = "Generated360Degree";

        static void Prefix(LevelCollectionTableView tableView, IPreviewBeatmapLevel level) 
        {
            
        }
    }*/

    /*[HarmonyPatch(typeof(StandardLevelDetailView))]
    [HarmonyPatch("RefreshContent", MethodType.Normal)]
    public class LevelUpdatePatcher
    {
        static void Prefix(StandardLevelDetailView __instance, ref IBeatmapLevel ____level, ref IDifficultyBeatmap ____selectedDifficultyBeatmap)
        {
            Plugin.Log.Info("[RefreshContent] refresh " + ____level.previewDifficultyBeatmapSets.Length + " , " + ____level.beatmapLevelData.difficultyBeatmapSets.Length);
            Plugin.Log.Info("[RefreshContent] type2 " + ____level.GetType().FullName);

            //if (!FieldHelper.Set(____level.beatmapLevelData, "_difficultyBeatmapSets", ____level.beatmapLevelData.difficultyBeatmapSets.AddItem()))
            //{
            //    Plugin.Log.Info("cleared");
            //}

            Plugin.Log.Info("[RefreshContent] song " + (____level?.songName ?? "null"));

            //__instance.actionButtonText = ____level.songName;
        }
    }*/
}


