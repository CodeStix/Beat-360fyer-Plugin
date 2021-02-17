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
        /// The preferred bar duration in seconds. The generator will loop the song in bars. 
        /// This is called 'preferred' because this value will change depending on a song's bpm (will be aligned around this value).
        /// </summary>
        public float PreferredBarDuration { get; set; } = 1.84f;  // Calculated from 130 bpm, which is a pretty standard bpm (60 / 130 bpm * 4 whole notes per bar ~= 1.84)
        /// <summary>
        /// When lower, less notes are required to create larger rotations (more degree turns at once, can get disorienting)
        /// </summary>
        public int RotationDivider { get; set; } = 4;
        /// <summary>
        /// The amount of rotations before stopping rotation events (rip cable otherwise) 
        /// </summary>
        public int LimitRotations { get; set; } = 320;
        /// <summary>
        /// The amount of rotations before preferring the other direction
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
        /// Minimum amount of seconds between each spin effect.
        /// </summary>
        public float SpinCooldown { get; set; } = 10f;
        /// <summary>
        /// Amount of time in seconds to cut of the front of a wall when rotating towards it.
        /// </summary>
        public float WallFrontCut { get; set; } = 0.12f;
        /// <summary>
        /// Amount of time in seconds to cut of the back of a wall when rotating towards it.
        /// </summary>
        public float WallBackCut { get; set; } = 0.15f;

        private static int Pow(int b, int exp)
        {
            if (exp == 0)
                return 1;
            for (int i = 1; i < exp; i++)
                b *= b;
            return b;
        }

        private static int Floor(float f)
        {
            int i = (int)f;
            return f - i >= 0.999f ? i + 1 : i;
        }

        public void Generate(IDifficultyBeatmap bm)
        {
            ModBeatmapData data = new ModBeatmapData(bm.beatmapData);

            // Amount of rotation events emitted
            int eventCount = 0;
            // Current rotation
            int totalRotation = 0;
            // Moments where a wall should be cut
            List<(float, int)> wallCutMoments = new List<(float, int)>();
            // Previous spin direction, false is left, true is right
            bool previousDirection = true;
            float previousSpinTime = float.MinValue;

            // Negative numbers rotate to the left, positive to the right
            void Rotate(float time, int amount, bool enableLimit = true)
            {
                if (amount == 0)
                    return;
                if (amount < -4)
                    amount = -4;
                if (amount > 4)
                    amount = 4;

                if (enableLimit)
                {
                    if (totalRotation + amount > LimitRotations || totalRotation + amount < -LimitRotations)
                        return;
                    totalRotation += amount;
                }

                previousDirection = amount > 0;
                eventCount++;
                wallCutMoments.Add((time, amount));

                // Place before note (- 0.005f)
                data.events.Add(new ModEvent(time, BeatmapEventType.Event15, amount > 0 ? 3 + amount : 4 + amount));
            }

            float beatDuration = 60f / bm.level.beatsPerMinute;

            float barLength = beatDuration; 
            while (barLength >= PreferredBarDuration * 1.25f)
                barLength /= 2f;
            while (barLength < PreferredBarDuration * 0.75f)
                barLength *= 2f;

            List<ModNoteData> notes = data.objects.OfType<ModNoteData>().ToList();
            List<ModNoteData> notesInBar = new List<ModNoteData>();
            List<ModNoteData> notesInBarBeat = new List<ModNoteData>();

            // Align bars to first note, the first note (almost always) identifies the start of the first bar
            float firstBeatmapNoteTime = notes[0].time;

#if DEBUG
            Plugin.Log.Info($"Setup bpm={bm.level.beatsPerMinute} beatDuration={beatDuration} barLength={barLength} firstNoteTime={firstBeatmapNoteTime}");
#endif

            for (int i = 0; i < notes.Count; )
            {
                float currentBarStart = Floor((notes[i].time - firstBeatmapNoteTime) / barLength) * barLength;
                float currentBarEnd = currentBarStart + barLength - 0.01f;

                notesInBar.Clear();
                for (; i < notes.Count && notes[i].time - firstBeatmapNoteTime < currentBarEnd; i++)
                {
                    if (!notes[i].IsBomb)
                        notesInBar.Add(notes[i]);
                }

                if (notesInBar.Count == 0) // If only bombs
                    continue;

                if (notesInBar.Count >= 2 && currentBarStart - previousSpinTime > SpinCooldown && notesInBar.All((e) => Math.Abs(e.time - notesInBar[0].time) < 0.001f))
                {
#if DEBUG
                    Plugin.Log.Info($"[Generator] Spin effect at {firstBeatmapNoteTime + currentBarStart}");
#endif

                    int leftCount = notesInBar.Count((e) => e.cutDirection == NoteCutDirection.Left || e.cutDirection == NoteCutDirection.UpLeft || e.cutDirection == NoteCutDirection.DownLeft);
                    int rightCount = notesInBar.Count((e) => e.cutDirection == NoteCutDirection.Right || e.cutDirection == NoteCutDirection.UpRight || e.cutDirection == NoteCutDirection.DownRight);
                    int spinDirection;
                    if (leftCount == rightCount)
                        spinDirection = previousDirection ? -1 : 1;
                    else if (leftCount > rightCount)
                        spinDirection = -1;
                    else // direction > 0
                        spinDirection = 1;

                    float spinStep = TotalSpinTime / 24;
                    for (int s = 0; s < 24; s++)
                    {
                        Rotate(firstBeatmapNoteTime + currentBarStart + spinStep * s, spinDirection, false);
                    }

                    // Do not emit more rotation events after this
                    previousSpinTime = currentBarStart;
                    continue;
                }

                // Divide the current bar in x pieces (or notes), for each piece, a rotation event CAN be emitted
                // Is calculated from the amount of notes in the current bar
                // barDivider | rotations
                // 0          | . . . . (no rotations)
                // 1          | r . . . (only on first beat)
                // 2          | r . r . (on first and third beat)
                // 4          | r r r r 
                // 8          | rrrrrrrr
                // ...
                int barDivider;
                if (notesInBar.Count >= 48)
                    barDivider = 0; // Too mush notes, do not rotate
                else if (notesInBar.Count >= 28)
                    barDivider = 1;
                else if (notesInBar.Count >= 18)
                    barDivider = 2;
                else if (notesInBar.Count >= 8)
                    barDivider = 4;
                else
                    barDivider = 8;

                if (barDivider <= 0)
                    continue;

#if DEBUG
                StringBuilder builder = new StringBuilder();
#endif

                // Iterate all the notes in the current bar in barDiviver pieces (bar is split in barDiviver pieces)
                float dividedBarLength = barLength / barDivider;
                for (int j = 0, k = 0; j < barDivider && k < notesInBar.Count; j++)
                {
                    notesInBarBeat.Clear();
                    for (; k < notesInBar.Count && Floor((notesInBar[k].time - firstBeatmapNoteTime - currentBarStart) / dividedBarLength) == j; k++)
                    {
                        notesInBarBeat.Add(notesInBar[k]);
                    }

#if DEBUG
                    // Debug purpose
                    if (j != 0)
                        builder.Append(',');
                    builder.Append(notesInBarBeat.Count);
#endif

                    if (notesInBarBeat.Count == 0) 
                        continue;

                    float firstNoteTime = notesInBarBeat[0].time; // ~= firstNoteTime + currentBarStart + j * dividedBarLength;
                    float nextNoteTime = notesInBarBeat.FirstOrDefault((e) => e.time - firstNoteTime > 0.001f)?.time ?? (k < notesInBar.Count ? notesInBar[k].time : i < notes.Count ? notes[i].time : float.MaxValue);

                    // Amount of notes pointing to the left/right
                    int leftCount = notesInBarBeat.Count((e) => e.lineIndex <= 1 || e.cutDirection == NoteCutDirection.Left || e.cutDirection == NoteCutDirection.UpLeft || e.cutDirection == NoteCutDirection.DownLeft);
                    int rightCount = notesInBarBeat.Count((e) => e.lineIndex >= 2 || e.cutDirection == NoteCutDirection.Right || e.cutDirection == NoteCutDirection.UpRight || e.cutDirection == NoteCutDirection.DownRight);

                    // Rotate more at once if less notes are coming, rotate less at once if notes are coming fast
                    int rotationDivider = (int)(RotationDivider / (nextNoteTime - firstNoteTime));
                    if (rotationDivider < 2)
                        rotationDivider = 2;

                    bool placeAfter = true; // notesInBarBeat.All((e) => Math.Abs(e.time - firstTime) < 0.001f);
                    float rotationTime;
                    if (placeAfter)
                    {
                        // Place the rotation event after the last note
                        rotationTime = notesInBarBeat[notesInBarBeat.Count - 1].time + 0.01f;
                    }
                    else
                    {
                        // Place the rotation event before the first note
                        rotationTime = firstNoteTime - 0.01f;
                    }

                    // Mean direction which all the notes are pointing to
                    int dir = -leftCount + rightCount;
                    int rotation = 0;
                    if (dir < 0)
                    {
                        // Most of the notes are pointing to the left, rotate to the left
                        rotation = dir / rotationDivider - 1;
                    }
                    else if (dir > 0)
                    {
                        // Most of the notes are pointing to the right, rotate to the right
                        rotation = dir / rotationDivider + 1;
                    }
                    else
                    {
                        // Rotate to left or right
                        int r = notesInBarBeat.Count / 2 / rotationDivider + 1;
                        if (totalRotation >= BottleneckRotations)
                        {
                            // Prefer rotating to the left if moved a lot to the right
                            rotation = -r;
                        }
                        else if (totalRotation <= -BottleneckRotations)
                        {
                            // Prefer rotating to the right if moved a lot to the left
                            rotation = r;
                        }
                        else
                        {
                            // Rotate based on previous direction
                            rotation = previousDirection ? r : -r;
                        }
                    }

                    Rotate(rotationTime, rotation);

#if DEBUG
                    Plugin.Log.Info($"[{firstNoteTime}] Rotate {rotation} (c={notesInBarBeat.Count},lc={leftCount},rc={rightCount},rotationTime={rotationTime},nextNoteTime={nextNoteTime},rotationDivider={rotationDivider})");
#endif
                }


#if DEBUG
                Plugin.Log.Info($"[{currentBarStart + firstBeatmapNoteTime}({(currentBarStart + firstBeatmapNoteTime) / beatDuration}) -> {currentBarEnd + firstBeatmapNoteTime}({(currentBarEnd + firstBeatmapNoteTime) / beatDuration})] count={notesInBar.Count} segments={builder} barDiviver={barDivider}");
#endif
            }


            // Cut walls, walls will be cut when a rotation event is emitted
            Queue<ModObstacleData> obstacles = new Queue<ModObstacleData>(data.objects.OfType<ModObstacleData>());
            while (obstacles.Count > 0)
            {
                ModObstacleData ob = obstacles.Dequeue();
                if (ob.duration <= 0f)
                    continue;
                foreach ((float cutTime, int cutAmount) in wallCutMoments)
                {
                    // If wall is uncomfortable for 360Degree mode, remove it
                    if (ob.lineIndex == 1 || ob.lineIndex == 2 || (ob.obstacleType == ObstacleType.FullHeight && ob.lineIndex == 0 && ob.width > 1))
                    {
                        // Wall is not fun in 360, remove it, walls with negative/0 duration will filtered out later
                        ob.duration = 0;
                    }
                    // If moved in direction of wall
                    else if ((ob.lineIndex <= 1 && cutAmount < 0) || (ob.lineIndex >= 2 && cutAmount > 0))
                    {
                        if (cutTime >= ob.time - WallFrontCut && cutTime < ob.time + ob.duration + WallBackCut)
                        {
                            // Split the wall in half by creating a second wall
                            float secondPartDuration = ob.duration - (cutTime - ob.time) - WallBackCut;
                            ModObstacleData secondPart = new ModObstacleData(cutTime + WallBackCut, ob.lineIndex, ob.obstacleType, secondPartDuration, ob.width);

                            // Modify first half of wall
                            float firstPartDuration = (cutTime - ob.time) - WallFrontCut;
#if DEBUG
                            Plugin.Log.Info($"Split wall at {ob.time}({ob.duration}) -> {ob.time}({firstPartDuration}) <|> {secondPart.time}({secondPart.duration})");
#endif
                            ob.duration = firstPartDuration;
                        }
                    }
                }
            }

            Plugin.Log.Info($"Emitted {eventCount} rotation events");

            if (!FieldHelper.Set(bm, "_beatmapData", data.ToBeatmap()))
            {
                Plugin.Log.Error($"Could not replace beatmap");
            }
        }

    }
}
