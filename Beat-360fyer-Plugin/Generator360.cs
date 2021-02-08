using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Beat_360fyer_Plugin
{
    public class Generator360
    {
        /// <summary>
        /// Maximum amount of rotation events per second (not per beat) 
        /// </summary>
        public int MaxRotationsPerSecond { get; set; } = 8;
        /// <summary>
        /// When lower, less notes are required to create larger rotations (more degree turns at once, can get disorienting)
        /// </summary>
        public float RotationDivider { get; set; } = 0.35f;
        /// <summary>
        /// The amount of rotations before stopping rotation events (rip cable otherwise) 
        /// </summary>
        public int LimitRotations { get; set; } = 32;
        /// <summary>
        /// The amount of rotations before preffering the other direction
        /// </summary>
        public int BottleneckRotations { get; set; } = 24;
        /// <summary>
        /// Enable the spin effect when no notes are coming.
        /// </summary>
        public bool EnableSpin { get; set; } = true;
        /// <summary>
        /// The total time 1 spin takes in seconds.
        /// </summary>
        public float TotalSpinTime { get; set; } = 0.3f;
        /// <summary>
        /// Amount of time in seconds to cut of the front of a wall when rotating towards it.
        /// </summary>
        public float WallFrontCut { get; set; } = 0.07f;
        /// <summary>
        /// Amount of time in seconds to cut of the back of a wall when rotating towards it.
        /// </summary>
        public float WallBackCut { get; set; } = 0.11f;

        public void Generate(IDifficultyBeatmap bm)
        {
            // Amount of rotation events emitted
            int eventCount = 0;
            // Current rotation
            int rotation = 0;
            // Moments where a wall should be cut
            List<(float, int)> wallCutMoments = new List<(float, int)>();
            // Previous spin direction, false is left, true is right
            bool previousDirection = false;
            // The time of the previous rotation event that was emitted
            float previousRotationTime = 0f;

            float alignedMinRotationInterval = 60f / bm.level.beatsPerMinute;
            while (alignedMinRotationInterval > 1f / MaxRotationsPerSecond * 1.25f)
                alignedMinRotationInterval *= 0.5f;
            while (alignedMinRotationInterval < 1f / MaxRotationsPerSecond * 0.75f)
                alignedMinRotationInterval *= 2f;

            Plugin.Log.Info($"Aligned {alignedMinRotationInterval} from {1f / MaxRotationsPerSecond} (bpm={bm.level.beatsPerMinute}, maxRot={MaxRotationsPerSecond})");

            // Negative numbers rotate to the left, positive to the right
            void Rotate(float time, int amount)
            {
                if (amount == 0)
                    return;
                if (amount < -4)
                    amount = -4;
                if (amount > 4)
                    amount = 4;
                if (rotation + amount > LimitRotations || rotation + amount < -LimitRotations)
                    return;
                previousDirection = amount > 0;
                previousRotationTime = time;
                time += 0.002f; // Small delay to place the rotation event after the note
                rotation += amount;
                eventCount++;
                wallCutMoments.Add((time, amount));
                bm.beatmapData.AddBeatmapEventData(new BeatmapEventData(time, BeatmapEventType.Event15, amount > 0 ? 3 + amount : 4 + amount));
            }

            // The time of 1 beat in seconds
            float beatDuration = 60f / bm.level.beatsPerMinute;

            Plugin.Log.Info($"[Generator] Start, bpm={bm.level.beatsPerMinute} beatDuration={beatDuration}");

            List<BeatmapObjectData> objects = new List<BeatmapObjectData>(bm.beatmapData.beatmapObjectsData);
            List<ObstacleData> obstacles = new List<ObstacleData>();
            List<NoteData> currentNotes = new List<NoteData>();
            for (int i = 0; i < objects.Count; i++)
            {
                currentNotes.Clear();

                // All the currentNotes will have around the same time (negligible, just use the first note's time)
                float time = objects[i].time;

                for (; i < objects.Count && (objects[i].time - time) * beatDuration <= 0.01f; i++)
                {
                    BeatmapObjectData d = objects[i];
                    if (d is NoteData n)
                    {
                        if (n.cutDirection != NoteCutDirection.None && n.cutDirection != NoteCutDirection.Any) // Filter bombs and any direction notes
                            currentNotes.Add(n);
                    }
                    else if (d is ObstacleData o)
                    {
                        obstacles.Add(o);
                    }
                }

                // If only bombs/obstacles
                if (currentNotes.Count == 0)
                {
                    continue;
                }

                // Bottleneck rotation events
                if ((time - previousRotationTime) * beatDuration < alignedMinRotationInterval)
                {
                    continue;
                }

                // Get next object's time, nextNoteTime cannot be zero
                float nextObjectTime = i >= objects.Count ? float.MaxValue : objects[i].time;

#if DEBUG
                if (!(time < nextObjectTime) || ((nextObjectTime - time) < 0.001f))
                    Plugin.Log.Warn($"Assert failed: time < nextObjectTime, time={time}, nextObjectTime={nextObjectTime}, nextObjectTime - time = {nextObjectTime - time}");
#endif
                // Amount of total notes, notes pointing to the left/right
                int count = currentNotes.Count;
                int leftCount = currentNotes.Count((e) =>
                        e.cutDirection == NoteCutDirection.DownLeft
                    || e.cutDirection == NoteCutDirection.Left
                    || e.cutDirection == NoteCutDirection.UpLeft
                    || ((e.lineIndex == 0 || e.lineIndex == 1) && (e.cutDirection != NoteCutDirection.Right && e.cutDirection != NoteCutDirection.DownRight && e.cutDirection != NoteCutDirection.UpRight) && e.noteLineLayer == NoteLineLayer.Base)); // (e.cutDirection == NoteCutDirection.Left && e.noteLineLayer != NoteLineLayer.Top) ||
                int rightCount = currentNotes.Count((e) =>
                    e.cutDirection == NoteCutDirection.DownRight
                    || e.cutDirection == NoteCutDirection.Right
                    || e.cutDirection == NoteCutDirection.UpRight
                    || ((e.lineIndex == 2 || e.lineIndex == 3) && (e.cutDirection != NoteCutDirection.Left && e.cutDirection != NoteCutDirection.DownLeft && e.cutDirection != NoteCutDirection.UpLeft) && e.noteLineLayer == NoteLineLayer.Base));

                // RotationDivider: 1f       | 2f
                // timeDiffInSec  | divider  | divider
                // 2              | 0.5      | 1
                // 1              | 1        | 2
                // 0.5            | 2        | 4
                // 0.25           | 4        | 8
                // 0.125          | 8        | 16
                // 0.1            | 16       | 32
                int divider = (int)(RotationDivider / ((nextObjectTime - time) * beatDuration));
                if (divider < 1) 
                    divider = 1;

                // Spin effect
                if (EnableSpin && count >= 2 && (nextObjectTime - time) * beatDuration > TotalSpinTime)
                {
                    Plugin.Log.Info($"[Generator] Spin effect at {time}: ({nextObjectTime} - {time}) * {beatDuration} = {(nextObjectTime - time) * beatDuration}");
                    const int SPIN_STEP = 2;
                    float spinStep = TotalSpinTime / (24 / SPIN_STEP) / beatDuration;

                    int spinDirection;
                    if (leftCount == rightCount)
                        spinDirection = previousDirection ? -SPIN_STEP : SPIN_STEP;
                    else if (leftCount > rightCount)
                        spinDirection = -SPIN_STEP;
                    else // direction > 0
                        spinDirection = SPIN_STEP;

                    for (int s = 0; s < 24 / SPIN_STEP; s++)
                    {
                        Rotate(time + spinStep * s, spinDirection);
                    }

                    // Do not emit more rotation events after this
                    continue;
                }

                // Normal rotation event
                if (leftCount > rightCount)
                {
                    Rotate(time, -(leftCount / divider + 1));
                }
                else if (rightCount > leftCount)
                {
                    Rotate(time, rightCount / divider + 1);
                }
                else if (count >= divider)
                {
                    int c = count / divider;
                    if (rotation <= -BottleneckRotations)
                    {
                        Rotate(time, c);
                    }
                    else if (rotation >= BottleneckRotations)
                    {
                        Rotate(time, -c);
                    }
                    else
                    {
                        Rotate(time, previousDirection ? -c : c);
                    }
                }

                //Plugin.Log.Info($"{leftCount} <- {count} -> {rightCount} at {time} [+{time - previousRotationTime} beats / +{(time - previousRotationTime) * beatDuration} seconds] (divider={divider}, timeTillNextObject={nextObjectTime - time})");
            }

            // Cut walls, walls will be cut when a rotation event is emitted
            foreach (ObstacleData ob in obstacles)
            {
                foreach ((float cutTime, int cutAmount) in wallCutMoments)
                {
                    // If wall is uncomfortable for 360Degree mode, remove it
                    if (ob.lineIndex == 1 || ob.lineIndex == 2 || (ob.obstacleType == ObstacleType.FullHeight && ob.lineIndex == 0 && ob.width > 1))
                    {
                        // TODO: Wall is not fun in 360, remove it
                        FieldHelper.SetProperty(ob, nameof(ob.duration), 0f);
                    }
                    // If moved in direction of wall
                    else if ((ob.lineIndex <= 1 && cutAmount < 0) || (ob.lineIndex >= 2 && cutAmount > 0))
                    {
                        float wallStartCutBeats = WallFrontCut / beatDuration;
                        if (cutTime >= ob.time - wallStartCutBeats && cutTime < ob.time + ob.duration / 2f)
                        {
                            // Cut front of wall
                            float cut = cutTime - (ob.time - wallStartCutBeats);

                            Plugin.Log.Info($"[Generator] Cut front wall at {ob.time}({ob.duration}) cut {cut}");

                            //ob.time += cut;
                            //FieldHelper.SetProperty(ob, nameof(ob.time), ob.time + cut);
                            ob.MoveTime(ob.time + cut);
                            //ob.duration -= cut;
                            FieldHelper.SetProperty(ob, nameof(ob.duration), ob.duration - cut);
                        }

                        float wallEndCutBeats = WallBackCut / beatDuration;
                        if (cutTime >= ob.time + ob.duration / 2 && cutTime < ob.time + ob.duration + wallEndCutBeats)
                        {
                            // Cut back of wall
                            float cut = (ob.time + ob.duration + wallEndCutBeats) - cutTime;

                            Plugin.Log.Info($"[Generator] Cut back wall at {ob.time}({ob.duration}) cut {cut}");

                            //ob.duration -= cut;
                            FieldHelper.SetProperty(ob, nameof(ob.duration), ob.duration - cut);
                        }
                    }

                    if (ob.duration <= 0f)
                    {
                        // TODO: remove wall instead of move to start
                        ob.MoveTime(0f);
                    }
                }
            }

            //int removedWalls = objects.RemoveAll((e) => e is ObstacleData d && d.duration <= 0f);
            //Plugin.Log.Info($"Removed {removedWalls} walls");

            // Resort events...
            // NOTE TO SELF: difficultyBeatmap.beatmapData.beatmapObjectsData/events MUST BE SORTED!
            List<BeatmapEventData> sorted = bm.beatmapData.beatmapEventsData.OrderBy((e) => e.time).ToList();
            FieldHelper.Set(bm.beatmapData, "_beatmapEventsData", sorted);

            Plugin.Log.Info($"[Generator] Emitted {eventCount} rotation events");
        }

    }
}
