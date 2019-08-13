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
using mrHelper.CustomActions;
using mrHelper.Common.Interfaces;
using mrHelper.Core;
using mrHelper.Client;
using mrHelper.Forms;

namespace mrHelper.App.Forms
{
   internal partial class mrHelperForm
   {
      private void addCustomActions()
      {
         List<ICommand> commands = Tools.LoadCustomActions();
         if (commands == null)
         {
            return;
         }

         int id = 0;
         System.Drawing.Point offSetFromGroupBoxTopLeft = new System.Drawing.Point
         {
            X = 10,
            Y = 17
         };
         System.Drawing.Size typicalSize = new System.Drawing.Size(83, 27);
         foreach (var command in commands)
         {
            string name = command.GetName();
            var button = new System.Windows.Forms.Button
            {
               Name = "customAction" + id,
               Location = offSetFromGroupBoxTopLeft,
               Size = typicalSize,
               Text = name,
               UseVisualStyleBackColor = true,
               Enabled = false,
               TabStop = false
            };
            button.Click += async (x, y) =>
            {
               labelWorkflowStatus.Text = "Command " + name + " is in progress";
               try
               {
                  await command.Run();
               }
               catch (Exception ex) // Whatever happened in Run()
               {
                  ExceptionHandlers.Handle(ex, "Custom action failed");
                  MessageBox.Show("Custom action failed", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                  return;
               }
               labelWorkflowStatus.Text = "Command " + name + " completed";
            };
            groupBoxActions.Controls.Add(button);
            offSetFromGroupBoxTopLeft.X += typicalSize.Width + 10;
            id++;
         }
      }

      private void loadConfiguration()
      {
         Debug.Assert(_settings.KnownHosts.Count == _settings.KnownAccessTokens.Count);
         // Remove all items except header
         for (int iListViewItem = 1; iListViewItem < listViewKnownHosts.Items.Count; ++iListViewItem)
         {
            listViewKnownHosts.Items.RemoveAt(iListViewItem);
         }
         for (int iKnownHost = 0; iKnownHost < _settings.KnownHosts.Count; ++iKnownHost)
         {
            string host = _settings.KnownHosts[iKnownHost];
            string accessToken = _settings.KnownAccessTokens[iKnownHost];
            addKnownHost(host, accessToken);
         }
         textBoxLocalGitFolder.Text = _settings.LocalGitFolder;
         checkBoxRequireTimer.Checked = _settings.RequireTimeTracking;
         checkBoxLabels.Checked = _settings.CheckedLabelsFilter;
         textBoxLabels.Text = _settings.LastUsedLabels;
         checkBoxShowinternalOnly.Checked = _settings.ShowinternalOnly;
         if (comboBoxDCDepth.Items.Contains(_settings.DiffContextDepth))
         {
            comboBoxDCDepth.Text = _settings.DiffContextDepth;
         }
         else
         {
            comboBoxDCDepth.SelectedIndex = 0;
         }
         checkBoxMinimizeOnClose.Checked = _settings.MinimizeOnClose;
         fillColorSchemesList();
      }

      private void integrateInTools()
      {
         DiffToolIntegration integration = new DiffToolIntegration(new GlobalGitConfiguration());

         try
         {
            integration.Integrate(new BC3Tool());
         }
         catch (Exception ex)
         {
            MessageBox.Show("Diff tool integration failed. Application cannot start. See logs for details",
               "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);

            if (ex is DiffToolIntegrationException || ex is GitOperationException)
            {
               ExceptionHandlers.Handle(ex,
                  String.Format("Cannot integrate \"{0}\"", diffTool.GetToolName()));
               return;
            }
            throw;
         }
      }

      private void loadSettings()
      {
         _settings = new UserDefinedSettings(true);
         loadConfiguration();
         _settings.PropertyChanged += onSettingsPropertyChanged;

         labelSpentTime.Text = labelSpentTimeDefaultText;
         buttonToggleTimer.Text = buttonStartTimerDefaultText;
         this.Text += " (" + Application.ProductVersion + ")";

         bool configured = listViewKnownHosts.Items.Count > 0
                        && textBoxLocalGitFolder.Text.Length > 0;
         if (configured)
         {
            tabControl.SelectedTab = tabPageMR;
         }
         else
         {
            tabControl.SelectedTab = tabPageSettings;
         }
      }

      async private void onApplicationStarted()
      {
         _timeTrackingTimer.Tick += new System.EventHandler(onTimer);

         _workflowManager = new WorkflowManager(_settings);
         _updateManager = new UpdateManager(_settings);
         _timeTrackingManager = new TimeTrackingManager(_settings);
         _discussionManager = new DiscussionManager(_settings);
         _gitClientFactory = new GitClientFactory();
         _gitClientInitializer = new GitClientInitializer(_gitClientFactory);
         _gitClientInitializer.OnInitializationStatusChange += (sender, e) => updateGitStatusText(e);

         updateHostsDropdownList();

         try
         {
            await initializeWorkflow();
         }
         catch (WorkflowException)
         {
            MessageBox.Show("Cannot initialize the workflow. Application cannot start. See logs for details",
               "Error", MessageBoxButtons.OK, MessageBoxIcons.Error)";
         }
      }
   }
}
