namespace PrincessRTFM.AutoMountBGM;

internal class MountData {
	private bool isUnlocked = false;
	private string bgmName = null!;

	public readonly ushort Id;
	public readonly ushort BgmId;
	public readonly string Name;

	public string Bgm {
		get {
			this.bgmName ??= Plugin.GetBgmName(this.BgmId);
			return this.bgmName;
		}
	}

	public bool Unlocked {
		get {
			if (!this.isUnlocked)
				this.isUnlocked = Plugin.CheckMountUnlocked(this.Id);
			return this.isUnlocked;
		}
	}

	public MountData(ushort id, string name, ushort bgm) {
		this.Id = id;
		this.Name = name[..1].ToUpper() + name[1..];
		this.BgmId = bgm;
	}
}
