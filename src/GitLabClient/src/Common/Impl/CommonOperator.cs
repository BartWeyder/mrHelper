using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using GitLabSharp;
using GitLabSharp.Accessors;
using GitLabSharp.Entities;
using Version = GitLabSharp.Entities.Version;
using System.Diagnostics;

namespace mrHelper.Client.Common
{
   /// <summary>
   /// Implements common interaction with GitLab
   /// </summary>
   internal static class CommonOperator
   {
      async internal static Task<IEnumerable<MergeRequest>> SearchMergeRequestsAsync(
         GitLabClient client, object search, int? maxResults, bool onlyOpen)
      {
         try
         {
            return (IEnumerable<MergeRequest>)(await client.RunAsync(
               async (gitlab) =>
               {
                  if (search is Types.SearchByIId sid)
                  {
                     return new MergeRequest[]
                        { await gitlab.Projects.Get(sid.ProjectName).MergeRequests.Get(sid.IId).LoadTaskAsync() };
                  }

                  BaseMergeRequestAccessor accessor = search is Types.SearchByProject sbp
                     ? (BaseMergeRequestAccessor)gitlab.Projects.Get(sbp.ProjectName).MergeRequests
                     : (BaseMergeRequestAccessor)gitlab.MergeRequests;
                  if (maxResults.HasValue)
                  {
                     PageFilter pageFilter = new PageFilter { PageNumber = 1, PerPage = maxResults.Value };
                     return await accessor.LoadTaskAsync(convertSearchToFilter(search, onlyOpen), pageFilter);
                  }
                  return await accessor.LoadAllTaskAsync(convertSearchToFilter(search, onlyOpen));
               }));
         }
         catch (Exception ex)
         {
            if (ex is GitLabSharpException || ex is GitLabRequestException || ex is GitLabClientCancelled)
            {
               throw new OperatorException(ex);
            }
            throw;
         }
      }

      async internal static Task<Version> GetLatestVersionAsync(GitLabClient client, string projectName, int iid)
      {
         try
         {
            IEnumerable<Version> versions = (IEnumerable<Version>)(await client.RunAsync(async (gitlab) =>
               await gitlab.Projects.Get(projectName).MergeRequests.Get(iid).
                  Versions.LoadTaskAsync(new PageFilter { PerPage = 1, PageNumber = 1 })));
            return versions.Count() > 0 ? versions.First() : new Version();
         }
         catch (Exception ex)
         {
            if (ex is GitLabSharpException || ex is GitLabRequestException || ex is GitLabClientCancelled)
            {
               throw new OperatorException(ex);
            }
            throw;
         }
      }

      async internal static Task<Note> GetMostRecentUpdatedNoteAsync(GitLabClient client, string projectName, int iid)
      {
         try
         {
            IEnumerable<Note> notes = (IEnumerable<Note>)(await client.RunAsync(async (gitlab) =>
               await gitlab.Projects.Get(projectName).MergeRequests.Get(iid).
                  Notes.LoadTaskAsync(new PageFilter { PerPage = 1, PageNumber = 1 },
                                      new SortFilter { Ascending = false, OrderBy = "updated_at" })));
            return notes.Count() > 0 ? notes.First() : new Note();
         }
         catch (Exception ex)
         {
            if (ex is GitLabSharpException || ex is GitLabRequestException || ex is GitLabClientCancelled)
            {
               throw new OperatorException(ex);
            }
            throw;
         }
      }

      private static MergeRequestsFilter convertSearchToFilter(object search, bool onlyOpen)
      {
         MergeRequestsFilter.WorkInProgressFilter wipFilter = onlyOpen
            ? MergeRequestsFilter.WorkInProgressFilter.Yes
            : MergeRequestsFilter.WorkInProgressFilter.All;
         MergeRequestsFilter.StateFilter stateFilter = onlyOpen
            ? MergeRequestsFilter.StateFilter.Open
            : MergeRequestsFilter.StateFilter.All;

         if (search is Types.SearchByIId sbi)
         {
            return new MergeRequestsFilter
               { WIP = wipFilter, State = stateFilter, IIds = new int[] { sbi.IId } };
         }
         else if (search is Types.SearchByProject sbp)
         {
            return new MergeRequestsFilter
               { WIP = wipFilter, State = stateFilter };
         }
         else if (search is Types.SearchByTargetBranch sbt)
         {
            return new MergeRequestsFilter
               { WIP = wipFilter, State = stateFilter, TargetBranch = sbt.TargetBranchName };
         }
         else if (search is Types.SearchByText sbtxt)
         {
            return new MergeRequestsFilter
               { WIP = wipFilter, State = stateFilter, Search = sbtxt.Text };
         }

         Debug.Assert(false);
         return new MergeRequestsFilter{};
      }
   }
}

