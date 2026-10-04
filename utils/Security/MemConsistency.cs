namespace Keel.Utils.Security
{
    public static class MemConsistency
    {
        private static readonly int[] s_referenceArray;

        static MemConsistency()
        {
            s_referenceArray = new int[1024 * 16];
            for (var i = 0; i < s_referenceArray.Length; i++)
                s_referenceArray[i] = i;
        }

        public static bool IsValid()
        {
            for (var i = 0; i < s_referenceArray.Length; i++)
            {
                if (i != s_referenceArray[i])
                    return false;
            }

            return true;
        }
    }
}