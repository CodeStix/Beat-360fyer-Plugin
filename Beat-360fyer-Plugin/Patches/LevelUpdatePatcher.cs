using HarmonyLib;
using LibBeatGenerator;
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
    [HarmonyPatch("StartStandardLevel", new[] { typeof(string), typeof(IDifficultyBeatmap), typeof(IPreviewBeatmapLevel), typeof(OverrideEnvironmentSettings), typeof(ColorScheme), typeof(GameplayModifiers), typeof(PlayerSpecificSettings), typeof(PracticeSettings), typeof(string), typeof(bool), typeof(Action), typeof(Action<DiContainer>), typeof(Action<StandardLevelScenesTransitionSetupDataSO, LevelCompletionResults>) })]
    public class TransitionPatcher
    {
        private static HashSet<IDifficultyBeatmap> generated = new HashSet<IDifficultyBeatmap>();

        private static Generator360 generator;

        static TransitionPatcher()
        {
            Generator360.Logger = Plugin.Log.Info;
            generator = new Generator360();
        }

        static void Prefix(string gameMode, IDifficultyBeatmap difficultyBeatmap, OverrideEnvironmentSettings overrideEnvironmentSettings, ref ColorScheme overrideColorScheme, GameplayModifiers gameplayModifiers, PlayerSpecificSettings playerSpecificSettings, PracticeSettings practiceSettings, string backButtonText, bool useTestNoteCutSoundEffects, Action beforeSceneSwitchCallback, Action<DiContainer> afterSceneSwitchCallback, Action<StandardLevelScenesTransitionSetupDataSO, LevelCompletionResults> levelFinishedCallback)
        {
#if DEBUG
            Plugin.Log.Info($"Starting ({difficultyBeatmap.GetType().FullName}) {difficultyBeatmap.SerializedName()} {gameMode} {difficultyBeatmap.difficulty} {difficultyBeatmap.level.songName}");
#endif
            string startingGameModeName = difficultyBeatmap.parentDifficultyBeatmapSet.beatmapCharacteristic.serializedName;
            if (startingGameModeName == GameModeHelper.GENERATED_360DEGREE_MODE || startingGameModeName == GameModeHelper.GENERATED_90DEGREE_MODE)
            {
                Plugin.Log.Info($"Generating rotation events for {startingGameModeName}...");

                // Colors are not copied from standard mode for some reason? Enforce it here
                var mapCustomColors = difficultyBeatmap.level.environmentInfo?.colorScheme?.colorScheme; 
                if (mapCustomColors != null && overrideColorScheme == null)
                {
                    Plugin.Log.Info($"Overriding custom colors with {mapCustomColors.environmentColor0} {mapCustomColors.environmentColor1}");
                    overrideColorScheme = mapCustomColors;
                }

                if (!generated.Contains(difficultyBeatmap))
                {
                    generated.Add(difficultyBeatmap);

                    generator.WallGenerator = Config.Instance.EnableWallGenerator;
                    generator.OnlyOneSaber = Config.Instance.OnlyOneSaber;
                    if (startingGameModeName == GameModeHelper.GENERATED_90DEGREE_MODE)
                    {
                        generator.LimitRotations = Config.Instance.LimitRotations90;
                        generator.BottleneckRotations = Config.Instance.LimitRotations90 / 2;
                    }
                    else if (startingGameModeName == GameModeHelper.GENERATED_360DEGREE_MODE)
                    {
                        generator.LimitRotations = Config.Instance.LimitRotations360;
                        generator.BottleneckRotations = Config.Instance.LimitRotations360 / 2;
                    }

                    ModBeatmapData mod = difficultyBeatmap.ToModBeatmap();
                    generator.Generate(mod);
                    mod.ToBeatmap(difficultyBeatmap);
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

                CustomDifficultyBeatmapSet customSet = new CustomDifficultyBeatmapSet(customGameMode);
                CustomDifficultyBeatmap[] difficulties = basedOnGameMode.difficultyBeatmaps.Select((e) => new CustomDifficultyBeatmap(e.level, customSet, e.difficulty, e.difficultyRank, e.noteJumpMovementSpeed, e.noteJumpStartBeatOffset, e.beatmapData.GetCopy())).ToArray();
                customSet.SetCustomDifficultyBeatmaps(difficulties);
                sets.Add(customSet);
            }

            // Update difficultyBeatmapSets
            if (level.beatmapLevelData is BeatmapLevelData data)
            {
                if (!FieldHelper.Set(data, "_difficultyBeatmapSets", sets.ToArray()))
                {
                    Plugin.Log.Error("Could not set new difficulty sets");
                    return;
                }
            }
            else
            {
                Plugin.Log.Error("Unsupported beatmapLevelData: " + (level.beatmapLevelData?.GetType().FullName ?? "null"));
            }
        }
    }
}


