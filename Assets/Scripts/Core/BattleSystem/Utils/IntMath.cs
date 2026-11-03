namespace Core.BattleSystem.Utils
{
    /// <summary>Integer math that gives the same answer on every device.</summary>
    public static class IntMath
    {
        /// <summary>The integer square root: the largest r with r * r &lt;= <paramref name="value"/>.</summary>
        public static long Sqrt(long value)
        {
            if (value <= 0)
                return 0;

            var root = (long)System.Math.Sqrt(value);
            while (root * root > value)
                root--;
            while ((root + 1) * (root + 1) <= value)
                root++;
            return root;
        }

        public static int Clamp(int value, int min, int max) => value < min ? min : value > max ? max : value;
    }
}
