using System;
using System.Collections;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Reflection;
using DebugMod.Hitbox;
using GlobalEnums;
using Modding;
using On;
using Modding.Utils;
using UnityEngine;
using UnityEngine.SceneManagement;
using USceneManager = UnityEngine.SceneManagement.SceneManager;
using static Shims.NET.System.Linq.Enumerable;
using System.Collections.Generic;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using Newtonsoft.Json;
using Modding.Patches;
using Object = UnityEngine.Object;
using static DebugMod.SaveState.SaveStateData;
using System.Security.Cryptography.X509Certificates;
using Shims.NET.System.Reflection;
using System.Runtime.Remoting.Metadata.W3cXsd2001;

namespace DebugMod
{
    /// <summary>
    /// Handles struct SaveStateData and individual SaveState operations
    /// </summary>
    internal class SaveState
    {
        // Some mods (ItemChanger) check type to detect vanilla scene loads.
        private class DebugModSaveStateSceneLoadInfo : GameManager.SceneLoadInfo { }

        //used to stop double loads/saves
        public static bool loadingSavestate { get; private set; }

        [Serializable]
        public class SaveStateData
        {
            public string saveStateIdentifier;
            public string saveScene;
            public string roomSpecificOptions = "0";
            public PlayerData savedPd;
            public object lockArea;
            public SceneData savedSd;
            public Vector3 savePos;
            public FieldInfo cameraLockArea;
            public string filePath;
            public bool isKinematized;
            public string[] loadedScenes;
            public string[] loadedSceneActiveScenes;
            //public bool quickmapStorageUsed;
            public List<EnemyPosition> enemyPosition;
            public List<FsmSaveState> fsmStates;
            public List<ComponentActiveStatus> componentActiveStatuses;
            public List<string> brokenBreakables;

            internal SaveStateData() { }

            internal SaveStateData(SaveStateData _data)
            {
                saveStateIdentifier = _data.saveStateIdentifier;
                saveScene = _data.saveScene;
                cameraLockArea = _data.cameraLockArea;
                savedPd = _data.savedPd;
                savedSd = _data.savedSd;
                savePos = _data.savePos;
                lockArea = _data.lockArea;
                isKinematized = _data.isKinematized;
                roomSpecificOptions = _data.roomSpecificOptions;
                //quickmapStorageUsed = _data.quickmapStorageUsed;

                if (_data.loadedScenes is not null)
                {
                    loadedScenes = new string[_data.loadedScenes.Length];
                    Array.Copy(_data.loadedScenes, loadedScenes, _data.loadedScenes.Length);
                }
                else
                {
                    loadedScenes = new[] { saveScene };
                }

                loadedSceneActiveScenes = new string[loadedScenes.Length];
                if (_data.loadedSceneActiveScenes is not null)
                {
                    Array.Copy(_data.loadedSceneActiveScenes, loadedSceneActiveScenes, loadedSceneActiveScenes.Length);
                }
                else
                {
                    for (int i = 0; i < loadedScenes.Length; i++)
                    {
                        loadedSceneActiveScenes[i] = loadedScenes[i];
                    }
                }

                if (_data.enemyPosition is not null)
                {
                    enemyPosition = _data.enemyPosition;
                }
                else
                {
                    enemyPosition = new();
                }

                if (_data.fsmStates is not null)
                {
                    fsmStates = _data.fsmStates;
                }
                else
                {
                    fsmStates = new();
                }

                if (_data.componentActiveStatuses is not null)
                {
                    componentActiveStatuses = _data.componentActiveStatuses;
                }
                else
                {
                    componentActiveStatuses = new();
                }

                if (_data.brokenBreakables is not null)
                {
                    brokenBreakables = _data.brokenBreakables;
                }
                else
                {
                    brokenBreakables = new();
                }
            }

