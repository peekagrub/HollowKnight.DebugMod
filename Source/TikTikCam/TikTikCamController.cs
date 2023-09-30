using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityStandardAssets.ImageEffects;
using Modding;
using Object = UnityEngine.Object;
using System.Reflection;
using Shims.NET.System.Reflection;
using HutongGames.PlayMaker;
using GlobalEnums;
using Modding.Utils;
using System.Runtime.CompilerServices;

namespace DebugMod.TikTikCam
{
    public class TikTikCamController : MonoBehaviour
    {
        private static TikTikCamController instance;

        public static GameObject tiktikClone = null;

        private Camera tiktikCam;
        private Camera knightCam;
        private tk2dCamera knightCamtk2d;
        private static readonly Rect cornerRect, fullScreenRect = new Rect(0, 0, 1, 1);

        internal GameObject followTiktik;

        private bool tiktikIsMainCam = false;

        static TikTikCamController()
        {
            float camProp = DebugMod.settings.TiktikCamProportions;
            cornerRect = new Rect(1 - camProp, 1 - camProp, camProp, camProp);
        }

        private void Awake()
        {
            instance = this;
            tiktikCam = gameObject.AddComponent<Camera>();
            knightCamtk2d = GameCameras.instance.tk2dCam;
            knightCam = GameManager.instance.cameraCtrl.cam;

            tiktikCam.CopyFrom(GameManager.instance.cameraCtrl.cam);
            gameObject.transform.position = new Vector3(knightCam.gameObject.transform.position.x, knightCam.gameObject.transform.position.y, knightCam.gameObject.transform.position.z);

            tiktikClone.transform.position = new Vector3(-10, -10, tiktikClone.transform.position.z);
            tiktikClone.SetActive(false);
            tiktikClone.name = "Tiktik Clone";
            DontDestroyOnLoad(tiktikClone);

        }

        private void LateUpdate()
        {
            if (GameManager.instance is null || GameManager.instance.sceneName == "Menu_Title" || followTiktik == null)
                return;

            transform.position = new Vector3(followTiktik.transform.position.x, followTiktik.transform.position.y, knightCam.transform.position.z);
        }

        private void OnDisable()
        {
            UnityEngine.SceneManagement.SceneManager.activeSceneChanged -= CheckCamEnabled;
            UnityEngine.SceneManagement.SceneManager.activeSceneChanged -= FocusOnTiktik;
            On.GameManager.ReturnToMainMenu -= GameManager_ReturnToMainMenu;
            On.HeroController.Start -= ReEnableCam;
            knightCamtk2d.CameraSettings.rect = fullScreenRect;
            knightCam.depth = 50;
        }

        private void OnEnable()
        {
            DontDestroyOnLoad(gameObject);
            UnityEngine.SceneManagement.SceneManager.activeSceneChanged += CheckCamEnabled;
            UnityEngine.SceneManagement.SceneManager.activeSceneChanged += FocusOnTiktik;
            if (GameManager.instance.IsGameplayScene()) 
                On.GameManager.ReturnToMainMenu += GameManager_ReturnToMainMenu;
            else 
                On.HeroController.Start += ReEnableCam;
            CheckCamEnabled();
            if (knightCam == null)
            {
                knightCamtk2d = GameCameras.instance.tk2dCam;
                knightCam = GameManager.instance.cameraCtrl.cam;
            }
            tiktikCam.rect = tiktikIsMainCam ? fullScreenRect : cornerRect;
            knightCamtk2d.CameraSettings.rect = tiktikIsMainCam ? cornerRect : fullScreenRect;
            tiktikCam.depth = tiktikIsMainCam ? 50 : 60;
            knightCam.depth = tiktikIsMainCam ? 60 : 50;
            if (followTiktik == null)
            {
                FocusOnTiktik();
            }
        }

        private IEnumerator GameManager_ReturnToMainMenu(On.GameManager.orig_ReturnToMainMenu orig, GameManager self)
        {
            knightCamtk2d.CameraSettings.rect = fullScreenRect;
            tiktikCam.enabled = false;
            On.HeroController.Start += ReEnableCam;
            yield return orig(self);
        }

        private void ReEnableCam(On.HeroController.orig_Start orig, HeroController self)
        {
            On.HeroController.Start -= ReEnableCam;
            orig(self);
            CheckCamEnabled();
            knightCamtk2d = GameCameras.instance.tk2dCam;
            knightCam = GameManager.instance.cameraCtrl.cam;
            tiktikCam.rect = tiktikIsMainCam ? fullScreenRect : cornerRect;
            knightCamtk2d.CameraSettings.rect = tiktikIsMainCam ? cornerRect : fullScreenRect;
            tiktikCam.depth = tiktikIsMainCam ? 50 : 60;
            knightCam.depth = tiktikIsMainCam ? 60 : 50;
        }

        private void CheckCamEnabled(Scene _, Scene a) => CheckCamEnabled();

        private void CheckCamEnabled()
        {
            tiktikCam.enabled = GameManager.instance != null && GameManager.instance.IsGameplayScene();
        }

        private void FocusOnTiktik(Scene _, Scene __) => FocusOnTiktik();

        private void FocusOnTiktik()
        {
            var oldtiktik = followTiktik;
            followTiktik = null;
            foreach (GameObject tiktik in FindObjectsOfType<GameObject>().Where(x => x.name.StartsWith("Climber") && x.activeInHierarchy))
            {
                if (tiktik == null || tiktik == oldtiktik)
                    continue;

                followTiktik = tiktik;
                return;
            }

            if (followTiktik == null)
            {
                if (tiktikClone == null)
                    return;
                followTiktik = Instantiate(tiktikClone);
                followTiktik.SetActive(true);
                followTiktik.transform.position = new Vector3(0, 0 - followTiktik.transform.GetScaleY() / 2f, followTiktik.transform.position.z);
            }
        }

        public static void SwapCameras()
        {
            if (instance == null || !instance.enabled) return;
            instance.tiktikCam.rect = instance.tiktikIsMainCam ? fullScreenRect : cornerRect;
            instance.knightCamtk2d.CameraSettings.rect = instance.tiktikIsMainCam ? cornerRect : fullScreenRect;
            instance.tiktikCam.depth = instance.tiktikIsMainCam ? 50 : 60;
            instance.knightCam.depth = instance.tiktikIsMainCam ? 60 : 50;
            instance.tiktikIsMainCam = !instance.tiktikIsMainCam;
        }

        private void OnPreCull()
        {
            HeroController.instance.vignette.enabled = false;
        }

        private void OnPostRender()
        {
            HeroController.instance.vignette.enabled = true;
        }
    }
}
