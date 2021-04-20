using HarmonyLib;
using IPA;
using IPA.Config;
using IPA.Config.Stores;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using IPALogger = IPA.Logging.Logger;
using IPAConfig = IPA.Config.Config;

namespace Beat360fyerPlugin
{
    [Plugin(RuntimeOptions.SingleStartInit)]
    public class Plugin
    {
        internal static Plugin Instance { get; private set; }
        internal static IPALogger Log { get; private set; }

        [Init]
        public void Init(IPALogger logger, IPAConfig conf)
        {
            Instance = this;
            Log = logger;
            Config.Instance = conf.Generated<Config>();
            Log.Info($"Beat-360fyer-Plugin initialized.");
        }

        [OnStart]
        public void OnApplicationStart()
        {
            Harmony harmony = new Harmony("nl.codestix.Beat360fyerPlugin");
            harmony.PatchAll();
        }

        [OnExit]
        public void OnApplicationQuit()
        {
        }
    }
}
