﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using GitLabSharp.Entities;
using mrHelper.Client.Common;
using mrHelper.Client.Types;
using mrHelper.Client.Versions;
using mrHelper.Common.Interfaces;
using Version = GitLabSharp.Entities.Version;

namespace mrHelper.Client.MergeRequests
{
   public class MergeRequestCache : IDisposable, ICachedMergeRequestProvider, IProjectCheckerFactory
   {
      public event Action<Common.UserEvents.MergeRequestEvent> MergeRequestEvent;

      public MergeRequestCache(Workflow.Workflow workflow, ISynchronizeInvoke synchronizeInvoke,
         IHostProperties settings, int autoUpdatePeriodMs)
      {
         _updateOperator = new UpdateOperator(settings);

         workflow.PostLoadHostProjects += (hostname, projects) =>
         {
            // TODO Current version supports updates of projects of the most recent loaded host only
            if (String.IsNullOrEmpty(_hostname) || _hostname != hostname)
            {
               _hostname = hostname;
               if (_updateManager != null)
               {
                  _updateManager.OnUpdate -= onUpdate;
                  _updateManager.Dispose();
               }

               _cache = new WorkflowDetailsCache();
               _updateManager = new UpdateManager(synchronizeInvoke, _updateOperator, _hostname, projects, _cache,
                  autoUpdatePeriodMs);
               _updateManager.OnUpdate += onUpdate;

               Trace.TraceInformation(String.Format(
                  "[MergeRequestCache] Set hostname for updates to {0}, will trace updates in {1} projects",
                  hostname, projects.Count()));
            }
         };

         workflow.PostLoadProjectMergeRequests += (hostname, project, mergeRequests) =>
            _cache.UpdateMergeRequests(hostname, project.Path_With_Namespace, mergeRequests);

         workflow.PostLoadLatestVersion += (hostname, projectname, mergeRequest, version) =>
            _cache.UpdateLatestVersion(new MergeRequestKey
            {
               ProjectKey = new ProjectKey { HostName = hostname, ProjectName = projectname },
               IId = mergeRequest.IId
            }, version);
      }

      public void Dispose()
      {
         _updateManager?.Dispose();
         _updateManager = null;
      }

      public IEnumerable<MergeRequest> GetMergeRequests(ProjectKey projectKey)
      {
         return _cache.Details.GetMergeRequests(projectKey);
      }

      public MergeRequest? GetMergeRequest(MergeRequestKey mrk)
      {
         IEnumerable<MergeRequest> mergeRequests = GetMergeRequests(mrk.ProjectKey);
         MergeRequest result = mergeRequests.FirstOrDefault(x => x.IId == mrk.IId);
         return result.Id == default(MergeRequest).Id ? new MergeRequest?() : result;
      }

      public IInstantProjectChecker GetLocalProjectChecker(MergeRequestKey mrk)
      {
         return new LocalProjectChecker(mrk, _cache.Details.Clone());
      }

      public IInstantProjectChecker GetLocalProjectChecker(ProjectKey projectKey)
      {
         return GetLocalProjectChecker(getLatestMergeRequest(projectKey));
      }

      public IInstantProjectChecker GetRemoteProjectChecker(MergeRequestKey mrk)
      {
         return new RemoteProjectChecker(mrk, _updateOperator);
      }

      public Version GetLatestVersion(MergeRequestKey mrk)
      {
         return _cache.Details.GetLatestVersion(mrk);
      }

      public IProjectCheckerFactory GetProjectCheckerFactory()
      {
         return this;
      }

      public IProjectWatcher GetProjectWatcher()
      {
         return _projectWatcher;
      }

      private MergeRequestKey getLatestMergeRequest(ProjectKey projectKey)
      {
         return _cache.Details.GetMergeRequests(projectKey).
            Select(x => new MergeRequestKey
            {
               ProjectKey = projectKey,
               IId = x.IId
            }).OrderByDescending(x => _cache.Details.GetLatestVersion(x).Created_At).FirstOrDefault();
      }

      /// <summary>
      /// Request to update the specified MR after the specified time period (in milliseconds)
      /// </summary>
      public void CheckForUpdates(MergeRequestKey mrk, int firstChanceDelay, int secondChanceDelay)
      {
         _updateManager?.RequestOneShotUpdate(mrk, firstChanceDelay, secondChanceDelay);
      }

      private void onUpdate(IEnumerable<UserEvents.MergeRequestEvent> updates)
      {
         _projectWatcher.ProcessUpdates(updates, _cache.Details);

         foreach (UserEvents.MergeRequestEvent update in updates)
         {
            MergeRequestEvent?.Invoke(update);
         }
      }

      private string _hostname;
      private WorkflowDetailsCache _cache = new WorkflowDetailsCache();
      private readonly ProjectWatcher _projectWatcher = new ProjectWatcher();
      private UpdateManager _updateManager;
      private UpdateOperator _updateOperator;
   }
}
