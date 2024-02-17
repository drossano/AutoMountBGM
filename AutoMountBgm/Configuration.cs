using System.Collections.Generic;

using Dalamud.Configuration;

namespace PrincessRTFM.AutoMountBGM;

public class Configuration: IPluginConfiguration {
	public int Version { get; set; } = 1;

	public HashSet<ushort> BgmDisabledMounts { get; set; } = new();

	public void Save() => Plugin.Interface.SavePluginConfig(this);
}
