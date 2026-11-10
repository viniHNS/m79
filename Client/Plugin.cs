using BepInEx;
using BepInEx.Logging;
using viniHNS.M79.Patches;

namespace viniHNS.M79
{
    // guid has to be unique (author.modname)
    [BepInPlugin("viniHNS.M79", "vini's M79", "1.0.0")]
    public class Plugin : BaseUnityPlugin
    {
        public static ManualLogSource LogSource;

        private void Awake()
        {
            LogSource = Logger;

            // nest round sound on all the 40mm launchers
            new NestSoundPatch().Enable();

            LogSource.LogInfo("M79 loaded!");
        }
    }
}
