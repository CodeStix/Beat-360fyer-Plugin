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
        public float WallFrontCut { get; set; } = 0.085f;
        /// <summary>
        /// Amount of time in seconds to cut of the back of a wall when rotating towards it.
        /// </summary>
        public float WallBackCut { get; set; } = 0.15f;

        private int Pow(int b, int exp)
        {
            if (exp == 0)
                return 1;
            for (int i = 1; i < exp; i++)
                b *= b;
            return b;
        }

        public int Floor(float f)
        {
            int i = (int)f;
            return f - i >= 0.99 ? i + 1 : i;
        }

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
                float currentBarEnd = currentBarStart + barLength - 0.01f; // Weird rounding on float comparing fix

                notesInBar.Clear();
                for (; i < notes.Count && notes[i].time - firstNoteTime < currentBarEnd; i++)
                {
                    notesInBar.Add(notes[i]);
                } 

                int count = notesInBar.Count;
                if (count > 20)
                    continue; // Too mush notes, do not rotate

                // Divide the current bar in x, for each piece, a rotation event can be emitted
                int barDiviver;
                if (count >= 16)
                    barDiviver = 1;
                else if (count >= 12)
                    barDiviver = 2;
                else if (count >= 8)
                    barDiviver = 4;
                else if (count >= 4)
                    barDiviver = 8;
                else
                    barDiviver = 16;

                // divisions | rotations
                // 0         | none
                // 1         | r . . . (only on first beat)
                // 2         | r . r . (on first and third beat)
                // 4         | r r r r (on all beats)
                // ...

                float dividedBarLength = barLength / barDiviver;

                StringBuilder builder = new StringBuilder();

                for (int j = 0, k = 0; j < barDiviver && k < notesInBar.Count; j++)
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

                    if (notesInBar.Count == 0) 
                        continue;

                    float realTime = firstNoteTime + currentBarStart + j * dividedBarLength;
                    int leftCount = notesInBarBeat.Count((e) => e.cutDirection == NoteCutDirection.Left || e.cutDirection == NoteCutDirection.UpLeft || e.cutDirection == NoteCutDirection.DownLeft);
                    int rightCount = notesInBarBeat.Count((e) => e.cutDirection == NoteCutDirection.Right || e.cutDirection == NoteCutDirection.UpRight || e.cutDirection == NoteCutDirection.DownRight);

                    int dir = -leftCount + rightCount;
                    if (dir < 0)
                    {
                        Plugin.Log.Info($"Rotate left 1 at {realTime}");
                        Rotate(realTime - 0.01f, -1);
                    }
                    else if (dir > 0)
                    {
                        Plugin.Log.Info($"Rotate right 1 at {realTime}");
                        Rotate(realTime - 0.01f, 1);
                    }
                    else
                    {
                        Plugin.Log.Info($"Rotate previous {(previousDirection ? 1 : -1)} at {realTime}");
                        Rotate(realTime - 0.01f, previousDirection ? 1 : -1);
                    }
                }


                Plugin.Log.Info($"[{currentBarStart + firstNoteTime}({(currentBarStart + firstNoteTime) / beatDuration}) -> {currentBarEnd + firstNoteTime}({(currentBarEnd + firstNoteTime) / beatDuration})] count={count} segments={builder} barDiviver={barDiviver} noteRealTime={barStartTime + firstNoteTime} noteRealBeat={(barStartTime + firstNoteTime) / beatDuration}");
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
