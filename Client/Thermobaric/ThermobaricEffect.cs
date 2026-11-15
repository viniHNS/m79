using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using EFT.Ballistics;
using EFT.Particles;
using Systems.Effects;
using UnityEngine;

namespace viniHNS.M79.Thermobaric
{
    // xm1060 blast. the round has ExplosionType = EffectName on the server and the game looks that name up in
    // Effects (ClientGameWorld.ShotDelegate -> Effects.EmitGrenade), so we just add one more entry there
    // its a clone of one of the game explosions (dust, shockwave, heat haze, sparks, smoke, sound) retuned, plus our
    // own fireball and black smoke flipbooks on top. Effects is rebuilt from the bundle every raid, so this runs
    // every raid too
    internal static class ThermobaricEffect
    {
        public const string EffectName = "m79_thermobaric";

        // our flipbooks, same layout as the game fireball (12x7 on 2048x1024, frame 0 top left)
        // "fireball" is additive (game fireball material), "smoke" is the sooty cloud behind it
        private static readonly string[] Layers = { "smoke", "fireball" };

        // "<child>.<what>" = multiplier (size, life, speed, count), "<child>.off" = 1 turns it off
        // anything not listed stays like the game one. names are the particle objects inside the game prefab
        private static readonly Dictionary<string, float> Defaults = new Dictionary<string, float>
        {
            // our layers (absolute values, not multipliers). count 0 = layer off
            { "fireball.count", 4f },
            { "fireball.sizeMin", 4f },
            { "fireball.sizeMax", 5.5f },
            { "fireball.lifeMin", 0.85f },
            { "fireball.lifeMax", 1.1f },
            { "fireball.delay", 0.03f },
            { "fireball.radius", 0.45f },
            { "fireball.speed", 0.35f },
            { "fireball.rotate", 1f },
            { "fireball.pivotY", 0f },
            { "fireball.tilesX", 12f },
            { "fireball.tilesY", 7f },
            // hue is baked in the flipbook, so the tint is just gain (above 1 goes into the game bloom)
            { "fireball.tintR", 2f },
            { "fireball.tintG", 1.8f },
            { "fireball.tintB", 1.6f },
            { "fireball.tintA", 0.5f },

            // smoke flipbook was rendered 1.5x wider than the fire one
            { "smoke.count", 3f },
            { "smoke.sizeMin", 6f },
            { "smoke.sizeMax", 8f },
            { "smoke.lifeMin", 1f },
            { "smoke.lifeMax", 1.3f },
            { "smoke.delay", 0.03f },
            { "smoke.radius", 0.4f },
            { "smoke.speed", 0.3f },
            { "smoke.rotate", 1f },
            { "smoke.pivotY", 0f },
            { "smoke.tilesX", 12f },
            { "smoke.tilesY", 7f },
            { "smoke.fudge", 20f },

            // game pieces
            { "explosion_center.size", 1.5f },
            { "flash.size", 1.8f },
            { "flash_area.size", 1.8f },
            { "DistortionWave.size", 2f },
            { "DistortionWave.life", 1.4f },
            { "HeatDistortion.size", 2.2f },
            { "HeatDistortion.life", 2.5f },
            { "DustAngle10.size", 1.5f },
            { "DustAngle10.speed", 1.4f },
            { "DustAngle45.size", 1.3f },
            { "BlackParticlesAnge15.size", 1.5f },
            { "BlackParticlesAnge15.life", 1.3f },
            { "long smoke.size", 1.5f },
            { "long smoke.life", 1.3f },
            { "Drop Sparks.count", 1.5f },
            // overpressure, not frag: less chunks flying
            { "Drop Concrete.count", 0.4f },
            { "Drop Concrete no subs.count", 0.4f },

            // light from the effects light pool (fades out linearly)
            { "light.r", 1f },
            { "light.g", 0.62f },
            { "light.b", 0.3f },
            { "light.range", 24f },
            { "light.intensity", 7f },
            { "light.time", 0.55f },
            { "light.shadows", 1f },
        };

        // game effect we clone, and the game particle whose material each layer copies
        private static readonly Dictionary<string, string> DefaultNames = new Dictionary<string, string>
        {
            { "base", "Grenade_new" },
            { "fireball.mat", "explosion_center" },
            { "smoke.mat", "BlackParticlesAnge15" },
        };

        private static readonly Dictionary<string, float> _values = new Dictionary<string, float>(Defaults);
        private static readonly Dictionary<string, string> _names = new Dictionary<string, string>(DefaultNames);

        private static readonly Dictionary<string, Texture2D> _textures = new Dictionary<string, Texture2D>();
        private static readonly Dictionary<string, Material> _materials = new Dictionary<string, Material>();
        private static BasicParticleSystemMediator _clone;

