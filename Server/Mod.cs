using System.Reflection;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Spt.Mod;
using Range = SemanticVersioning.Range;

namespace M79;

public record ModMetadata : IModMetadata
{
    public string ModGuid { get; init; } = "com.viniHNS.M79";
    public string Name { get; init; } = "vini's M79";
    public string Author { get; init; } = "viniHNS";
    public List<string>? Contributors { get; init; }
    public SemanticVersioning.Version Version { get; init; } = new("1.0.0");
    public Range SptVersion { get; init; } = new("~4.1.6");
    public bool HasPrepatcher { get; init; } = false;
    public List<string>? Incompatibilities { get; init; }

    public Dictionary<string, Range>? ModDependencies { get; init; } = new()
    {
        { "com.wtt.commonlib", new Range("~3.0.6") }
    };

    public string? Url { get; init; } = "";
    public string License { get; init; } = "MIT";
}

// wtt core loads at Preload (the PostSptLoad part at PostLoad), so we go right after it
// the items have to exist before the profiles load (SaveCallbacks) or the ones already bought break the profile
[Injectable(TypePriority = OnLoadOrder.Preload + 2)]
public class M79Mod(
    WTTServerCommonLib.WTTServerCommonLib wttCommon,
    ISptLogger<M79Mod> logger
) : IOnLoad
{
    public async Task OnLoadAsync(CancellationToken cancellationToken)
    {
        var assembly = Assembly.GetExecutingAssembly();

        await wttCommon.CustomItemServiceExtended.CreateCustomItems(assembly);
        await wttCommon.CustomWeaponPresetService.CreateCustomWeaponPresets(assembly, "db/weaponPresets/GlobalPresets");
        await wttCommon.CustomLocaleService.CreateCustomLocales(assembly);
        await wttCommon.CustomAssortSchemeService.CreateCustomAssortSchemes(assembly, "db/weaponPresets/Assorts");

        logger.Info("M79 loaded");
    }
}
