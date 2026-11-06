using BepInEx.Configuration;
using HalfGripTools;
using UnityEngine;

namespace HalfGripSlapPack
{
    public sealed class Mod : HalfGripMod
    {
        private GameObject host;

        public override void Load(ModContext ctx)
        {
            ModLog.Initialize(ctx.Config, ctx.Log);
            ChassisCompatibility.Initialize(message => ctx.Log.LogWarning(message));
            ctx.PatchAll();
            host = new GameObject("SlapPack.Mirrors");
            Object.DontDestroyOnLoad(host);
            host.AddComponent<MirrorInstaller>().Initialize(ctx);
            host.AddComponent<WindowDiagnostics>().Initialize(ctx);
            var windows = host.AddComponent<WindowInstaller>();
            windows.Initialize(ctx);
            var windowWeather = host.AddComponent<WindowWeatherInstaller>();
            windowWeather.Initialize(ctx, windows);
            host.AddComponent<TurnSignalInstaller>().Initialize(ctx);
            var wipers = host.AddComponent<WiperInstaller>();
            wipers.Initialize(ctx);
            host.AddComponent<HeldGunWeatherOcclusion>().Initialize(ctx, wipers, windowWeather);
        }

        public override void Unload()
        {
            if (!host) return;
            host.GetComponent<HeldGunWeatherOcclusion>().Stop();
            host.GetComponent<MirrorInstaller>().RemoveMirror();
            host.GetComponent<WindowInstaller>().RemoveWindows();
            host.GetComponent<WindowWeatherInstaller>().RemoveWeather();
            host.GetComponent<TurnSignalInstaller>().RemoveSignals();
            host.GetComponent<WiperInstaller>().RemoveWipers();
            Object.Destroy(host);
            host = null;
        }
    }

    internal sealed class MirrorSettings
    {
        internal readonly ConfigEntry<float> Sensitivity, YawLimit, PitchLimit, Yaw, Pitch, MirrorFOV;
        internal readonly ConfigEntry<bool> IncludeCarInterior;
        private readonly ConfigFile config;

        internal MirrorSettings(ConfigFile config)
        {
            this.config = config;
            MirrorFOV = config.Bind("Mirror", "MirrorFOV", 16f,
                new ConfigDescription("Rear-view camera vertical field of view in degrees. Lower values zoom in; the original is about 28 degrees.",
                    new AcceptableValueRange<float>(10f, 60f)));
            IncludeCarInterior = config.Bind("Mirror", "IncludeCarInterior", true,
                "Include the layers used by the car's 3D cabin models in the rear-view camera. False restores the camera's original layer inclusion.");
            Sensitivity = config.Bind("Mirror", "Sensitivity", 8f,
                new ConfigDescription("Degrees per unit of cockpit hand movement.", new AcceptableValueRange<float>(0.1f, 40f)));
            YawLimit = config.Bind("Mirror", "HorizontalLimit", 30f,
                new ConfigDescription("Maximum horizontal adjustment in degrees.", new AcceptableValueRange<float>(1f, 60f)));
            PitchLimit = config.Bind("Mirror", "VerticalLimit", 20f,
                new ConfigDescription("Maximum vertical adjustment in degrees.", new AcceptableValueRange<float>(1f, 45f)));
            Yaw = config.Bind("Mirror", "SavedYaw", 0f, "Last horizontal adjustment in degrees.");
            Pitch = config.Bind("Mirror", "SavedPitch", 0f, "Last vertical adjustment in degrees.");
        }

        internal void Save(MirrorAdjustment adjustment)
        {
            if (Yaw.Value == adjustment.Yaw && Pitch.Value == adjustment.Pitch) return;
            Yaw.Value = adjustment.Yaw;
            Pitch.Value = adjustment.Pitch;
            config.Save();
        }
    }
}
