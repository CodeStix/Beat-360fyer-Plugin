using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using HarmonyLib;
using UnityEngine.SceneManagement;

namespace Beat_360fyer_Plugin
{
    /// <summary>
    /// Monobehaviours (scripts) are added to GameObjects.
    /// For a full list of Messages a Monobehaviour can receive from the game, see https://docs.unity3d.com/ScriptReference/MonoBehaviour.html.
    /// </summary>
    public class Beat_360fyer_PluginController : MonoBehaviour
    {
        public static Beat_360fyer_PluginController Instance { get; private set; }

        // These methods are automatically called by Unity, you should remove any you aren't using.
        #region Monobehaviour Messages
        /// <summary>
        /// Only ever called once, mainly used to initialize variables.
        /// </summary>
        private void Awake()
        {
            // For this particular MonoBehaviour, we only want one instance to exist at any time, so store a reference to it in a static property
            //   and destroy any that are created while one already exists.
            if (Instance != null)
            {
                Plugin.Log.Warn($"Instance of {GetType().Name} already exists, destroying.");
                GameObject.DestroyImmediate(this);
                return;
            }
            GameObject.DontDestroyOnLoad(this); // Don't destroy this object on scene changes
            Instance = this;
            Plugin.Log.Info($"{name}: Awake()");
        }
        /// <summary>
        /// Only ever called once on the first frame the script is Enabled. Start is called after any other script's Awake() and before Update().
        /// </summary>
        private void Start()
        {
        }

        private void SceneManager_activeSceneChanged(Scene arg0, Scene arg1)
        {
            
        }

        //private IEnumerator LookForMenu()
        //{
        //    while(true)
        //    {
        //        yield return new WaitForSeconds(3f);
        //        LevelCollectionNavigationController view = FindObjectOfType<LevelCollectionNavigationController>();
        //        Plugin.Log.Info($"Found {(view?.ToString() ?? "null")}");
        //        StandardLevelDetailView view2 = FindObjectOfType<StandardLevelDetailView>();
        //        Plugin.Log.Info($"Found2 {(view2?.ToString() ?? "null")}");
        //        if(view2 != null)
        //            view2.didChangeDifficultyBeatmapEvent += View2_didChangeDifficultyBeatmapEvent;
        //    }
        //}

        private void View2_didChangeDifficultyBeatmapEvent(StandardLevelDetailView arg1, IDifficultyBeatmap arg2)
        {
            Plugin.Log.Info($"selected {(arg2.level.songName)}");
        }


        /// <summary>
        /// Called every frame if the script is enabled.
        /// </summary>
        private void Update()
        {

        }

        /// <summary>
        /// Called every frame after every other enabled script's Update().
        /// </summary>
        private void LateUpdate()
        {

        }

        /// <summary>
        /// Called when the script becomes enabled and active
        /// </summary>
        private void OnEnable()
        {

        }

        /// <summary>
        /// Called when the script becomes disabled or when it is being destroyed.
        /// </summary>
        private void OnDisable()
        {

        }

        /// <summary>
        /// Called when the script is being destroyed.
        /// </summary>
        private void OnDestroy()
        {
            SceneManager.activeSceneChanged -= SceneManager_activeSceneChanged;
            Plugin.Log.Info($"{name}: OnDestroy()");
            if (Instance == this)
                Instance = null; // This MonoBehaviour is being destroyed, so set the static instance property to null.
        }
        #endregion
    }
}
