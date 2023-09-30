using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace DebugMod.TikTikCam
{
    public class TikTikCamHandler
    {
        private static GameObject tiktikCam;
        private static TikTikCamController controller;

        public static void CreateTikTikCam()
        {
            tiktikCam = new GameObject();
            bool enabled = DebugMod.settings.TiktikCamDefaultState;
            DebugMod.instance.Log(enabled);
            tiktikCam.SetActive(enabled);
            controller = tiktikCam.AddComponent<TikTikCamController>();
            tiktikCam.name = "TikTik Cam";
            UnityEngine.Object.DontDestroyOnLoad(tiktikCam);
        }

        public static void ToggleTikTikCam()
        {
            tiktikCam.SetActive(!tiktikCam.activeSelf);
            controller.enabled = tiktikCam.activeSelf;
        }

        public static void DestroyTikTikCam()
        {
            UnityEngine.Object.DestroyImmediate(tiktikCam);
        }
    }
}
