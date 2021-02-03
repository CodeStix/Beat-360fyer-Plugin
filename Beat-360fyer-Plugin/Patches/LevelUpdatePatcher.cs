using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Zenject;

namespace Beat_360fyer_Plugin.Patches
{
    [HarmonyPatch(typeof(MenuTransitionsHelper))]
    [HarmonyPatch("StartStandardLevel", new[] { typeof(string), typeof(IDifficultyBeatmap), typeof(OverrideEnvironmentSettings), typeof(ColorScheme), typeof(GameplayModifiers), typeof(PlayerSpecificSettings), typeof(PracticeSettings), typeof(string), typeof(bool), typeof(Action), typeof(Action<DiContainer>), typeof(Action<StandardLevelScenesTransitionSetupDataSO, LevelCompletionResults>) })]
    public class TransitionPatcher
    {
        static void Prefix(string gameMode, IDifficultyBeatmap difficultyBeatmap, OverrideEnvironmentSettings overrideEnvironmentSettings, ColorScheme overrideColorScheme, GameplayModifiers gameplayModifiers, PlayerSpecificSettings playerSpecificSettings, PracticeSettings practiceSettings, string backButtonText, bool useTestNoteCutSoundEffects, Action beforeSceneSwitchCallback, Action<DiContainer> afterSceneSwitchCallback, Action<StandardLevelScenesTransitionSetupDataSO, LevelCompletionResults> levelFinishedCallback)
        {
            // CustomDifficultyBeatmap
            Plugin.Log.Info($"[StartStandardLevel] Starting ({difficultyBeatmap.GetType().FullName}) {difficultyBeatmap.SerializedName()} {gameMode} {difficultyBeatmap.difficulty} {difficultyBeatmap.level.songName}");

            if (difficultyBeatmap.parentDifficultyBeatmapSet.beatmapCharacteristic.serializedName == GameModeHelper.GENERATED_360DEGREE_MODE)
            {
                Plugin.Log.Info("[StartStandardLevel] Generating rotation events...");
                int count = 0;

                // Negative numbers rotate to the left, positive to the right
                void Rotate(float time, int amount)
                {
                    if (amount == 0) 
                        return;
                    count++;
                    difficultyBeatmap.beatmapData.AddBeatmapEventData(new BeatmapEventData(time, BeatmapEventType.Event15, amount > 0 ? 3 + amount : 4 + amount));
                }

                // Generate rotation events
                // difficultyBeatmap.beatmapData.beatmapObjectsData MUST BE SORTED!

                // The minimum amount of time between each rotation event
                const float MIN_ROTATION_INTERVAL = 0.5f;
                // The amount of rotations to bottleneck the rotation event at
                const int BOTTLENECK_ROTATIONS = 12;
                // The amount of rotations before limiting rotation events
                const int LIMIT_ROTATIONS = 30;

                float minTimeFrame = 60 / difficultyBeatmap.level.beatsPerMinute;
                while (minTimeFrame < MIN_ROTATION_INTERVAL)
                    minTimeFrame *= 2;

                float start = difficultyBeatmap.level.songTimeOffset;
                int offset = 0;
                bool favorDirection = false; // false is left, true is right
                List<NoteData> lastNotes = new List<NoteData>();
                foreach (BeatmapObjectData data in difficultyBeatmap.beatmapData.beatmapObjectsData)
                {
                    if (data.time >= start + minTimeFrame)
                    {
                        int leftCount = lastNotes.Count((e) => (e.cutDirection == NoteCutDirection.Left && e.noteLineLayer != NoteLineLayer.Top) || e.cutDirection == NoteCutDirection.DownLeft || e.cutDirection == NoteCutDirection.UpLeft || e.lineIndex == 0 || e.lineIndex == 1);
                        int rightCount = lastNotes.Count((e) => (e.cutDirection == NoteCutDirection.Right && e.noteLineLayer != NoteLineLayer.Top) || e.cutDirection == NoteCutDirection.DownRight || e.cutDirection == NoteCutDirection.UpRight || e.lineIndex == 2 || e.lineIndex == 3);

                        int dir = -leftCount + rightCount;

                        // Limit rotations
                        if (offset < -BOTTLENECK_ROTATIONS)
                            favorDirection = true;
                        else if (offset > BOTTLENECK_ROTATIONS)
                            favorDirection = false;

                        // Favor previous direction
                        if (favorDirection) 
                            rightCount++;
                        else
                            leftCount++;

                        if (dir < -1 && offset >= -LIMIT_ROTATIONS)
                        {
                            Rotate(data.time, dir / 4);
                            offset += dir / 4;
                            favorDirection = false;
                            Plugin.Log.Info($"[StartStandardLevel] Go left {-(leftCount / 4 + 1)}");
                        }
                        else if (dir > 1 && offset <= LIMIT_ROTATIONS)
                        {
                            Rotate(data.time, dir / 4);
                            offset += dir / 4;
                            favorDirection = true;
                            Plugin.Log.Info($"[StartStandardLevel] Go right {(rightCount/ 4 + 1)}");
                        }
                        else if (leftCount == rightCount && rightCount >= 2)
                        {
                            Rotate(data.time, favorDirection ? 1 : -1);
                            Plugin.Log.Info($"[StartStandardLevel] No l {leftCount}, r {rightCount} ({lastNotes.Count})");
                        }

                        lastNotes.Clear();
                        start = data.time;
                    }

                    if (data is NoteData note)
                    {
                        lastNotes.Add(note);
                    }
                    else if (data is ObstacleData obstacle)
                    {

                        // cut off walls
                    }
                }

                // Resort events...
                List<BeatmapEventData> sorted = difficultyBeatmap.beatmapData.beatmapEventsData.OrderBy((e) => e.time).ToList();
                FieldHelper.Set(difficultyBeatmap.beatmapData, "_beatmapEventsData", sorted);

                Plugin.Log.Info($"[StartStandardLevel] Emitted {count} rotation events");
            }
        }
    }

