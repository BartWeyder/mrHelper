﻿using System;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using GitLabSharp.Entities;
using mrHelper.App.Helpers;
using mrHelper.Common.Exceptions;
using mrHelper.Common.Tools;
using mrHelper.GitLabClient;
using mrHelper.Integration.GitUI;

namespace mrHelper.App.Forms
{
   partial class AcceptMergeRequestForm
   {
      private void AcceptMergeRequestForm_Load(object sender, EventArgs e)
      {
         invokeFetchAndApply(true);
      }

      private void buttonDiscussions_Click(object sender, EventArgs e)
      {
         BeginInvoke(new Action(async () => await _onOpenDiscussions?.Invoke(_mergeRequestKey, _title, _author)));
      }

      async private void buttonToggleWIP_Click(object sender, EventArgs e)
      {
         traceInformation("Changing WIP by user request");
         try
         {
            buttonToggleWIP.Enabled = false;
            MergeRequest mergeRequest = await toggleWipAsync();
            applyMergeRequest(mergeRequest);
         }
         catch (MergeRequestEditorException ex)
         {
            reportErrorToUser(ex);
         }
      }

      async private void buttonRebase_Click(object sender, EventArgs e)
      {
         traceInformation("Starting Rebase by user request");
         try
         {
            showRebaseInProgress();
            MergeRequestRebaseResponse response = await rebaseAsync();
            applyMergeRequestRebaseResponse(response);
         }
         catch (MergeRequestEditorException ex)
         {
            reportErrorToUser(ex);
         }
      }

