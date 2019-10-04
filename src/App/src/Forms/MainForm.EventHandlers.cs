using System;
using System.Drawing;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using GitLabSharp.Entities;
using mrHelper.Client.Tools;
using mrHelper.Client.Persistence;
using mrHelper.Client.TimeTracking;
using mrHelper.Core.Interprocess;
using mrHelper.Client.Discussions;
using mrHelper.Client.Workflow;
using mrHelper.Client.Git;

namespace mrHelper.App.Forms
{
   internal partial class MainForm
   {
      /// <summary>
      /// All exceptions thrown within this method are fatal errors, just pass them to upper level handler
      /// </summary>
      async private void MrHelperForm_Load(object sender, EventArgs e)
      {
         CommonTools.Win32Tools.EnableCopyDataMessageHandling(this.Handle);

         loadSettings();
         addCustomActions();
         integrateInTools();
         await onApplicationStarted();
      }

      async private void MrHelperForm_FormClosing(object sender, FormClosingEventArgs e)
      {
         if (checkBoxMinimizeOnClose.Checked && !_exiting)
         {
            onHideToTray(e);
         }
         else
         {
            if (_workflow != null)
            {
               try
               {
                  _persistentStorage.Serialize();
               }
               catch (PersistenceStateSerializationException ex)
               {
                  ExceptionHandlers.Handle(ex, "Cannot serialize the state");
               }

               Core.Interprocess.SnapshotSerializer.CleanUpSnapshots();

               Hide();
               e.Cancel = true;
               await _workflow.CancelAsync();
               _workflow.Dispose();
               _workflow = null;
               Close();
            }
         }
      }

      private void NotifyIcon_DoubleClick(object sender, EventArgs e)
      {
         ShowInTaskbar = true;
         Show();
      }

      async private void ButtonDifftool_Click(object sender, EventArgs e)
      {
         await onLaunchDiffToolAsync();
      }

      async private void ButtonAddComment_Click(object sender, EventArgs e)
      {
         await onAddCommentAsync();
      }

      async private void ButtonNewDiscussion_Click(object sender, EventArgs e)
      {
         await onNewDiscussionAsync();
      }

      async private void ButtonTimeTrackingStart_Click(object sender, EventArgs e)
      {
         if (isTrackingTime())
         {
            await onStopTimer(true);
         }
         else
         {
            onStartTimer();
         }
      }

      async private void ButtonTimeTrackingCancel_Click(object sender, EventArgs e)
      {
         Debug.Assert(isTrackingTime());
         await onStopTimer(false);
      }

      async private void ButtonTimeEdit_Click(object sender, EventArgs s)
      {
         // Store data before opening a modal dialog
         MergeRequestKey mrk = getMergeRequestKey().Value;

         TimeSpan oldSpan = TimeSpan.Parse(labelTimeTrackingTrackedTime.Text);
         using (EditTimeForm form = new EditTimeForm(oldSpan))
         {
            if (form.ShowDialog() == DialogResult.OK)
            {
               TimeSpan newSpan = form.GetTimeSpan();
               bool add = newSpan > oldSpan;
               TimeSpan diff = add ? newSpan - oldSpan : oldSpan - newSpan;
               if (diff != TimeSpan.Zero)
               {
                  await _timeTrackingManager.AddSpanAsync(add, diff, mrk);

                  updateTotalTime(mrk);
                  labelWorkflowStatus.Text = "Total spent time updated";

                  Trace.TraceInformation(String.Format("[MainForm] Total time for MR {0} (project {1}) changed to {2}",
                     mrk.IId, mrk.ProjectKey.ProjectName, diff.ToString()));
               }
            }
         }
      }

      private void ExitToolStripMenuItem_Click(object sender, EventArgs e)
      {
         _exiting = true;
         this.Close();
      }

      private void ButtonBrowseLocalGitFolder_Click(object sender, EventArgs e)
      {
         localGitFolderBrowser.SelectedPath = textBoxLocalGitFolder.Text;
         if (localGitFolderBrowser.ShowDialog() == DialogResult.OK)
         {
            string newFolder = localGitFolderBrowser.SelectedPath;
            if (getGitClientFactory(newFolder) != null)
            {
               textBoxLocalGitFolder.Text = localGitFolderBrowser.SelectedPath;
               _settings.LocalGitFolder = localGitFolderBrowser.SelectedPath;

               MessageBox.Show("Git folder is changed, but it will not affect already opened Diff Tool and Discussions views",
                  "Information", MessageBoxButtons.OK, MessageBoxIcon.Information);

               labelWorkflowStatus.Text = "Parent folder for git repositories changed";
               Trace.TraceInformation(String.Format("[MainForm] Parent folder changed to {0}",
                  newFolder));
            }
         }
      }