    [HarmonyPatch(typeof(StandardLevelDetailView))]
    [HarmonyPatch("SetContent")]
    public class LevelUpdatePatcher
    {
        static void Prefix(StandardLevelDetailView __instance, IBeatmapLevel level, BeatmapDifficulty defaultDifficulty, BeatmapCharacteristicSO defaultBeatmapCharacteristic, PlayerData playerData, bool showPlayerStats)
        {
            if (level.beatmapLevelData.difficultyBeatmapSets.Any((e) => e.beatmapCharacteristic.serializedName == GameModeHelper.GENERATED_360DEGREE_MODE))
            {
                // Already added the generated 360 gamemode
                return;
            }

            IDifficultyBeatmapSet standard = level.beatmapLevelData.difficultyBeatmapSets.FirstOrDefault((e) => e.beatmapCharacteristic.serializedName == "Standard");
            if (standard == null)
            {
                // Level does not have a standard mode to base its 360 mode on
                return;
            }

            BeatmapCharacteristicSO customGameMode = GameModeHelper.GetGenerated360GameMode();
            CustomDifficultyBeatmapSet custom360DegreeSet = new CustomDifficultyBeatmapSet(customGameMode);
            CustomDifficultyBeatmap[] difficulties = standard.difficultyBeatmaps.Select((e) => new CustomDifficultyBeatmap(e.level, custom360DegreeSet, e.difficulty, e.difficultyRank, e.noteJumpMovementSpeed, e.noteJumpStartBeatOffset, e.beatmapData.GetCopy())).ToArray();
            custom360DegreeSet.SetCustomDifficultyBeatmaps(difficulties);

            IDifficultyBeatmapSet[] newSets = level.beatmapLevelData.difficultyBeatmapSets.AddItem(custom360DegreeSet).ToArray();
            
            if (level.beatmapLevelData is BeatmapLevelData data)
            {
                if (!FieldHelper.Set(data, "_difficultyBeatmapSets", newSets))
                {
                    Plugin.Log.Warn("[SetContent] Could not set new difficulty sets");
                    return;
                }
            }
            else
            {
                Plugin.Log.Info("[SetContent] Unsupported data: " + (level.beatmapLevelData?.GetType().FullName ?? "null"));
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

    /*[HarmonyPatch(typeof(SinglePlayerLevelSelectionFlowCoordinator))]
    [HarmonyPatch("StartLevel")]
    class PlayButtonPatcher
    {
        static void Prefix(Action beforeSceneSwitchCallback, bool practice, ref LevelSelectionNavigationController ___levelSelectionNavigationController)
        {
            IDifficultyBeatmap bm = ___levelSelectionNavigationController.selectedDifficultyBeatmap;

            Plugin.Log.Info("[StartLevel] starting level: " + bm.SerializedName());

            if (bm.parentDifficultyBeatmapSet.beatmapCharacteristic.serializedName == GameModeHelper.GENERATED_360DEGREE_MODE)
            {
                Plugin.Log.Info("[StartLevel] starting generated 360");
                bm.beatmapData.AddBeatmapEventData(new BeatmapEventData(1f, BeatmapEventType.Event15, 1));
                bm.beatmapData.AddBeatmapEventData(new BeatmapEventData(2f, BeatmapEventType.Event15, 2));
                bm.beatmapData.AddBeatmapEventData(new BeatmapEventData(3f, BeatmapEventType.Event15, 3));
                bm.beatmapData.AddBeatmapEventData(new BeatmapEventData(4f, BeatmapEventType.Event15, 4));
            }
        }
    }*/
}


