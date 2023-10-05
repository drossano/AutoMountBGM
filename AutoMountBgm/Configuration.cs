namespace PrincessRTFM.AutoMountBGM;

using System.Collections.Generic;
using System.Linq;

using Dalamud.Configuration;

public class Configuration: IPluginConfiguration {
	public int Version { get; set; } = 1;

	public HashSet<ushort> BgmDisabledMounts { get; set; } = new();

	public void Save() => Plugin.Interface.SavePluginConfig(this);
}
