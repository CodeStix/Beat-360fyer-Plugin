using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Beat_360fyer_Plugin
{
    public static class GameModeHelper
    {
        private static Dictionary<string, BeatmapCharacteristicSO> customGamesModes = new Dictionary<string, BeatmapCharacteristicSO>();

        public const string GENERATED_360DEGREE_MODE = "Generated360Degree";

        public static BeatmapCharacteristicSO GetGenerated360GameMode()
        {
            return GetCustomGameMode(GENERATED_360DEGREE_MODE, null, "GEN360", "Generated 360 mode");
        }

        public static BeatmapCharacteristicSO GetCustomGameMode(string serializedName, Sprite icon, string name, string description, bool requires360Movement = true, bool containsRotationEvents = true, int numberOfColors = 2)
        {
            if (customGamesModes.TryGetValue(serializedName, out BeatmapCharacteristicSO bcso))
                return bcso;

            if (icon == null)
            {
                Texture2D tex = new Texture2D(50, 50);
                tex.SetPixel(0, 0, Color.red);
                icon = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            }

            BeatmapCharacteristicSO customGameMode = BeatmapCharacteristicSO.CreateInstance<BeatmapCharacteristicSO>();
            FieldHelper.Set(customGameMode, "_icon", icon);
            FieldHelper.Set(customGameMode, "_characteristicNameLocalizationKey", name);
            FieldHelper.Set(customGameMode, "_descriptionLocalizationKey", description);
            FieldHelper.Set(customGameMode, "_serializedName", serializedName);
            FieldHelper.Set(customGameMode, "_compoundIdPartName", serializedName); // What is _compoundIdPartName?
            FieldHelper.Set(customGameMode, "_sortingOrder", 100);
            FieldHelper.Set(customGameMode, "_containsRotationEvents", containsRotationEvents);
            FieldHelper.Set(customGameMode, "_requires360Movement", requires360Movement);
            FieldHelper.Set(customGameMode, "_numberOfColors", numberOfColors);
            return customGameMode;
        }

        // Way to get built in game modes

        //CustomLevelLoader customLevelLoader = UnityEngine.Object.FindObjectOfType<CustomLevelLoader>();
        //if (customLevelLoader == null)
        //{
        //    Plugin.Log.Info("customLevelLoader is null");
        //    return;
        //}
        //BeatmapCharacteristicCollectionSO defaultGameModes = FieldHelper.Get<BeatmapCharacteristicCollectionSO>(customLevelLoader, "_beatmapCharacteristicCollection");
        //if (defaultGameModes == null)
        //{
        //    Plugin.Log.Info("beatmapCharacteristicCollection is null");
        //    return;
        //}
        //Plugin.Log.Info("characteristics: " + string.Join(", ", defaultGameModes.beatmapCharacteristics.Select((e) => e.serializedName)));
        //BeatmapCharacteristicSO default360GameMode = defaultGameModes.GetBeatmapCharacteristicBySerializedName("360Degree");
        //if(default360GameMode == null)
        //{
        //    Plugin.Log.Info("default360GameMode is null");
        //    return;
        //}
    }
}
