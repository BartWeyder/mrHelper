﻿using System;
using System.Collections.Generic;
using GitLabSharp.Entities;

namespace mrHelper.Client.Workflow
{
   public interface IWorkflowEventNotifier
   {
      event Action<string, User, IEnumerable<Project>> Connected;
      event Action<string, Project, IEnumerable<MergeRequest>> LoadedMergeRequests;
      event Action<string, string, MergeRequest, GitLabSharp.Entities.Version> LoadedMergeRequestVersion;
      event Action<string, IEnumerable<Project>> LoadedProjects;
   }
}

