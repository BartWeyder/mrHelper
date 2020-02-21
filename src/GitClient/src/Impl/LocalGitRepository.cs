﻿using System;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;
using mrHelper.Common.Tools;
using mrHelper.Common.Interfaces;
using mrHelper.Common.Exceptions;

namespace mrHelper.GitClient
{
   /// <summary>
   /// Provides access to git repository.
   /// </summary>
   internal class LocalGitRepository : ILocalGitRepository
   {
      // @{ IGitRepository
      IGitRepositoryData IGitRepository.Data => DoesRequireClone() ? null : _data;

      public ProjectKey ProjectKey { get; }
      // @{ IGitRepository

      // @{ ILocalGitRepository
      public ILocalGitRepositoryData Data => DoesRequireClone() ? null : _data;

      public string Path { get; }

      public ILocalGitRepositoryUpdater Updater => _updater;

      public event Action<ILocalGitRepository> Updated;
      public event Action<ILocalGitRepository> Disposed;

      public bool DoesRequireClone()
      {
         if (!_cached_canClone.HasValue) { canClone(); }
         if (!_cached_isValidRepository.HasValue) { isValidRepository(); }

         Debug.Assert(_cached_canClone.Value || _cached_isValidRepository.Value);
         return !_cached_isValidRepository.Value;
      }
      // @} ILocalGitRepository

      // Host Name and Project Name

      /// <summary>
      /// Construct LocalGitRepository with a path that either does not exist or it is empty
      /// or points to a valid git repository
      /// Throws ArgumentException if requirements on `path` argument are not met
      /// </summary>
      internal LocalGitRepository(ProjectKey projectKey, string path, IProjectWatcher projectWatcher,
         ISynchronizeInvoke synchronizeInvoke)
      {
         Path = path;
         if (!canClone() && !isValidRepository())
         {
            throw new ArgumentException("Path \"" + path + "\" already exists but it is not a valid git repository");
         }

         ProjectKey = projectKey;
         _updater = new LocalGitRepositoryUpdater(projectWatcher,
            async (reportProgress) =>
         {
            bool clone = canClone();
            string arguments = clone
               ? String.Format("clone --progress {0}/{1} {2}",
                  ProjectKey.HostName, ProjectKey.ProjectName, StringUtils.EscapeSpaces(Path))
               : "fetch --progress";

            if (_updateOperationDescriptor == null)
            {
               _updateOperationDescriptor = _operationManager.CreateDescriptor(
                  "git", arguments, clone ? String.Empty : Path, reportProgress);

               try
               {
                  await _operationManager.Wait(_updateOperationDescriptor);
               }
               finally
               {
                  _updateOperationDescriptor = null;
               }

               if (clone)
               {
                  resetCachedState();
               }

               Updated?.Invoke(this);
            }
            else
            {
               await _operationManager.Join(_updateOperationDescriptor, reportProgress);
            }
         },
         projectKeyToCheck => ProjectKey.Equals(projectKeyToCheck),
         synchronizeInvoke,
            async () =>
         {
            try
            {
               await _operationManager.Cancel(_updateOperationDescriptor);
            }
            finally
            {
               _updateOperationDescriptor = null;
            }
         });

         _operationManager = new GitOperationManager(synchronizeInvoke, path);
         _data = new LocalGitRepositoryData(_operationManager, Path);

         Trace.TraceInformation(String.Format(
            "[LocalGitRepository] Created LocalGitRepository at path {0} for host {1} and project {2} "
          + "can clone at this path = {3}, isValidRepository = {4}",
            path, ProjectKey.HostName, ProjectKey.ProjectName, canClone(), isValidRepository()));
      }

      async internal Task DisposeAsync()
      {
         Trace.TraceInformation(String.Format("[LocalGitRepository] Disposing LocalGitRepository at path {0}", Path));
         _updater.Dispose();
         _data.DisableUpdates();
         await _operationManager.CancelAll();
         Disposed?.Invoke(this);
      }

      /// <summary>
      /// Check if Clone can be called for this LocalGitRepository
      /// </summary>
      private bool canClone()
      {
         _cached_canClone = !Directory.Exists(Path) || !Directory.EnumerateFileSystemEntries(Path).Any();
         return _cached_canClone.Value;
      }

      private bool isValidRepository()
      {
         try
         {
            _cached_isValidRepository = Directory.Exists(Path)
                && ExternalProcess.Start("git", "rev-parse --is-inside-work-tree", true, Path).StdErr.Count() == 0;
         }
         catch (Exception ex)
         {
            if (ex is ExternalProcessFailureException || ex is ExternalProcessSystemException)
            {
               _cached_isValidRepository = false;
            }
            else
            {
               throw;
            }
         }

         return _cached_isValidRepository.Value;
      }

      private void resetCachedState()
      {
         _cached_canClone = null;
         _cached_isValidRepository = null;
      }

      private bool? _cached_isValidRepository;
      private bool? _cached_canClone;
      private LocalGitRepositoryData _data;
      private LocalGitRepositoryUpdater _updater;
      private IExternalProcessManager _operationManager;
      private ExternalProcess.AsyncTaskDescriptor _updateOperationDescriptor;
   }
}
