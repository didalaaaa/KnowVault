namespace KnowVault.Ingest
{
	/// <summary>
	/// 一次切片使用的参数（由调用方按知识库设置决定，缺省取全局配置）
	/// </summary>
	public class ChunkOptions
	{
		/// <summary>单片目标长度（字符）</summary>
		public int SizeChars { get; set; } = 1000;

		/// <summary>相邻切片重叠（字符）</summary>
		public int OverlapChars { get; set; } = 120;
	}
}
