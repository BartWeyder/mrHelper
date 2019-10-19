using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using GitLabSharp.Entities;
using mrHelper.CustomActions;
using mrHelper.Common.Interfaces;
using mrHelper.Core;
using mrHelper.Client;
using mrHelper.App.Controls;
using mrHelper.Client.Updates;
using mrHelper.Client.Tools;
using mrHelper.Client.Git;
using System.Drawing;
using mrHelper.App.Helpers;
using mrHelper.CommonControls;
using static mrHelper.Client.Services.ServiceManager;

namespace mrHelper.App.Forms
{
   internal partial class MainForm
   {
      User? getCurrentUser()
      {
         Debug.Assert(_currentUser != null);
         return _currentUser.Value;
      }

      string getHostName()
      {
         return comboBoxHost.SelectedItem != null ? ((HostComboBoxItem)comboBoxHost.SelectedItem).Host : String.Empty;
      }

      MergeRequest? getMergeRequest()
      {
         if (listViewMergeRequests.SelectedItems.Count > 0)
         {
            FullMergeRequestKey fmk = (FullMergeRequestKey)listViewMergeRequests.SelectedItems[0].Tag;
            return fmk.MergeRequest;
         }
         Debug.Assert(false);
         return null;
      }

      MergeRequestKey? getMergeRequestKey()
      {
         if (listViewMergeRequests.SelectedItems.Count > 0)
         {
            FullMergeRequestKey fmk = (FullMergeRequestKey)listViewMergeRequests.SelectedItems[0].Tag;
            return new MergeRequestKey(fmk.HostName, fmk.Project.Path_With_Namespace, fmk.MergeRequest.IId);
         }
         Debug.Assert(false);
         return null;
      }

      /// <summary>
      /// Populates host list with list of known hosts from Settings
      /// </summary>
      private void updateHostsDropdownList()
      {
         if (listViewKnownHosts.Items.Count == 0)
         {
            disableComboBox(comboBoxHost, String.Empty);
            return;
         }

         enableComboBox(comboBoxHost);

         for (int idx = comboBoxHost.Items.Count - 1; idx >= 0; --idx)
         {
            HostComboBoxItem item = (HostComboBoxItem)comboBoxHost.Items[idx];
            if (!listViewKnownHosts.Items.Cast<ListViewItem>().Any(x => x.Text == item.Host))
            {
               comboBoxHost.Items.RemoveAt(idx);
            }
         }

         foreach (ListViewItem item in listViewKnownHosts.Items)
         {
            if (!comboBoxHost.Items.Cast<HostComboBoxItem>().Any(x => x.Host == item.Text))
            {
               HostComboBoxItem hostItem = new HostComboBoxItem
               {
                  Host = item.Text,
                  AccessToken = item.SubItems[1].Text
               };
               comboBoxHost.Items.Add(hostItem);
            }
         }
      }

      private enum PreferredSelection
      {
         Initial,
         Latest
      }

      private void selectHost(PreferredSelection preferred)
      {
         if (comboBoxHost.Items.Count == 0)
         {
            return;
         }

         comboBoxHost.SelectedIndex = -1;

         HostComboBoxItem initialSelectedItem = comboBoxHost.Items.Cast<HostComboBoxItem>().ToList().SingleOrDefault(
            x => x.Host == getInitialHostName());
         HostComboBoxItem defaultSelectedItem = (HostComboBoxItem)comboBoxHost.Items[comboBoxHost.Items.Count - 1];
         switch (preferred)
         {
            case PreferredSelection.Initial:
               if (!String.IsNullOrEmpty(initialSelectedItem.Host))
               {
                  comboBoxHost.SelectedItem = initialSelectedItem;
               }
               else
               {
                  comboBoxHost.SelectedItem = defaultSelectedItem;
               }
               break;

            case PreferredSelection.Latest:
               comboBoxHost.SelectedItem = defaultSelectedItem;
               break;
         }
      }

      private bool selectMergeRequest(string projectname, int iid, bool exact)
      {
         foreach (ListViewItem item in listViewMergeRequests.Items)
         {
            FullMergeRequestKey key = (FullMergeRequestKey)(item.Tag);
            if (projectname == String.Empty ||
                (iid == key.MergeRequest.IId && projectname == key.Project.Path_With_Namespace))
            {
               item.Selected = true;
               return true;
            }
         }

         if (exact)
         {
            return false;
         }

         // selected an item from the proper group
         foreach (ListViewGroup group in listViewMergeRequests.Groups)
         {
            if (projectname == group.Name && group.Items.Count > 0)
            {
               group.Items[0].Selected = true;
               return true;
            }
         }

         // select whatever
         foreach (ListViewGroup group in listViewMergeRequests.Groups)
         {
            if (group.Items.Count > 0)
            {
               group.Items[0].Selected = true;
               return true;
            }
         }

         return false;
      }

