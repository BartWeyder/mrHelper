using System;
using System.Diagnostics;
using System.Windows.Forms;
using GitLabSharp;
using mrCore;
using mrCustomActions;
using mrDiffTool;

namespace mrHelperUI
{
   public class ExceptionHandlers
   {
      static public void Handle(Exception ex, string meaning, bool show = true)
      {
         Trace.TraceError("[{0}] {1}: {2}", ex.GetType().ToString(), meaning, ex.Message);
         showMessageBox(meaning, show);
      }

      static public void Handle(GitLabRequestException ex, string meaning, bool show = true)
      {
         Trace.TraceError("[{0}] {1}: {2}\nNested WebException: {3}",
            ex.GetType().ToString(), meaning, ex.Message, ex.WebException.Message);
         showMessageBox(meaning, show);
      }

      static public void Handle(DiffToolIntegrationException ex, string meaning, bool show = true)
      {
         Trace.TraceError("[{0}] {1}: {2}\nNested Exception: {3}",
            ex.GetType().ToString(), meaning, ex.Message,
            (ex.NestedException != null ? ex.NestedException.Message : "N/A"));
         showMessageBox(meaning, show);
      }

      static public void Handle(CustomCommandLoaderException ex, string meaning, bool show = true)
      {
         Trace.TraceError("[{0}] {1}: {2}\nNested Exception: {3}",
            ex.GetType().ToString(), meaning, ex.Message,
            (ex.NestedException != null ? ex.NestedException.Message : "N/A"));
         showMessageBox(meaning, show);
      }

      static public void Handle(GitOperationException ex, string meaning, bool show = true)
      {
         Trace.TraceError("[{0}] {1}: {2}\nDetails:\n{3}", ex.GetType().ToString(), meaning, ex.Message, ex.Details);
         showMessageBox(meaning, show);
      }

      static public void HandleUnhandled(Exception ex, bool show)
      {
         Trace.TraceError("Unhandled exception: {0}\nCallstack:\n{1}", ex.Message, ex.StackTrace);
         showMessageBox("Fatal error occurred, see details in log file", show);
         Application.Exit();
      }

      static private void showMessageBox(string text, bool show)
      {
         if (show)
         {
            MessageBox.Show(text, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
         }
      }
   }
}