            public void AddComponentStatuses()
            {
                HashSet<Component> processedComponents = new();

                bool testAndAdd(Component c)
                {
                    if (c == null || processedComponents.Contains(c)) return false;
                    processedComponents.Add(c);
                    return true;
                }
                foreach (var action in Object.FindObjectsOfType<PlayMakerFSM>()
                             .Where(fsm => fsm.gameObject.scene.name is not null and not "DontDestroyOnLoad")
                             .SelectMany(fsm => fsm.FsmStates)
                             .SelectMany(s => s.Actions))
                {
                    var fsm = action.Fsm;
                    switch (action)
                    {
                        case SetCollider sbc:
                            var sbcComponent = GetOwnerComponent<BoxCollider2D>(fsm, sbc.gameObject);
                            if (!testAndAdd(sbcComponent)) continue;
                            this.componentActiveStatuses.Add(new ComponentActiveStatus(sbcComponent, sbcComponent.isActiveAndEnabled));
                            break;
                        case SetCircleCollider scc:
                            var sccComponent = GetOwnerComponent<CircleCollider2D>(fsm, scc.gameObject);
                            if (!testAndAdd(sccComponent)) continue;
                            this.componentActiveStatuses.Add(new ComponentActiveStatus(sccComponent, sccComponent.isActiveAndEnabled));
                            break;
                        case SetPolygonCollider spc:
                            var spcComponent = GetOwnerComponent<PolygonCollider2D>(fsm, spc.gameObject);
                            if (!testAndAdd(spcComponent)) continue;
                            this.componentActiveStatuses.Add(new ComponentActiveStatus(spcComponent, spcComponent.isActiveAndEnabled));
                            break;
                        case SetMeshRenderer smr:
                            var smrComponent = GetOwnerComponent<MeshRenderer>(fsm, smr.gameObject);
                            if (!testAndAdd(smrComponent)) continue;
                            this.componentActiveStatuses.Add(new ComponentActiveStatus(smrComponent, smrComponent.enabled));
                            break;
                    }
                }
            }

            private static T GetOwnerComponent<T>(Fsm fsm, FsmOwnerDefault owner) where T : class
            {
                var go = fsm.GetOwnerDefaultTarget(owner);
                return go == null ? null : go.GetComponent<T>();
            }

            [Serializable]
            public class EnemyPosition
            {
                public const char SEPARATOR = '`';

                public string Name;
                public Vector3 Position;
            }

            [Serializable]
            public class FsmSaveState
            {
                public string parentName;
                public string fsmName;
                public string stateName;

                public bool waitRealTime;
                public float waitTimer;
                public float waitTime;

                public Dictionary<string, float> fsmFloats = new();
                public Dictionary<string, int> fsmInts = new();
                public Dictionary<string, bool> fsmBools = new();
                public Dictionary<string, string> fsmStrings = new();

                public static TDict ToDict<TDict, TVal>(IEnumerable<NamedVariable> vars) where TDict : Dictionary<string, TVal>, new()
                {
                    TDict result = new TDict();
                    if (vars == null) return result;

                    foreach (var v in vars)
                    {
                        try
                        {
                            result.Add(v.Name, (TVal)v.RawValue);
                        }
                        catch (ArgumentException) { }
                    }

                    return result;
                }

                public static void FromDict<T>(Dictionary<string, T> values, FsmVariables vars)
                {
                    foreach (var entry in values)
                    {
                        var v = vars.GetVariable(entry.Key);
                        if (v == null) continue;

                        v.RawValue = entry.Value;
                    }
                }
            }

            [Serializable]
            public class ComponentActiveStatus
            {
                public string go;
                public string type;
                public bool enabled;

                public ComponentActiveStatus(Component c, bool enabled)
                {
                    go = c.gameObject.name;
                    type = c.GetType().FullName;
                    this.enabled = enabled;
                }

                [JsonConstructor]
                public ComponentActiveStatus(string go, string type, bool enabled)
                {
                    this.go = go;
                    this.type = type;
                    this.enabled = enabled;
                }
            }
        }

        [SerializeField]
        public SaveStateData data;

        internal SaveState()
        {
            data = new SaveStateData();
        }

        #region saving

