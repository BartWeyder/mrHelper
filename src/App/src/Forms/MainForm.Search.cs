using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Collections.Generic;
using GitLabSharp.Entities;
using mrHelper.Common.Exceptions;
using mrHelper.Common.Interfaces;
using mrHelper.GitLabClient;

namespace mrHelper.App.Forms
{
   internal partial class MainForm
   {
      private bool startSearchWorkflowDefaultExceptionHandler(Exception ex)
      {
         if (ex is DataCacheException || ex is UnknownHostException)
         {
            if (!(ex is DataCacheConnectionCancelledException))
            {
               disableAllSearchUIControls(true);
               ExceptionHandlers.Handle("Cannot perform merge request search", ex);
               string message = ex.Message;
               if (ex is DataCacheException wx)
               {
                  message = wx.UserMessage;
               }
               MessageBox.Show(message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            return true;
         }
         return false;
      }

      private void searchMergeRequests(object query, int? maxResults, Func<Exception, bool> exceptionHandler = null)
      {
         BeginInvoke(new Action(async () => await searchMergeRequestsAsync(query, maxResults, exceptionHandler)), null);
      }

      async private Task searchMergeRequestsAsync(object query, int? maxResults,
         Func<Exception, bool> exceptionHandler = null)
      {
         try
         {
            await startSearchWorkflowAsync(getHostName(), query, maxResults);
         }
         catch (Exception ex)
         {
            if (exceptionHandler == null)
            {
               exceptionHandler = new Func<Exception, bool>((e) => startSearchWorkflowDefaultExceptionHandler(e));
            }
            if (!exceptionHandler(ex))
            {
               throw;
            }
         }
      }

      private void onSearchMergeRequestSelectionChanged(FullMergeRequestKey fmk)
      {
         Debug.Assert(fmk.MergeRequest != null && fmk.MergeRequest.IId != 0);

         Trace.TraceInformation(String.Format("[MainForm.Search] User requested to change merge request to IId {0}",
            fmk.MergeRequest.IId.ToString()));

         onSingleSearchMergeRequestLoaded(fmk);

         IMergeRequestCache cache = _searchDataCache.MergeRequestCache;
         if (cache != null)
         {
            MergeRequestKey mrk = new MergeRequestKey(fmk.ProjectKey, fmk.MergeRequest.IId);
            GitLabSharp.Entities.Version latestVersion = cache.GetLatestVersion(mrk);
            onSearchComparableEntitiesLoaded(latestVersion, fmk.MergeRequest,
               cache.GetCommits(mrk), cache.GetVersions(mrk));
         }
      }

      ///////////////////////////////////////////////////////////////////////////////////////////////////

      /// <summary>
      /// Connects Search DataCache to GitLab
      /// </summary>
      async private Task startSearchWorkflowAsync(string hostname, object query, int? maxResults)
      {
         labelWorkflowStatus.Text = String.Empty;
         disableAllSearchUIControls(true);

         if (String.IsNullOrWhiteSpace(hostname))
         {
            return;
         }

         if (Program.Settings.GetAccessToken(hostname) == String.Empty)
         {
            throw new UnknownHostException(hostname);
         }

         await loadAllSearchMergeRequests(hostname, query, maxResults);
      }

      async private Task loadAllSearchMergeRequests(string hostname, object query, int? maxResults)
      {
         SearchCriteria searchCriteria = new SearchCriteria(new object[] { query });
         onLoadAllSearchMergeRequests(searchCriteria, hostname);

         DataCacheConnectionContext sessionContext = new DataCacheConnectionContext(
            new DataCacheCallbacks(null, null),
            new DataCacheUpdateRules(null, null),
            new SearchBasedContext(searchCriteria, maxResults, false));

         await _searchDataCache.Connect(new GitLabInstance(hostname, Program.Settings), sessionContext);

         foreach (ProjectKey projectKey in _searchDataCache.MergeRequestCache.GetProjects())
         {
            onProjectSearchMergeRequestsLoaded(projectKey,
               _searchDataCache.MergeRequestCache.GetMergeRequests(projectKey));
         }

         onAllSearchMergeRequestsLoaded();
      }

      ///////////////////////////////////////////////////////////////////////////////////////////////////

      private void onLoadAllSearchMergeRequests(SearchCriteria criteria, string hostname)
      {
         listViewFoundMergeRequests.Items.Clear();
         labelWorkflowStatus.Text = String.Format("Searching by criteria: {0} at {1}...",
            criteria.ToString(), hostname);
      }

      private void onProjectSearchMergeRequestsLoaded(ProjectKey projectKey,
         IEnumerable<MergeRequest> mergeRequests)
      {
         createListViewGroupForProject(listViewFoundMergeRequests, projectKey, true);
         fillListViewSearchMergeRequests(projectKey, mergeRequests);
      }

      private void onAllSearchMergeRequestsLoaded()
      {
         if (listViewFoundMergeRequests.Items.Count > 0)
         {
            enableListView(listViewFoundMergeRequests);
            labelWorkflowStatus.Text = String.Empty;
         }
         else
         {
            labelWorkflowStatus.Text = "Nothing found. Try more specific search query.";
         }

         if (isSearchMode())
         {
            selectMergeRequest(listViewFoundMergeRequests, new MergeRequestKey?(), false);
         }
      }

      ///////////////////////////////////////////////////////////////////////////////////////////////////

      private void onSingleSearchMergeRequestLoaded(FullMergeRequestKey fmk)
      {
         if (!isSearchMode())
         {
            // because this callback updates controls shared between Live and Search tabs
            return;
         }

         onSingleMergeRequestLoadedCommon(fmk, _searchDataCache);
      }

      private void onSearchComparableEntitiesLoaded(GitLabSharp.Entities.Version latestVersion,
         MergeRequest mergeRequest, IEnumerable<Commit> commits, IEnumerable<GitLabSharp.Entities.Version> versions)
      {
         if (!isSearchMode())
         {
            // because this callback updates controls shared between Live and Search tabs
            return;
         }

         onComparableEntitiesLoadedCommon(latestVersion, mergeRequest, commits, versions, listViewFoundMergeRequests);
      }

      ///////////////////////////////////////////////////////////////////////////////////////////////////

      private void disableAllSearchUIControls(bool clearListView)
      {
         disableListView(listViewFoundMergeRequests, clearListView);

         if (!isSearchMode())
         {
            // to avoid touching controls shared between Live and Search tabs
            return;
         }

         disableCommonUIControls();
      }

      ///////////////////////////////////////////////////////////////////////////////////////////////////

      private void fillListViewSearchMergeRequests(ProjectKey projectKey, IEnumerable<MergeRequest> mergeRequests)
      {
         listViewFoundMergeRequests.BeginUpdate();

         foreach (MergeRequest mergeRequest in mergeRequests)
         {
            FullMergeRequestKey fmk = new FullMergeRequestKey(projectKey, mergeRequest);
            ListViewItem item = createListViewMergeRequestItem(listViewFoundMergeRequests, fmk);
            listViewFoundMergeRequests.Items.Add(item);
            setListViewSubItemsTags(item, fmk);
         }

         recalcRowHeightForMergeRequestListView(listViewFoundMergeRequests);

         listViewFoundMergeRequests.EndUpdate();
      }
   }
}

