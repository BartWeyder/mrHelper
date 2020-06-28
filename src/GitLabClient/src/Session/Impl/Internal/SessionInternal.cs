﻿using System;
using mrHelper.Client.Discussions;
using mrHelper.Client.MergeRequests;
using mrHelper.Client.Repository;
using mrHelper.Client.TimeTracking;
using mrHelper.Client.Types;

namespace mrHelper.Client.Session
{
   internal class SessionInternal : IDisposable
   {
      internal SessionInternal(
         MergeRequestManager mergeRequestManager,
         DiscussionManager discussionManager,
         TimeTrackingManager timeTrackingManager,
         RepositoryManager repositoryManager)
      {
         _mergeRequestManager = mergeRequestManager;
         _discussionManager = discussionManager;
         _timeTrackingManager = timeTrackingManager;
         _repositoryManager = repositoryManager;
      }

      public void Dispose()
      {
         _mergeRequestManager.Dispose();
         _discussionManager.Dispose();
         _timeTrackingManager.Dispose();
      }

      public IMergeRequestCache MergeRequestCache => _mergeRequestManager;

      public IDiscussionCache DiscussionCache => _discussionManager;

      public ITotalTimeCache TotalTimeCache => _timeTrackingManager;

      public ITimeTracker GetTimeTracker(MergeRequestKey mrk) =>
         _timeTrackingManager?.GetTracker(mrk);

      public IDiscussionEditor GetDiscussionEditor(MergeRequestKey mrk, string discussionId) =>
         _discussionManager?.GetDiscussionEditor(mrk, discussionId);

      public IDiscussionCreator GetDiscussionCreator(MergeRequestKey mrk) =>
         _discussionManager?.GetDiscussionCreator(mrk);

      public IRepositoryAccessor GetRepositoryAccessor() =>
         _repositoryManager?.GetRepositoryAccessor();

      private MergeRequestManager _mergeRequestManager;
      private DiscussionManager _discussionManager;
      private TimeTrackingManager _timeTrackingManager;
      private RepositoryManager _repositoryManager;
   }
}

