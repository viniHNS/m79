using System.Collections.Generic;
using System.Reflection;
using EFT;
using EFT.InventoryLogic;
using HarmonyLib;
using SPT.Reflection.Patching;
using UnityEngine;

namespace viniHNS.M79.Patches
{
    // hornet's nest shot sound
    // m79 = thump + ks23 blast. the other launchers keep their own shot and get the ks23 on top of it
    // (premixed, theres one bank per launcher sound in m79_sounds.bundle)
    // every local shot goes thru WeaponSoundPlayer.FireBullet (m203 too, w/ its own sound player) so we just swap
    // the banks there for that one call and put em back after. sound only, recoil and accuracy dont change
    internal class NestSoundPatch : ModulePatch
    {
        private const string NestId = "956d95b681fc6f0014584840";

        // launcher Body bank -> nest bank. fn40gl_main is also the m32a1 one
        private static readonly Dictionary<string, string> NestBanks = new Dictionary<string, string>
        {
            { "m79_main", "m79_nest" },
            { "fn40gl_main", "m79_nest_fn40gl" },
            { "m203_main", "m79_nest_m203" },
        };

        private static readonly Dictionary<string, SoundBank> _banks = new Dictionary<string, SoundBank>();
        private static readonly HashSet<string> _warned = new HashSet<string>();

        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(WeaponSoundPlayer), nameof(WeaponSoundPlayer.FireBullet));
        }

        [PatchPrefix]
        private static void Prefix(WeaponSoundPlayer __instance, Ammo ammo, ref SoundBank[] __state)
        {
            __state = null;
            if (ammo == null || ammo.TemplateId.ToString() != NestId || __instance.Body == null)
            {
                return;
            }

            // unknown launcher (other mods etc) just keeps its normal sound
            if (!NestBanks.TryGetValue(__instance.Body.name, out string bankName))
            {
                Warn(__instance.Body.name, "no nest bank for launcher sound " + __instance.Body.name + ", keeping the normal one");
                return;
            }

            SoundBank bank = FindBank(bankName);
            if (bank == null)
            {
                return;
            }

            __state = new[] { __instance.Body, __instance.BodySilenced, __instance.Doublet, __instance.DoubletSilenced };
            __instance.Body = bank;
            __instance.BodySilenced = bank;
            __instance.Doublet = bank;
            __instance.DoubletSilenced = bank;
        }

        // finalizer and not postfix, so the banks come back even if FireBullet throws
        [PatchFinalizer]
        private static void Finalizer(WeaponSoundPlayer __instance, SoundBank[] __state)
        {
            if (__state == null)
            {
                return;
            }

            __instance.Body = __state[0];
            __instance.BodySilenced = __state[1];
            __instance.Doublet = __state[2];
            __instance.DoubletSilenced = __state[3];
        }

        private static SoundBank FindBank(string bankName)
        {
            // unity null check, if the bundle got unloaded it looks again
            if (_banks.TryGetValue(bankName, out SoundBank cached) && cached != null)
            {
                return cached;
            }

            // m79_sounds.bundle is a dependency of the nest round bundle (bundles.json) and the game loads all the
            // assets of a bundle, so the banks are in memory whenever theres a nest round around
            foreach (SoundBank b in Resources.FindObjectsOfTypeAll<SoundBank>())
            {
                if (b != null && NestBanks.ContainsValue(b.name))
                {
                    _banks[b.name] = b;
                }
            }

            if (!_banks.TryGetValue(bankName, out SoundBank bank) || bank == null)
            {
                Warn(bankName, "sound bank " + bankName + " not loaded, using the launcher's own shot sound");
                return null;
            }

            // clips arent preloaded (same as the game banks), load them now or the first shot comes late
            foreach (EnvironmentVariety env in bank.Environments)
            {
                foreach (DistanceVarity slot in env.Clips)
                {
                    foreach (AudioClip clip in slot.Clips)
                    {
                        if (clip != null)
                        {
                            clip.LoadAudioData();
                        }
                    }
                }
            }

            Plugin.LogSource.LogInfo("NestSoundPatch: found " + bankName);
            return bank;
        }

        private static void Warn(string key, string msg)
        {
            if (_warned.Add(key))
            {
                Plugin.LogSource.LogWarning("NestSoundPatch: " + msg);
            }
        }
    }
}
