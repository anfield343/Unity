using System.Collections.Generic;
using System.Linq;
using GitHub.Logging;
using UnityEditor;

namespace GitHub.Unity
{
    class LfsLocksModificationProcessor : UnityEditor.AssetModificationProcessor
    {
        private static ILogging Logger = LogHelper.GetLogger<LfsLocksModificationProcessor>();
        private static IRepository repository;
        private static IPlatform platform;
        private static IEnvironment environment;

        private static Dictionary<NPath, GitLock> locks = new Dictionary<NPath, GitLock>();
        private static CacheUpdateEvent lastLocksChangedEvent;
        private static string loggedInUser;

        public static void Initialize(IEnvironment env, IPlatform plat)
        {
            environment = env;
            platform = plat;
            platform.Keychain.ConnectionsChanged += UserMayHaveChanged;

            repository = environment.Repository;
            if (repository != null)
            {
                repository.LocksChanged += RepositoryOnLocksChanged;
                repository.CheckAndRaiseEventsIfCacheNewer(CacheType.GitLocks, lastLocksChangedEvent);
            }
        }

        public static string[] OnWillSaveAssets(string[] paths)
        {
            return paths;
        }

        public static AssetMoveResult OnWillMoveAsset(string oldPath, string newPath)
        {
            return IsLocked(oldPath) || IsLocked(newPath) ? AssetMoveResult.FailedMove : AssetMoveResult.DidNotMove;
        }

        public static AssetDeleteResult OnWillDeleteAsset(string assetPath, RemoveAssetOptions option)
        {
            return IsLocked(assetPath) ? AssetDeleteResult.FailedDelete : AssetDeleteResult.DidNotDelete;
        }

        public static bool IsOpenForEdit(string assetPath, out string message)
        {
            var lck = GetLock(assetPath);
            message = lck.HasValue ? "File is locked for editing by " + lck.Value.Owner : null;
            return !lck.HasValue;
        }

        private static void RepositoryOnLocksChanged(CacheUpdateEvent cacheUpdateEvent)
        {
            if (!lastLocksChangedEvent.Equals(cacheUpdateEvent))
            {
                lastLocksChangedEvent = cacheUpdateEvent;
                locks = repository.CurrentLocks.ToDictionary(gitLock => gitLock.Path);
            }
        }

        private static void UserMayHaveChanged()
        {
            loggedInUser = platform.Keychain.Connections.Select(x => x.Username).FirstOrDefault();
        }

        private static bool IsLocked(string assetPath)
        {
            return GetLock(assetPath).HasValue;
        }

        private static GitLock? GetLock(string assetPath)
        {
            if (repository == null)
                return null;

            GitLock lck;
            var repositoryPath = environment.GetRepositoryPath(assetPath.ToNPath());
            if (!locks.TryGetValue(repositoryPath, out lck) || lck.Owner.Name.Equals(loggedInUser))
                return null;
            return lck;
        }
    }
}
