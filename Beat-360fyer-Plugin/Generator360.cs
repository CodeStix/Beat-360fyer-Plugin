using CustomJSONData;
using CustomJSONData.CustomBeatmap;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Beat360fyerPlugin
{
    public class Generator360
    {
        /// <summary>
        /// The preferred bar duration in seconds. The generator will loop the song in bars. 
        /// This is called 'preferred' because this value will change depending on a song's bpm (will be aligned around this value).
        /// </summary>
        public float PreferredBarDuration { get; set; } = 1.84f;  // Calculated from 130 bpm, which is a pretty standard bpm (60 / 130 bpm * 4 whole notes per bar ~= 1.84)
        /// <summary>
        /// The amount of rotations before stopping rotation events (rip cable otherwise) (24 is one full rotation)
        /// </summary>
        public int LimitRotations { get; set; } = 28;
        /// <summary>
        /// The amount of rotations before preferring the other direction (24 is one full rotation)
        /// </summary>
        public int BottleneckRotations { get; set; } = 14;
        /// <summary>
        /// Enable the spin effect when no notes are coming.
        /// </summary>
        public bool EnableSpin { get; set; } = false;
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
        public float WallFrontCut { get; set; } = 0.2f;
        /// <summary>
        /// Amount of time in seconds to cut of the back of a wall when rotating towards it.
        /// </summary>
        public float WallBackCut { get; set; } = 0.45f;
        /// <summary>
        /// True if you want to generate walls, walls are cool in 360 mode
        /// </summary>
        public bool WallGenerator { get; set; } = false;
        

        private static int Floor(float f)
        {
            int i = (int)f;
            return f - i >= 0.999f ? i + 1 : i;
        }

        public void Generate(IDifficultyBeatmap bm)
        {
            ModBeatmapData data = new ModBeatmapData((CustomBeatmapData)bm.beatmapData);

            bool containsCustomWalls = data.objects.Count((e) => e is CustomObstacleData d && ((IDictionary<string, object>)d.customData).ContainsKey("_position")) > 12;

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
                    if (totalRotation + amount > LimitRotations)
                        amount = Math.Min(amount, Math.Max(0, LimitRotations - totalRotation));
                    else if (totalRotation + amount < -LimitRotations)
                        amount = Math.Max(amount, Math.Min(0, -(LimitRotations + totalRotation)));
                    if (amount == 0)
                        return;

                    totalRotation += amount;
                }

                previousDirection = amount > 0;
                eventCount++;
                wallCutMoments.Add((time, amount));

                data.events.Add(new CustomBeatmapEventData(time, BeatmapEventType.Event15, amount > 0 ? 3 + amount : 4 + amount, Trees.Tree()));
            }

            float beatDuration = 60f / bm.level.beatsPerMinute;

            // Align PreferredBarDuration to beatDuration
            float barLength = beatDuration; 
            while (barLength >= PreferredBarDuration * 1.25f)
                barLength /= 2f;
            while (barLength < PreferredBarDuration * 0.75f)
                barLength *= 2f;

            List<NoteData> notes = data.objects.OfType<NoteData>().ToList();
            List<NoteData> notesInBar = new List<NoteData>();
            List<NoteData> notesInBarBeat = new List<NoteData>();

            // Align bars to first note, the first note (almost always) identifies the start of the first bar
            float firstBeatmapNoteTime = notes[0].time;

#if DEBUG
            Plugin.Log.Info($"Setup bpm={bm.level.beatsPerMinute} beatDuration={beatDuration} barLength={barLength} firstNoteTime={firstBeatmapNoteTime}");
#endif

            for (int i = 0; i < notes.Count; )
            {
                float currentBarStart = Floor((notes[i].time - firstBeatmapNoteTime) / barLength) * barLength;
                float currentBarEnd = currentBarStart + barLength - 0.001f;

                notesInBar.Clear();
                for (; i < notes.Count && notes[i].time - firstBeatmapNoteTime < currentBarEnd; i++)
                {
                    // If isn't bomb
                    if (notes[i].cutDirection != NoteCutDirection.None)
                        notesInBar.Add(notes[i]);
                }

                if (notesInBar.Count == 0)
                    continue;

                if (EnableSpin && notesInBar.Count >= 2 && currentBarStart - previousSpinTime > SpinCooldown && notesInBar.All((e) => Math.Abs(e.time - notesInBar[0].time) < 0.001f))
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
                    else
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
                // 8          |brrrrrrrr
                // ...        | ...
                // TODO: Create formula out of these if statements
                int barDivider;
                if (notesInBar.Count >= 58)
                    barDivider = 0; // Too mush notes, do not rotate
                else if (notesInBar.Count >= 38)
                    barDivider = 1;
                else if (notesInBar.Count >= 26)
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

                    float currentBarBeatStart = firstBeatmapNoteTime + currentBarStart + j * dividedBarLength;

                    // Determine the rotation direction based on the last notes in the bar
                    NoteData lastNote = notesInBarBeat[notesInBarBeat.Count - 1];
                    IEnumerable<NoteData> lastNotes = notesInBarBeat.Where((e) => Math.Abs(e.time - lastNote.time) < 0.005f);

                    // Amount of notes pointing to the left/right
                    int leftCount = lastNotes.Count((e) => e.lineIndex <= 1 || e.cutDirection == NoteCutDirection.Left || e.cutDirection == NoteCutDirection.UpLeft || e.cutDirection == NoteCutDirection.DownLeft);
                    int rightCount = lastNotes.Count((e) => e.lineIndex >= 2 || e.cutDirection == NoteCutDirection.Right || e.cutDirection == NoteCutDirection.UpRight || e.cutDirection == NoteCutDirection.DownRight);

                    NoteData afterLastNote = (k < notesInBar.Count ? notesInBar[k] : i < notes.Count ? notes[i] : null);

                    // Determine amount to rotate at once
                    // TODO: Create formula out of these if statements
                    int rotationCount = 1;
                    if (afterLastNote != null)
                    {
                        float timeDiff = afterLastNote.time - lastNote.time;
                        if (notesInBarBeat.Count >= 2)
                        {
                            if (timeDiff >= barLength)
                                rotationCount = 3;
                            else if (timeDiff >= barLength / 4)
                                rotationCount = 2;
                        }
                    }

                    int rotation = 0;
                    if (leftCount > rightCount)
                    {
                        // Most of the notes are pointing to the left, rotate to the left
                        rotation = -rotationCount;
                    }
                    else if (rightCount > leftCount)
                    {
                        // Most of the notes are pointing to the right, rotate to the right
                        rotation = rotationCount;
                    }
                    else
                    {
                        // Rotate to left or right
                        if (totalRotation >= BottleneckRotations)
                        {
                            // Prefer rotating to the left if moved a lot to the right
                            rotation = -rotationCount;
                        }
                        else if (totalRotation <= -BottleneckRotations)
                        {
                            // Prefer rotating to the right if moved a lot to the left
                            rotation = rotationCount;
                        }
                        else
                        {
                            // Rotate based on previous direction
                            rotation = previousDirection ? rotationCount : -rotationCount;
                        }
                    }

                    if (totalRotation >= BottleneckRotations && rotationCount > 1)
                    {
                        rotationCount = 1;
                    }
                    else if (totalRotation <= -BottleneckRotations && rotationCount < -1)
                    {
                        rotationCount = -1; 
                    }

                    if (totalRotation >= LimitRotations - 1 && rotationCount > 0)
                    {
                        rotationCount = -rotationCount;
                    }
                    else if (totalRotation <= -LimitRotations + 1 && rotationCount < 0)
                    {
                        rotationCount = -rotationCount;
                    }


                    // Finally rotate
                    Rotate(lastNote.time + 0.01f, rotation);

                    if (WallGenerator && !containsCustomWalls)
                    {
                        float wallTime = currentBarBeatStart;
                        float wallDuration = dividedBarLength;

                        // Check if there is already a wall
                        bool generateWall = true;
                        foreach (ObstacleData obs in data.objects.OfType<ObstacleData>())
                        {
                            if (obs.time + obs.duration >= wallTime && obs.time < wallTime + wallDuration)
                            {
                                generateWall = false;
                                break;
                            }
                        }

                        if (generateWall && afterLastNote != null)
                        {
                            if (!notesInBarBeat.Any((e) => e.lineIndex == 3))
                            {
                                ObstacleType type = notesInBarBeat.Any((e) => e.lineIndex == 2) ? ObstacleType.Top : ObstacleType.FullHeight;

                                if (afterLastNote.lineIndex == 3 && !(type == ObstacleType.Top && afterLastNote.noteLineLayer == NoteLineLayer.Base))
                                    wallDuration = afterLastNote.time - WallBackCut - wallTime;

                                if (wallDuration > 0f)
                                {
                                    // Workaround for NoodleExtensions error, why tf does this work??
                                    dynamic t = Trees.Tree();
                                    t.bpm = bm.level.beatsPerMinute;
                                    data.objects.Add(new CustomObstacleData(wallTime, 3, type, wallDuration, 1, t));
                                }
                            }
                            if (!notesInBarBeat.Any((e) => e.lineIndex == 0))
                            {
                                ObstacleType type = notesInBarBeat.Any((e) => e.lineIndex == 1) ? ObstacleType.Top : ObstacleType.FullHeight;

                                if (afterLastNote.lineIndex == 0 && !(type == ObstacleType.Top && afterLastNote.noteLineLayer == NoteLineLayer.Base))
                                    wallDuration = afterLastNote.time - WallBackCut - wallTime;

                                if (wallDuration > 0f)
                                {
                                    // Workaround for NoodleExtensions error, why tf does this work??
                                    dynamic t = Trees.Tree();
                                    t.bpm = bm.level.beatsPerMinute;
                                    data.objects.Add(new CustomObstacleData(wallTime, 0, type, wallDuration, 1, t));
                                }
                            }
                        }
                    }

#if DEBUG
                    Plugin.Log.Info($"[{currentBarBeatStart}] Rotate {rotation} (c={notesInBarBeat.Count},lc={leftCount},rc={rightCount},lastNotes={lastNotes.Count()},rotationTime={lastNote.time + 0.01f},afterLastNote={afterLastNote?.time},rotationCount={rotationCount})");
#endif
                }


#if DEBUG
                Plugin.Log.Info($"[{currentBarStart + firstBeatmapNoteTime}({(currentBarStart + firstBeatmapNoteTime) / beatDuration}) -> {currentBarEnd + firstBeatmapNoteTime}({(currentBarEnd + firstBeatmapNoteTime) / beatDuration})] count={notesInBar.Count} segments={builder} barDiviver={barDivider}");
#endif
            }


            // Cut walls, walls will be cut when a rotation event is emitted
            Queue<CustomObstacleData> obstacles = new Queue<CustomObstacleData>(data.objects.OfType<CustomObstacleData>());
            while (obstacles.Count > 0)
            {
                CustomObstacleData ob = obstacles.Dequeue();
                foreach ((float cutTime, int cutAmount) in wallCutMoments)
                {
                    if (ob.duration <= 0f)
                        break;

                    bool noCutMargin = false;
                    if (ob.customData is ExpandoObject && ob.customData != null)
                    {
                        IDictionary<string, object> customDataDict = (IDictionary<string, object>)ob.customData;
                        noCutMargin = customDataDict.ContainsKey("_position");
                    }

                    // If wall is uncomfortable for 360Degree mode, remove it
                    if (ob.lineIndex == 1 || ob.lineIndex == 2 || (ob.lineIndex == 0 && ob.width > 1))
                    {
                        // Wall is not fun in 360, remove it, walls with negative/0 duration will filtered out later
                        ob.UpdateDuration(0f);
                    }
                    // If moved in direction of wall
                    else if ((ob.lineIndex <= 1 && cutAmount < 0) || (ob.lineIndex >= 2 && cutAmount > 0))
                    {
                        int cutMultiplier = Math.Abs(cutAmount);
                        float frontCut = noCutMargin ? 0f : WallFrontCut;
                        float backCut = noCutMargin ? 0f : WallBackCut;
                        if (cutTime >= ob.time - frontCut && cutTime < ob.time + ob.duration + backCut * cutMultiplier)
                        {
                            float firstPartTime = ob.time;
                            float firstPartDuration = (cutTime - backCut * cutMultiplier) - firstPartTime;
                            float secondPartTime = cutTime + frontCut;
                            float secondPartDuration = (ob.time + ob.duration) - secondPartTime;

                            if (secondPartDuration > 0f && firstPartDuration <= 0.01f)
                            {
                                ob.MoveTime(secondPartTime);
                                ob.UpdateDuration(secondPartDuration);
                            }
                            else
                            {
                                // Split the wall in half by creating a second wall
                                if (secondPartDuration > 0.01f)
                                {
                                    dynamic t = Trees.Copy(ob.customData);
                                    t.bpm = bm.level.beatsPerMinute;
                                    CustomObstacleData secondPart = new CustomObstacleData(secondPartTime, ob.lineIndex, ob.obstacleType, secondPartDuration, ob.width, t);
                                    data.objects.Add(secondPart);
                                    obstacles.Enqueue(secondPart);
                                }

                                // Modify first half of wall
                                ob.MoveTime(firstPartTime);
                                ob.UpdateDuration(Math.Max(firstPartDuration, 0f));
                            }

                           
#if DEBUG
                            Plugin.Log.Info($"Split wall at {ob.time}({ob.duration}) -> {ob.time}({firstPartDuration}) <|> {secondPartTime}({secondPartDuration}) cutMultiplier={cutMultiplier}");
#endif
                            
                        }
                    }
                }
            }

            Plugin.Log.Info($"Emitted {eventCount} rotation events");

            if (!FieldHelper.Set(bm, "_beatmapData", data.ToBeatmap()))
            {
                Plugin.Log.Error($"Could not replace beatmap");
            }

            Plugin.Log.Info($"Contains custom walls: {containsCustomWalls}");
        }

    }
}