      private void selectNotReviewedCommits(out int left, out int right)
      {
         Debug.Assert(comboBoxLeftCommit.Items.Count == comboBoxRightCommit.Items.Count);

         left = 0;
         right = 0;

         Debug.Assert(getMergeRequestKey().HasValue);
         MergeRequestKey mrk = getMergeRequestKey().Value;
         if (!_reviewedCommits.ContainsKey(mrk))
         {
            left = 0;
            right = comboBoxRightCommit.Items.Count - 1;
            return;
         }

         int? iNewestOfReviewedCommits = new Nullable<int>();
         HashSet<string> reviewedCommits = _reviewedCommits[mrk];
         for (int iItem = 0; iItem < comboBoxLeftCommit.Items.Count; ++iItem)
         {
            string sha = ((CommitComboBoxItem)(comboBoxLeftCommit.Items[iItem])).SHA;
            if (reviewedCommits.Contains(sha))
            {
               iNewestOfReviewedCommits = iItem;
               break;
            }
         }

         if (!iNewestOfReviewedCommits.HasValue)
         {
            return;
         }

         left = Math.Max(0, iNewestOfReviewedCommits.Value - 1);

         // note that it should not be left + 1 because Left CB is shifted comparing to Right CB
         right = Math.Min(left, comboBoxRightCommit.Items.Count - 1);
      }

      private void checkComboboxCommitsOrder(bool shouldReorderRightCombobox)
      {
         if (comboBoxLeftCommit.SelectedItem == null || comboBoxRightCommit.SelectedItem == null)
         {
            return;
         }

         // Left combobox cannot select a commit older than in right combobox (replicating gitlab web ui behavior)
         CommitComboBoxItem leftItem = (CommitComboBoxItem)(comboBoxLeftCommit.SelectedItem);
         CommitComboBoxItem rightItem = (CommitComboBoxItem)(comboBoxRightCommit.SelectedItem);
         Debug.Assert(leftItem.TimeStamp.HasValue);

         if (rightItem.TimeStamp.HasValue)
         {
            // Check if order is broken
            if (leftItem.TimeStamp.Value <= rightItem.TimeStamp.Value)
            {
               if (shouldReorderRightCombobox)
               {
                  comboBoxRightCommit.SelectedIndex = comboBoxLeftCommit.SelectedIndex;
               }
               else
               {
                  comboBoxLeftCommit.SelectedIndex = comboBoxRightCommit.SelectedIndex;
               }
            }
         }
         else
         {
            // It is ok because a commit w/o timestamp is the oldest one
         }
      }

      private string getGitTag(bool left)
      {
         // swap sides to be consistent with gitlab web ui
         if (!left)
         {
            Debug.Assert(comboBoxLeftCommit.SelectedItem != null);
            return ((CommitComboBoxItem)comboBoxLeftCommit.SelectedItem).SHA;
         }
         else
         {
            Debug.Assert(comboBoxRightCommit.SelectedItem != null);
            return ((CommitComboBoxItem)comboBoxRightCommit.SelectedItem).SHA;
         }
      }

      private void showTooltipBalloon(string title, string text)
      {
         notifyIcon.BalloonTipTitle = title;
         notifyIcon.BalloonTipText = text;
         notifyIcon.ShowBalloonTip(notifyTooltipTimeout);

         Trace.TraceInformation(String.Format("Tooltip: Title \"{0}\" Text \"{1}\"", title, text)); 
      }

      private bool addKnownHost(string host, string accessToken)
      {
         Trace.TraceInformation(String.Format("[MainForm] Adding host {0} with token {1}",
            host, accessToken));

         foreach (ListViewItem listItem in listViewKnownHosts.Items)
         {
            if (listItem.Text == host)
            {
               Trace.TraceInformation(String.Format("[MainForm] Host name already exists"));
               return false;
            }
         }

         var item = new ListViewItem(host);
         item.SubItems.Add(accessToken);
         listViewKnownHosts.Items.Add(item);
         return true;
      }