        public static void Register(Effects effects)
        {
            if (effects == null || effects.EffectsArray == null)
            {
                return;
            }

            if (effects.EffectsArray.Any(e => e != null && e.Name == EffectName))
            {
                return;
            }

            Effects.Effect source = FindSource(effects);
            if (source == null)
            {
                Plugin.LogSource.LogWarning("thermobaric: no game explosion to clone, the xm1060 blast wont show");
                return;
            }

            Effects.Effect effect = new Effects.Effect
            {
                Name = EffectName,
                MaterialTypes = new MaterialType[0],
                Sound = source.Sound,
                SoundFP = source.SoundFP,
                Decal = source.Decal,
                UseDeferredDecals = source.UseDeferredDecals,
                ParticlesShift = source.ParticlesShift,
            };
            Build(effect, source);

            Effects.Effect[] list = new Effects.Effect[effects.EffectsArray.Length + 1];
            effects.EffectsArray.CopyTo(list, 0);
            list[list.Length - 1] = effect;
            effects.EffectsArray = list;
            // rebuilds the name lookup and hands the light pool / decals to the new entry
            effects.InitDictionaryAndNames();

            Plugin.LogSource.LogInfo("thermobaric: effect registered (base " + source.Name + ", own layers: " + string.Join(", ", _textures.Keys.ToArray()) + ")");
        }

        private static Effects.Effect FindSource(Effects effects)
        {
            Effects.Effect found = null;
            foreach (Effects.Effect e in effects.EffectsArray)
            {
                if (e == null || e.BasicParticleSystemMediator == null || e.Name == EffectName)
                {
                    continue;
                }

                if (e.Name == _names["base"])
                {
                    return e;
                }

                // fallback, the 40mm one of the game
                if (e.Name == "smallgrenade_expl")
                {
                    found = e;
                }
            }

            return found;
        }

        private static void Build(Effects.Effect effect, Effects.Effect source)
        {
            if (_clone != null)
            {
                UnityEngine.Object.Destroy(_clone.gameObject);
            }

            BasicParticleSystemMediator original = source.BasicParticleSystemMediator;
            _clone = UnityEngine.Object.Instantiate(original, original.transform.parent);
            _clone.name = "Effect M79 Thermobaric Mediator";

            foreach (ParticleSystem root in _clone._particleSystems)
            {
                if (root == null)
                {
                    continue;
                }

                root.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                // layers first: they need the game materials before Tune switches anything off
                List<GameObject> added = new List<GameObject>();
                foreach (string layer in Layers)
                {
                    GameObject go = AddLayer(root, layer);
                    if (go != null)
                    {
                        added.Add(go);
                    }
                }

                foreach (ParticleSystem ps in root.GetComponentsInChildren<ParticleSystem>(true))
                {
                    if (!added.Contains(ps.gameObject))
                    {
                        Tune(ps);
                    }
                }
            }

            effect.BasicParticleSystemMediator = _clone;
            effect.Light = Get("light.intensity", 0f) > 0f;
            effect.LightColor = new Color(Get("light.r", 1f), Get("light.g", 0.6f), Get("light.b", 0.3f), 1f);
            effect.LightRange = Get("light.range", 20f);
            effect.LightIntensity = Get("light.intensity", 6f);
            effect.LightTime = Get("light.time", 0.5f);
            effect.LightMaxDist = 400f;
            effect.WithShadows = Get("light.shadows", 1f) > 0.5f;
        }

        private static void Tune(ParticleSystem ps)
        {
            string n = ps.gameObject.name;
            if (Get(n + ".off", 0f) > 0.5f)
            {
                ps.gameObject.SetActive(false);
                return;
            }

            ParticleSystem.MainModule main = ps.main;
            float k;
            if (TryGet(n + ".size", out k))
            {
                main.startSize = Mul(main.startSize, k);
            }

            if (TryGet(n + ".life", out k))
            {
                main.startLifetime = Mul(main.startLifetime, k);
            }

            if (TryGet(n + ".speed", out k))
            {
                main.startSpeed = Mul(main.startSpeed, k);
            }

            if (TryGet(n + ".count", out k))
            {
                ParticleSystem.EmissionModule emission = ps.emission;
                emission.rateOverTime = Mul(emission.rateOverTime, k);
                for (int i = 0; i < emission.burstCount; i++)
                {
                    ParticleSystem.Burst burst = emission.GetBurst(i);
                    burst.count = Mul(burst.count, k);
                    emission.SetBurst(i, burst);
                }
            }
        }

