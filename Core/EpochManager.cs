using UnityEngine.SceneManagement;

namespace StrandedDeepDiagnostics.Core
{
    internal sealed class EpochManager
    {
        private bool _hadPlayers;

        public int ProcessEpoch { get; private set; }
        public int WorldEpoch { get; private set; }
        public int SceneEpoch { get; private set; }

        public EpochManager()
        {
            ProcessEpoch = 1;
            WorldEpoch = 0;
            SceneEpoch = 0;
        }

        public bool ObservePlayerPresence(bool hasPlayers)
        {
            bool worldStarted = false;

            if (hasPlayers && !_hadPlayers)
            {
                WorldEpoch++;
                worldStarted = true;
            }

            _hadPlayers = hasPlayers;
            return worldStarted;
        }

        public void NotifySceneLoaded(Scene scene, LoadSceneMode mode)
        {
            SceneEpoch++;
        }

        public void NotifySceneUnloaded(Scene scene)
        {
            SceneEpoch++;
        }

        public void NotifyActiveSceneChanged(Scene previous, Scene next)
        {
            SceneEpoch++;
        }
    }
}
