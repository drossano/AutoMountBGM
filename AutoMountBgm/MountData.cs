namespace PrincessRTFM.AutoMountBGM;

public class MountData(ushort id, string name, ushort bgm) {
	private bool isUnlocked = false;
	private string bgmName = null!;

	public ushort Id { get; init; } = id;
	public ushort BgmId { get; init; } = bgm;
	public string Name { get; init; } = name[..1].ToUpper() + name[1..];

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
}
