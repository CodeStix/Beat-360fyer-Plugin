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

                // Generate rotation events
                // difficultyBeatmap.beatmapData.beatmapObjectsData MUST BE SORTED!

                // The amount of rotations to bottleneck the rotation event at (will prefer to go to the other direction)
                const int BOTTLENECK_ROTATIONS = 10;
                // The amount of rotations before stopping rotation events
                const int LIMIT_ROTATIONS = 28;
                // Enable the spin effect when no notes are coming
                const bool ENABLE_SPIN = true;
                const float SPIN_STEP_TIME = 0.03f;
                // Amount of time to cut of the front/back of a wall when rotating towards it
                const float WALL_START_CUT = 0.25f;
                const float WALL_END_CUT = 0.65f;

                // Amount of rotation events emitted
                int eventCount = 0;
                // Current rotation
                int rotation = 0;
                // Moments where a wall should be cut
                List<(float, int)> wallCutMoments = new List<(float, int)>();

                // Negative numbers rotate to the left, positive to the right
                void Rotate(float time, int amount)
                {
                    if (amount == 0)
                        return;
                    if (amount < -4) 
                        amount = -4;
                    if (amount > 4) 
                        amount = 4;
                    if (rotation + amount > LIMIT_ROTATIONS || rotation + amount < -LIMIT_ROTATIONS) 
                        return;
                    time += 0.005f; // Small delay to place the rotation event after the note
                    rotation += amount;
                    eventCount++;
                    wallCutMoments.Add((time, amount));
                    difficultyBeatmap.beatmapData.AddBeatmapEventData(new BeatmapEventData(time, BeatmapEventType.Event15, amount > 0 ? 3 + amount : 4 + amount));
                }

                // Loop time increment, will be a around a second that is aligned with beatsPerMinute
                float alignedSecond = 60f / difficultyBeatmap.level.beatsPerMinute;
                while (alignedSecond > 1.25f)
                    alignedSecond *= 0.5f;
                while (alignedSecond < 0.75f)
                    alignedSecond *= 2;

                // Start time of current fragment, fragment will be of size deltaTime
                float start = difficultyBeatmap.level.songTimeOffset;
                // Time of last spin or last note
                float spinTimer = float.MaxValue;

                Plugin.Log.Info($"[StartStandardLevel] Start, deltaTime={alignedSecond} start={start}");

                // Previous spin direction, false is left, true is right
                bool previousDirection = false;
                List<NoteData> lastNotes = new List<NoteData>();
                List<ObstacleData> obstacles = new List<ObstacleData>();
                foreach (BeatmapObjectData currentNote in difficultyBeatmap.beatmapData.beatmapObjectsData)
                {
                    while (currentNote.time >= start + alignedSecond)
                    {
                        // A 'second' (aligned to beatsPerMinute) has passed

                        if (ENABLE_SPIN && start - spinTimer >= SPIN_STEP_TIME * 24)
                        {
                            // Spin effect
                            Plugin.Log.Info($"[StartStandardLevel] Spin effect at {spinTimer} ({start - spinTimer} > {SPIN_STEP_TIME * 24})");
                            for (int i = 0; i < 24; i++)
                            {
                                Rotate(spinTimer + SPIN_STEP_TIME * i, previousDirection ? 1 : -1);
                            }
                            spinTimer = float.MaxValue;
                        }

                        // lastNotes.Count = amount of notes last second
                        if (lastNotes.Count == 0)
                        {
                            start += alignedSecond;
                            continue;
                        }

                        // Total notes and amount of notes that point to the left and right, including floor notes on their side
                        int count = lastNotes.Count;
                        int leftCount = lastNotes.Count((e) => 
                            e.cutDirection == NoteCutDirection.DownLeft 
                            || e.cutDirection == NoteCutDirection.Left
                            || e.cutDirection == NoteCutDirection.UpLeft 
                            || ((e.lineIndex == 0 || e.lineIndex == 1) && (e.cutDirection != NoteCutDirection.Right && e.cutDirection != NoteCutDirection.DownRight && e.cutDirection != NoteCutDirection.UpRight) && e.noteLineLayer == NoteLineLayer.Base)); // (e.cutDirection == NoteCutDirection.Left && e.noteLineLayer != NoteLineLayer.Top) || 
                        int rightCount = lastNotes.Count((e) => 
                            e.cutDirection == NoteCutDirection.DownRight 
                            || e.cutDirection == NoteCutDirection.Right
                            || e.cutDirection == NoteCutDirection.UpRight
                            || ((e.lineIndex == 2 || e.lineIndex == 3) && (e.cutDirection != NoteCutDirection.Left && e.cutDirection != NoteCutDirection.DownLeft && e.cutDirection != NoteCutDirection.UpLeft) && e.noteLineLayer == NoteLineLayer.Base));

                        // Limit rotations
                        if (rotation < -BOTTLENECK_ROTATIONS)
                            rightCount += -rotation / BOTTLENECK_ROTATIONS; // Prefer going to the right when moved a lot to the left
                        else if (rotation > BOTTLENECK_ROTATIONS)
                            leftCount += rotation / BOTTLENECK_ROTATIONS; // Prefer going to the left when moved a lot to the right

                        // Favor previous direction
                        //if (previousDirection) 
                        //    rightCount++;
                        //else
                        //    leftCount++;

                        int leftRight = -leftCount + rightCount;
                        if (leftRight <= -2)
                        {
                            Rotate(lastNotes[count - 1].time, -(leftCount / 6 + 1));
                            previousDirection = false;
                            Plugin.Log.Info($"[StartStandardLevel] <- {-(leftCount / 4 + 1)} ({count} nps)");
                        }
                        else if (leftRight >= 2)
                        {
                            Rotate(lastNotes[count - 1].time, rightCount / 6 + 1);
                            previousDirection = true;
                            Plugin.Log.Info($"[StartStandardLevel] -> {(rightCount / 4 + 1)} ({count} nps)");
                        }
                        else if (count <= 2) 
                        { 
                            if (eventCount / start < 1f)
                            {
                                // If not a lot of rotation events have been spawned
                                int n = UnityEngine.Random.Range(1, 3);
                                Rotate(lastNotes[0].time, previousDirection ? n : -n);
                                Plugin.Log.Info($"[StartStandardLevel] <2 n {(previousDirection ? n : -n)} {count} ({eventCount}/{start}={eventCount / start})");
                            }
                        }
                        else if (count <= 8)
                        {
                            Rotate(lastNotes[0].time, previousDirection ? 1 : -1);
                            Plugin.Log.Info($"[StartStandardLevel] <8 {count}");
                        }
                        else if (count <= 12)
                        {
                            bool prevDir = false;
                            float prev = float.MinValue;
                            int i = 0;
                            foreach(NoteData n in lastNotes)
                            {
                                if (n.time - prev >= alignedSecond / 8f)
                                {
                                    Rotate(n.time - 0.01f, prevDir ? -1 : 1);
                                    prevDir = !prevDir;
                                    prev = n.time;
                                    i++;
                                }
                            }
                            Plugin.Log.Info($"[StartStandardLevel] <12 {count} ({i})");
                        }
                        else if (count <= 16)
                        {
                            bool prevDir = false;
                            float prev = float.MinValue;
                            int i = 0;
                            foreach (NoteData n in lastNotes)
                            {
                                if (n.time - prev >= alignedSecond / 8f)
                                {
                                    Rotate(n.time, prevDir ? -1 : 1);
                                    prevDir = !prevDir;
                                    prev = n.time;
                                    i++;
                                }
                            }
                            Plugin.Log.Info($"[StartStandardLevel] <16 {count} ({i})");
                        }
                        else
                        {
                            Plugin.Log.Info($"[StartStandardLevel] >16 {count}");
                            // Nothing, way too fast
                        }
                        
                        lastNotes.Clear();
                        start += alignedSecond;
                    }

                    if (currentNote is NoteData note)
                    {
                        spinTimer = note.time;
                        lastNotes.Add(note);
                    }
                    else if (currentNote is ObstacleData obstacle)
                    {
                        obstacles.Add(obstacle);
                    }
                }

                // Cut walls, walls will be cut when a rotation event is emitted
                foreach(ObstacleData ob in obstacles)
                {
                    foreach((float cutTime, int cutAmount) in wallCutMoments)
                    {
                        // If wall is uncomfortable for 360Degree mode
                        if (ob.lineIndex == 1 || ob.lineIndex == 2 || (ob.obstacleType == ObstacleType.FullHeight && ob.lineIndex == 0 && ob.width > 1))
                        {
                            // TODO: Wall is not fun in 360, remove it
                            FieldHelper.SetProperty(ob, nameof(ob.duration), 0f);
                        }
                        // If moved in direction of wall
                        else if ((ob.lineIndex <= 1 && cutAmount < 0) || (ob.lineIndex >= 2 && cutAmount > 0))
                        {
                            if (cutTime >= ob.time - WALL_START_CUT && cutTime < ob.time + ob.duration / 2f)
                            {
                                // Cut front of wall
                                float cut = cutTime - (ob.time - WALL_START_CUT);

                                Plugin.Log.Info($"Cut front wall at {ob.time}({ob.duration}) cut {cut}");

                                //ob.time += cut;
                                //FieldHelper.SetProperty(ob, nameof(ob.time), ob.time + cut);
                                ob.MoveTime(ob.time + cut);
                                //ob.duration -= cut;
                                FieldHelper.SetProperty(ob, nameof(ob.duration), ob.duration - cut);
                            }

                            if (cutTime >= ob.time + ob.duration / 2 && cutTime < ob.time + ob.duration + WALL_END_CUT)
                            {
                                // Cut back of wall
                                float cut = (ob.time + ob.duration + WALL_END_CUT) - cutTime;

                                Plugin.Log.Info($"Cut back wall at {ob.time}({ob.duration}) cut {cut}");

                                //ob.duration -= cut;
                                FieldHelper.SetProperty(ob, nameof(ob.duration), ob.duration - cut);
                            }
                        }

                        if (ob.duration <= 0f)
                        {
                            // TODO: Remove wall
                            Plugin.Log.Warn($"Need to remove wall at {ob.time}, has negative duration (duration is {ob.duration})");
                        }
                    }
                }

                // Resort events...
                List<BeatmapEventData> sorted = difficultyBeatmap.beatmapData.beatmapEventsData.OrderBy((e) => e.time).ToList();
                FieldHelper.Set(difficultyBeatmap.beatmapData, "_beatmapEventsData", sorted);

                Plugin.Log.Info($"[StartStandardLevel] Emitted {eventCount} rotation events");
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