        public void SaveTempState(bool detailedSavestate)
        {
            //save level state before savestates so levers and dead enemies persist properly
            GameManager.instance.SaveLevelState();
            data.saveScene = GameManager.instance.GetSceneNameString();
            data.saveStateIdentifier = $"(tmp)_{data.saveScene}-{DateTime.Now.ToString("H:mm_d-MMM")}";
            data.savedPd = JsonConvert.DeserializeObject<PlayerData>(
                JsonConvert.SerializeObject(
                    PlayerData.instance,
                    Formatting.Indented,
                    new JsonSerializerSettings
                    {
                        ContractResolver = ShouldSerializeContractResolver.Instance,
                        TypeNameHandling = TypeNameHandling.Auto,
                        Converters = JsonConverterTypes.ConverterTypes
                    }
                ),
                new JsonSerializerSettings()
                {
                    ContractResolver = ShouldSerializeContractResolver.Instance,
                    TypeNameHandling = TypeNameHandling.Auto,
                    ObjectCreationHandling = ObjectCreationHandling.Replace,
                    Converters = JsonConverterTypes.ConverterTypes
                }
            );
            data.savedSd = JsonConvert.DeserializeObject<SceneData>(
                JsonConvert.SerializeObject(
                    SceneData.instance,
                    Formatting.Indented,
                    new JsonSerializerSettings()
                    {
                        ContractResolver = ShouldSerializeContractResolver.Instance,
                        TypeNameHandling = TypeNameHandling.Auto,
                        Converters = JsonConverterTypes.ConverterTypes
                    }
                ),
                new JsonSerializerSettings()
                {
                    ContractResolver = ShouldSerializeContractResolver.Instance,
                    TypeNameHandling = TypeNameHandling.Auto,
                    ObjectCreationHandling = ObjectCreationHandling.Replace,
                    Converters = JsonConverterTypes.ConverterTypes
                }
            );
            data.savePos = HeroController.instance.gameObject.transform.position;
            data.cameraLockArea = typeof(CameraController).GetField("currentLockArea", BindingFlags.Instance | BindingFlags.NonPublic);
            data.lockArea = data.cameraLockArea.GetValue(GameManager.instance.cameraCtrl);
            data.isKinematized = HeroController.instance.GetComponent<Rigidbody2D>().isKinematic;
            data.roomSpecificOptions = "0";
            var scenes = SceneWatcher.LoadedScenes;
            data.loadedScenes = scenes.Select(s => s.name).ToArray();
            data.loadedSceneActiveScenes = scenes.Select(s => s.activeSceneWhenLoaded).ToArray();
            //data.quickmapStorageUsed = HeroController.instance.gameObject.LocateMyFSM("Map Control").FsmVariables
            //    .FindFsmGameObject("Inventory").Value != null;
            Console.AddLine("Saved temp state");
            data.enemyPosition = new();
            data.fsmStates = new();
            data.componentActiveStatuses = new();
            data.brokenBreakables = new();
            if (detailedSavestate)
            {
                Console.AddLine("Making Detailed Save State");
                data.AddComponentStatuses();

                HashSet<GameObject> processedGos = new();
                foreach (var go in Object.FindObjectsOfType<Collider2D>().Select(o => o.gameObject))
                {
                    if (processedGos.Contains(go)) continue;

                    if (go.LocateMyFSM("health_manager_enemy"))
                    {
                        data.enemyPosition.Add(new()
                        {
                            Name = go.name,
                            Position = go.transform.position
                        });
                    }
                    else
                    {
                        var fsm = go.LocateMyFSM("FSM");
                        if (fsm != null && fsm.FsmEvents.Any(e => e.Name == "BREAKABLE DEACTIVE")
                                        && fsm.FsmVariables.BoolVariables.First(v => v.Name == "Activated").Value)
                        {
                            data.brokenBreakables.Add(go.name + go.transform.position);
                        }
                    }

                    processedGos.Add(go);
                }

                foreach (var fsm in Object.FindObjectsOfType<PlayMakerFSM>().Where(fsm => !(fsm.gameObject.scene.name is null or "DontDestroyOnload")))
                {
                    try
                    {
                        FsmSaveState state = new FsmSaveState
                        {
                            parentName = fsm.gameObject.name,
                            fsmName = fsm.name,
                            stateName = fsm.ActiveStateName,
                            fsmFloats = FsmSaveState.ToDict<Dictionary<string, float>, float>(fsm.FsmVariables.FloatVariables),
                            fsmInts = FsmSaveState.ToDict<Dictionary<string, int>, int>(fsm.FsmVariables.IntVariables),
                            fsmBools = FsmSaveState.ToDict<Dictionary<string, bool>, bool>(fsm.FsmVariables.BoolVariables),
                            fsmStrings = FsmSaveState.ToDict<Dictionary<string, string>, string>(fsm.FsmVariables.StringVariables),
                        };

                        if (fsm.Fsm.ActiveState != null)
                        {
                            var wait = fsm.Fsm.ActiveState.Actions.FirstOrDefault(a => a is Wait or WaitRandom);
                            if (wait != null)
                            {
                                state.waitRealTime = (bool)(wait.GetType().GetField("realTime")!.GetValue(wait));
                                state.waitTimer = (float)(wait.GetType()
                                    .GetField("timer", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(wait));
                                state.waitTime = wait is Wait w ? w.time.Value
                                    : (float)(wait.GetType().GetField("time", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(wait));
                            }
                        }
                        data.fsmStates.Add(state);
                    }
                    catch (Exception e)
                    {
                        DebugMod.instance.LogError($"Exception storing FSM state for {fsm.gameObject.name}-{fsm.FsmName}::{fsm.Fsm.ActiveState?.Name}: {e}");
                    }
                }
            }
        }

        public void NewSaveStateToFile(int paramSlot, bool detailedSavestate)
        {
            SaveTempState(detailedSavestate);
            SaveStateToFile(paramSlot);
        }

        public void SaveStateToFile(int paramSlot)
        {
            try
            {
                if (data.saveStateIdentifier.StartsWith("(tmp)_"))
                {
                    data.saveStateIdentifier = data.saveStateIdentifier.Substring(6);
                }
                else if (String.IsNullOrEmpty(data.saveStateIdentifier))
                {
                    throw new Exception("No temp save state set");
                }

                string saveStateFile = Path.Combine(SaveStateManager.path, $"savestate{paramSlot}.json");
                File.WriteAllText(saveStateFile,
                    JsonConvert.SerializeObject(
                        data,
                        Formatting.Indented,
                        new JsonSerializerSettings()
                        {
                            ContractResolver = ShouldSerializeContractResolver.Instance,
                            TypeNameHandling = TypeNameHandling.Auto,
                            Converters = JsonConverterTypes.ConverterTypes
                        }
                    )
                );
            }
            catch (Exception ex)
            {
                DebugMod.instance.LogDebug(ex.Message);
                throw ex;
            }
        }
        #endregion

        #region loading

        //loadDuped is used by external mods
        public void LoadTempState(bool loadDuped = false)
        {
            if (!PlayerDeathWatcher.playerDead &&
                !HeroController.instance.cState.transitioning &&
                HeroController.instance.transform.parent == null && // checks if in elevator/conveyor
                !loadingSavestate)
            {
                GameManager.instance.StartCoroutine(LoadStateCoro(loadDuped));
            }
            else
            {
                Console.AddLine("SaveStates cannot be loaded when dead, transitioning, or on elevators");
            }
        }

        //loadDuped is used by external mods
        public void NewLoadStateFromFile(bool loadDuped = false)
        {
            LoadStateFromFile(SaveStateManager.currentStateSlot);
            LoadTempState(loadDuped);
        }

        public void LoadStateFromFile(int paramSlot)
        {
            try
            {
                data.filePath = Path.Combine(SaveStateManager.path, $"savestate{paramSlot}.json");

                if (File.Exists(data.filePath))
                {
                    //DebugMod.instance.Log("checked filepath: " + data.filePath);
                    using FileStream fileStream = File.OpenRead(data.filePath);
                    using var reader = new StreamReader(fileStream);
                    string json = reader.ReadToEnd();

                    SaveStateData tmpData = JsonConvert.DeserializeObject<SaveStateData>(
                        json,
                        new JsonSerializerSettings()
                        {
                            ContractResolver = ShouldSerializeContractResolver.Instance,
                            TypeNameHandling = TypeNameHandling.Auto,
                            ObjectCreationHandling = ObjectCreationHandling.Replace,
                            Converters = JsonConverterTypes.ConverterTypes
                        }
                    );
                    try
                    {
                        data = new SaveStateData(tmpData);

                        DebugMod.instance.Log("Load SaveState ready: " + data.saveStateIdentifier);
                    }
                    catch (Exception ex)
                    {
                        DebugMod.instance.LogError("Error applying save state data: " + ex);
                    }
                }
            }
            catch (Exception ex)
            {
                DebugMod.instance.LogDebug(ex);
                throw;
            }
        }

        //loadDuped is used by external mods
        private IEnumerator LoadStateCoro(bool loadDuped)
        {
            //var used to prevent saves/loads, double save/loads softlock in menderbug, double load, black screen, etc
            loadingSavestate = true;

            //timer for loading savestates
            System.Diagnostics.Stopwatch loadingStateTimer = new System.Diagnostics.Stopwatch();
            loadingStateTimer.Start();

            //called here because this needs to be done here
            if (DebugMod.savestateFixes)
            {
                //TODO: Cleaner way to do this?
                //prevent hazard respawning
                if (DebugMod.CurrentHazardCoro != null) HeroController.instance.StopCoroutine(DebugMod.CurrentHazardCoro);
                if (DebugMod.CurrentInvulnCoro != null) HeroController.instance.StopCoroutine(DebugMod.CurrentInvulnCoro);
                DebugMod.CurrentHazardCoro = null;
                DebugMod.CurrentInvulnCoro = null;

                //fixes knockback storage
                ReflectionHelper.CallMethod(HeroController.instance, "CancelDamageRecoil");

                //ends hazard respawn animation
                var invPulse = HeroController.instance.GetComponent<InvulnerablePulse>();
                invPulse.stopInvulnerablePulse();
            }

            if (data.savedPd == null || string.IsNullOrEmpty(data.saveScene)) yield break;

            //remove dialogues if exists
            PlayMakerFSM.BroadcastEvent("BOX DOWN DREAM");
            PlayMakerFSM.BroadcastEvent("CONVO CANCEL");

            //step 1 of clearing soul
            HeroController.instance.TakeMPQuick(PlayerData.instance.MPCharge);
            HeroController.instance.SetMPCharge(0);
            PlayerData.instance.MPReserve = 0;
            PlayMakerFSM.BroadcastEvent("MP DRAIN");
            PlayMakerFSM.BroadcastEvent("MP LOSE");
            PlayMakerFSM.BroadcastEvent("MP RESERVE DOWN");

            GameManager.instance.entryGateName = "";
            GameManager.instance.startedOnThisScene = true;

            //Menderbug room loads faster (Thanks Magnetic Pizza)
            string dummySceneName =
                UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "Room_Mender_House" ?
                    "Room_Mender_House":
                    "Room_Sly_Storeroom";

            USceneManager.LoadScene(dummySceneName);

            yield return new WaitUntil(() => USceneManager.GetActiveScene().name == dummySceneName);

            JsonConvert.PopulateObject(
                JsonConvert.SerializeObject(
                    data.savedSd,
                    Formatting.Indented,
                    new JsonSerializerSettings()
                    {
                        ContractResolver = ShouldSerializeContractResolver.Instance,
                        TypeNameHandling = TypeNameHandling.Auto,
                        Converters = JsonConverterTypes.ConverterTypes
                    }
                ),
                SceneData.instance,
                new JsonSerializerSettings()
                {
                    ContractResolver = ShouldSerializeContractResolver.Instance,
                    TypeNameHandling = TypeNameHandling.Auto,
                    ObjectCreationHandling = ObjectCreationHandling.Replace,
                    Converters = JsonConverterTypes.ConverterTypes
                }
            );
            GameManager.instance.ResetSemiPersistentItems();

            yield return null;

            JsonConvert.PopulateObject(
                JsonConvert.SerializeObject(
                    data.savedPd,
                    Formatting.Indented,
                    new JsonSerializerSettings()
                    {
                        ContractResolver = ShouldSerializeContractResolver.Instance,
                        TypeNameHandling = TypeNameHandling.Auto,
                        Converters = JsonConverterTypes.ConverterTypes
                    }
                ),
                PlayerData.instance,
                new JsonSerializerSettings()
                {
                    ContractResolver = ShouldSerializeContractResolver.Instance,
                    TypeNameHandling = TypeNameHandling.Auto,
                    ObjectCreationHandling = ObjectCreationHandling.Replace,
                    Converters = JsonConverterTypes.ConverterTypes
                }
            );

            GameManager.instance.ResetSemiPersistentItems();

            SceneWatcher.LoadedSceneInfo[] sceneData = data
                .loadedScenes
                .Zip(data.loadedSceneActiveScenes, (name, gameplay) => new SceneWatcher.LoadedSceneInfo(name, gameplay))
                .ToArray();

            sceneData[0].LoadHook();

            //this kills enemies that were dead on the state, they respawn from previous code
            SceneData.instance = JsonConvert.DeserializeObject<SceneData>(
                JsonConvert.SerializeObject(
                    data.savedSd,
                    Formatting.Indented,
                    new JsonSerializerSettings()
                    {
                        ContractResolver = ShouldSerializeContractResolver.Instance,
                        TypeNameHandling = TypeNameHandling.Auto,
                        Converters = JsonConverterTypes.ConverterTypes
                    }
                ),
                new JsonSerializerSettings()
                {
                    ContractResolver = ShouldSerializeContractResolver.Instance,
                    TypeNameHandling = TypeNameHandling.Auto,
                    ObjectCreationHandling = ObjectCreationHandling.Replace,
                    Converters = JsonConverterTypes.ConverterTypes
                }
            );

            GameManager.instance.ChangeToSceneWithInfo
            (
                new DebugModSaveStateSceneLoadInfo
                {
                    SceneName = data.saveScene,
                    EntryGateName = "",
                    EntryDelay = 0f,
                }
            );

            yield return new WaitUntil(() => USceneManager.GetActiveScene().name == data.saveScene);

            GameManager.instance.cameraCtrl.PositionToHero(false);

            ReflectionHelper.SetField(GameManager.instance.cameraCtrl, "isGameplayScene", true);

            if (loadDuped)
            {
                yield return new WaitUntil(() => GameManager.instance.IsInSceneTransition == false);
                for (int i = 1; i < sceneData.Length; i++)
                {
                    On.GameManager.UpdateSceneName += sceneData[i].UpdateSceneNameOverride;
                    AsyncOperation loadop = USceneManager.LoadSceneAsync(sceneData[i].name, LoadSceneMode.Additive);
                    loadop.allowSceneActivation = true;
                    yield return loadop;
                    On.GameManager.UpdateSceneName -= sceneData[i].UpdateSceneNameOverride;
                    GameManager.instance.RefreshTilemapInfo(sceneData[i].name);
                    GameManager.instance.cameraCtrl.SceneInit();
                }
                GameManager.instance.BeginScene();
            }

            if (data.lockArea != null)
            {
                GameManager.instance.cameraCtrl.LockToArea(data.lockArea as CameraLockArea);
            }

            GameManager.instance.cameraCtrl.FadeSceneIn();

            HeroController.instance.CharmUpdate();

            PlayMakerFSM.BroadcastEvent("CHARM INDICATOR CHECK");    //update twister             
            PlayMakerFSM.BroadcastEvent("UPDATE NAIL DAMAGE");       //update nail

            var timeAtCharmIndicatorCheck = loadingStateTimer.ElapsedMilliseconds + 100;

            //step 2,manually trigger vessels lol, this is the only way besides turning off the mesh, but that will break stuff when you collect them
            if (PlayerData.instance.MPReserveMax < 33) GameObject.Find("Vessel 1").LocateMyFSM("vessel_orb").SetState("Init");
            else GameObject.Find("Vessel 1").LocateMyFSM("vessel_orb").SetState("Up Check");
            if (PlayerData.instance.MPReserveMax < 66) GameObject.Find("Vessel 2").LocateMyFSM("vessel_orb").SetState("Init");
            else GameObject.Find("Vessel 2").LocateMyFSM("vessel_orb").SetState("Up Check");
            if (PlayerData.instance.MPReserveMax < 99) GameObject.Find("Vessel 3").LocateMyFSM("vessel_orb").SetState("Init");
            else GameObject.Find("Vessel 3").LocateMyFSM("vessel_orb").SetState("Up Check");
            if (PlayerData.instance.MPReserveMax < 132) GameObject.Find("Vessel 4").LocateMyFSM("vessel_orb").SetState("Init");
            else GameObject.Find("Vessel 4").LocateMyFSM("vessel_orb").SetState("Up Check");
            //step 3, take and add some soul
            HeroController.instance.TakeMP(1);
            HeroController.instance.AddMPChargeSpa(1);
            //step 4, run animations later to actually add the soul on the main vessel
            PlayMakerFSM.BroadcastEvent("MP DRAIN");
            PlayMakerFSM.BroadcastEvent("MP LOSE");
            PlayMakerFSM.BroadcastEvent("MP RESERVE DOWN");

            HeroController.instance.geoCounter.geoTextMesh.text = data.savedPd.geo.ToString();

            GameCameras.instance.hudCanvas.gameObject.SetActive(true);

            FieldInfo cameraGameplayScene = typeof(CameraController).GetField("isGameplayScene", BindingFlags.Instance | BindingFlags.NonPublic);

            cameraGameplayScene.SetValue(GameManager.instance.cameraCtrl, true);

            yield return null;

            HeroController.instance.gameObject.transform.position = data.savePos;
            HeroController.instance.transitionState = HeroTransitionState.WAITING_TO_TRANSITION;
            HeroController.instance.GetComponent<Rigidbody2D>().isKinematic = data.isKinematized;

            loadingStateTimer.Stop();
            loadingSavestate = false;

            if (loadDuped && DebugMod.settings.ShowHitBoxes > 0)
            {
                int cs = DebugMod.settings.ShowHitBoxes;
                DebugMod.settings.ShowHitBoxes = 0;
                yield return new WaitUntil(() => HitboxViewer.State == 0);
                DebugMod.settings.ShowHitBoxes = cs;
            }

            ReflectionHelper.CallMethod(HeroController.instance, "FinishedEnteringScene", true, false);
            UpdateUIStateFromGameState();

            if (data.enemyPosition.Count > 0)
            {
                var gos = Object.FindObjectsOfType<Collider2D>()
                    .Select(c2d => c2d.gameObject)
                    .Where(go => go.LocateMyFSM("health_manager_enemy"))
                    .GroupBy(go => go.name)
                    .ToDictionary(x => x.Key, x => x.ToList());
                foreach (var epos in data.enemyPosition)
                {
                    if (!gos.ContainsKey(epos.Name) || gos[epos.Name].Count == 0)
                    {
                        DebugMod.instance.LogError($"Couldn't find enemy \"{epos.Name}\" after loading savestate");
                        continue;
                    }

                    var go = gos[epos.Name][0];
                    go.transform.position = epos.Position;
                    gos[epos.Name].RemoveAt(0);
                }
            }

            if (data.brokenBreakables.Count > 0)
            {
                var breakables = new HashSet<string>(data.brokenBreakables);
                foreach (var go in Object.FindObjectsOfType<Collider2D>()
                             .Select(c2d => c2d.gameObject)
                             .Where(go =>
                             {
                                 var fsm = go.LocateMyFSM("FSM");
                                 return fsm != null && fsm.FsmEvents.Any(e => e.Name == "BREAKABLE DEACTIVE");
                             }))
                {
                    if (breakables.Contains(go.name + go.transform.position))
                    {
                        go.LocateMyFSM("FSM").SendEvent("BREAK");
                    }
                }
            }

            if (data.componentActiveStatuses.Count > 0)
            {
                yield return null;
                HashSet<string> types = new();
                Dictionary<string, List<bool>> statuses = new();
                foreach (var cs in data.componentActiveStatuses)
                {
                    types.Add(cs.type);
                    string k = $"{cs.go}-{cs.type}";
                    if (!statuses.ContainsKey(k))
                    {
                        statuses.Add(k, new List<bool>());
                    }
                    statuses[k].Add(cs.enabled);
                }

                Assembly unityAssembly = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetType("UnityEngine.GameObject") != null);
                if (unityAssembly == null)
                {
                    throw new Exception("Unable to find Unity assembly");
                }

                foreach (var component in types.Select(unityAssembly.GetType)
                             .SelectMany(Object.FindObjectsOfType))
                {
                    Component c = (Component)component;
                    if (statuses.TryGetValue($"{c.gameObject.name}-{c.GetType().FullName}", out var l) && l.Count > 0)
                    {
                        bool enabled = l[0];
                        l.RemoveAt(0);
                        switch (c)
                        {
                            case MeshRenderer mr:
                                mr.enabled = enabled;
                                break;
                            case Collider2D c2d:
                                c2d.enabled = enabled;
                                break;
                            default:
                                throw new Exception($"Unknown component type {c.GetType()}");
                        }
                    }
                }
            }

            if (data.fsmStates.Count > 0)
            {
                Dictionary<string, Queue<FsmSaveState>> states = new();
                foreach (var fsmState in data.fsmStates)
                {
                    string k = fsmState.parentName + "-" + fsmState.fsmName;
                    if (!states.ContainsKey(k))
                    {
                        states.Add(k, new Queue<FsmSaveState>());
                    }
                    states[k].Enqueue(fsmState);
                }

                foreach (var fsm in Object.FindObjectsOfType<PlayMakerFSM>())
                {
                    if (states.TryGetValue(fsm.gameObject.name + '-' + fsm.FsmName, out var l) && l.Count > 0)
                    {
                        FsmSaveState s = l.Dequeue();

                        FsmSaveState.FromDict(s.fsmFloats, fsm.FsmVariables);
                        FsmSaveState.FromDict(s.fsmInts, fsm.FsmVariables);
                        FsmSaveState.FromDict(s.fsmBools, fsm.FsmVariables);
                        FsmSaveState.FromDict(s.fsmStrings, fsm.FsmVariables);

                        if (s.stateName == "")
                        {
                            fsm.Fsm.Stop();
                        }
                        else
                        {
                            if (fsm.ActiveStateName != s.stateName)
                            {
                                fsm.SetState(s.stateName);
                            }

                            var wait = fsm.Fsm.ActiveState?.Actions.FirstOrDefault(a => a is Wait or WaitRandom);
                            if (wait != null)
                            {
                                var type = wait.GetType();
                                type.GetField("realTime").SetValue(wait, s.waitRealTime);
                                type.GetField("timer", BindingFlags.NonPublic | BindingFlags.Instance)!
                                    .SetValue(wait, s.waitTimer);
                                type.GetField("startTime", BindingFlags.NonPublic | BindingFlags.Instance)!
                                    .SetValue(wait, FsmTime.RealtimeSinceStartup - s.waitTimer);
                            }
                        }
                    }
                }
            }

            if (data.roomSpecificOptions != "0" && data.roomSpecificOptions != null)
            {
                RoomSpecific.DoRoomSpecific(data.saveScene, data.roomSpecificOptions);
            }

            // need to wait before the health_display fsms are in the right state
            yield return new WaitForSeconds(0.1f);

            //moving this here seems to work now? no need to toggle canvas
            //preserve correct hp amount
            bool isInfiniteHp = DebugMod.infiniteHP;
            DebugMod.infiniteHP = false;
            //prevent flower break by taking it, then giving it back
            PlayerData.instance.hasXunFlower = false;
            PlayerData.instance.health = data.savedPd.health;
            HeroController.instance.TakeHealth(1);
            HeroController.instance.AddHealth(1);
            PlayerData.instance.hasXunFlower = data.savedPd.hasXunFlower;
            DebugMod.infiniteHP = isInfiniteHp;

            //this fixes actual lifeblood not just charms, and obsoletes UPDATE BLUE HEALTH
            int healthBlue = data.savedPd.healthBlue;
            for (int i = 0; i < healthBlue; i++)
            {
                PlayMakerFSM.BroadcastEvent("ADD BLUE HEALTH");
            }

            //invuln
            HeroController.instance.gameObject.LocateMyFSM("Roar Lock").SetState("Regrain Control");
            HeroController.instance.cState.invulnerable = false;

            //removes things like bench storage no clip float etc
            if (DebugMod.settings.SaveStateGlitchFixes) SaveStateGlitchFixes();

            //Benchwarp fixes courtesy of homothety, needed since savestates are now performed while paused
            // Revert pause menu timescale
            Time.timeScale = 1f;
            GameManager.instance.FadeSceneIn();

            // We have to set the game non-paused because TogglePauseMenu sucks and UIClosePauseMenu doesn't do it for us.
            GameManager.instance.isPaused = false;

            // Restore various things normally handled by exiting the pause menu. None of these are necessary afaik
            GameCameras.instance.ResumeCameraShake();
            if (HeroController.SilentInstance != null)
            {
                HeroController.instance.UnPause();
            }

            //This allows the next pause to stop the game correctly
            Time.timeScale = 1f;

            TimeSpan loadingStateTime = loadingStateTimer.Elapsed;
            // Console.AddLine($"Loaded savestate in " + loadingStateTime.ToString(@"ss\.fff") + "s");
            Console.AddLine($"Loaded savestate in {loadingStateTime.Seconds}.{loadingStateTime.Milliseconds:03}s");
        }

        // equivelant to GameMangager::UpdateUIStateFromGameState on 1.3+
        private void UpdateUIStateFromGameState()
        {
            if (GameManager.instance.ui is not null)
            {
                GameManager.instance.ui.SetUIStartState(GameManager.instance.gameState);
                return;
            }
            ReflectionHelper.GetPropertyInfo(typeof(GameManager), "ui", true).SetValue(GameManager.instance, Object.FindObjectOfType<UIManager>());
            if (GameManager.instance.ui is not null)
            {
                GameManager.instance.ui.SetUIStartState(GameManager.instance.gameState);
                return;
            }
            DebugMod.instance.LogError("Could not find the UI manager in this scene.");
        }

        //these are toggleable, as they will prevent glitches from persisting
        private void SaveStateGlitchFixes()
        {
            var rb2d = HeroController.instance.GetComponent<Rigidbody2D>();
            GameObject knight = GameObject.Find("Knight");
            PlayMakerFSM wakeFSM = knight.LocateMyFSM("Dream Return");
            PlayMakerFSM spellFSM = knight.LocateMyFSM("Spell Control");

            //White screen fixes
            wakeFSM.SetState("Idle");

            //float
            HeroController.instance.AffectedByGravity(true);
            rb2d.gravityScale = 0.79f;
            spellFSM.SetState("Inactive");

            //no clip
            rb2d.isKinematic = false;

            //bench storage
            GameManager.instance.SetPlayerDataBool(nameof(PlayerData.atBench), false);

            if (HeroController.SilentInstance != null)
            {
                if (HeroController.instance.cState.onConveyor || HeroController.instance.cState.onConveyorV || HeroController.instance.cState.inConveyorZone)
                {
                    HeroController.instance.GetComponent<ConveyorMovementHero>()?.StopConveyorMove();
                    HeroController.instance.cState.inConveyorZone = false;
                    HeroController.instance.cState.onConveyor = false;
                    HeroController.instance.cState.onConveyorV = false;
                }

                HeroController.instance.cState.nearBench = false;
            }
        }
        #endregion

        #region helper functionality

        public bool IsSet()
        {
            bool isSet = !String.IsNullOrEmpty(data.saveStateIdentifier);
            return isSet;
        }

        public string GetSaveStateID()
        {
            return data.saveStateIdentifier;
        }

        public string[] GetSaveStateInfo()
        {
            return new string[]
            {
                data.saveStateIdentifier,
                data.saveScene
            };
        }
        public SaveState.SaveStateData DeepCopy()
        {
            return new SaveState.SaveStateData(this.data);
        }

        #endregion
    }
}