      private bool removeKnownHost()
      {
         if (listViewKnownHosts.SelectedItems.Count > 0)
         {
            Trace.TraceInformation(String.Format("[MainForm] Removing host name {0}",
               listViewKnownHosts.SelectedItems[0].ToString()));

            listViewKnownHosts.Items.Remove(listViewKnownHosts.SelectedItems[0]);
            return true;
         }
         return false;
      }

      private string getHostWithPrefix(string host)
      {
         string supportedProtocolPrefix = "https://";
         string unsupportedProtocolPrefix = "http://";

         if (host.StartsWith(supportedProtocolPrefix))
         {
            return host;
         }
         else if (host.StartsWith(unsupportedProtocolPrefix))
         {
           return host.Replace(unsupportedProtocolPrefix, supportedProtocolPrefix);
         }

         return supportedProtocolPrefix + host;
      }

      private string getDefaultColorSchemeFileName()
      {
         return String.Format("{0}.{1}", DefaultColorSchemeName, ColorSchemeFileNamePrefix);
      }

      private void fillColorSchemesList()
      {
         string defaultFileName = getDefaultColorSchemeFileName();
         string defaultFilePath = Path.Combine(Directory.GetCurrentDirectory(), defaultFileName);

         comboBoxColorSchemes.Items.Clear();

         string selectedScheme = null;
         string[] files = Directory.GetFiles(Directory.GetCurrentDirectory());
         if (files.Contains(defaultFilePath))
         {
            // put Default scheme first in the list
            comboBoxColorSchemes.Items.Add(defaultFileName);
         }

         foreach (string file in files)
         {
            if (file.EndsWith(ColorSchemeFileNamePrefix))
            {
               string scheme = Path.GetFileName(file);
               if (file != defaultFilePath)
               {
                  comboBoxColorSchemes.Items.Add(scheme);
               }
               if (scheme == _settings.ColorSchemeFileName)
               {
                  selectedScheme = scheme;
               }
            }
         }

         if (selectedScheme != null)
         {
            comboBoxColorSchemes.SelectedItem = selectedScheme;
         }
         else if (comboBoxColorSchemes.Items.Count > 0)
         {
            comboBoxColorSchemes.SelectedIndex = 0;
         }
      }

