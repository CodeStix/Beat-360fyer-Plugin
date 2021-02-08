using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Beat_360fyer_Plugin
{
    public class ModBeatmapData
    {
        public int numberOfLines;
        public List<ModObject> objects = new List<ModObject>();
        public List<ModEvent> events = new List<ModEvent>();

        public ModBeatmapData(BeatmapData from)
        {
            numberOfLines = from.numberOfLines;
            foreach (BeatmapObjectData d in from.beatmapObjectsData)
                objects.Add(ModObject.FromObject(d));
            foreach (BeatmapEventData e in from.beatmapEventsData)
                events.Add(new ModEvent(e));
        }

        public BeatmapData ToBeatmap()
        {
            BeatmapData bm = new BeatmapData(numberOfLines);
            foreach (ModObject o in objects)
                bm.AddBeatmapObjectData(o.ToObject());
            foreach (ModEvent o in events)
                bm.AddBeatmapEventData(o.ToEvent());
            return bm;
        }
    }

    public class ModEvent
    {
        public BeatmapEventType type;
        public float time;
        public int value;

        public ModEvent(BeatmapEventData ev)
        {
            time = ev.time;
            type = ev.type;
            value = ev.value;
        }

        public ModEvent(float time, BeatmapEventType type, int value)
        {
            this.time = time;
            this.type = type;
            this.value = value;
        }

        public BeatmapEventData ToEvent()
        {
            return new BeatmapEventData(time, type, value);
        }
    }

    public abstract class ModObject
    {
        public float time;
        public int lineIndex;

        public ModObject(float time, int lineIndex)
        {
            this.time = time;
            this.lineIndex = lineIndex;
        }

        public static ModObject FromObject(BeatmapObjectData obj)
        {
            if (obj is NoteData note)
                return new ModNoteData(note);
            else if (obj is ObstacleData obstacle)
                return new ModObstacleData(obstacle);
            else if (obj is WaypointData way)
                return new ModWaypointData(way);
            throw new InvalidOperationException("Invalid type " + obj.GetType().FullName);
        }

        public abstract BeatmapObjectData ToObject();
    }

    public class ModObstacleData : ModObject
    {
        public ObstacleType obstacleType;
        public float duration;
        public int width;

        public ModObstacleData(ObstacleData data) : base(data.time, data.lineIndex)
        {
            obstacleType = data.obstacleType;
            duration = data.duration;
            width = data.width;
        }

        public ModObstacleData(float time, int lineIndex, ObstacleType type, float duration, int width) : base(time, lineIndex)
        {
            obstacleType = type;
            this.duration = duration;
            this.width = width;
        }

        public override BeatmapObjectData ToObject()
        {
            return new ObstacleData(time, lineIndex, obstacleType, duration, width);
        }
    }

    public class ModNoteData : ModObject
    {
        public NoteCutDirection cutDirection;
        public NoteLineLayer noteLineLayer;
        public ColorType colorType;

        public bool IsBomb => cutDirection == NoteCutDirection.None;

        public ModNoteData(NoteData data) : base(data.time, data.lineIndex)
        {
            cutDirection = data.cutDirection;
            noteLineLayer = data.noteLineLayer;
            colorType = data.colorType;
        }

        public ModNoteData(float time, int lineIndex, NoteLineLayer layer, NoteCutDirection cutDirection, ColorType type) : base(time, lineIndex)
        {
            this.cutDirection = cutDirection;
            noteLineLayer = layer;
            this.colorType = type;
        }

        public override BeatmapObjectData ToObject()
        {
            return NoteData.CreateBasicNoteData(time, lineIndex, noteLineLayer, colorType, cutDirection);
        }
    }

    public class ModWaypointData : ModObject
    {
        public OffsetDirection offsetDirection;
        public NoteLineLayer noteLineLayer;

        public ModWaypointData(WaypointData data) : base(data.time, data.lineIndex)
        {
            offsetDirection = data.offsetDirection;
            noteLineLayer = data.noteLineLayer;
        }

        public override BeatmapObjectData ToObject()
        {
            return new WaypointData(time, lineIndex, noteLineLayer, offsetDirection);
        }
    }
}
