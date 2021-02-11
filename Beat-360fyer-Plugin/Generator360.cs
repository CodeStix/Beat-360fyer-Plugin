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
        /// (this is a float because the rotations will be around this value, depends per song because it gets aligned with each songs bpm)
        /// </summary>
        public float MaxRotationsPerSecond { get; set; } = 4f;
        /// <summary>
        /// When lower, less notes are required to create larger rotations (more degree turns at once, can get disorienting)
        /// </summary>
        public float RotationDivider { get; set; } = 1f;
        /// <summary>
        /// The amount of rotations before stopping rotation events (rip cable otherwise) 
        /// </summary>
        public int LimitRotations { get; set; } = 320;
        /// <summary>
        /// The amount of rotations before preffering the other direction
        /// </summary>
        public int BottleneckRotations { get; set; } = 240;
        /// <summary>
        /// Enable the spin effect when no notes are coming.
        /// </summary>
        public bool EnableSpin { get; set; } = true;
        /// <summary>
        /// The total time 1 spin takes in seconds.
        /// </summary>
        public float TotalSpinTime { get; set; } = 0.6f;
        /// <summary>
        /// Amount of time in seconds to cut of the front of a wall when rotating towards it.
        /// </summary>
        public float WallFrontCut { get; set; } = 0.085f;
        /// <summary>
        /// Amount of time in seconds to cut of the back of a wall when rotating towards it.
        /// </summary>
        public float WallBackCut { get; set; } = 0.15f;

        public void Generate(IDifficultyBeatmap bm)
        {
            ModBeatmapData data = new ModBeatmapData(bm.beatmapData);
#if DEBUG
            // Remove all laser events (will be used to debug)
            data.events = data.events.Where((e) => e.type != BeatmapEventType.Event0 || e.type != BeatmapEventType.Event2 || e.type != BeatmapEventType.Event3).ToList();
            data.events.Add(new ModEvent(0f, BeatmapEventType.Event12, 0));
            data.events.Add(new ModEvent(0f, BeatmapEventType.Event13, 0));
#endif

            // Amount of rotation events emitted
            int eventCount = 0;
            // Current rotation
            int rotation = 0;
            // Moments where a wall should be cut
            List<(float, int)> wallCutMoments = new List<(float, int)>();
            // Previous spin direction, false is left, true is right
            bool previousDirection = true;
            // The time of the previous rotation event that was emitted
            float previousNoteWithRotationTime = 0f;

            float alignedMinRotationInterval = 60f / bm.level.beatsPerMinute;
            while (alignedMinRotationInterval > 1f / MaxRotationsPerSecond * 1.25f)
                alignedMinRotationInterval *= 0.5f;
            while (alignedMinRotationInterval < 1f / MaxRotationsPerSecond * 0.75f)
                alignedMinRotationInterval *= 2f;

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
                rotation += amount;
                eventCount++;
                wallCutMoments.Add((time, amount));

                // Place before note (- 0.005f)
                data.events.Add(new ModEvent(time, BeatmapEventType.Event15, amount > 0 ? 3 + amount : 4 + amount));
#if DEBUG
                // Light debug indicator
                data.events.Add(new ModEvent(time, BeatmapEventType.Event0, 1));
                data.events.Add(new ModEvent(time + 0.05f, BeatmapEventType.Event0, 0));
#endif
            }

            // The time of 1 beat in seconds
            //float beatDuration = 60f / bm.level.beatsPerMinute;

            Plugin.Log.Info($"[Generator] Start, alignedMinRotationInterval={alignedMinRotationInterval} (~{1f / MaxRotationsPerSecond}) bpm={bm.level.beatsPerMinute}");

            List<ModNoteData> currentNotes = new List<ModNoteData>();
            for (int i = 0; i < data.objects.Count; )
            {
                currentNotes.Clear();

                // All the currentNotes will have around the same time (negligible, just use the first note's time)
                float time = data.objects[i].time;

                for (; i < data.objects.Count && data.objects[i].time - time <= 0.01f; i++)
                {
                    ModObject d = data.objects[i];
                    if (d is ModNoteData n)
                    {
                        if (!n.IsBomb && n.cutDirection != NoteCutDirection.Any) // Filter bombs and any direction notes
                            currentNotes.Add(n);
                    }
                }

                // If only bombs/obstacles
                if (currentNotes.Count == 0)
                {
                    continue;
                }

                // Get next object's time, nextNoteTime cannot be zero
                float nextNoteTime = float.MaxValue;
                for(int j = i; j < data.objects.Count; j++)
                {
                    if (data.objects[j] is ModNoteData n)
                    {
                        nextNoteTime = n.time;
                        break;
                    }
                }

#if DEBUG
                if (!(time < nextNoteTime) || ((nextNoteTime - time) < 0.001f))
                    Plugin.Log.Warn($"Assert failed: time < nextObjectTime, time={time}, nextObjectTime={nextNoteTime}, nextObjectTime - time = {nextNoteTime - time}");
#endif

                // Amount of total notes, notes pointing to the left/right
                int count = currentNotes.Count;
                int leftCount = currentNotes.Count((e) =>
                    e.cutDirection == NoteCutDirection.DownLeft
                    || e.cutDirection == NoteCutDirection.Left
                    || e.cutDirection == NoteCutDirection.UpLeft || e.lineIndex == 0); // + currentNotes.Count((e) => e.lineIndex == 0 || e.lineIndex == 1); 
                int rightCount = currentNotes.Count((e) =>
                    e.cutDirection == NoteCutDirection.DownRight
                    || e.cutDirection == NoteCutDirection.Right
                    || e.cutDirection == NoteCutDirection.UpRight || e.lineIndex == 3); // + currentNotes.Count((e) => e.lineIndex == 2 || e.lineIndex == 3);

                // Spin effect
                if (EnableSpin && count >= 2 && nextNoteTime - time > TotalSpinTime + 0.6f)
                {
                    Plugin.Log.Info($"[Generator] Spin effect at {time}: ({nextNoteTime} - {time}) = {(nextNoteTime - time)}");
                    float spinStep = TotalSpinTime / 24;

                    int spinDirection;
                    if (leftCount == rightCount)
                        spinDirection = previousDirection ? 1 : -1;
                    else if (leftCount > rightCount)
                        spinDirection = -1;
                    else // direction > 0
                        spinDirection = 1;

                    for (int s = 0; s < 24; s++)
                    {
                        Rotate(time + spinStep * s, spinDirection);
                    }

                    // Do not emit more rotation events after this
                    previousNoteWithRotationTime = time;
                    continue;
                }

                // Bottleneck rotation events
                if (time - previousNoteWithRotationTime < alignedMinRotationInterval)
                {
                    continue;
                }

                // Determine the amount we will spin at once (1, 2, 3 or 4 times)
                int divider = (int)(RotationDivider / (nextNoteTime - time));
                if (divider < 1)
                    divider = 1;

                bool placeAfter = false; // (nextNoteTime - time) > 0.4f;
                float rotateTime = time + (placeAfter ? 0.001f : -0.001f);

                // Normal rotation event
                int dir = -leftCount + rightCount;
                if (dir <= -1)
                {
                    Plugin.Log.Info($"[{time}] Rotate left {dir / divider - 1} (dir={dir},divider={divider},leftCount={leftCount},rightCount={rightCount},count={count},placeAfter={placeAfter},nextNoteTime={nextNoteTime})");
                    Rotate(rotateTime, dir / divider - 1);
                    previousNoteWithRotationTime = time;
                }
                else if (dir >= 1)
                {
                    Plugin.Log.Info($"[{time}] Rotate right {dir / divider + 1} (dir={dir},divider={divider},leftCount={leftCount},rightCount={rightCount},count={count},placeAfter={placeAfter},nextNoteTime={nextNoteTime})");
                    Rotate(rotateTime, dir / divider + 1);
                    previousNoteWithRotationTime = time;
                }
                else if (count >= divider)
                {
                    int c = count / divider;
                    if (rotation <= -BottleneckRotations)
                    {
                        Plugin.Log.Warn($"[{time}] Bottlenecking left rotations at {time}");
                        Rotate(rotateTime, c);
                        previousNoteWithRotationTime = time;
                    }
                    else if (rotation >= BottleneckRotations)
                    {
                        Plugin.Log.Warn($"[{time}] Bottlenecking right rotations at {time}");
                        Rotate(rotateTime, -c);
                        previousNoteWithRotationTime = time;
                    }
                    else
                    {
                        Plugin.Log.Info($"[{time}] Rotate {(previousDirection ? c : -c)} (c={c},leftCount={leftCount},rightCount={rightCount},count={count},placeAfter={placeAfter},nextNoteTime={nextNoteTime})");
                        Rotate(rotateTime, previousDirection ? c : -c);
                        previousNoteWithRotationTime = time;
                    }
                }

                //Plugin.Log.Info($"{leftCount} <- {count} -> {rightCount} at {time} [+{time - previousRotationTime} beats / +{(time - previousRotationTime)} seconds] (divider={divider}, timeTillNextObject={nextObjectTime - time})");
            }

            // Cut walls, walls will be cut when a rotation event is emitted
            foreach (ModObstacleData ob in data.objects.OfType<ModObstacleData>())
            {
                foreach ((float cutTime, int cutAmount) in wallCutMoments)
                {
                    // If wall is uncomfortable for 360Degree mode, remove it
                    if (ob.lineIndex == 1 || ob.lineIndex == 2 || (ob.obstacleType == ObstacleType.FullHeight && ob.lineIndex == 0 && ob.width > 1))
                    {
                        // Wall is not fun in 360, remove it, walls with negative duration will filtered out later
                        ob.duration = 0;
                    }
                    // If moved in direction of wall
                    else if ((ob.lineIndex <= 1 && cutAmount < 0) || (ob.lineIndex >= 2 && cutAmount > 0))
                    {
                        float wallStartCutBeats = WallFrontCut;
                        if (cutTime >= ob.time - wallStartCutBeats && cutTime < ob.time + ob.duration / 2f)
                        {
                            // Cut front of wall
                            float cut = cutTime - (ob.time - wallStartCutBeats);

                            Plugin.Log.Info($"[Generator] Cut front wall at {ob.time} duration={ob.duration} cut={cut}");

                            ob.time += cut;
                            ob.duration -= cut;
                        }

                        float wallEndCutBeats = WallBackCut;
                        if (cutTime >= ob.time + ob.duration / 2 && cutTime < ob.time + ob.duration + wallEndCutBeats)
                        {
                            // Cut back of wall
                            float cut = (ob.time + ob.duration + wallEndCutBeats) - cutTime;

                            Plugin.Log.Info($"[Generator] Cut back wall at {ob.time} duration={ob.duration} cut={cut}");

                            ob.duration -= cut;
                        }
                    }
                }
            }

            Plugin.Log.Info($"[Generator] Emitted {eventCount} rotation events");

            if (!FieldHelper.Set(bm, "_beatmapData", data.ToBeatmap()))
            {
                Plugin.Log.Error($"Could not replace beatmap");
            }
        }

    }
}
