using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using GitLabSharp.Entities;
using mrHelper.Client.Tools;
using mrHelper.Client.Updates;
using mrHelper.Client.Workflow;
using System.ComponentModel;
using System.Diagnostics;

namespace mrHelper.Client.Updates
{
   // TODO: It is not enough to have unique project id because of multiple hosts

   internal class WorkflowDetails : IWorkflowDetails
   {
      internal WorkflowDetails()
      {
         ProjectNames = new Dictionary<int, string>();
         MergeRequests = new Dictionary<int, List<MergeRequest>>();
         Changes = new Dictionary<int, DateTime>();
      }

      private WorkflowDetails(WorkflowDetails details)
      {
         ProjectNames = new Dictionary<int, string>(details.ProjectNames);
         MergeRequests = new Dictionary<int, List<MergeRequest>>(details.MergeRequests);
         Changes = new Dictionary<int, DateTime>(details.Changes);
      }

      /// <summary>
      /// Create a copy of object
      /// </summary>
      public IWorkflowDetails Clone()
      {
         return new WorkflowDetails(this);
      }

      /// <summary>
      /// Return project name (Path_With_Namespace) by unique project Id
      /// </summary>
      public string GetProjectName(int projectId)
      {
         Debug.Assert(ProjectNames.ContainsKey(projectId));
         return ProjectNames.ContainsKey(projectId) ? ProjectNames[projectId] : String.Empty;
      }

      /// <summary>
      /// Add a project name/id pair to the cache
      /// </summary>
      internal void SetProjectName(int projectId, string name)
      {
         ProjectNames[projectId] = name;
      }

      /// <summary>
      /// Return a list of merge requests by unique project id
      /// </summary>
      public List<MergeRequest> GetMergeRequests(int projectId)
      {
         return MergeRequests.ContainsKey(projectId) ? MergeRequests[projectId] : new List<MergeRequest>();
      }

      /// <summary>
      /// Add a merge request to a list of merge requests for the given project
      /// </summary>
      internal void AddMergeRequest(int projectId, MergeRequest mergeRequest)
      {
         if (MergeRequests.ContainsKey(projectId))
         {
            if (!MergeRequests[projectId].Any((x) => x.Id == mergeRequest.Id))
            {
               MergeRequests[projectId].Add(mergeRequest);
            }
         }
         else
         {
            MergeRequests[projectId] = new List<MergeRequest> { mergeRequest };
         }
      }

      /// <summary>
      /// Return a timestamp of the most recent version of a specified merge request
      /// </summary>
      public DateTime GetLatestChangeTimestamp(int mergeRequestId)
      {
         return Changes.ContainsKey(mergeRequestId) ? Changes[mergeRequestId] : DateTime.MinValue;
      }

      /// <summary>
      /// Update a timestamp of the most recent version of a specified merge request
      /// </summary>
      internal void SetLatestChangeTimestamp(int mergeRequestId, DateTime timestamp)
      {
         Changes[mergeRequestId] = timestamp;
      }

      /// <summary>
      /// Return project Id by merge request Id
      /// </summary>
      public int GetProjectId(int mergeRequestId)
      {
         foreach (KeyValuePair<int, List<MergeRequest>> mergeRequests in MergeRequests)
         {
            if (mergeRequests.Value.Any((x) => x.Id == mergeRequestId))
            {
               return mergeRequests.Key;
            }
         }
         return 0;
      }

      // maps unique project id to project's Path with Namespace property
      private Dictionary<int, string> ProjectNames;

      // maps unique project id to list of merge requests
      private Dictionary<int, List<MergeRequest>> MergeRequests;

      // maps unique Merge Request Id (not IId) to a timestamp of its latest version
      private Dictionary<int, DateTime> Changes;
   }
}

