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
        public float BeatsPerMinute { get; }

        public ModBeatmapData(int numberOfLines, float beatsPerMinute)
        {
            NumberOfLines = numberOfLines;
            BeatsPerMinute = beatsPerMinute;
        }

        /// <summary>
        /// Removes walls with a 0 duration, notes with time &lt;= 0 and sorts everything by time.
        /// Call this function before converting to native type so everything is in the right order.
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
        public ModObstacleType obstacleType;
        public float duration;
        public int width;
        public IDictionary<string, object> customData;

        public ModObstacleData(float time, int lineIndex, ModObstacleType type, float duration, int width = 1, IDictionary<string, object> customData = null) : base(time, lineIndex)
        {
            this.obstacleType = type;
            this.duration = duration;
            this.width = width;
            this.customData = customData;
        }
    }

    public enum ModNoteLineLayer
    {
        Base,
        Upper,
        Top
    }
    
    public class ModNoteData : ModBeatmapObjectData
    {
        public ModNoteCutDirection cutDirection;
        public ModColorType colorType;
        public ModNoteLineLayer noteLineLayer;

        public ModNoteData(float time, int lineIndex, ModNoteLineLayer layer, ModNoteCutDirection cutDirection, ModColorType colorType) : base(time, lineIndex)
        {
            this.cutDirection = cutDirection;
            this.noteLineLayer = layer;
            this.colorType = colorType;
        }

        public static ModNoteData CreateBomb(float time, int lineIndex, ModNoteLineLayer layer)
        {
            return new ModNoteData(time, lineIndex, layer, ModNoteCutDirection.None, ModColorType.None);
        }

        public void MirrorLineIndex(int numberOfLines)
        {
            lineIndex = numberOfLines - 1 - lineIndex;
            SwitchColorType();
            MirrorCutDirection();
        }

        public void MirrorCutDirection()
        {
            switch(cutDirection)
            {
                case ModNoteCutDirection.Left:
                    cutDirection = ModNoteCutDirection.Right;
                    break;
                case ModNoteCutDirection.Right:
                    cutDirection = ModNoteCutDirection.Left;
                    break;
                case ModNoteCutDirection.UpLeft:
                    cutDirection = ModNoteCutDirection.UpRight;
                    break;
                case ModNoteCutDirection.UpRight:
                    cutDirection = ModNoteCutDirection.UpLeft;
                    break;
                case ModNoteCutDirection.DownLeft:
                    cutDirection = ModNoteCutDirection.DownRight;
                    break;
                case ModNoteCutDirection.DownRight:
                    cutDirection = ModNoteCutDirection.DownLeft;
                    break;
            }
        }

        public void SwitchColorType()
        {
            if (colorType == ModColorType.ColorA)
            {
                colorType = ModColorType.ColorB;
            }
            else if (colorType == ModColorType.ColorB)
            {
                colorType = ModColorType.ColorA;
            }
        }
    }
}
