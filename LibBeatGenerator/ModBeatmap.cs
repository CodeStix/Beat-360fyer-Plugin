using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace LibBeatGenerator
{
    public class ModBeatmapData
    {
        public List<ModBeatmapObjectData> objects = new List<ModBeatmapObjectData>();
        public List<ModBeatmapEventData> events = new List<ModBeatmapEventData>();

        public int NumberOfLines { get; }

        public ModBeatmapData(int numberOfLines)
        {
            NumberOfLines = numberOfLines;
        }

        /// <summary>
        /// Removes walls with a 0 duration, notes with time &lt;= 0 and sorts everything by time.
        /// </summary>
        public void SortAndRemove()
        {
            objects = objects.Where((o) => !(o is ModObstacleData ob && ob.duration == 0f) && o.time > 0f).OrderBy((e) => e.time).ToList();
            events = events.OrderBy((e) => e.time).ToList();
        }
    }

    public abstract class ModBeatmapObjectData
    {
        public float time;
        public int lineIndex;

        public ModBeatmapObjectData(float time, int lineIndex)
        {
            this.time = time;
            this.lineIndex = lineIndex;
        }
    }
    
    public enum ModBeatmapEventType
    {
        Event0 = 0,
        Event1 = 1,
        Event2 = 2,
        Event3 = 3,
        Event4 = 4,
        Event5 = 5,
        Event6 = 6,
        Event7 = 7,
        Event8 = 8,
        Event9 = 9,
        Event10 = 10,
        Event11 = 11,
        Event12 = 12,
        Event13 = 13,
        Event14 = 14,
        Event15 = 0xF,
        VoidEvent = -1,
        Special0 = 40,
        Special1 = 41,
        Special2 = 42,
        Special3 = 43
    }

    public class ModBeatmapEventData
    {
        public ModBeatmapEventType type;
        public float time; 
        public int value;

        public ModBeatmapEventData(float time, ModBeatmapEventType type, int value)
        {
            this.time = time;
            this.type = type;
            this.value = value;
        }
    }

    public enum ModColorType
    {
        ColorA = 0,
        ColorB = 1,
        None = -1
    }

    public enum ModNoteCutDirection
    {
        Up,
        Down,
        Left,
        Right,
        UpLeft,
        UpRight,
        DownLeft,
        DownRight,
        Any,
        None
    }

    public enum ModObstacleType
    {
        FullHeight,
        Top
    }


    public class ModObstacleData : ModBeatmapObjectData
    {
        public ModObstacleType type;
        public float duration;

        public ModObstacleData(float time, int lineIndex, float duration, ModObstacleType type = ModObstacleType.FullHeight) : base(time, lineIndex)
        {
            this.type = type;
            this.duration = duration;
        }
    }

    public class ModNoteData : ModBeatmapObjectData
    {
        public ModNoteCutDirection cutDirection;
        public ModColorType colorType;

        public ModNoteData(float time, int lineIndex, ModNoteCutDirection cutDirection, ModColorType colorType) : base(time, lineIndex)
        {
            this.cutDirection = cutDirection;
            this.colorType = colorType;
        }

        public static ModNoteData CreateBomb(float time, int lineIndex)
        {
            return new ModNoteData(time, lineIndex, ModNoteCutDirection.None, ModColorType.None);
        }
    }
}
