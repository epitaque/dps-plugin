using Xunit;
using DpsPlugin;

namespace DpsPluginTest {
    public class UnitTest1
    {
        [Fact]
        public void Test1()
        {
            DpsPlugin.DpsPlugin.FindPotency("s", 1);
        }
    }
}
