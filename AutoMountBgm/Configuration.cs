using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

using Dalamud.Configuration;

namespace PrincessRTFM.AutoMountBGM;

[SuppressMessage("Design", "CA1051:Do not declare visible instance fields", Justification = "directly passed to Imgui methods as ref params")]
public class Configuration: IPluginConfiguration {
	public int Version { get; set; } = 1;

	public HashSet<ushort> BgmDisabledMounts { get; set; } = [];
	public bool DisableBorderlessBgm = true;

	public void Save() => Plugin.Interface.SavePluginConfig(this);
}
