using UnityEngine;

namespace StrandedDeepDiagnostics.Players
{
    internal sealed class PlayerContext
    {
        public int DisplayIndex;
        public UnityEngine.Object PlayerObject;
        public string PlayerTypeName;
        public string PlayerId;
        public Camera Camera;
        public string CameraPath;
        public Rect CameraRect;

        public bool IsValid
        {
            get { return PlayerObject != null && Camera != null; }
        }

        public string DisplayName
        {
            get
            {
                if (!string.IsNullOrEmpty(PlayerId) && PlayerId != "<unknown>")
                {
                    return "P" + DisplayIndex + " (Id=" + PlayerId + ")";
                }

                return "P" + DisplayIndex;
            }
        }
    }
}
