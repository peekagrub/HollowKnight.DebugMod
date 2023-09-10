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

        public static event Action OnGiveAllCharms;
        public static event Action OnRemoveAllCharms;
        
        private static void UpdateCharmsEffects()
        {
            PlayMakerFSM.BroadcastEvent("CHARM INDICATOR CHECK");
            PlayMakerFSM.BroadcastEvent("CHARM EQUIP CHECK");
        }

        [BindableMethod(name = "Give All Charms", category = "Charms")]
        public static void GiveAllCharms()
        {
            for (int i = 1; i <= 40; i++)
            {
                PlayerData.instance.SetBoolInternal("gotCharm_" + i, true);
            }

            PlayerData.instance.charmSlots = 10;
            PlayerData.instance.hasCharm = true;
            PlayerData.instance.charmsOwned = 40;
            PlayerData.instance.royalCharmState = 4;
            PlayerData.instance.gotShadeCharm = true;
            PlayerData.instance.charmCost_36 = 0;
            PlayerData.instance.charmSlots = 11;

            if (OnGiveAllCharms != null)
            {
                foreach (Action toInvoke in OnGiveAllCharms.GetInvocationList())
                {
                    try
                    {
                        toInvoke();
                    }
                    catch (Exception ex)
                    {
                        DebugMod.instance.LogError(ex);
                    }
                }
            }

            UpdateCharmsEffects();

            Console.AddLine("Added all charms to inventory");
        }

        [BindableMethod(name = "Remove All Charms", category = "Charms")]
        public static void RemoveAllCharms()
        {
            for (int i = 1; i <= 40; i++)
            {
                PlayerData.instance.SetBoolInternal("gotCharm_" + i, false);
                PlayerData.instance.SetBoolInternal("equippedCharm_" + i, false);
            }

            PlayerData.instance.charmSlots = 3;
            PlayerData.instance.hasCharm = false;
            PlayerData.instance.charmsOwned = 0;
            PlayerData.instance.royalCharmState = 0;
            PlayerData.instance.gotShadeCharm = false;
            PlayerData.instance.charmSlots = 3;
            PlayerData.instance.equippedCharms.Clear();
            
            if (OnRemoveAllCharms != null)
            {
                foreach (Action toInvoke in OnRemoveAllCharms.GetInvocationList())
                {
                    try
                    {
                        toInvoke();
                    }
                    catch (Exception ex)
                    {
                        DebugMod.instance.LogError(ex);
                    }
                }
            }
            
            UpdateCharmsEffects();
            Console.AddLine("Removed all charms from inventory");
        }

        [BindableMethod(name = "Increment Kingsoul", category = "Charms")]
        public static void IncreaseKingsoulLevel()
        {
            if (!PlayerData.instance.gotCharm_36)
            {
                PlayerData.instance.gotCharm_36 = true;
            }

            PlayerData.instance.royalCharmState++;

            PlayerData.instance.gotShadeCharm = PlayerData.instance.royalCharmState == 4;

            if (PlayerData.instance.royalCharmState >= 5)
            {
                PlayerData.instance.royalCharmState = 0;
            }

            PlayerData.instance.charmCost_36 = PlayerData.instance.royalCharmState switch
            {
                3 => 5,
                4 => 0,
                _ => PlayerData.instance.charmCost_36,
            };
            
            UpdateCharmsEffects();
        }

        [BindableMethod(name = "Fix Fragile Heart", category = "Charms")]
        public static void FixFragileHeart()
        {
            if (PlayerData.instance.brokenCharm_23)
            {
                PlayerData.instance.brokenCharm_23 = false;
                UpdateCharmsEffects();
                Console.AddLine("Fixed fragile heart");
            }
        }

        [BindableMethod(name = "Fix Fragile Greed", category = "Charms")]
        public static void FixFragileGreed()
        {
            if (PlayerData.instance.brokenCharm_24)
            {
                PlayerData.instance.brokenCharm_24 = false;
                UpdateCharmsEffects();
                Console.AddLine("Fixed fragile greed");
            }
        }

        [BindableMethod(name = "Fix Fragile Strength", category = "Charms")]
        public static void FixFragileStrength()
        {
            if (PlayerData.instance.brokenCharm_25)
            {
                PlayerData.instance.brokenCharm_25 = false;
                UpdateCharmsEffects();
                Console.AddLine("Fixed fragile strength");
            }
        }

        [BindableMethod(name = "Overcharm", category = "Charms")]
        public static void ToggleOvercharm()
        {
            PlayerData.instance.canOvercharm = true;
            PlayerData.instance.overcharmed = !PlayerData.instance.overcharmed;

            Console.AddLine("Set overcharmed: " + PlayerData.instance.overcharmed);
        }

        [BindableMethod(name = "Add Charm Notch", category = "Charms")]
        public static void AddCharmNotch()
        {
            PlayerData.instance.charmSlots++;
        }
        
        [BindableMethod(name = "Decrease Charm Notch", category = "Charms")]
        public static void DecreaseCharmNotch()
        {
            PlayerData.instance.charmSlots--;
        }
    }
}