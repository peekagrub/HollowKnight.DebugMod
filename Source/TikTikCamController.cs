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

namespace DebugMod
{
    public class TikTikCamController : MonoBehaviour
    {
        public static TikTikCamController instance { get; private set; }

        public static GameObject tiktikClone = null;

        private Camera tiktikCam;
        private Camera knightCam;
        private tk2dCamera knightCamtk2d;
        private readonly Rect cornerRect, fullScreenRect = new Rect(0, 0, 1, 1);

        private GameObject followTiktik;

        private bool tiktikIsMainCam = true;
        private bool showTiktik = true;

        public TikTikCamController()
        {
            float camProp = DebugMod.settings.tiktikCamProportions;
            cornerRect = new Rect(1 - camProp, 1 - camProp, camProp, camProp);
        }

        private void Start()
        {
            instance = this;
            tiktikCam = gameObject.AddComponent<Camera>();
            knightCamtk2d = GameCameras.instance.tk2dCam;
            knightCam = GameManager.instance.cameraCtrl.cam;

            tiktikCam.CopyFrom(GameManager.instance.cameraCtrl.cam);
            gameObject.transform.position = new Vector3(knightCam.gameObject.transform.position.x, knightCam.gameObject.transform.position.y, knightCam.gameObject.transform.position.z);

            knightCamtk2d.CameraSettings.rect = cornerRect;
            knightCam.depth += 10;

            tiktikCam.cullingMask &= ~(1 << knightCam.gameObject.layer);

            tiktikClone.transform.position = new Vector3(-10, -10, tiktikClone.transform.position.z);
            tiktikClone.SetActive(false);
            tiktikClone.name = "Tiktik Clone";
            DontDestroyOnLoad(tiktikClone);
            FocusOnTiktik();
            CheckCamEnabled();

            UnityEngine.SceneManagement.SceneManager.activeSceneChanged += CheckCamEnabled;
            UnityEngine.SceneManagement.SceneManager.activeSceneChanged += FocusOnTiktik;
            On.GameManager.ReturnToMainMenu += GameManager_ReturnToMainMenu;
        }

        private void LateUpdate()
        {
            if (GameManager.instance is null || GameManager.instance.sceneName == "Menu_Title" || followTiktik == null)
                return;

            transform.position = new Vector3(followTiktik.transform.position.x, followTiktik.transform.position.y, knightCam.transform.position.z);
        }

        private IEnumerator GameManager_ReturnToMainMenu(On.GameManager.orig_ReturnToMainMenu orig, GameManager self)
        {
            knightCamtk2d.CameraSettings.rect = fullScreenRect;
            tiktikCam.enabled = false;
            showTiktik = false;
            On.HeroController.Start += ReEnableCam;
            yield return orig(self);
        }

        private void ReEnableCam(On.HeroController.orig_Start orig, HeroController self)
        {
            On.HeroController.Start -= ReEnableCam;
            orig(self);
            showTiktik = true;
            CheckCamEnabled();
            knightCamtk2d = GameCameras.instance.tk2dCam;
            knightCam = GameManager.instance.cameraCtrl.cam;
            tiktikIsMainCam = !tiktikIsMainCam;
            SwapCameras();
        }

        private void CheckCamEnabled(Scene _, Scene a) => CheckCamEnabled();

        private void CheckCamEnabled()
        {
            tiktikCam.enabled = showTiktik && GameManager.instance is not null && GameManager.instance.IsGameplayScene();
        }

        private void FocusOnTiktik(Scene _, Scene __) => FocusOnTiktik();

        private void FocusOnTiktik()
        {
            var oldTiktik = followTiktik;
            followTiktik = null;
            foreach (GameObject tiktik in FindObjectsOfType<GameObject>().Where(x => x.name == "Climber 1" && x.activeInHierarchy))
            {
                if (tiktik == null || tiktik == oldTiktik)
                    continue;

                followTiktik = tiktik;
                break;
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

        public void SwapCameras()
        {
            tiktikCam.rect = tiktikIsMainCam ? cornerRect : fullScreenRect;
            knightCamtk2d.CameraSettings.rect = tiktikIsMainCam ? fullScreenRect : cornerRect;
            tiktikCam.depth = tiktikIsMainCam ? 60 : 50;
            knightCam.depth = tiktikIsMainCam ? 50 : 60;
            tiktikIsMainCam = !tiktikIsMainCam;
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
