using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Zenject;

namespace Beat360fyerPlugin.Patches
{
    [HarmonyPatch(typeof(MenuTransitionsHelper))]
    [HarmonyPatch("StartStandardLevel", new[] { typeof(string), typeof(IDifficultyBeatmap), typeof(OverrideEnvironmentSettings), typeof(ColorScheme), typeof(GameplayModifiers), typeof(PlayerSpecificSettings), typeof(PracticeSettings), typeof(string), typeof(bool), typeof(Action), typeof(Action<DiContainer>), typeof(Action<StandardLevelScenesTransitionSetupDataSO, LevelCompletionResults>) })]
    public class TransitionPatcher
    {
        private static HashSet<IDifficultyBeatmap> generated = new HashSet<IDifficultyBeatmap>();

        static void Prefix(string gameMode, IDifficultyBeatmap difficultyBeatmap, OverrideEnvironmentSettings overrideEnvironmentSettings, ColorScheme overrideColorScheme, GameplayModifiers gameplayModifiers, PlayerSpecificSettings playerSpecificSettings, PracticeSettings practiceSettings, string backButtonText, bool useTestNoteCutSoundEffects, Action beforeSceneSwitchCallback, Action<DiContainer> afterSceneSwitchCallback, Action<StandardLevelScenesTransitionSetupDataSO, LevelCompletionResults> levelFinishedCallback)
        {
            Plugin.Log.Info($"Starting ({difficultyBeatmap.GetType().FullName}) {difficultyBeatmap.SerializedName()} {gameMode} {difficultyBeatmap.difficulty} {difficultyBeatmap.level.songName}");

            string startingGameModeName = difficultyBeatmap.parentDifficultyBeatmapSet.beatmapCharacteristic.serializedName;
            if (startingGameModeName == GameModeHelper.GENERATED_360DEGREE_MODE || startingGameModeName == GameModeHelper.GENERATED_90DEGREE_MODE)
            {
                Plugin.Log.Info("Generating rotation events...");

                if (!generated.Contains(difficultyBeatmap))
                {
                    generated.Add(difficultyBeatmap);
                    Generator360 gen = new Generator360();

                    if (startingGameModeName == GameModeHelper.GENERATED_90DEGREE_MODE)
                    {
                        gen.BottleneckRotations = 2;
                        gen.LimitRotations = 3;
                    }

                    gen.Generate(difficultyBeatmap);
                }
                else
                {
                    Plugin.Log.Info("Already generated rotation events");
                }
            }
        }
    }

    [HarmonyPatch(typeof(StandardLevelDetailView))]
    [HarmonyPatch("SetContent")]
    public class LevelUpdatePatcher
    {
        static void Prefix(StandardLevelDetailView __instance, IBeatmapLevel level, BeatmapDifficulty defaultDifficulty, BeatmapCharacteristicSO defaultBeatmapCharacteristic, PlayerData playerData, bool showPlayerStats)
        {
            List<BeatmapCharacteristicSO> toGenerate = new List<BeatmapCharacteristicSO>();
            if (Config.Instance.ShowGenerated90)
                toGenerate.Add(GameModeHelper.GetGenerated90GameMode());
            if (Config.Instance.ShowGenerated360)
                toGenerate.Add(GameModeHelper.GetGenerated360GameMode());

            List<IDifficultyBeatmapSet> sets = new List<IDifficultyBeatmapSet>(level.beatmapLevelData.difficultyBeatmapSets);

            // Generate each custom gamemode
            foreach (BeatmapCharacteristicSO customGameMode in toGenerate)
            {
                if (level.beatmapLevelData.difficultyBeatmapSets.Any((e) => e.beatmapCharacteristic.serializedName == GameModeHelper.GENERATED_360DEGREE_MODE))
                {
                    // Already added the generated gamemode
                    continue;
                }

                IDifficultyBeatmapSet standard = level.beatmapLevelData.difficultyBeatmapSets.FirstOrDefault((e) => e.beatmapCharacteristic.serializedName == "Standard");
                if (standard == null)
                {
                    // Level does not have a standard mode to base its 360 mode on
                    continue;
                }

                CustomDifficultyBeatmapSet customSet = new CustomDifficultyBeatmapSet(customGameMode);
                CustomDifficultyBeatmap[] difficulties = standard.difficultyBeatmaps.Select((e) => new CustomDifficultyBeatmap(e.level, customSet, e.difficulty, e.difficultyRank, e.noteJumpMovementSpeed, e.noteJumpStartBeatOffset, e.beatmapData.GetCopy())).ToArray();
                customSet.SetCustomDifficultyBeatmaps(difficulties);
                sets.Add(customSet);
            }

            // Update difficultyBeatmapSets
            if (level.beatmapLevelData is BeatmapLevelData data)
            {
                if (!FieldHelper.Set(data, "_difficultyBeatmapSets", sets.ToArray()))
                {
                    Plugin.Log.Warn("Could not set new difficulty sets");
                    return;
                }
            }
            else
            {
                Plugin.Log.Info("Unsupported beatmapLevelData: " + (level.beatmapLevelData?.GetType().FullName ?? "null"));
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


