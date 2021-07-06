using CustomJSONData.CustomBeatmap;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Beat360fyerPlugin
{
    public class ModBeatmapData
    {
        public List<BeatmapObjectData> objects = new List<BeatmapObjectData>();
        public List<BeatmapEventData> events = new List<BeatmapEventData>();

        private CustomBeatmapData from;

        public ModBeatmapData(CustomBeatmapData from)
        {
            this.from = from;
            foreach (BeatmapObjectData d in from.beatmapObjectsData)
                objects.Add(d);
            foreach (BeatmapEventData e in from.beatmapEventsData)
                events.Add(e);
        }

        public CustomBeatmapData ToBeatmap()
        {
            CustomBeatmapData bm = new CustomBeatmapData(from.numberOfLines);
            foreach (BeatmapObjectData o in objects.OrderBy((e) => e.time))
                if (!(o is ObstacleData ob && ob.duration == 0f) && o.time > 0f)
                    bm.AddBeatmapObjectData(o);
            foreach (BeatmapEventData o in events.OrderBy((e) => e.time))
                bm.AddBeatmapEventData(o);
            CustomBeatmapData.CopyAvailableSpecialEventsPerKeywordDictionary(this.from, bm);
            CustomBeatmapData.CopyCustomData(this.from, bm);
            return bm;
        }
    }

}
