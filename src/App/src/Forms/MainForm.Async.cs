using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using GitLabSharp.Entities;
using mrHelper.App.Helpers;
using mrHelper.CustomActions;
using mrHelper.Common.Interfaces;
using mrHelper.Common.Exceptions;
using mrHelper.Core;
using mrHelper.Core.Interprocess;
using mrHelper.Client.Tools;
using mrHelper.Client.Workflow;
using mrHelper.Client.Discussions;
using mrHelper.Client.Git;
using GitLabSharp.Accessors;

namespace mrHelper.App.Forms
{
   internal partial class MainForm
   {
      async private Task showDiscussionsFormAsync()
      {
         // Store data before async/await
         User currentUser = _workflow.State.CurrentUser;
         MergeRequestKey mrk = _workflow.State.MergeRequestKey;
         MergeRequest mergeRequest = _workflow.State.MergeRequest;

         GitClient client = getGitClient(mrk.ProjectKey);
         if (client != null)
         {
            enableControlsOnGitAsyncOperation(false);
            try
            {
               // Using remote checker because there are might be discussions reported by other users on newer commits
               await _gitClientUpdater.UpdateAsync(client,
                  _updateManager.GetRemoteProjectChecker(mrk), updateGitStatusText);
            }
            catch (Exception ex)
            {
               if (ex is RepeatOperationException)
               {
                  return;
               }
               else if (ex is CancelledByUserException)
               {
                  if (MessageBox.Show("Without up-to-date git repository, some context code snippets might be missing. "
                     + "Do you want to continue?", "Warning", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) ==
                        DialogResult.No)
                  {
                     return;
                  }
                  else
                  {
                     client = null;
                  }
               }
               else
               {
                  Debug.Assert(ex is ArgumentException || ex is GitOperationException);
                  ExceptionHandlers.Handle(ex, "Cannot initialize/update git repository");
                  MessageBox.Show("Cannot initialize git repository",
                     "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                  return;
               }
            }
            finally
            {
               enableControlsOnGitAsyncOperation(true);
            }
         }
         else
         {
            if (MessageBox.Show("Without git repository, context code snippets will be missing. "
               + "Do you want to continue?", "Warning", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) ==
                  DialogResult.No)
            {
               return;
            }
            else
            {
               client = null;
            }
         }

         List<Discussion> discussions = await loadDiscussionsAsync(mrk);
         if (discussions == null)
         {
            return;
         }

         labelWorkflowStatus.Text = "Rendering Discussions Form...";
         labelWorkflowStatus.Update();

         DiscussionsForm form;
         try
         {
            DiscussionsForm discussionsForm = new DiscussionsForm(mrk, mergeRequest.Title, mergeRequest.Author, client,
               int.Parse(comboBoxDCDepth.Text), _colorScheme, discussions, _discussionManager, currentUser,
                  async (key) =>
               {
                  try
                  {
                     GitClient gitClient = getGitClient(key.ProjectKey);
                     if (!gitClient.DoesRequireClone())
                     {
                        // Using remote checker because there are might be discussions reported
                        // by other users on newer commits
                        await gitClient.Updater.ManualUpdateAsync(_updateManager.GetRemoteProjectChecker(key), null);
                     }
                  }
                  catch (GitOperationException ex)
                  {
                     ExceptionHandlers.Handle(ex, "Cannot update git repository on refreshing discussions");
                  }
               });
            form = discussionsForm;
         }
         catch (NoDiscussionsToShow)
         {
            MessageBox.Show("No discussions to show.", "Information",
               MessageBoxButtons.OK, MessageBoxIcon.Information);
            labelWorkflowStatus.Text = "No discussions to show";
            return;
         }
         catch (ArgumentException ex)
         {
            ExceptionHandlers.Handle(ex, "Cannot show Discussions form");
            MessageBox.Show("Cannot show Discussions form", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            labelWorkflowStatus.Text = "Cannot show Discussions";
            return;
         }

         labelWorkflowStatus.Text = "Discussions opened";

         Trace.TraceInformation(String.Format("[MainForm] Opened Discussions for MR IId {0} (at {1})",
            mrk.IId, (client?.Path ?? "null")));

         form.Show();
      }

      async private Task onLaunchDiffToolAsync()
      {
         if (comboBoxLeftCommit.SelectedItem == null || comboBoxRightCommit.SelectedItem == null)
         {
            Debug.Assert(false);
            return;
         }

         // Keep data before async/await
         string leftSHA = getGitTag(true /* left */);
         string rightSHA = getGitTag(false /* right */);

         MergeRequestKey mrk = _workflow.State.MergeRequestKey;

         GitClient client = getGitClient(mrk.ProjectKey);
         if (client != null)
         {
            enableControlsOnGitAsyncOperation(false);
            try
            {
               // Using local checker because it does not make a GitLab request and it is quite enough here because
               // user may select only those commits that already loaded and cached and have timestamps less
               // than latest merge request version
               await _gitClientUpdater.UpdateAsync(client,
                  _updateManager.GetLocalProjectChecker(mrk), updateGitStatusText);
            }
            catch (Exception ex)
            {
               if (ex is CancelledByUserException)
               {
                  // User declined to create a repository
                  MessageBox.Show("Cannot launch a diff tool without up-to-date git repository", "Warning",
                     MessageBoxButtons.OK, MessageBoxIcon.Warning);
               }
               else if (!(ex is RepeatOperationException))
               {
                  Debug.Assert(ex is ArgumentException || ex is GitOperationException);
                  ExceptionHandlers.Handle(ex, "Cannot initialize/update git repository");
                  MessageBox.Show("Cannot initialize git repository",
                     "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
               }
               return;
            }
            finally
            {
               enableControlsOnGitAsyncOperation(true);
            }
         }
         else
         {
            MessageBox.Show("Cannot launch a diff tool without up-to-date git repository", "Warning",
               MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
         }

         labelWorkflowStatus.Text = "Launching diff tool...";

         int pid;
         try
         {
            pid = client.DiffTool(mrHelper.DiffTool.DiffToolIntegration.GitDiffToolName, leftSHA, rightSHA);
         }
         catch (GitOperationException ex)
         {
            string message = "Could not launch diff tool";
            ExceptionHandlers.Handle(ex, message);
            MessageBox.Show(message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            labelWorkflowStatus.Text = message;
            return;
         }

         labelWorkflowStatus.Text = "Diff tool launched";

         Trace.TraceInformation(String.Format("[MainForm] Launched DiffTool for SHA {0} vs SHA {1} (at {2}). PID {3}",
            leftSHA, rightSHA, client.Path, pid.ToString()));

         saveInterprocessSnapshot(pid, leftSHA, rightSHA);

         if (!_reviewedCommits.ContainsKey(mrk))
         {
            _reviewedCommits[mrk] = new HashSet<string>();
         }

         _reviewedCommits[mrk].Add(leftSHA);
         _reviewedCommits[mrk].Add(rightSHA);

         comboBoxLeftCommit.Refresh();
         comboBoxRightCommit.Refresh();
      }

      async private Task onAddCommentAsync()
      {
         // Store data before opening a modal dialog
         string title = _workflow.State.MergeRequest.Title;
         MergeRequestKey mrk = _workflow.State.MergeRequestKey;

         string caption = String.Format("Add comment to merge request \"{0}\"", title);
         using (NewDiscussionItemForm form = new NewDiscussionItemForm(caption))
         {
            if (form.ShowDialog() == DialogResult.OK)
            {
               if (form.Body.Length == 0)
               {
                  MessageBox.Show("Comment body cannot be empty", "Warning",
                     MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                  return;
               }

               DiscussionCreator creator = _discussionManager.GetDiscussionCreator(mrk);

               labelWorkflowStatus.Text = "Adding a comment...";
               await creator.CreateNoteAsync(new CreateNewNoteParameters { Body = form.Body });
               labelWorkflowStatus.Text = "Comment added";
            }
         }
      }

      async private Task onNewDiscussionAsync()
      {
         // Store data before opening a modal dialog
         string title = _workflow.State.MergeRequest.Title;
         MergeRequestKey mrk = _workflow.State.MergeRequestKey;

         string caption = String.Format("Create a new discussion in merge request \"{0}\"", title);
         using (NewDiscussionItemForm form = new NewDiscussionItemForm(caption))
         {
            if (form.ShowDialog() == DialogResult.OK)
            {
               if (form.Body.Length == 0)
               {
                  MessageBox.Show("Discussion body cannot be empty", "Warning",
                     MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                  return;
               }

               DiscussionCreator creator = _discussionManager.GetDiscussionCreator(mrk);

               labelWorkflowStatus.Text = "Creating a discussion...";
               await creator.CreateDiscussionAsync(new NewDiscussionParameters { Body = form.Body });
               labelWorkflowStatus.Text = "Discussion created";
            }
         }
      }

      private void saveInterprocessSnapshot(int pid, string leftSHA, string rightSHA)
      {
         Snapshot snapshot;
         snapshot.AccessToken = GetCurrentAccessToken();
         snapshot.Refs.LeftSHA = leftSHA;     // Base commit SHA in the source branch
         snapshot.Refs.RightSHA = rightSHA;   // SHA referencing HEAD of this merge request
         snapshot.Host = GetCurrentHostName();
         snapshot.MergeRequestIId = GetCurrentMergeRequestIId();
         snapshot.Project = GetCurrentProjectName();
         snapshot.TempFolder = textBoxLocalGitFolder.Text;

         SnapshotSerializer serializer = new SnapshotSerializer();
         serializer.SerializeToDisk(snapshot, pid);
      }

      async private Task<List<Discussion>> loadDiscussionsAsync(MergeRequestKey mrk)
      {
         labelWorkflowStatus.Text = "Loading discussions...";
         List<Discussion> discussions;
         try
         {
            discussions = await _discussionManager.GetDiscussionsAsync(mrk);
         }
         catch (DiscussionManagerException)
         {
            string message = "Cannot load discussions from GitLab";
            MessageBox.Show(message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            labelWorkflowStatus.Text = message;
            return null;
         }
         labelWorkflowStatus.Text = "Discussions loaded";
         return discussions;
      }
   }
}

