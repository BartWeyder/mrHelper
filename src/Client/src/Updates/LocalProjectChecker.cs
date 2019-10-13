﻿using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using GitLabSharp.Entities;
using mrHelper.Client.Tools;
using mrHelper.Client.Git;

namespace mrHelper.Client.Updates
{
   /// <summary>
   /// Detects the latest change in a merge request using Local cache only
   /// </summary>
   public class LocalProjectChecker : IInstantProjectChecker
   {
      internal LocalProjectChecker(MergeRequestKey mrk, IWorkflowDetails details)
      {
         MergeRequestKey = mrk;
         Details = details;
      }

      /// <summary>
      /// Get a timestamp of the most recent change of a project the merge request belongs to
      /// Throws nothing
      /// </summary>
      async public Task<DateTime> GetLatestChangeTimestampAsync()
      {
         return await Task.FromResult(Details.GetLatestChangeTimestamp(MergeRequestKey));

         /*
            Commented out: advanced algorithm of detecting the most latest timestamp
            It optimizes things in some cases but in case of big number of MRs it may become inefficient
            and cause often `git fetch` calls. May be it will be optimized and uncommented later.

            int projectId = Details.GetProjectId(MergeRequestId);
            Debug.Assert(projectId != 0);

            DateTime dateTime = DateTime.MinValue;

            List<MergeRequest> mergeRequests = Details.GetMergeRequests(projectId);
            foreach (MergeRequest mergeRequest in mergeRequests)
            {
               DateTime latestChange = Details.GetLatestChangeTimestamp(mergeRequest.Id);
               dateTime = latestChange > dateTime ? latestChange : dateTime;
            }

            return dateTime;
         */
      }

      public override string ToString()
      {
         return String.Format("LocalProjectChecker. MergeRequest IId: {0}", MergeRequestKey.IId);
      }

      private MergeRequestKey MergeRequestKey { get; }
      private IWorkflowDetails Details { get; }
   }
}

