using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using DebugMod.Hitbox;
using DebugMod.MonoBehaviours;
using GlobalEnums;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using Modding;
using Newtonsoft.Json;
using UnityEngine;
using Object = UnityEngine.Object;
using USceneManager = UnityEngine.SceneManagement.SceneManager;

namespace DebugMod
{
    public static partial class BindableFunctions
    {

        [BindableMethod(name = "Respawn Ghost", category = "Bosses")]
        public static void RespawnGhost()
        {
            BossHandler.RespawnGhost();
        }

        [BindableMethod(name = "Respawn Boss", category = "Bosses")]
        public static void RespawnBoss()
        {
            BossHandler.RespawnBoss();
        }

        [BindableMethod(name = "Respawn Failed Champ", category = "Bosses")]
        public static void ToggleFailedChamp()
        {
            PlayerData.instance.falseKnightDreamDefeated = !PlayerData.instance.falseKnightDreamDefeated;

            Console.AddLine("Set Failed Champion killed: " + PlayerData.instance.falseKnightDreamDefeated);
        }

        [BindableMethod(name = "Respawn Soul Tyrant", category = "Bosses")]
        public static void ToggleSoulTyrant()
        {
            PlayerData.instance.mageLordDreamDefeated = !PlayerData.instance.mageLordDreamDefeated;

            Console.AddLine("Set Soul Tyrant killed: " + PlayerData.instance.mageLordDreamDefeated);
        }

        [BindableMethod(name = "Respawn Lost Kin", category = "Bosses")]
        public static void ToggleLostKin()
        {
            PlayerData.instance.infectedKnightDreamDefeated = !PlayerData.instance.infectedKnightDreamDefeated;

            Console.AddLine("Set Lost Kin killed: " + PlayerData.instance.infectedKnightDreamDefeated);
        }
    }
}