      private void ComboBoxColorSchemes_SelectionChangeCommited(object sender, EventArgs e)
      {
         initializeColorScheme();
         _settings.ColorSchemeFileName = (sender as ComboBox).Text;
      }

      async private void ComboBoxHost_SelectionChangeCommited(object sender, EventArgs e)
      {
         string hostname = (sender as ComboBox).Text;
         await switchHostByUserAsync(hostname);
      }

      private void ListBoxFilteredMergeRequests_MeasureItem(object sender, System.Windows.Forms.MeasureItemEventArgs e)
      {
         if (e.Index < 0)
         {
            return;
         }

         ListBox listBox = sender as ListBox;
         e.ItemHeight = listBox.Font.Height * 2 + 2;
      }

      private void drawComboBoxEdit(DrawItemEventArgs e, ComboBox comboBox, Color backColor, string text)
      {
         if (backColor == SystemColors.Window)
         {
            backColor = Color.FromArgb(225, 225, 225); // Gray shade similar to original one
         }
         using (Brush brush = new SolidBrush(backColor))
         {
            e.Graphics.FillRectangle(brush, e.Bounds);
         }

         e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
         e.Graphics.DrawString(text, comboBox.Font, SystemBrushes.ControlText, e.Bounds);
      }

      Graphics GetGraphics(DrawItemEventArgs e) => e.Graphics;
      Graphics GetGraphics(DrawListViewSubItemEventArgs e) => e.Graphics;

      Rectangle GetBounds(DrawItemEventArgs e) => e.Bounds;
      Rectangle GetBounds(DrawListViewSubItemEventArgs e) => e.Bounds;

      private void fillRectangle<T>(T e, Color backColor, bool isSelected)
      {
         if (isSelected)
         {
            GetGraphics((dynamic)e).FillRectangle(SystemBrushes.Highlight, GetBounds((dynamic)e));
         }
         else
         {
            using (Brush brush = new SolidBrush(backColor))
            {
               GetGraphics((dynamic)e).FillRectangle(brush, GetBounds((dynamic)e));
            }
         }
      }

      private void ListViewMergeRequests_DrawSubItem(object sender, DrawListViewSubItemEventArgs e)
      {
         Tuple<string, MergeRequest> projectAndMergeRequest = (Tuple<string, MergeRequest>)(e.Item.Tag);
         string projectname = projectAndMergeRequest.Item1;
         MergeRequest mergeRequest = projectAndMergeRequest.Item2;

         e.DrawBackground();

         bool isSelected = e.Item.Selected;
         fillRectangle(e, getMergeRequestColor(mergeRequest), isSelected);

         //e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

         Brush textBrush = isSelected ? SystemBrushes.HighlightText : SystemBrushes.ControlText;

         switch (e.ColumnIndex)
         {
            case 0:
               e.Graphics.DrawString(mergeRequest.IId.ToString(), e.Item.ListView.Font, textBrush, new PointF(e.Bounds.X, e.Bounds.Y));
               break;
            case 1:
               e.Graphics.DrawString(mergeRequest.Author.Name, e.Item.ListView.Font, textBrush, new PointF(e.Bounds.X, e.Bounds.Y));
               break;
            case 2:
               e.Graphics.DrawString(mergeRequest.Title, e.Item.ListView.Font, textBrush, new PointF(e.Bounds.X, e.Bounds.Y));
               break;
            case 3:
               string labels = String.Join(", ", mergeRequest.Labels.ToArray());
               e.Graphics.DrawString(labels, e.Item.ListView.Font, textBrush, new PointF(e.Bounds.X, e.Bounds.Y));
               break;
         }

         e.DrawFocusRectangle(e.Bounds);
      }

      private void ListViewMergeRequests_DrawColumnHeader(object sender, DrawListViewColumnHeaderEventArgs e)
      {
         e.DrawDefault = true;
      }

      async private void ListViewMergeRequests_MouseClick(object sender, MouseEventArgs e)
      {
         ListView listView = (sender as ListView);

         if (listView.SelectedItems.Count > 0)
         {
            FullMergeRequestKey key = (FullMergeRequestKey)(listView.SelectedItems[0].Tag);
            await switchMergeRequestByUserAsync(key.HostName, key.Project, key.MergeRequest.IId);
         }
      }

