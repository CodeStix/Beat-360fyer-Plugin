using CustomJSONData;
using CustomJSONData.CustomBeatmap;
using LibBeatGenerator;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Beat360fyerPlugin
{
    public static class ModBeatmapConverter
    {
        private static IDictionary<string, object> CreateCustomDataDictionary(dynamic custom)
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

        private static dynamic CreateDynamicCustomData(IDictionary<string, object> custom, IDifficultyBeatmap bm)
        {
            if (custom == null)
            {
                dynamic t = Trees.Tree();
                // Workaround for NoodleExtensions error, why tf does this work??
                t.bpm = bm.level.beatsPerMinute;
                return t;
            }
            else
            {
                return custom;
            }
        }

        public static ModBeatmapData ToModBeatmap(this IDifficultyBeatmap bm)
        {
            ModBeatmapData mod = new ModBeatmapData(bm.beatmapData.numberOfLines, bm.level.beatsPerMinute);
            CustomBeatmapData data = (CustomBeatmapData)bm.beatmapData;

            foreach(CustomBeatmapEventData ev in data.beatmapEventsData)
            {
                mod.events.Add(new ModBeatmapEventData(ev.time, (ModBeatmapEventType)ev.type, ev.value, CreateCustomDataDictionary(ev.customData)));
            }

            foreach (BeatmapObjectData obj in data.beatmapObjectsData)
            {
                if (obj is CustomNoteData nd)
                    mod.objects.Add(new ModNoteData(nd.time, nd.lineIndex, (ModNoteLineLayer)nd.noteLineLayer, (ModNoteCutDirection)nd.cutDirection, (ModColorType)nd.colorType, CreateCustomDataDictionary(nd.customData)));
                else if (obj is CustomObstacleData ob)
                    mod.objects.Add(new ModObstacleData(ob.time, ob.lineIndex, (ModObstacleType)ob.obstacleType, ob.duration, ob.width, CreateCustomDataDictionary(ob.customData)));
                else if (obj is CustomWaypointData wd)
                    mod.objects.Add(new ModWaypointData(wd.time, wd.lineIndex, (ModNoteLineLayer)wd.noteLineLayer, (ModOffsetDirection)wd.offsetDirection, CreateCustomDataDictionary(wd.customData)));
            }

            return mod;
        }

        public static void ToBeatmap(this ModBeatmapData from, IDifficultyBeatmap originalBeatmap)
        {
            CustomBeatmapData original = (CustomBeatmapData)originalBeatmap.beatmapData;

            CustomBeatmapData bm = new CustomBeatmapData(from.NumberOfLines);
            foreach (ModBeatmapEventData ev in from.events)
            {
                bm.AddBeatmapEventData(new CustomBeatmapEventData(ev.time, (BeatmapEventType)ev.type, ev.value, ev.customData));
            }
            foreach (ModBeatmapObjectData d in from.objects)
            {
                if (d is ModNoteData nd)
                {
                    MethodInfo createCustomNoteMethod = typeof(CustomNoteData).GetMethod("CreateBasicNoteData", BindingFlags.NonPublic | BindingFlags.Static);
                    bm.AddBeatmapObjectData((BeatmapObjectData)createCustomNoteMethod.Invoke(null, new object[] { nd.time, nd.lineIndex, (NoteLineLayer)nd.noteLineLayer, (ColorType)nd.colorType, (NoteCutDirection)nd.cutDirection, CreateDynamicCustomData(nd.customData, originalBeatmap) }));
                }
                else if (d is ModObstacleData ob)
                    bm.AddBeatmapObjectData(new CustomObstacleData(ob.time, ob.lineIndex, (ObstacleType)ob.obstacleType, ob.duration, ob.width, CreateDynamicCustomData(ob.customData, originalBeatmap)));
                else if (d is ModWaypointData wd)
                    bm.AddBeatmapObjectData(new CustomWaypointData(wd.time, wd.lineIndex, (NoteLineLayer)wd.noteLineLayer, (OffsetDirection)wd.offsetDirection, CreateDynamicCustomData(wd.customData, originalBeatmap)));
            }

            CustomBeatmapData.CopyAvailableSpecialEventsPerKeywordDictionary(original, bm);
            CustomBeatmapData.CopyCustomData(original, bm);

            if (!FieldHelper.Set(originalBeatmap, "_beatmapData", bm))
            {
                Plugin.Log.Error($"Could not replace beatmap");
            }
        }
        

    }
}
