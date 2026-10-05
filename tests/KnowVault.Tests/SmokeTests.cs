using Xunit;

namespace KnowVault.Tests
{
	/// <summary>确认测试基建可跑通的占位用例。</summary>
	public class SmokeTests
	{
		/// <summary>最简单的断言，用于验证测试工程被正确发现与执行。</summary>
		[Fact]
		public void Test_infrastructure_works() => Assert.True(true);
	}
}
