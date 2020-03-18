using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using GitLabSharp.Entities;
using mrHelper.GitClient;
using mrHelper.Common.Exceptions;
using mrHelper.Common.Interfaces;
using mrHelper.Client.Repository;
using mrHelper.Client.Types;
using mrHelper.Client.Versions;
using System.Diagnostics;

namespace mrHelper.App.Helpers
{
   internal class CommitChainCreator
   {
      internal CommitChainCreator(IHostProperties hostProperties, Action<string> onStatusChange,
         Action<string> onGitStatusChange, Action<bool> onCancelEnabled, ILocalGitRepository repo, MergeRequestKey mrk)
      {
         _hostProperties = hostProperties;
         _onStatusChange = onStatusChange;
         _onGitStatusChange = onGitStatusChange;
         _onCancelEnabled = onCancelEnabled;
         _repo = repo;
         _mrk = mrk;
      }

      internal CommitChainCreator(IHostProperties hostProperties, Action<string> onStatusChange,
         Action<string> onGitStatusChange, Action<bool> onCancelEnabled, ILocalGitRepository repo, string headSha)
      {
         _hostProperties = hostProperties;
         _onStatusChange = onStatusChange;
         _onGitStatusChange = onGitStatusChange;
         _onCancelEnabled = onCancelEnabled;
         _repo = repo;
         _headSha = headSha;
      }

      async public Task<bool> CreateChainAsync()
      {
         if (_repo == null)
         {
            return false;
         }

         IsCancelEnabled = true;
         if (_headSha != null)
         {
            if (!_repo.ContainsSHA(_headSha))
            {
               return await createBranches(new string[] { _headSha });
            }
            return true;
         }

         _versionManager = new VersionManager(_hostProperties);
         IEnumerable<string> shas = await getAllVersionHeadSHA(_mrk);
         return await createBranches(shas);
      }

      async public Task CancelAsync()
      {
         if (!IsCancelEnabled)
         {
            return;
         }

         if (_repositoryManager != null)
         {
            await _repositoryManager.CancelAsync();
         }

         if (_versionManager != null)
         {
            await _versionManager.CancelAsync();
         }

         await _repo.Updater.CancelUpdate();
      }

      async private Task<IEnumerable<string>> getAllVersionHeadSHA(MergeRequestKey mrk)
      {
         IEnumerable<GitLabSharp.Entities.Version> versions;
         try
         {
            _onStatusChange("Loading meta information about versions from GitLab");

            versions = await _versionManager.GetVersions(mrk);
            if (versions == null)
            {
               return null; // cancelled by user
            }
         }
         catch (VersionManagerException ex)
         {
            ExceptionHandlers.Handle("Cannot load meta information about versions", ex);
            return null;
         }

         Trace.TraceInformation(String.Format("[CommitChainCreator] Found {0} versions", versions.Count()));

         HashSet<string> heads = new HashSet<string>();
         int iVersion = 1;
         foreach (GitLabSharp.Entities.Version version in versions)
         {
            GitLabSharp.Entities.Version? versionDetailed;
            try
            {
               _onStatusChange(String.Format(
                  "Loading versions from GitLab: {0}/{1}", iVersion++, versions.Count()));

               versionDetailed = await _versionManager.GetVersion(version, mrk);
               if (versionDetailed == null)
               {
                  return null; // cancelled by user
               }
            }
            catch (VersionManagerException ex)
            {
               ExceptionHandlers.Handle("Cannot load meta information about versions", ex);
               return null;
            }

            if (!_repo.ContainsSHA(versionDetailed.Value.Head_Commit_SHA))
            {
               heads.Add(versionDetailed.Value.Head_Commit_SHA);
               Trace.TraceInformation(String.Format(
                  "[CommitChainCreator] SHA {0} is not found in {1}",
                  versionDetailed.Value.Head_Commit_SHA, _repo.Path));
            }
            else
            {
               Trace.TraceInformation(String.Format(
                  "[CommitChainCreator] SHA {0} is found in {1}",
                  versionDetailed.Value.Head_Commit_SHA, _repo.Path));
            }
         }
         return heads;
      }

      async private Task<bool> createBranches(IEnumerable<string> shas)
      {
         if (shas == null)
         {
            return false;
         }

         if (!shas.Any())
         {
            return true;
         }

         Trace.TraceInformation(String.Format(
            "[CommitChainCreator] Will create and delete {0} branches in {1} at {2}",
            shas.Count(), _repo.ProjectKey.ProjectName, _repo.ProjectKey.HostName));

         _repositoryManager = new RepositoryManager(_hostProperties);

         string getFakeSha(string sha) => "fake_" + sha;

         try
         {
            int iBranch = 1;
            foreach (string sha in shas)
            {
               _onStatusChange(String.Format(
                  "Creating branches at GitLab: {0}/{1}", iBranch++, shas.Count()));

               Branch? branch = await _repositoryManager.CreateNewBranchAsync(
                  _repo.ProjectKey, getFakeSha(sha), sha);
               if (branch == null)
               {
                  return false; // cancelled
               }
            }

            _onStatusChange("Fetching new branches from remote repository");
            await _repo.Updater.Update(null, _onGitStatusChange);
            _onGitStatusChange(String.Empty);
         }
         catch (RepositoryManagerException ex)
         {
            ExceptionHandlers.Handle("Cannot create a branch at GitLab", ex);
         }
         finally
         {
            IsCancelEnabled = false;

            int iBranch = 1;
            foreach (string sha in shas)
            {
               try
               {
                  _onStatusChange(String.Format(
                     "Deleting branches at GitLab: {0}/{1}", iBranch++, shas.Count()));

                  await _repositoryManager.DeleteBranchAsync(
                     _repo.ProjectKey, getFakeSha(sha));
               }
               catch (RepositoryManagerException ex)
               {
                  ExceptionHandlers.Handle("Cannot delete a branch at GitLab", ex);
                  continue;
               }
            }
         }

         return true;
      }

      private bool IsCancelEnabled
      {
         get
         {
            return _isCancelEnabled;
         }
         set
         {
            _onCancelEnabled(value);
            _isCancelEnabled = value;
         }
      }

      private readonly IHostProperties _hostProperties;
      private readonly Action<string> _onStatusChange;
      private readonly Action<string> _onGitStatusChange;
      private readonly Action<bool> _onCancelEnabled;
      private readonly ILocalGitRepository _repo;
      private readonly MergeRequestKey _mrk;
      private readonly string _headSha;

      private RepositoryManager _repositoryManager;
      private VersionManager _versionManager;
      private bool _isCancelEnabled;
   }
}