        // one of our flipbooks: a copy of the game fireball particle (plays the sheet once over its life) w/ our
        // texture on a copy of a game material, few big billboards instead of lots of small ones
        private static GameObject AddLayer(ParticleSystem root, string layer)
        {
            int count = Mathf.RoundToInt(Get(layer + ".count", 0f));
            if (count < 1)
            {
                return null;
            }

            Transform gameBall = FindChild(root.transform, "explosion_center");
            Transform matSource = FindChild(root.transform, _names[layer + ".mat"]);
            if (gameBall == null || matSource == null)
            {
                return null;
            }

            ParticleSystemRenderer matRenderer = matSource.GetComponent<ParticleSystemRenderer>();
            Texture2D tex = LayerTexture(layer);
            if (tex == null || matRenderer == null || matRenderer.sharedMaterial == null)
            {
                return null;
            }

            GameObject go = UnityEngine.Object.Instantiate(gameBall.gameObject, gameBall.parent);
            go.name = "m79_" + layer;
            ParticleSystem ps = go.GetComponent<ParticleSystem>();
            ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
            if (ps == null || renderer == null)
            {
                UnityEngine.Object.Destroy(go);
                return null;
            }

            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            Material mat;
            if (!_materials.TryGetValue(layer, out mat) || mat == null)
            {
                mat = new Material(matRenderer.sharedMaterial) { name = "m79_thermobaric_" + layer };
                mat.hideFlags = HideFlags.HideAndDontSave;
                _materials[layer] = mat;
            }

            mat.mainTexture = tex;
            // only touch the tint if its set, the smoke one keeps what the game material has
            float r;
            if (TryGet(layer + ".tintR", out r) && mat.HasProperty("_TintColor"))
            {
                mat.SetColor("_TintColor", new Color(r, Get(layer + ".tintG", r), Get(layer + ".tintB", r), Get(layer + ".tintA", 0.5f)));
            }

            renderer.sharedMaterial = mat;
            renderer.pivot = new Vector3(0f, Get(layer + ".pivotY", 0f), 0f);
            renderer.sortingFudge = Get(layer + ".fudge", 0f);

            ParticleSystem.MainModule main = ps.main;
            main.startDelay = Get(layer + ".delay", 0.03f);
            main.startLifetime = new ParticleSystem.MinMaxCurve(Get(layer + ".lifeMin", 0.85f), Get(layer + ".lifeMax", 1.1f));
            main.startSize = new ParticleSystem.MinMaxCurve(Get(layer + ".sizeMin", 4f), Get(layer + ".sizeMax", 5.5f));
            main.startSpeed = Get(layer + ".speed", 0.35f);
            if (Get(layer + ".rotate", 1f) < 0.5f)
            {
                // flipbooks that have an up (ground burst) cant spin
                main.startRotation = 0f;
            }

            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });

            ParticleSystem.ShapeModule shape = ps.shape;
            shape.radius = Get(layer + ".radius", 0.45f);

            ParticleSystem.TextureSheetAnimationModule sheet = ps.textureSheetAnimation;
            sheet.numTilesX = Mathf.Max(1, Mathf.RoundToInt(Get(layer + ".tilesX", 12f)));
            sheet.numTilesY = Mathf.Max(1, Mathf.RoundToInt(Get(layer + ".tilesY", 7f)));
            return go;
        }

        private static Texture2D LayerTexture(string layer)
        {
            Texture2D cached;
            if (_textures.TryGetValue(layer, out cached) && cached != null)
            {
                return cached;
            }

            byte[] png;
            using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream("viniHNS.M79.Thermobaric." + layer + ".png"))
            {
                if (s == null)
                {
                    return null;
                }

                png = new byte[s.Length];
                int read = 0;
                while (read < png.Length)
                {
                    int got = s.Read(png, read, png.Length - read);
                    if (got <= 0)
                    {
                        break;
                    }

                    read += got;
                }
            }

            Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, true);
            if (!ImageConversion.LoadImage(tex, png, true))
            {
                UnityEngine.Object.Destroy(tex);
                return null;
            }

            tex.name = "m79_thermobaric_" + layer;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.hideFlags = HideFlags.HideAndDontSave;
            _textures[layer] = tex;
            return tex;
        }

        private static Transform FindChild(Transform root, string name)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == name)
                {
                    return t;
                }
            }

            return null;
        }

        private static ParticleSystem.MinMaxCurve Mul(ParticleSystem.MinMaxCurve c, float k)
        {
            switch (c.mode)
            {
                case ParticleSystemCurveMode.Constant:
                    c.constant *= k;
                    break;
                case ParticleSystemCurveMode.TwoConstants:
                    c.constantMin *= k;
                    c.constantMax *= k;
                    break;
                default:
                    c.curveMultiplier *= k;
                    break;
            }

            return c;
        }

        private static bool TryGet(string key, out float value)
        {
            return _values.TryGetValue(key, out value);
        }

        private static float Get(string key, float fallback)
        {
            float v;
            return _values.TryGetValue(key, out v) ? v : fallback;
        }
    }
}