      private void ComboBoxCommits_DrawItem(object sender, System.Windows.Forms.DrawItemEventArgs e)
      {
         if (e.Index < 0)
         {
            return;
         }

         ComboBox comboBox = sender as ComboBox;
         CommitComboBoxItem item = (CommitComboBoxItem)(comboBox.Items[e.Index]);

         e.DrawBackground();

         if ((e.State & DrawItemState.ComboBoxEdit) == DrawItemState.ComboBoxEdit)
         {
            drawComboBoxEdit(e, comboBox, getCommitComboBoxItemColor(item), formatCommitComboboxItem(item));
         }
         else
         {
            bool isSelected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
            fillRectangle(e, getCommitComboBoxItemColor(item), isSelected);

            Brush textBrush = isSelected ? SystemBrushes.HighlightText : SystemBrushes.ControlText;
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            e.Graphics.DrawString(formatCommitComboboxItem(item), comboBox.Font, textBrush, e.Bounds);
         }

         e.DrawFocusRectangle();
      }

      private void ComboBoxLeftCommit_SelectedIndexChanged(object sender, EventArgs e)
      {
         checkComboboxCommitsOrder(true /* I'm left one */);
         setCommitComboboxTooltipText(sender as ComboBox, toolTip);
      }

      private void ComboBoxRightCommit_SelectedIndexChanged(object sender, EventArgs e)
      {
         checkComboboxCommitsOrder(false /* because I'm the right one */);
         setCommitComboboxTooltipText(sender as ComboBox, toolTip);
      }

      private void ComboBoxHost_Format(object sender, ListControlConvertEventArgs e)
      {
         formatHostListItem(e);
      }

