namespace VisualNovel.Runtime
{
    /// <summary>
    /// Plain static state carried from the Main Menu into the gameplay scene.
    /// Survives the scene load since it's just app-domain static data, no GameObject needed.
    /// </summary>
    public static class GameBootstrapState
    {
        public static bool HasPendingLoad;
        public static SaveData PendingSaveData;

        public static bool HasPendingEnding;
        public static EndingType PendingEndingType;
    }
}
