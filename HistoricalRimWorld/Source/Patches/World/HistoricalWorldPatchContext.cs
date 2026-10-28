namespace HistoricalRimWorld.Patches.World
{
    internal static class HistoricalWorldPatchContext
    {
        private static readonly System.Collections.Generic.HashSet<int> notifiedBlockedCaravans =
            new System.Collections.Generic.HashSet<int>();

        [System.ThreadStatic]
        private static int playerCaravanPathDepth;

        public static bool IsPlayerCaravanPath => playerCaravanPathDepth > 0;

        public static void EnterPlayerCaravanPath()
        {
            playerCaravanPathDepth++;
        }

        public static void ExitPlayerCaravanPath()
        {
            if (playerCaravanPathDepth > 0) playerCaravanPathDepth--;
        }

        public static bool MarkCaravanBlockNotified(int caravanId) => notifiedBlockedCaravans.Add(caravanId);

        public static void ClearCaravanBlockNotification(int caravanId) => notifiedBlockedCaravans.Remove(caravanId);

        public static void ResetNotifications() => notifiedBlockedCaravans.Clear();
    }
}
