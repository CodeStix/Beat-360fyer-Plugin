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
        public float MaxRotationsPerSecond { get; set; } = 8f;
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
            int rotation = 0;
            // Moments where a wall should be cut
            List<(float, int)> wallCutMoments = new List<(float, int)>();
            // Previous spin direction, false is left, true is right
            bool previousDirection = true;

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
                    if (rotation + amount > LimitRotations || rotation + amount < -LimitRotations)
                        return;
                    rotation += amount;
                }

                previousDirection = amount > 0;
                eventCount++;
                wallCutMoments.Add((time, amount));

                // Place before note (- 0.005f)
                data.events.Add(new ModEvent(time, BeatmapEventType.Event15, amount > 0 ? 3 + amount : 4 + amount));
            }

            float beatDuration = 60f / bm.level.beatsPerMinute;

            const int BAR_NOTE_COUNT = 4; // 4 whole notes
            const float PREFFERED_BAR_LENGTH = 1.84f; // Bar length in seconds for 130 bpm
            float barLength = beatDuration * BAR_NOTE_COUNT; 
            while (barLength >= PREFFERED_BAR_LENGTH * 1.25f)
                barLength /= 2f;
            while (barLength < PREFFERED_BAR_LENGTH * 0.75f)
                barLength *= 2f;

            List<ModNoteData> notes = data.objects.OfType<ModNoteData>().ToList();
            List<ModNoteData> notesInBar = new List<ModNoteData>();
            List<ModNoteData> notesInBarBeat = new List<ModNoteData>();

            // Align bars to first note
            float firstNoteTime = notes[0].time;

            Plugin.Log.Info($"Setup bpm={bm.level.beatsPerMinute} beatDuration={beatDuration} barLength={barLength} barAlign={firstNoteTime}");


            for (int i = 0; i < notes.Count; )
            {
                float barStartTime = notes[i].time - firstNoteTime;

                float currentBarStart = Floor((notes[i].time - firstNoteTime) / barLength) * barLength;
                float currentBarEnd = currentBarStart + barLength - 0.01f;

                notesInBar.Clear();
                for (; i < notes.Count && notes[i].time - firstNoteTime < currentBarEnd; i++)
                {
                    notesInBar.Add(notes[i]);
                }

                if (notesInBar.Count >= 2 && notesInBar.All((e) => Math.Abs(e.time - notesInBar[0].time) < 0.001f))
                {
                    Plugin.Log.Info($"[Generator] Spin effect at {firstNoteTime + currentBarStart}");
                   
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
                        Rotate(firstNoteTime + currentBarStart + spinStep * s, spinDirection, false);
                    }

                    // Do not emit more rotation events after this
                    continue;
                }

                // Divide the current bar in x, for each piece, a rotation event CAN be emitted
                // Is calculated from the amount of notes in the current bar
                int barDivider;
                if (notesInBar.Count >= 36)
                    barDivider = 0; // Too mush notes, do not rotate
                else if (notesInBar.Count >= 26)
                    barDivider = 1;
                else if (notesInBar.Count >= 18)
                    barDivider = 2;
                else if (notesInBar.Count >= 8)
                    barDivider = 4;
                else
                    barDivider = 8;

                if (barDivider <= 0)
                    continue;

                // divisions | rotations
                // 0         | none
                // 1         | r . . . (only on first beat)
                // 2         | r . r . (on first and third beat)
                // 4         | r r r r (on all beats)
                // ...

                StringBuilder builder = new StringBuilder();

                // Iterate all the notes in the current bar in barDiviver pieces (bar is split in barDiviver pieces)
                float dividedBarLength = barLength / barDivider;
                for (int j = 0, k = 0; j < barDivider && k < notesInBar.Count; j++)
                {
                    notesInBarBeat.Clear();
                    for (; k < notesInBar.Count && Floor((notesInBar[k].time - firstNoteTime - currentBarStart) / dividedBarLength) == j; k++)
                    {
                        notesInBarBeat.Add(notesInBar[k]);
                    }

                    // Debug purpose
                    if (j != 0)
                        builder.Append(',');
                    builder.Append(notesInBarBeat.Count);

                    if (notesInBarBeat.Count == 0) 
                        continue;

                    int leftCount = notesInBarBeat.Count((e) => e.lineIndex <= 1 || e.cutDirection == NoteCutDirection.Left || e.cutDirection == NoteCutDirection.UpLeft || e.cutDirection == NoteCutDirection.DownLeft);
                    int rightCount = notesInBarBeat.Count((e) => e.lineIndex >= 2 || e.cutDirection == NoteCutDirection.Right || e.cutDirection == NoteCutDirection.UpRight || e.cutDirection == NoteCutDirection.DownRight);

                    // Place the rotation event after all the beat segment notes if they are all at the same time, otherwise, place the event before the notes.
                    float time = notesInBarBeat[0].time; // ~= firstNoteTime + currentBarStart + j * dividedBarLength;
                    float rotationTime = notesInBarBeat.All((e) => Math.Abs(e.time - notesInBarBeat[0].time) < 0.001f) ? time + 0.01f : time - 0.01f;
                    int dir = -leftCount + rightCount;
                    if (dir < 0)
                    {
                        Rotate(rotationTime, -1);
                    }
                    else if (dir > 0)
                    {
                        Rotate(rotationTime, 1);
                    }
                    else
                    {
                        bool equalDirection = previousDirection;
                        if (rotation >= BottleneckRotations)
                            equalDirection = false; // Prefer rotating to the left
                        else if (rotation <= -BottleneckRotations)
                            equalDirection = true; // Prefer rotating to the right
                        Rotate(rotationTime, equalDirection ? 1 : -1);
                    }
                }


                Plugin.Log.Info($"[{currentBarStart + firstNoteTime}({(currentBarStart + firstNoteTime) / beatDuration}) -> {currentBarEnd + firstNoteTime}({(currentBarEnd + firstNoteTime) / beatDuration})] count={notesInBar.Count} segments={builder} barDiviver={barDivider} noteRealTime={barStartTime + firstNoteTime} noteRealBeat={(barStartTime + firstNoteTime) / beatDuration}");
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
                            Plugin.Log.Info($"[Generator] Split wall at {ob.time}({ob.duration}) -> {ob.time}({firstPartDuration}) <|> {secondPart.time}({secondPart.duration})");
                            ob.duration = firstPartDuration;
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