      private void LinkLabelConnectedTo_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
      {
         try
         {
            // this should open a browser
            Process.Start(linkLabelConnectedTo.Text);
         }
         catch (Exception ex) // see Process.Start exception list
         {
            ExceptionHandlers.Handle(ex, "Cannot open URL");
            MessageBox.Show("Cannot open URL", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
         }
      }

      async private void ButtonAddKnownHost_Click(object sender, EventArgs e)
      {
         using (AddKnownHostForm form = new AddKnownHostForm())
         {
            if (form.ShowDialog() == DialogResult.OK)
            {
               if (!onAddKnownHost(form.Host, form.AccessToken))
               {
                  MessageBox.Show("Such host is already in the list", "Host will not be added",
                     MessageBoxButtons.OK, MessageBoxIcon.Warning);
                  return;
               }

               _settings.KnownHosts = listViewKnownHosts.Items.Cast<ListViewItem>().Select(i => i.Text).ToList();
               _settings.KnownAccessTokens = listViewKnownHosts.Items.Cast<ListViewItem>()
                  .Select(i => i.SubItems[1].Text).ToList();

               await switchHostByUserAsync(getInitialHostName());
            }
         }
      }

      async private void ButtonRemoveKnownHost_Click(object sender, EventArgs e)
      {
         if (onRemoveKnownHost())
         {
            _settings.KnownHosts = listViewKnownHosts.Items.Cast<ListViewItem>().Select(i => i.Text).ToList();
            _settings.KnownAccessTokens = listViewKnownHosts.Items.Cast<ListViewItem>()
               .Select(i => i.SubItems[1].Text).ToList();

            await switchHostByUserAsync(getInitialHostName());
         }
      }

      private void CheckBoxMinimizeOnClose_CheckedChanged(object sender, EventArgs e)
      {
         _settings.MinimizeOnClose = (sender as CheckBox).Checked;
      }

      async private void TextBoxLabels_KeyDown(object sender, KeyEventArgs e)
      {
         if (e.KeyData == Keys.Enter)
         {
            await onTextBoxLabelsUpdate();
         }
      }

      async private void TextBoxLabels_LostFocus(object sender, EventArgs e)
      {
         await onTextBoxLabelsUpdate();
      }

      async private Task onTextBoxLabelsUpdate()
      {
         _settings.LastUsedLabels = textBoxLabels.Text;

         if (_workflow != null && _settings.CheckedLabelsFilter)
         {
            // emulate host change to reload merge request list
            await switchHostByUserAsync(getHostName());
         }
      }

      async private void CheckBoxLabels_CheckedChanged(object sender, EventArgs e)
      {
         _settings.CheckedLabelsFilter = (sender as CheckBox).Checked;

         if (_workflow != null)
         {
            // emulate host change to reload merge request list
            await switchHostByUserAsync(getHostName());
         }
      }

      private void comboBoxDCDepth_SelectedIndexChanged(object sender, EventArgs e)
      {
         _settings.DiffContextDepth = (sender as ComboBox).Text;
      }

      async private void ButtonDiscussions_Click(object sender, EventArgs e)
      {
         await showDiscussionsFormAsync();
      }

      private void LinkLabelAbortGit_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
      {
         getGitClient(getMergeRequestKey().Value.ProjectKey)?.CancelAsyncOperation();
      }

      protected override void WndProc(ref Message rMessage)
      {
         if (rMessage.Msg == CommonTools.NativeMethods.WM_COPYDATA)
         {
            string argumentString = CommonTools.Win32Tools.ConvertMessageToText(rMessage.LParam);

            BeginInvoke(new Action(
               async () =>
               {
                  string[] arguments = argumentString.Split('|');
                  if (arguments.Length < 2)
                  {
                     Debug.Assert(false);
                     Trace.TraceError(String.Format("Invalid WM_COPYDATA message content: {0}", argumentString));
                     return;
                  }

                  if (arguments[1] == "diff")
                  {
                     await onDiffCommand(argumentString);
                  }
                  else
                  {
                     await onOpenCommand(argumentString);
                  }
               }));
         }

         base.WndProc(ref rMessage);
      }

      private static string formatCommitComboboxItem(CommitComboBoxItem item)
      {
         return item.Text + (item.IsLatest ? " [Latest]" : String.Empty);
      }

      private static void setCommitComboboxTooltipText(ComboBox comboBox, ToolTip tooltip)
      {
         if (comboBox.SelectedItem == null)
         {
            tooltip.SetToolTip(comboBox, String.Empty);
            return;
         }

         CommitComboBoxItem item = (CommitComboBoxItem)(comboBox.SelectedItem);

         string timestampText = String.Empty;
         if (item.TimeStamp != null)
         {
            timestampText = String.Format("({0})", item.TimeStamp.Value.ToLocalTime().ToString());
         }
         string tooltipText = String.Format("{0} {1} {2}",
            item.Text, timestampText, (item.IsLatest ? "[Latest]" : String.Empty));

         tooltip.SetToolTip(comboBox, tooltipText);
      }

      private void formatHostListItem(ListControlConvertEventArgs e)
      {
         HostComboBoxItem item = (HostComboBoxItem)(e.ListItem);
         e.Value = item.Host;
      }

      private static void formatProjectsListItem(ListControlConvertEventArgs e)
      {
         Project item = (Project)(e.ListItem);
         e.Value = item.Path_With_Namespace;
      }

      private void onSettingsPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
      {
         _settings.Update();
      }

      private void onHideToTray(FormClosingEventArgs e)
      {
         e.Cancel = true;
         if (_requireShowingTooltipOnHideToTray)
         {
            // TODO: Maybe it's a good idea to save the requireShowingTooltipOnHideToTray state
            // so it's only shown once in a lifetime
            showTooltipBalloon("Information", "I will now live in your tray");
            _requireShowingTooltipOnHideToTray = false;
         }
         Hide();
         ShowInTaskbar = false;
      }

      private void onTimer(object sender, EventArgs e)
      {
         labelTimeTrackingTrackedTime.Text = _timeTracker.Elapsed.ToString(@"hh\:mm\:ss");
      }

      private bool onAddKnownHost(string host, string accessToken)
      {
         if (!addKnownHost(host, accessToken))
         {
            return false;
         }

         updateHostsDropdownList();
         return true;
      }

      private bool onRemoveKnownHost()
      {
         if (listViewKnownHosts.SelectedItems.Count > 0)
         {
            Trace.TraceInformation(String.Format("[MainForm] Removing host name {0}",
               listViewKnownHosts.SelectedItems[0].ToString()));

            listViewKnownHosts.Items.Remove(listViewKnownHosts.SelectedItems[0]);
            updateHostsDropdownList();
            return true;
         }
         return false;
      }

      private void onStartTimer()
      {
         Debug.Assert(!isTrackingTime());

         // Update button text and enabled state
         buttonTimeTrackingStart.Text = buttonStartTimerTrackingText;
         buttonTimeTrackingStart.BackColor = System.Drawing.Color.LightGreen;
         buttonTimeTrackingCancel.Enabled = true;
         buttonTimeTrackingCancel.BackColor = System.Drawing.Color.Tomato;

         // Start timer
         _timeTrackingTimer.Start();

         // Reset and start stopwatch
         Debug.Assert(getMergeRequestKey().Value.IId != default(MergeRequest).IId);
         _timeTracker = _timeTrackingManager.GetTracker(getMergeRequestKey().Value);
         _timeTracker.Start();

         // Take care of controls that 'time tracking' mode shares with normal mode
         updateTotalTime(null);
         labelTimeTrackingTrackedTime.Text = labelSpentTimeDefaultText;
      }

      async private Task onStopTimer(bool send)
      {
         if (!isTrackingTime())
         {
            return;
         }

         // Reset member right now to not send tracked time again on re-entrance
         TimeTracker timeTracker = _timeTracker;
         _timeTracker = null;

         // Stop stopwatch and send tracked time
         if (send)
         {
            TimeSpan span = timeTracker.Elapsed;
            if (span.TotalSeconds > 1)
            {
               labelWorkflowStatus.Text = "Sending tracked time...";
               string duration = span.ToString("hh") + "h " + span.ToString("mm") + "m " + span.ToString("ss") + "s";
               string status = String.Format("Tracked time {0} sent successfully", duration);
               try
               {
                  await timeTracker.StopAsync();
               }
               catch (TimeTrackerException)
               {
                  status = "Error occurred. Tracked time is not sent!";
                  MessageBox.Show(status, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
               }
               labelWorkflowStatus.Text = status;
            }
            else
            {
               labelWorkflowStatus.Text = "Tracked time less than 1 second is ignored";
            }
         }
         else
         {
            timeTracker.Cancel();
            labelWorkflowStatus.Text = "Time tracking cancelled";
         }

         // Stop timer
         _timeTrackingTimer.Stop();

         // Update button text and enabled state
         buttonTimeTrackingStart.Text = buttonStartTimerDefaultText;
         buttonTimeTrackingStart.BackColor = System.Drawing.Color.Transparent;
         buttonTimeTrackingCancel.Enabled = false;
         buttonTimeTrackingCancel.BackColor = System.Drawing.Color.Transparent;

         // Show actual merge request details
         bool isMergeRequestSelected = getMergeRequest().Value.IId != default(MergeRequest).IId;
         updateTimeTrackingMergeRequestDetails(
            isMergeRequestSelected ? getMergeRequest().Value : new Nullable<MergeRequest>());

         // Take care of controls that 'time tracking' mode shares with normal mode
         updateTotalTime(isMergeRequestSelected ?  getMergeRequestKey().Value : new Nullable<MergeRequestKey>());
      }

      private void onPersistentStorageSerialize(IPersistentStateSetter writer)
      {
         writer.Set("SelectedHost", getHostName());

         Dictionary<string, HashSet<string>> reviewedCommits = _reviewedCommits.ToDictionary(
               item => item.Key.ProjectKey.HostName
               + "|" + item.Key.ProjectKey.ProjectName
               + "|" + item.Key.IId.ToString(),
               item => item.Value);
         writer.Set("ReviewedCommits", reviewedCommits);
      }

      private void onPersistentStorageDeserialize(IPersistentStateGetter reader)
      {
         string hostname = (string)reader.Get("SelectedHost");
         if (hostname != null)
         {
            _initialHostName = hostname;
         }

         Dictionary<string, object> reviewedCommits = (Dictionary<string, object>)reader.Get("ReviewedCommits");
         if (reviewedCommits != null)
         {
            _reviewedCommits = reviewedCommits.ToDictionary(
               item =>
               {
                  string[] splitted = item.Key.Split('|');

                  Debug.Assert(splitted.Length == 3);

                  string host = splitted[0];
                  string projectName = splitted[1];
                  int iid = int.Parse(splitted[2]);
                  return new MergeRequestKey
                  {
                     ProjectKey = new ProjectKey { HostName = host, ProjectName = projectName },
                     IId = iid
                  };
               },
               item =>
               {
                  HashSet<string> commits = new HashSet<string>();
                  foreach (string commit in (ArrayList)item.Value)
                  {
                     commits.Add(commit);
                  }
                  return commits;
               });
         }
      }
   }
}