      async private void buttonMerge_Click(object sender, EventArgs e)
      {
         string traceMessage = String.Format(
            "Starting Merge by user request. _isSquashNeeded: {0}, _isRemoteBranchDeletionNeeded: {1}",
            _isSquashNeeded.ToString(), _isRemoteBranchDeletionNeeded.ToString());
         traceInformation(traceMessage);

         try
         {
            showMergeInProgress();
            await fixupSquashFlagAsync();
            MergeRequest mergeRequest = await mergeAsync(getSquashCommitMessage(), _isRemoteBranchDeletionNeeded.Value);
            postProcessMerge(mergeRequest);
         }
         catch (MergeRequestEditorException ex)
         {
            ExceptionHandlers.Handle("Failed to merge", ex);
            if (areConflictsFoundAtMerge(ex))
            {
               MessageBox.Show("GitLab was unable to complete the merge. Rebase branch locally and try again",
                  "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
               return;
            }
            reportErrorToUser(ex);
         }
      }

      private async Task fixupSquashFlagAsync()
      {
         // Modify MR manually here because for some reason "squash" query parameter
         // sometimes does not affect the merge. For instance, this occurs when
         // Merge_Error is already set to "Failed to squash", in this case simply
         // set "squash=false" has no effect.
         MergeRequest mergeRequest = await setSquashAsync(_isSquashNeeded.Value);
         Debug.Assert(mergeRequest.Squash == _isSquashNeeded.Value);
      }

      private void linkLabelOpenGitExtensions_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
      {
         traceInformation(String.Format("Launch Git Extensions for {0}", _repositoryPath));
         GitExtensionsIntegrationHelper.Browse(_repositoryPath);
      }

      private void linkLabelOpenSourceTree_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
      {
         traceInformation(String.Format("Launch Source Tree for {0}", _repositoryPath));
         SourceTreeIntegrationHelper.Browse(_repositoryPath);
      }

      private void linkLabelOpenExplorer_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
      {
         traceInformation(String.Format("Launch Explorer for {0}", _repositoryPath));
         ExternalProcess.Start("explorer", StringUtils.EscapeSpaces(_repositoryPath), false, ".");
      }

      private void linkLabelOpenAtGitLab_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
      {
         if (!String.IsNullOrWhiteSpace(_webUrl))
         {
            UrlHelper.OpenBrowser(_webUrl);
         }
      }

      private void checkBoxSquash_CheckedChanged(object sender, EventArgs e)
      {
         Debug.Assert(sender == checkBoxSquash);
         _isSquashNeeded = checkBoxSquash.Checked;
         applySquashCommitMessageVisibility();
      }

      private void applySquashCommitMessageVisibility()
      {
         if (!_isSquashNeeded.Value)
         {
            int newFormHeight = _formDefaultMinimumHeight - _groupBoxCommitMessageDefaultHeight;
            this.MinimumSize = new System.Drawing.Size(this.MinimumSize.Width, newFormHeight);
            this.Size = new System.Drawing.Size(this.Size.Width, newFormHeight);
            groupBoxMergeCommitMessage.Height = 0;
            groupBoxMergeCommitMessage.Visible = false;
         }
         else
         {
            int newFormHeight = _formDefaultMinimumHeight;
            this.MinimumSize = new System.Drawing.Size(this.MinimumSize.Width, newFormHeight);
            this.Size = new System.Drawing.Size(this.Size.Width, newFormHeight);
            groupBoxMergeCommitMessage.Height = _groupBoxCommitMessageDefaultHeight;
            groupBoxMergeCommitMessage.Visible = true;
         }
      }

      private void checkBoxDeleteSourceBranch_CheckedChanged(object sender, EventArgs e)
      {
         Debug.Assert(sender == checkBoxDeleteSourceBranch);
         _isRemoteBranchDeletionNeeded = checkBoxDeleteSourceBranch.Checked;
      }

      private void comboBoxCommit_SelectedIndexChanged(object sender, EventArgs e)
      {
         if (comboBoxCommit.SelectedItem == null)
         {
            return;
         }

         Commit commit = comboBoxCommit.SelectedItem as Commit;
         textBoxCommitMessage.Text = StringUtils.ConvertNewlineUnixToWindows(commit.Message);
      }

      private void comboBoxCommit_Format(object sender, ListControlConvertEventArgs e)
      {
         Commit item = (Commit)(e.ListItem);
         e.Value = item.Title;
      }

      private void buttonClose_Click(object sender, EventArgs e)
      {
         Trace.TraceInformation("[MainForm] User cancelled merge");
         Close();
      }

      private void initializeGitUILinks()
      {
         var storageType = ConfigurationHelper.GetPreferredStorageType(Program.Settings);
         bool isGitRepositoryUsed = storageType == StorageSupport.LocalCommitStorageType.FullGitRepository;
         bool isRepositoryAvailable = isGitRepositoryUsed && !String.IsNullOrEmpty(_repositoryPath);

         linkLabelOpenGitExtensions.Visible = isRepositoryAvailable && GitExtensionsIntegrationHelper.IsInstalled();
         linkLabelOpenSourceTree.Visible = isRepositoryAvailable && SourceTreeIntegrationHelper.IsInstalled();
         linkLabelOpenExplorer.Visible = isRepositoryAvailable;

         traceInformation(String.Format(
            "GitUI link label visibility: Git Extensions: {0}, Source Tree: {1}, Explorer: {2}",
            linkLabelOpenGitExtensions.Visible.ToString(),
            linkLabelOpenSourceTree.Visible.ToString(),
            linkLabelOpenExplorer.Visible.ToString()));
      }

      private void updateControls()
      {
         htmlPanelTitle.Text = convertTextToHtml(_title);
         labelAuthor.Text = _author?.Name ?? String.Empty;
         labelProject.Text = _mergeRequestKey.ProjectKey.ProjectName;
         labelSourceBranch.Text = _sourceBranchName;
         labelTargetBranch.Text = _targetBranchName;

         bool isWIP = _wipStatus == WorkInProgressState.Yes;
         updateWorkInProgressControls(isWIP);

         bool areUnresolvedDiscussions = _discussionState == DiscussionsState.NotResolved;
         updateDiscussionControls(areUnresolvedDiscussions);

         updateRebaseControls();

         bool isRemoteRebaseNeeded = _rebaseState == RemoteRebaseState.Required;
         bool isLocalRebaseNeded = _rebaseState == RemoteRebaseState.Failed;
         bool isRebaseNotAvailable = _rebaseState == RemoteRebaseState.NotAvailable
                                  || _rebaseState == RemoteRebaseState.InProgress;
         bool areConflictsPossible = isRemoteRebaseNeeded || isLocalRebaseNeded || isRebaseNotAvailable;
         bool areDependenciesResolved = !isWIP && !areUnresolvedDiscussions && !areConflictsPossible;
         updateMergeControls(areDependenciesResolved);

         if (comboBoxCommit.Items.Count == 0)
         {
            comboBoxCommit.Items.AddRange(_commits.ToArray());
            if (comboBoxCommit.Items.Count > 0)
            {
               comboBoxCommit.SelectedIndex = 0;
            }
         }

         checkBoxSquash.Checked = _isSquashNeeded.Value;
         checkBoxDeleteSourceBranch.Checked = _isRemoteBranchDeletionNeeded.Value;

         string urlTooltip = String.IsNullOrEmpty(_webUrl) ? String.Empty : _webUrl;
         toolTip.SetToolTip(linkLabelOpenAtGitLab, urlTooltip);
      }

      private void updateWorkInProgressControls(bool isWIP)
      {
         labelWIPStatus.Text = isWIP ? "This is a Work in Progress" : "This is not a Work in Progress";
         labelWIPStatus.ForeColor = isWIP ? Color.Red : Color.Green;
         buttonToggleWIP.Enabled = isWIP;
      }

      private void updateDiscussionControls(bool areUnresolvedDiscussions)
      {
         labelDiscussionStatus.Text = areUnresolvedDiscussions
            ? "Please resolve unresolved threads" : "All discussions resolved";
         labelDiscussionStatus.ForeColor = areUnresolvedDiscussions ? Color.Red : Color.Green;
         buttonDiscussions.Enabled = areUnresolvedDiscussions;
      }

      private void updateMergeControls(bool areDependenciesResolved)
      {
         Debug.Assert(_state == "merged" || _state == "opened"); // see updateMergeRequestInformation()
         if (_state == "merged")
         {
            labelMergeStatus.Text = "Already merged";
            labelMergeStatus.ForeColor = Color.Green;
            buttonMerge.Enabled = false;
            return;
         }

         if (!areDependenciesResolved)
         {
            labelMergeStatus.Text = "Please resolve warnings above to continue with merge";
            labelMergeStatus.ForeColor = Color.Red;
            buttonMerge.Enabled = false;
            return;
         }

         switch (_mergeStatus)
         {
            case MergeStatus.NotAvailable:
               Debug.Assert(false); // why dependencies resolved then?
               labelMergeStatus.Text = "Checking for conflicts...";
               labelMergeStatus.ForeColor = Color.Blue;
               buttonMerge.Enabled = false;
               break;

            case MergeStatus.CanBeMerged:
               labelMergeStatus.Text = "Can be merged. Merge type: Fast-forward merge without a merge commit";
               labelMergeStatus.ForeColor = Color.Green;
               buttonMerge.Enabled = true;
               break;

            case MergeStatus.CannotBeMerged:
               Debug.Assert(false); // why dependencies resolved then?
               labelMergeStatus.Text = "Merge Request cannot be merged due to some GitLab issues";
               labelMergeStatus.ForeColor = Color.Red;
               buttonMerge.Enabled = false;
               break;
         }
      }

      private void updateRebaseControls()
      {
         if (_state == "merged")
         {
            showRebaseUnneeded();
            return;
         }

         switch (_rebaseState)
         {
            case RemoteRebaseState.NotAvailable:
               labelRebaseStatus.Text = "Cannot obtain a state of rebase operation from GitLab";
               labelRebaseStatus.ForeColor = Color.Red;
               buttonRebase.Enabled = false;
               break;

            case RemoteRebaseState.Required:
               labelRebaseStatus.Text = "Fast-forward merge is not possible";
               labelRebaseStatus.ForeColor = Color.Red;
               buttonRebase.Enabled = true;
               break;

            case RemoteRebaseState.InProgress:
               showRebaseInProgress();
               break;

            case RemoteRebaseState.Failed:
               labelRebaseStatus.Text = _rebaseError;
               labelRebaseStatus.ForeColor = Color.Red;
               buttonRebase.Enabled = false;
               break;

            case RemoteRebaseState.SucceededOrNotNeeded:
               showRebaseUnneeded();
               break;
         }
      }

      private void showRebaseInProgress()
      {
         labelRebaseStatus.Text = "Rebase is in progress...";
         labelRebaseStatus.ForeColor = Color.Blue;
         buttonRebase.Enabled = false;
      }

      private void showRebaseUnneeded()
      {
         labelRebaseStatus.Text = "Rebase is unneeded";
         labelRebaseStatus.ForeColor = Color.Green;
         buttonRebase.Enabled = false;
      }

      private void showMergeInProgress()
      {
         labelMergeStatus.Text = "Merge in progress...";
         labelMergeStatus.ForeColor = Color.Blue;
         buttonMerge.Enabled = false;
      }
   }
}

