using System;
using System.Numerics;
using Dalamud.Configuration;
using Dalamud.Plugin;
using AsMobPlate.Localization;

namespace AsMobPlate;

[Serializable]
public sealed class Configuration : IPluginConfiguration
{
    [NonSerialized]
    private IDalamudPluginInterface? pluginInterface;

    public int Version { get; set; } = 9;
    public UiLanguage Language = UiLanguage.JP;

    public bool ShowARank = true;
    public bool ShowSRank = true;
    public bool ShowSsMinions = true;
    public bool ShowSsBoss = true;
    public bool ShowHuntProgress = true;
    public bool ShowDefeated = true;
    public float DefeatedDisplaySeconds = 30;
    public Vector2 HuntProgressPosition = new(24, 180);
    public bool ShowHpBar = true;
    public bool ShowHpPercent = true;
    public bool ShowDistance = true;
    public bool ShowTimeToKill = true;
    public bool ShowAnnouncedStartTime = true;
    public bool ShowEtAtAnnouncement = true;
    public bool ShowInProgressLabel = true;
    public bool ShowArrivalWarning = true;
    public float ArrivalDistance = 25;
    public float ArrivalPreparationSeconds = 3;
    public Vector4 ArrivalWarningRed = new(1, 0, 0, 0.5f);
    public Vector4 ArrivalWarningYellow = new(1, 1, 0, 0.5f);
    public bool ShowCountdownFrame = true;
    public bool EnableCountdownOnAnnouncedStart = true;
    public bool ShowObjectIndex;
    public bool EnableNotificationSound = true;
    public bool EnableTts = true;
    public bool EnableChatNotification = false;
    public bool EnableMapFlagOnDetection = true;
    public int SoundEffectId = 15;
    public int SoundRepeatCount = 4;

    public float MaxDistance = 2000.0f;
    public float ARankNotificationDistance = 200.0f;
    public float SRankNotificationDistance = 1000.0f;
    public float Scale = 1.0f;
    public float YOffset = 0.0f;
    public float NameFontSize = 16.0f;
    public float HpBarWidth = 180.0f;
    public float HpBarHeight = 18.0f;
    public float CountdownFrameThickness = 2.0f;
    public float TtkSampleWindowSeconds = 45.0f;
    // Keep the serialized key; v9 uses this as a symmetric window around the game ET.
    public float EtStartPastToleranceMinutes = 120.0f;
    public float StartTimeDisplayAfterSeconds = 300.0f;
    public int CountdownSeconds = 10;
    public float NotificationCooldownSeconds = 30.0f;
    public float SoundRepeatIntervalSeconds = 0.25f;
    public int TtsVolume = 85;
    public int TtsRate = 0;
    public string TtsFormat = "{rank} rank hunt: {name}";

    public Vector4 ARankTextColor = new(1.0f, 0.84f, 0.22f, 1.0f);
    public Vector4 SRankTextColor = new(1.0f, 0.35f, 0.35f, 1.0f);
    public Vector4 HpBarColor = new(0.20f, 0.84f, 0.36f, 1.0f);
    public Vector4 BackgroundColor = new(0.02f, 0.02f, 0.02f, 0.72f);
    public Vector4 InProgressBackgroundColor = new(0.22f, 0.03f, 0.03f, 0.82f);
    public Vector4 CountdownFrameColor = new(1.0f, 0.86f, 0.22f, 1.0f);

    public void Initialize(IDalamudPluginInterface pluginInterface)
    {
        this.pluginInterface = pluginInterface;

        if (this.Version < 2)
        {
            if (Math.Abs(this.MaxDistance - 200.0f) < 0.01f)
                this.MaxDistance = 2000.0f;
        }

        if (this.Version < 9)
        {
            this.Version = 9;
            this.Save();
        }
    }

    public void Save()
    {
        this.pluginInterface?.SavePluginConfig(this);
    }
}
