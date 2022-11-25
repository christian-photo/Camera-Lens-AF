using LensAF;
using LensAF.Util;

namespace LensAFTest
{
    [TestClass]
    public class UnitTest1
    {
        [TestMethod]
        public void TestMethod1()
        {
            List<FocusPoint> points = new List<FocusPoint>()
            {
                new FocusPoint() { HFR = 8.345345345, Iteration= 1 },
                new FocusPoint() { HFR = 1.72384, Iteration= 2 },
                new FocusPoint() { HFR = 2.2342344, Iteration= 3 },
                new FocusPoint() { HFR = 3.5345345, Iteration= 4 },
                new FocusPoint() { HFR = 5.345345345, Iteration= 5 },
                new FocusPoint() { HFR = 6.345345345, Iteration= 6 },
                new FocusPoint() { HFR = 8.345345345, Iteration= 7 },
            };
            int res = new AutoFocus(new CancellationToken(), null, null).DetermineFinalFocusPoint(points);
            Assert.AreEqual(5, res);
        }
    }
}