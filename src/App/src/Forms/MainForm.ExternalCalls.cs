using System;
using System.Linq;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows.Forms;
using mrHelper.Core.Interprocess;
using mrHelper.Client.Tools;
using mrHelper.Client.Workflow;
using mrHelper.Client.Discussions;
using mrHelper.Client.Git;

namespace mrHelper.App.Forms
{
   internal partial class MainForm
   {
      async private Task onDiffCommand(string argumentsString)
      {
         string[] argumentsEx = argumentsString.Split('|');
         int gitPID = int.Parse(argumentsEx[argumentsEx.Length - 1]);

         string[] arguments = new string[argumentsEx.Length - 1];
         Array.Copy(argumentsEx, 0, arguments, 0, argumentsEx.Length - 1);

         SnapshotSerializer serializer = new SnapshotSerializer();
         Snapshot snapshot;
         try
         {
            snapshot = serializer.DeserializeFromDisk(gitPID);
         }
         catch (System.IO.IOException ex)
         {
            ExceptionHandlers.Handle(ex, "Cannot de-serialize snapshot");
            MessageBox.Show(
               "Make sure that diff tool was launched from Merge Request Helper which is still running",
               "Cannot create a discussion",
               MessageBoxButtons.OK, MessageBoxIcon.Error,
               MessageBoxDefaultButton.Button1, MessageBoxOptions.ServiceNotification);
            return;
         }

         DiffArgumentParser diffArgumentParser = new DiffArgumentParser(arguments);
         DiffCallHandler handler;
         try
         {
            handler = new DiffCallHandler(diffArgumentParser.Parse(), snapshot);
         }
         catch (ArgumentException ex)
         {
            ExceptionHandlers.Handle(ex, "Cannot parse diff tool arguments");
            MessageBox.Show("Bad arguments passed from diff tool", "Cannot create a discussion",
               MessageBoxButtons.OK, MessageBoxIcon.Error,
               MessageBoxDefaultButton.Button1, MessageBoxOptions.ServiceNotification);
            return;
         }

         Common.Interfaces.IGitRepository gitRepository = null;
         if (_gitClientFactory.ParentFolder == snapshot.TempFolder)
         {
            GitClient client = _gitClientFactory.GetClient(snapshot.Host, snapshot.Project);
            gitRepository = client;
         }

         try
         {
            await handler.HandleAsync(gitRepository);
         }
         catch (DiscussionCreatorException ex)
         {
            ExceptionHandlers.Handle(ex, "Cannot de-serialize snapshot");
            MessageBox.Show(
               "Something went wrong at GitLab. See Merge Request Helper log files for details",
               "Cannot create a discussion",
               MessageBoxButtons.OK, MessageBoxIcon.Error,
               MessageBoxDefaultButton.Button1, MessageBoxOptions.ServiceNotification);
         }
      }

      async private Task onOpenCommand(string argumentsString)
      {
         string[] arguments = argumentsString.Split('|');
         string url = arguments[1];

         await connectToUrlAsync(url);
      }

      private void reportErrorOnConnect(string url, string msg, Exception ex, bool error)
      {
         MessageBox.Show(String.Format("{0}Cannot open merge request from URL. Reason: {1}", msg, ex.Message),
            error ? "Error" : "Warning", MessageBoxButtons.OK,
            error ? MessageBoxIcon.Error : MessageBoxIcon.Exclamation,
            MessageBoxDefaultButton.Button1, MessageBoxOptions.ServiceNotification);
         ExceptionHandlers.Handle(ex, String.Format("Cannot open URL {0}", url));
      }

      async private Task<bool> startWorkflowAsync(GitLabSharp.ParsedMergeRequestUrl mergeRequestUrl, bool reloadAll)
      {
         return await startWorkflowAsync(mergeRequestUrl.Host, mergeRequestUrl.Project, mergeRequestUrl.IId,
            reloadAll, true);
      }

      async private void unhideFilteredMergeRequestAsync(GitLabSharp.ParsedMergeRequestUrl mergeRequestUrl, string url)
      {
         if (MessageBox.Show("Merge Request is hidden by filters, do you want to reset them?", "Warning",
               MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
         {
            return;
         }

         _lastMergeRequestsByHosts[mergeRequestUrl.Host] =
            new MergeRequestKey(mergeRequestUrl.Host, mergeRequestUrl.Project, mergeRequestUrl.IId);

         checkBoxLabels.Checked = false;

         if (!await startWorkflowAsync(mergeRequestUrl, false))
         {
            Trace.TraceError(String.Format("[MainForm] Cannot open URL {0}, although MR is cached", url));
         }
      }

      async private Task<bool> connectToUrlAsync(string url)
      {
         Trace.TraceInformation(String.Format("[MainForm] Connecting to URL {0}", url));

         string prefix = mrHelper.Common.Constants.Constants.CustomProtocolName + "://";
         url = url.StartsWith(prefix) ? url.Substring(prefix.Length) : url;

         Func<GitLabSharp.ParsedMergeRequestUrl, FullMergeRequestKey, bool> equal = (mergeRequestUrl, key) =>
            key.HostName == mergeRequestUrl.Host
         && key.MergeRequest.IId == mergeRequestUrl.IId
         && key.Project.Path_With_Namespace == mergeRequestUrl.Project;

         try
         {
            GitLabSharp.ParsedMergeRequestUrl mergeRequestUrl = new GitLabSharp.ParsedMergeRequestUrl(url);

            bool reloadAll = listViewMergeRequests.Items.Count == 0
               || ((FullMergeRequestKey)(listViewMergeRequests.Items[0].Tag)).HostName != mergeRequestUrl.Host;

            bool ok = (!reloadAll && await startWorkflowAsync(mergeRequestUrl, false))
                                  || await startWorkflowAsync(mergeRequestUrl, true);
            if (!ok)
            {
               if (_allMergeRequests.Any(x => equal(mergeRequestUrl, x)))
               {
                  unhideFilteredMergeRequestAsync(mergeRequestUrl, url);
               }
               else
               {
                  reportErrorOnConnect(url, String.Format(
                     "Current version supports connection to URL for Open WIP merge requests only. "), null, false);
               }
            }
         }
         catch (Exception ex)
         {
            if (ex is NoProjectsException)
            {
               reportErrorOnConnect(url, String.Format("Check {0} file. ",
                  mrHelper.Common.Constants.Constants.ProjectListFileName), ex, true);
            }
            else if (ex is NotEnabledProjectException)
            {
               reportErrorOnConnect(url, String.Format(
                  "Current version supports connection to URL for projects listed in {0} only. ",
                  mrHelper.Common.Constants.Constants.ProjectListFileName), ex, false);
            }
            else if (ex is UriFormatException || ex is WorkflowException)
            {
               reportErrorOnConnect(url, String.Empty, ex, true);
            }
            else
            {
               Debug.Assert(false);
            }
            return false;
         }

         return true;
      }
   }
}

