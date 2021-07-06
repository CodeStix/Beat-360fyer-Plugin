using CustomJSONData.CustomBeatmap;
using LibBeatGenerator;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Beat360fyerPlugin
{
    public static class ModBeatmapConverter
    {
        private static IDictionary<string, object> ConvertCustomData(dynamic custom)
        {
            if (custom is ExpandoObject && custom != null)
            {
                return (IDictionary<string, object>)custom;
            }
            else
            {
                return null;
            }
        }

        public static void ToModBeatmap(this IDifficultyBeatmap bm)
        {
            ModBeatmapData mod = new ModBeatmapData(bm.beatmapData.numberOfLines, bm.level.beatsPerMinute);
            CustomBeatmapData data = (CustomBeatmapData)bm.beatmapData;

            foreach(CustomBeatmapEventData ev in data.beatmapEventsData)
            {
                mod.events.Add(new ModBeatmapEventData(ev.time, (ModBeatmapEventType)ev.type, ev.value, ConvertCustomData(ev.customData)));
            }

            foreach (BeatmapObjectData obj in data.beatmapObjectsData)
            {
                if (obj is CustomNoteData nd)
                    mod.objects.Add(new ModNoteData(nd.time, nd.lineIndex, (ModNoteLineLayer)nd.noteLineLayer, (ModNoteCutDirection)nd.cutDirection, (ModColorType)nd.colorType, ConvertCustomData(nd.customData)));
                else if (obj is CustomObstacleData ob)
                    mod.objects.Add(new ModObstacleData(ob.time, ob.lineIndex, (ModObstacleType)ob.obstacleType, ob.duration, ob.width, ConvertCustomData(ob.customData)));
                else if (obj is CustomWaypointData wd)
                    mod.objects.Add(new ModWaypointData(wd.time, wd.lineIndex, (ModNoteLineLayer)wd.noteLineLayer, (ModOffsetDirection)wd.offsetDirection, ConvertCustomData(wd.customData)));
            }
        }
        

    }
}
