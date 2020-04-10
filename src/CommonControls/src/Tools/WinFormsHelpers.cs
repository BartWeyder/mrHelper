﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace mrHelper.CommonControls.Tools
{
   public static class WinFormsHelpers
   {
      public static void FillComboBox(ComboBox comboBox, IEnumerable<string> choices, string defaultChoice)
      {
         foreach (string choice in choices)
         {
            comboBox.Items.Add(choice);
         }

         string selectedChoice = null;
         foreach (string choice in comboBox.Items.Cast<string>())
         {
            if (choice == defaultChoice)
            {
               selectedChoice = choice;
            }
         }

         if (selectedChoice != null)
         {
            comboBox.SelectedItem = selectedChoice;
         }
         else
         {
            comboBox.SelectedIndex = 0;
         }
      }

      public static Dictionary<string, int> GetListViewDisplayIndices(ListView listView)
      {
         Dictionary<string, int> columnIndices = new Dictionary<string, int>();
         foreach (ColumnHeader column in listView.Columns)
         {
            columnIndices[(string)column.Tag] = column.DisplayIndex;
         }
         return columnIndices;
      }

      /// <summary>
      /// Changes DisplayIndex properties of Column Headers of the given List View control
      /// in accordance with columnDisplayIndices argument.
      /// This function expects that columnDisplayIndices has indices for all list view columns.
      /// </summary>
      public static void ReorderListViewColumns(ListView listview, Dictionary<string, int> columnDisplayIndices)
      {
         // Remove indices from an auxiliary collection one-by-one
         List<int> indices = Enumerable.Range(0, listview.Columns.Count).ToList();
         foreach (KeyValuePair<string, int> column in columnDisplayIndices)
         {
            if (!indices.Remove(column.Value))
            {
               Trace.TraceWarning(String.Format(
                  "[WinFormsHelpers] columnDisplayIndices argument contains unexpected value {0}", column.Value));
            }
         }

         // If not all indices are removed, this means that not all indices are stored in Settings, let's discard them
         if (indices.Any())
         {
            throw new ArgumentException("Wrong value of columnDisplayIndices argument");
         }

         foreach (ColumnHeader column in listview.Columns)
         {
            string columnName = (string)column.Tag;
            column.DisplayIndex = columnDisplayIndices[columnName];
         }
      }

      /// <summary>
      /// This function is needed because there is no way to catch a moment when list view's columns
      /// are reordered and can tell their new indices.
      /// ColumnReordered() event is called before list view column indices are changed.
      /// </summary>
      public static Dictionary<string, int> GetListViewDisplayIndicesOnColumnReordered(ListView listView,
         int oldDisplayIndex, int newDisplayIndex)
      {
         Debug.Assert(oldDisplayIndex != newDisplayIndex);

         bool moveForward = newDisplayIndex > oldDisplayIndex;
         Dictionary<string, int> columnsModified =
            listView
            .Columns
            .Cast<ColumnHeader>()
            .ToDictionary(
               item => (string)item.Tag,
               item =>
               {
                  if (moveForward && item.DisplayIndex > oldDisplayIndex && item.DisplayIndex <= newDisplayIndex)
                  {
                     return item.DisplayIndex - 1;
                  }
                  else if (!moveForward && item.DisplayIndex >= newDisplayIndex && item.DisplayIndex < oldDisplayIndex)
                  {
                     return item.DisplayIndex + 1;
                  }
                  else if (item.DisplayIndex == oldDisplayIndex)
                  {
                     return newDisplayIndex;
                  }
                  return item.DisplayIndex;
               });

         return columnsModified;
      }

      /// <summary>
      /// From https://stackoverflow.com/questions/28880301/listview-ownerdraw-with-allowcolumnreorder-dont-work-correct
      /// </summary>
      public static Rectangle GetFirstColumnCorrectRectangle(ListView listView, ListViewItem item)
      {
         for (int iColumn = 0; iColumn < listView.Columns.Count; ++iColumn)
         {
            if (listView.Columns[iColumn].DisplayIndex ==
                listView.Columns[0].DisplayIndex - 1)
            {
               return new Rectangle(item.SubItems[iColumn].Bounds.Right, item.SubItems[iColumn].Bounds.Y,
                                    listView.Columns[0].Width, item.SubItems[iColumn].Bounds.Height);
            }
         }
         Debug.Assert(false);
         return new Rectangle();
      }

      public static IEnumerable<Control> GetAllSubControls(Control container)
      {
         List<Control> controlList = new List<Control>();
         foreach (Control control in container.Controls)
         {
            controlList.AddRange(GetAllSubControls(control));
            controlList.Add(control);
         }
         return controlList;
      }

      public static void FixNonStandardDPIIssue(Control control, float designTimeFontSize, int designTimeDPI)
      {
         // Sometimes Windows DPI behavior is strange when changed to non-default (and even back)
         // without signing out - windows got scaled incorrectly but after signing out they work ok.
         // There is a workaround for it.
         // Component positions are defined at design-time with DPI 96 and when ResumeLauout occurs within
         // InitializeComponent(), .NET checks CurrentAutoScaleDimensions to figure out a scale factor.
         // CurrentAutoScaleDimensions depends on the current font and we need to set it explicitly in advance.
         // This font has to be scaled in accordance with current DPI what gives a proper scale factor for
         // ResumeLayout().

         float currentDPI = control.DeviceDpi;
         float newEmSize = designTimeFontSize * (designTimeDPI / currentDPI);
         float oldEmSize = control.Font.Size;

         control.Font = new System.Drawing.Font(control.Font.FontFamily, newEmSize,
            control.Font.Style, System.Drawing.GraphicsUnit.Point, control.Font.GdiCharSet, control.Font.GdiVerticalFont);

         Trace.TraceInformation(String.Format(
            "[{0}] FixNonStandardDPIIssue(): Current DPI = {1}. Old font emSize = {2}. New font emSize = {3}. "
          + "Design-time: Font-Size: {4}, DPI: {5}",
            control.ToString(), currentDPI, oldEmSize, newEmSize, designTimeFontSize, designTimeDPI));
      }

      public static void LogScaleDimensions(ContainerControl control)
      {
         Trace.TraceInformation(String.Format("[{0}] CurrentAutoScaleDimensions = {1}/{2}, AutoScaleDimensions = {3}/{4}",
            control.ToString(),
            control.CurrentAutoScaleDimensions.Width, control.CurrentAutoScaleDimensions.Height,
            control.AutoScaleDimensions.Width, control.AutoScaleDimensions.Height));
      }

      public static bool ShowConfirmationDialog(string confirmationText)
      {
         return MessageBox.Show(confirmationText, "Confirmation", MessageBoxButtons.YesNo, MessageBoxIcon.Question,
            MessageBoxDefaultButton.Button2) == DialogResult.Yes;
      }
   }
}

