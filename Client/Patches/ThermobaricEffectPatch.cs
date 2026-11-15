using System;
using System.Reflection;
using HarmonyLib;
using SPT.Reflection.Patching;
using Systems.Effects;
using viniHNS.M79.Thermobaric;

namespace viniHNS.M79.Patches
{
    // the game makes a fresh Effects object from its bundle on every raid (GameWorld.InitializeEffects), so the
    // xm1060 blast gets added right after it wakes up
    internal class ThermobaricEffectPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(Effects), nameof(Effects.Awake));
        }

        [PatchPostfix]
        private static void Postfix(Effects __instance)
        {
            try
            {
                ThermobaricEffect.Register(__instance);
            }
            catch (Exception e)
            {
                // never break the game effects because of ours
                Plugin.LogSource.LogError("thermobaric: couldnt register the effect: " + e);
            }
        }
    }
}
