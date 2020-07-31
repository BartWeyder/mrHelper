﻿namespace mrHelper.App.Forms.Helpers
{
   internal struct CreateNewMergeRequestState
   {
      public CreateNewMergeRequestState(string defaultProject, string assigneeUsername,
         bool isSquashNeeded, bool isBranchDeletionNeeded)
      {
         DefaultProject = defaultProject;
         AssigneeUsername = assigneeUsername;
         IsSquashNeeded = isSquashNeeded;
         IsBranchDeletionNeeded = isBranchDeletionNeeded;
      }

      internal string DefaultProject { get; }
      internal string AssigneeUsername { get; }
      internal bool IsSquashNeeded { get; }
      internal bool IsBranchDeletionNeeded { get; }
   }
}

