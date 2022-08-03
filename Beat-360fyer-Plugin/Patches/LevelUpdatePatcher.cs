using BeatmapSaveDataVersion3;
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

    [HarmonyPatch(typeof(BeatmapDataTransformHelper), "CreateTransformedBeatmapData")]
    public class BeatmapDataTransformHelperPatcher
    {
        static void Postfix(ref IReadonlyBeatmapData __result, IReadonlyBeatmapData beatmapData, IPreviewBeatmapLevel beatmapLevel, GameplayModifiers gameplayModifiers, bool leftHanded, EnvironmentEffectsFilterPreset environmentEffectsFilterPreset, EnvironmentIntensityReductionOptions environmentIntensityReductionOptions, MainSettingsModelSO mainSettingsModel)
        {
            if (TransitionPatcher.startingGameMode == GameModeHelper.GENERATED_360DEGREE_MODE || TransitionPatcher.startingGameMode == GameModeHelper.GENERATED_90DEGREE_MODE)
            {
                Plugin.Log.Info($"Generating rotation events for {TransitionPatcher.startingGameMode}...");

                Generator360 gen = new Generator360();
                gen.WallGenerator = Config.Instance.EnableWallGenerator;
                gen.OnlyOneSaber = Config.Instance.OnlyOneSaber;

                if (TransitionPatcher.startingGameMode == GameModeHelper.GENERATED_90DEGREE_MODE)
                {
                    gen.LimitRotations = Config.Instance.LimitRotations90;
                    gen.BottleneckRotations = Config.Instance.LimitRotations90 / 2;
                }
                else if (TransitionPatcher.startingGameMode == GameModeHelper.GENERATED_360DEGREE_MODE)
                {
                    gen.LimitRotations = Config.Instance.LimitRotations360;
                    gen.BottleneckRotations = Config.Instance.LimitRotations360 / 2;
                }

                __result = gen.Generate(__result, beatmapLevel.beatsPerMinute);
            }
        }
    }

    [HarmonyPatch(typeof(MenuTransitionsHelper))]
    [HarmonyPatch("StartStandardLevel", new[] { typeof(string), typeof(IDifficultyBeatmap), typeof(IPreviewBeatmapLevel), typeof(OverrideEnvironmentSettings), typeof(ColorScheme), typeof(GameplayModifiers), typeof(PlayerSpecificSettings), typeof(PracticeSettings), typeof(string), typeof(bool), typeof(bool), typeof(Action), typeof(Action<DiContainer>), typeof(Action<StandardLevelScenesTransitionSetupDataSO, LevelCompletionResults>) })]
    public class TransitionPatcher
    {
        public static string startingGameMode;

        static void Prefix(string gameMode, IDifficultyBeatmap difficultyBeatmap, IPreviewBeatmapLevel previewBeatmapLevel, OverrideEnvironmentSettings overrideEnvironmentSettings, ColorScheme overrideColorScheme, GameplayModifiers gameplayModifiers, PlayerSpecificSettings playerSpecificSettings, PracticeSettings practiceSettings, string backButtonText, bool useTestNoteCutSoundEffects, bool startPaused, Action beforeSceneSwitchCallback, Action<DiContainer> afterSceneSwitchCallback, Action<StandardLevelScenesTransitionSetupDataSO, LevelCompletionResults> levelFinishedCallback)
        {
            Plugin.Log.Info($"Starting ({difficultyBeatmap.GetType().FullName}) {difficultyBeatmap.SerializedName()} {gameMode} {difficultyBeatmap.difficulty} {difficultyBeatmap.level.songName}");
            startingGameMode = difficultyBeatmap.parentDifficultyBeatmapSet.beatmapCharacteristic.serializedName;
        }
    }


    [HarmonyPatch(typeof(StandardLevelDetailView))]
    [HarmonyPatch("SetContent")]
    public class LevelUpdatePatcher
    {
        static void Prefix(StandardLevelDetailView __instance, IBeatmapLevel level, BeatmapDifficulty defaultDifficulty, BeatmapCharacteristicSO defaultBeatmapCharacteristic, PlayerData playerData)
        {
            List<BeatmapCharacteristicSO> toGenerate = new List<BeatmapCharacteristicSO>();
            if (Config.Instance.ShowGenerated360)
                toGenerate.Add(GameModeHelper.GetGenerated360GameMode());
            if (Config.Instance.ShowGenerated90)
                toGenerate.Add(GameModeHelper.GetGenerated90GameMode());

            List<IDifficultyBeatmapSet> sets = new List<IDifficultyBeatmapSet>(level.beatmapLevelData.difficultyBeatmapSets);

            // Generate each custom gamemode
            foreach (BeatmapCharacteristicSO customGameMode in toGenerate)
            {
                if (level.beatmapLevelData.difficultyBeatmapSets.Any((e) => e.beatmapCharacteristic.serializedName == GameModeHelper.GENERATED_360DEGREE_MODE))
                {
                    // Already added the generated gamemode
                    continue;
                }

                IDifficultyBeatmapSet basedOnGameMode = level.beatmapLevelData.difficultyBeatmapSets.FirstOrDefault((e) => e.beatmapCharacteristic.serializedName == Config.Instance.BasedOn);
                if (basedOnGameMode == null)
                {
                    // Level does not have a standard mode to base its 360 mode on
                    continue;
                }

                IDifficultyBeatmapSet newSet;
                if (basedOnGameMode.difficultyBeatmaps[0] is BeatmapLevelSO.DifficultyBeatmap)
                {
                    IReadOnlyList<IDifficultyBeatmap> difficultyBeatmaps = basedOnGameMode.difficultyBeatmaps.Select((bm) => new BeatmapLevelSO.DifficultyBeatmap(bm.level, bm.difficulty, bm.difficultyRank, bm.noteJumpMovementSpeed, bm.noteJumpStartBeatOffset, FieldHelper.Get<BeatmapDataSO>(bm, "_beatmapData"))).ToList();
                    newSet = new DifficultyBeatmapSet(customGameMode, difficultyBeatmaps);
                }
                else if (basedOnGameMode.difficultyBeatmaps[0] is CustomDifficultyBeatmap)
                {
                    CustomDifficultyBeatmapSet customSet = new CustomDifficultyBeatmapSet(customGameMode);
                    CustomDifficultyBeatmap[] difficultyBeatmaps = basedOnGameMode.difficultyBeatmaps.Select((bm) =>
                    {
                        CustomDifficultyBeatmap cbm = (CustomDifficultyBeatmap)bm;
                        return new CustomDifficultyBeatmap(cbm.level, customSet, cbm.difficulty, cbm.difficultyRank, cbm.noteJumpMovementSpeed, cbm.noteJumpStartBeatOffset, cbm.beatsPerMinute, cbm.beatmapSaveData, cbm.beatmapDataBasicInfo);
                    }).ToArray();
                    customSet.SetCustomDifficultyBeatmaps(difficultyBeatmaps);

                    newSet = customSet;
                }
                else
                {
                    continue;
                }

                sets.Add(newSet);
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
}