      private void initializeColorScheme()
      {
         Func<string, bool> createColorScheme =
            (filename) =>
         {
            try
            {
               _colorScheme = new ColorScheme(filename, _expressionResolver);
               return true;
            }
            catch (Exception ex) // whatever de-serialization exception
            {
               ExceptionHandlers.Handle(ex, "Cannot create a color scheme");
            }
            return false;
         };

         if (comboBoxColorSchemes.SelectedIndex < 0 || comboBoxColorSchemes.Items.Count < 1)
         {
            // nothing is selected or list is empty, create an empty scheme
            _colorScheme = new ColorScheme();
         }

         // try to create a scheme for the selected item
         else if (!createColorScheme(comboBoxColorSchemes.Text))
         {
            _colorScheme = new ColorScheme();
            MessageBox.Show("Cannot initialize color scheme", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
         }
      }

      private void disableListView(ListView listView, bool clear)
      {
         listView.Enabled = false;
         foreach (ListViewItem item in listView.Items)
         {
            item.Selected = false;
         }

         if (clear)
         {
            listView.Items.Clear();
         }
      }

      private void enableListView(ListView listView)
      {
         listView.Enabled = true;
      }

      private void disableComboBox(ComboBox comboBox, string text)
      {
         SelectionPreservingComboBox spComboBox = (SelectionPreservingComboBox)comboBox;
         spComboBox.DroppedDown = false;
         spComboBox.SelectedIndex = -1;
         spComboBox.Items.Clear();
         spComboBox.Enabled = false;

         spComboBox.DropDownStyle = ComboBoxStyle.DropDown;
         spComboBox.Text = text;
      }

      private void enableComboBox(ComboBox comboBox)
      {
         SelectionPreservingComboBox spComboBox = (SelectionPreservingComboBox)comboBox;
         spComboBox.Enabled = true;
         spComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
      }

      private void enableControlsOnGitAsyncOperation(bool enabled)
      {
         linkLabelAbortGit.Visible = !enabled;
         buttonDiffTool.Enabled = enabled;
         buttonDiscussions.Enabled = enabled;
         comboBoxHost.Enabled = enabled;
         listViewMergeRequests.Enabled = enabled;
         enableMergeRequestFilterControls(enabled);
         tabPageSettings.Controls.Cast<Control>().ToList().ForEach((x) => x.Enabled = enabled);

         if (enabled)
         {
            updateGitStatusText(String.Empty);
         }
      }

      private void enableMergeRequestFilterControls(bool enabled)
      {
         checkBoxLabels.Enabled = enabled;
         textBoxLabels.Enabled = enabled;
      }

      private void updateMergeRequestDetails(MergeRequest? mergeRequest)
      {
         richTextBoxMergeRequestDescription.Text =
            mergeRequest.HasValue ? mergeRequest.Value.Description : String.Empty;
         richTextBoxMergeRequestDescription.Update();
         linkLabelConnectedTo.Text = mergeRequest.HasValue ? mergeRequest.Value.Web_Url : String.Empty;
      }

      private void updateTimeTrackingMergeRequestDetails(MergeRequest? mergeRequest)
      {
         if (isTrackingTime())
         {
            return;
         }

         labelTimeTrackingMergeRequestName.Visible = mergeRequest.HasValue;
         buttonTimeTrackingStart.Enabled = mergeRequest.HasValue;

         if (mergeRequest.HasValue)
         {
            Debug.Assert(getMergeRequestKey().HasValue);

            labelTimeTrackingMergeRequestName.Text =
               mergeRequest.Value.Title + "   " + "[" + getMergeRequestKey()?.ProjectKey.ProjectName + "]";
         }
      }

      private void updateTotalTime(MergeRequestKey? mrk)
      {
         if (isTrackingTime())
         {
            labelTimeTrackingTrackedLabel.Text = "Tracked Time:";
            buttonEditTime.Enabled = false;
            return;
         }

         if (!mrk.HasValue)
         {
            labelTimeTrackingTrackedLabel.Text = String.Empty;
            labelTimeTrackingTrackedTime.Text = String.Empty;
            buttonEditTime.Enabled = false;
         }
         else
         {
            labelTimeTrackingTrackedLabel.Text = "Total Time:";
            labelTimeTrackingTrackedTime.Text = _timeTrackingManager.GetTotalTime(mrk.Value).ToString(@"hh\:mm\:ss");
            buttonEditTime.Enabled = true;
         }
      }

      private void enableMergeRequestActions(bool enabled)
      {
         linkLabelConnectedTo.Visible = enabled;
         buttonAddComment.Enabled = enabled;
         buttonNewDiscussion.Enabled = enabled;
      }

      private void enableCommitActions(bool enabled)
      {
         buttonDiscussions.Enabled = enabled; // not a commit action but depends on git
         buttonDiffTool.Enabled = enabled;
         enableCustomActions(enabled);
      }

      private void enableCustomActions(bool enabled)
      {
         if (!enabled || !getMergeRequest().HasValue)
         {
            groupBoxActions.Controls.Cast<Control>().ToList().ForEach(x => x.Enabled = false);
            return;
         }

         MergeRequest mergeRequest = getMergeRequest().Value;
         foreach (Control control in groupBoxActions.Controls)
         {
            string dependency = (string)control.Tag;
            string resolved = String.IsNullOrEmpty(dependency) ? String.Empty : _expressionResolver.Resolve(dependency);
            control.Enabled = resolved == String.Empty ||
               mergeRequest.Labels.Any(x => String.Format("{{Label:{0}}}", x) == resolved);
         }
      }

      private void addCommitsToComboBoxes(List<Commit> commits, string baseSha, string targetBranch)
      {
         CommitComboBoxItem latestCommitItem = new CommitComboBoxItem(commits[0])
         {
            IsLatest = true
         };
         comboBoxLeftCommit.Items.Add(latestCommitItem);
         for (int i = 1; i < commits.Count; i++)
         {
            CommitComboBoxItem item = new CommitComboBoxItem(commits[i]);
            if (comboBoxLeftCommit.Items.Cast<CommitComboBoxItem>().Any(x => x.SHA == item.SHA))
            {
               continue;
            }
            comboBoxLeftCommit.Items.Add(item);
            comboBoxRightCommit.Items.Add(item);
         }

         // Add target branch to the right combo-box
         CommitComboBoxItem baseCommitItem = new CommitComboBoxItem(baseSha, targetBranch + " [Base]", null)
         {
            IsBase = true
         };
         comboBoxRightCommit.Items.Add(baseCommitItem);
      }

      private static string formatMergeRequestForDropdown(MergeRequest mergeRequest)
      {
         return String.Format("{0} [{1}] [{2}]",
            mergeRequest.Title, mergeRequest.Author.Username, String.Join(", ", mergeRequest.Labels.ToArray()));
      }

      /// <summary>
      /// Typically called from another thread
      /// </summary>
      private void updateGitStatusText(string text)
      {
         if (labelGitStatus.InvokeRequired)
         {
            Invoke(new Action<string>(updateGitStatusText), new object [] { text });
         }
         else
         {
            labelGitStatus.Visible = true;
            labelGitStatus.Text = text;
         }
      }

      private void notifyOnMergeRequestEvent(string projectName, MergeRequest mergeRequest, string title)
      {
         showTooltipBalloon(title, "\""
            + mergeRequest.Title
            + "\" from "
            + mergeRequest.Author.Name
            + " in project "
            + (projectName == String.Empty ? "N/A" : projectName));
      }

      private void notifyOnMergeRequestUpdates(List<UpdatedMergeRequest> updates)
      {
         string[] selected = getSelectedLabels();
         List<UpdatedMergeRequest> interesting = selected == null ?
            updates : updates.Where(x => !isFilteredMergeRequest(x, selected)).ToList();

         interesting.Where((x) => x.UpdateKind == UpdateKind.New).ToList().ForEach((x) =>
            notifyOnMergeRequestEvent(x.Project.Path_With_Namespace, x.MergeRequest,
               "New merge request"));

         interesting.Where((x) => x.UpdateKind == UpdateKind.CommitsUpdated
                            || x.UpdateKind == UpdateKind.CommitsAndLabelsUpdated).ToList().ForEach((x) =>
            notifyOnMergeRequestEvent(x.Project.Path_With_Namespace, x.MergeRequest,
               "New commits in merge request"));
      }

      private string[] getSelectedLabels()
      {
         if (!_settings.CheckedLabelsFilter)
         {
            return null;
         }

         return _settings.LastUsedLabels.Split(',').Select(x => x.Trim(' ')).ToArray();
      }

      private static MergeRequest GetMergeRequest(MergeRequest x) => x;
      private static MergeRequest GetMergeRequest(UpdatedMergeRequest x) => x.MergeRequest;

      private bool isFilteredMergeRequest<T>(T mergeRequestT, string[] selected)
      {
         if (selected == null)
         {
            return false;
         }

         MergeRequest mergeRequest = GetMergeRequest((dynamic)mergeRequestT);
         foreach (string item in selected)
         {
            if (item.StartsWith(authorLabelPrefix))
            {
               if (mergeRequest.Author.Username.StartsWith(item.Substring(1)))
               {
                  return false;
               }
            }
            else if (item.StartsWith(gitlabLabelPrefix))
            {
               if (mergeRequest.Labels.Any(x => x.StartsWith(item)))
               {
                  return false;
               }
            }
            else if (item.All(x => Char.IsLetter(x)))
            {
               if (mergeRequest.Author.Username.StartsWith(item)
                  || mergeRequest.Labels.Any(x => x.StartsWith(gitlabLabelPrefix + item)))
               {
                  return false;
               }
            }
         }

         return true;
      }

      private GitClientFactory getGitClientFactory(string localFolder)
      {
         if (_gitClientFactory == null || _gitClientFactory.ParentFolder != localFolder)
         {
            _gitClientFactory?.Dispose();

            try
            {
               _gitClientFactory = new GitClientFactory(localFolder, _updateManager.GetProjectWatcher(), this);
            }
            catch (ArgumentException ex)
            {
               ExceptionHandlers.Handle(ex, String.Format("Cannot create GitClientFactory"));

               try
               {
                  Directory.CreateDirectory(localFolder);
               }
               catch (Exception ex2)
               {
                  string message = String.Format("Cannot create folder \"{0}\"", localFolder);
                  ExceptionHandlers.Handle(ex2, message);
                  MessageBox.Show(message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                  return null;
               }
            }
         }
         return _gitClientFactory;
      }

      /// <summary>
      /// Make some checks and create a Client
      /// </summary>
      /// <returns>null if could not create a GitClient</returns>
      private GitClient getGitClient(ProjectKey key, bool showMessageBoxOnError)
      {
         GitClientFactory factory = getGitClientFactory(_settings.LocalGitFolder);
         if (factory == null)
         {
            return null;
         }

         GitClient client;
         try
         {
            client = factory.GetClient(key.HostName, key.ProjectName);
         }
         catch (ArgumentException ex)
         {
            ExceptionHandlers.Handle(ex, String.Format("Cannot create GitClient"));
            if (showMessageBoxOnError)
            {
               MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            return null;
         }

         Debug.Assert(client != null);
         return client;
      }

      private string getInitialHostName()
      {
         // If Last Selected Host is in the list, select it as initial host.
         // Otherwise, select the first host from the list.
         for (int iKnownHost = 0; iKnownHost < _settings.KnownHosts.Count; ++iKnownHost)
         {
            if (_settings.KnownHosts[iKnownHost] == _initialHostName)
            {
               return _initialHostName;
            }
         }
         return _settings.KnownHosts.Count > 0 ? _settings.KnownHosts[0] : String.Empty;
      }

      private bool isTrackingTime()
      {
         return _timeTracker != null;
      }

      private System.Drawing.Color getMergeRequestColor(MergeRequest mergeRequest)
      {
         foreach (KeyValuePair<string, Color> color in _colorScheme)
         {
            // by author
            {
               string colorName = String.Format("MergeRequests_{{Author:{0}}}", mergeRequest.Author.Username);
               if (colorName == color.Key)
               {
                  return color.Value;
               }
            }

            // by labels
            foreach (string label in mergeRequest.Labels)
            {
               string colorName = String.Format("MergeRequests_{{Label:{0}}}", label);
               if (colorName == color.Key)
               {
                  return color.Value;
               }
            }
         }
         return SystemColors.Window;
      }

      private System.Drawing.Color getCommitComboBoxItemColor(CommitComboBoxItem item)
      {
         Debug.Assert(getMergeRequestKey().HasValue);
         MergeRequestKey mrk = getMergeRequestKey().Value;
         bool wasReviewed = _reviewedCommits.ContainsKey(mrk) && _reviewedCommits[mrk].Contains(item.SHA);
         return wasReviewed || item.IsBase ? SystemColors.Window :
            _colorScheme.GetColorOrDefault("Commits_NotReviewed", SystemColors.Window);
      }

      /// <summary>
      /// Clean up records that correspond to merge requests that have been closed
      /// </summary>
      private void cleanupReviewedCommits(string hostname, string projectname, List<MergeRequest> mergeRequests)
      {
         MergeRequestKey[] toRemove = _reviewedCommits.Keys.Where(
            (x) => x.ProjectKey.HostName == hostname
                && x.ProjectKey.ProjectName == projectname
                && !mergeRequests.Any((y) => x.IId == y.IId)).ToArray();
         foreach (MergeRequestKey key in toRemove)
         {
            _reviewedCommits.Remove(key);
         }
      }

      private void updateVisibleMergeRequests()
      {
         foreach (FullMergeRequestKey fmk in _allMergeRequests)
         {
            int index = listViewMergeRequests.Items.Cast<ListViewItem>().ToList().FindIndex(
               x => FullMergeRequestKey.SameMergeRequest((FullMergeRequestKey)x.Tag, fmk));
            if (index == -1)
            {
               addListViewMergeRequestItem(fmk);
            }
            else
            {
               setListViewItemTag(listViewMergeRequests.Items[index], fmk);
            }
         }

         string[] selected = getSelectedLabels();
         for (int index = listViewMergeRequests.Items.Count - 1; index >= 0; --index)
         {
            FullMergeRequestKey fmk = (FullMergeRequestKey)listViewMergeRequests.Items[index].Tag;
            if (!_allMergeRequests.Any(x => FullMergeRequestKey.SameMergeRequest(x, fmk))
               || isFilteredMergeRequest(fmk.MergeRequest, selected))
            {
               listViewMergeRequests.Items.RemoveAt(index);
            }
         }

         recalcRowHeightForMergeRequestListView(listViewMergeRequests);
         listViewMergeRequests.Invalidate();
      }

      private void addListViewMergeRequestItem(FullMergeRequestKey fmk)
      {
         ListViewGroup group = listViewMergeRequests.Groups[fmk.Project.Path_With_Namespace];
         ListViewItem item = listViewMergeRequests.Items.Add(new ListViewItem(new string[]
            {
               String.Empty, // Column IId (stub)
               String.Empty, // Column Author (stub)
               String.Empty, // Column Title (stub)
               String.Empty, // Column Labels (stub)
               String.Empty, // Column Jira (stub)
            }, group));
         setListViewItemTag(item, fmk);
      }

      private void setListViewItemTag(ListViewItem item, FullMergeRequestKey fmk)
      {
         item.Tag = fmk;

         string author = String.Format("{0}\n({1}{2})",
            fmk.MergeRequest.Author.Name, authorLabelPrefix, fmk.MergeRequest.Author.Username);

         string jiraServiceUrl = _serviceManager.GetJiraServiceUrl();
         string jiraTask = getJiraTask(fmk.MergeRequest);
         string jiraTaskUrl = jiraServiceUrl != String.Empty && jiraTask != String.Empty ?
            jiraServiceUrl + "/browse/" + jiraTask : String.Empty;

         item.SubItems[0].Tag = new ListViewSubItemInfo(() => fmk.MergeRequest.IId.ToString(), () => fmk.MergeRequest.Web_Url);
         item.SubItems[1].Tag = new ListViewSubItemInfo(() => author,                          () => String.Empty);
         item.SubItems[2].Tag = new ListViewSubItemInfo(() => fmk.MergeRequest.Title,          () => String.Empty);
         item.SubItems[3].Tag = new ListViewSubItemInfo(() => formatLabels(fmk.MergeRequest),  () => String.Empty);
         item.SubItems[4].Tag = new ListViewSubItemInfo(() => jiraTask,                        () => jiraTaskUrl);
      }

      private void recalcRowHeightForMergeRequestListView(ListView listView)
      {
         if (listView.Items.Count == 0)
         {
            return;
         }

         int maxLineCount = listView.Items.Cast<ListViewItem>().
            Select((x) => formatLabels(((FullMergeRequestKey)(x.Tag)).MergeRequest).Count((y) => y == '\n')).Max() + 1;
         setListViewRowHeight(listView, listView.Font.Height * maxLineCount + 2);
      }

      private static string formatLabels(MergeRequest mergeRequest)
      {
         mergeRequest.Labels.Sort();

         var query = mergeRequest.Labels.GroupBy(
            (label) => label.StartsWith(gitlabLabelPrefix) && label.IndexOf('-') != -1 ?
               label.Substring(0, label.IndexOf('-')) : label,
            (label) => label,
            (baseLabel, labels) => new
            {
               Labels = labels
            });

         StringBuilder stringBuilder = new StringBuilder();
         foreach (var group in query)
         {
            stringBuilder.Append(String.Join(",", group.Labels));
            stringBuilder.Append("\n");
         }

         return stringBuilder.ToString().TrimEnd('\n');
      }

      private static readonly Regex jira_re = new Regex(@"(?'name'(?!([A-Z0-9a-z]{1,10})-?$)[A-Z]{1}[A-Z0-9]+-\d+)");
      private static string getJiraTask(MergeRequest mergeRequest)
      {
         Match m = jira_re.Match(mergeRequest.Title);
         return !m.Success || m.Groups.Count < 1 || !m.Groups["name"].Success ? String.Empty : m.Groups["name"].Value;
      }

      private static void setListViewRowHeight(ListView listView, int height)
      {
         ImageList imgList = new ImageList
         {
            ImageSize = new Size(1, height)
         };
         listView.SmallImageList = imgList;
      }

      private void openBrowser(string text)
      {
         Trace.TraceInformation(String.Format("[Mainform] Opening browser with URL {0}", text));

         try
         {
            Process.Start(text);
         }
         catch (Exception ex) // see Process.Start exception list
         {
            ExceptionHandlers.Handle(ex, "Cannot open URL");
            MessageBox.Show("Cannot open URL", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
         }
      }

      private int GetIndex<T>(ListView.ListViewItemCollection collection, Func<T, bool> match)
      {
         for (int index = collection.Count - 1; index >= 0; --index)
         {
            if (match((dynamic)(collection[index].Tag)))
            {
               return index;
            }
         }
         return -1;
      }

      private int GetIndex<T>(List<FullMergeRequestKey> collection, Func<T, bool> match)
      {
         return collection.FindIndex(x => match((dynamic)x));
      }

      private void findAndProcess<T, U>(T collection, Func<U, bool> match, Action<int> action)
      {
         int index = GetIndex((dynamic)collection, match);
         if (index != -1)
         {
            action(index);
         }
      }

      private struct UpdateHint
      {
         public bool UpdateAllVisibleRows;
         public bool ReloadCurrent;

         public static UpdateHint operator + (UpdateHint a, UpdateHint b)
         {
            return new UpdateHint
            {
               UpdateAllVisibleRows = a.UpdateAllVisibleRows || b.UpdateAllVisibleRows,
               ReloadCurrent = a.ReloadCurrent || b.ReloadCurrent,
            };
         }
      }

      private void processUpdatesAsync(List<UpdatedMergeRequest> updates)
      {
         notifyOnMergeRequestUpdates(updates);

         Func<FullMergeRequestKey, UpdateHint> processNew = (fmk) =>
         {
            _allMergeRequests.Add(fmk);
            return new UpdateHint { UpdateAllVisibleRows = true };
         };

         Func<FullMergeRequestKey, UpdateHint> processClosed = (fmk) =>
         {
            findAndProcess<List<FullMergeRequestKey>, FullMergeRequestKey>(
               _allMergeRequests, x => FullMergeRequestKey.SameMergeRequest(x, fmk),
               (index) => _allMergeRequests.RemoveAt(index));
            return new UpdateHint { UpdateAllVisibleRows = true };
         };

         Func<FullMergeRequestKey, UpdateHint> processLabelsUpdated = (fmk) =>
         {
            findAndProcess<List<FullMergeRequestKey>, FullMergeRequestKey>(
               _allMergeRequests, x => FullMergeRequestKey.SameMergeRequest(x, fmk),
               (index) => _allMergeRequests[index] = fmk);
            return new UpdateHint { UpdateAllVisibleRows = true };
         };

         Func<FullMergeRequestKey, UpdateHint> processCommitsUpdated = (fmk) =>
         {
            bool reloadCurrent = false;
            findAndProcess<ListView.ListViewItemCollection, FullMergeRequestKey>(
               listViewMergeRequests.Items, x => FullMergeRequestKey.SameMergeRequest(x, fmk),
               (index) => reloadCurrent = reloadCurrent || listViewMergeRequests.Items[index].Selected);
            return new UpdateHint { ReloadCurrent = reloadCurrent };
         };

         UpdateHint updateHint = new UpdateHint();
         foreach (UpdatedMergeRequest mergeRequest in updates)
         {
            FullMergeRequestKey fmk = new FullMergeRequestKey(
               mergeRequest.HostName, mergeRequest.Project, mergeRequest.MergeRequest);

            switch (mergeRequest.UpdateKind)
            {
               case UpdateKind.New:                      updateHint += processNew(fmk);            break;
               case UpdateKind.Closed:                   updateHint += processClosed(fmk);         break;
               case UpdateKind.CommitsUpdated:           updateHint += processCommitsUpdated(fmk); break;
               case UpdateKind.LabelsUpdated:            updateHint += processLabelsUpdated(fmk);  break;
               case UpdateKind.CommitsAndLabelsUpdated:  updateHint += processCommitsUpdated(fmk)
                                                                     + processLabelsUpdated(fmk);  break;
            }
         }

         if (updateHint.UpdateAllVisibleRows)
         {
            updateVisibleMergeRequests();
         }

         if (updateHint.ReloadCurrent)
         {
            Trace.TraceInformation("[MainForm] Reloading current Merge Request");

            Debug.Assert(listViewMergeRequests.SelectedItems.Count > 0);
            ListViewItem selected = listViewMergeRequests.SelectedItems[0];
            selected.Selected = false;
            selected.Selected = true;
         }
      }

      private bool checkForApplicationUpdates()
      {
         LatestVersionInformation? info = _serviceManager.GetLatestVersionInfo();
         if (!info.HasValue || info?.VersionNumber == String.Empty || info?.VersionNumber == Application.ProductVersion)
         {
            return false;
         }

         Trace.TraceInformation(String.Format("[CheckForUpdates] New version {0} is found", info?.VersionNumber));

         if (String.IsNullOrEmpty(info?.InstallerFilePath) || !File.Exists(info?.InstallerFilePath))
         {
            Trace.TraceWarning(String.Format("[CheckForUpdates] Installer cannot be found at \"{0}\"",
               info?.InstallerFilePath));
            return false;
         }

         Task.Run(
            () =>
         {
            string filename = Path.GetFileName(info?.InstallerFilePath);
            string tempFolder = Environment.GetEnvironmentVariable("TEMP");
            string destFilePath = Path.Combine(tempFolder, filename);

            try
            {
               if (File.Exists(destFilePath))
               {
                  File.Delete(destFilePath);
               }
               File.Copy(info?.InstallerFilePath, destFilePath);
            }
            catch (Exception ex)
            {
               ExceptionHandlers.Handle(ex, "Cannot download a new version");
               return;
            }

            _newVersionFilePath = destFilePath;
            BeginInvoke(new Action(() => linkLabelNewVersion.Visible = true));
         });

         return true;
      }

      private static readonly string authorLabelPrefix = "#";
      private static readonly string gitlabLabelPrefix = "@";
   }
}

