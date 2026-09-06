using UnityEngine;

namespace VisualNovel.UI
{
    /// <summary>
    /// Sarabun ships as an embedded asset (Assets/Resources/Fonts), unlike an OS dynamic
    /// font, so it survives serialization/reload and works the same in builds.
    /// </summary>
    public static class ThaiFont
    {
        private static Font cachedRegular;
        private static Font cachedBold;

        public static Font Get()
        {
            if (cachedRegular == null)
            {
                cachedRegular = Resources.Load<Font>("Fonts/Sarabun-Regular");
            }
            return cachedRegular;
        }

        public static Font GetBold()
        {
            if (cachedBold == null)
            {
                cachedBold = Resources.Load<Font>("Fonts/Sarabun-Bold");
            }
            return cachedBold;
        }
    }
}
