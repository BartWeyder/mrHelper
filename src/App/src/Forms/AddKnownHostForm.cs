﻿using System.Windows.Forms;

namespace mrHelper.App.Forms
{
   internal partial class AddKnownHostForm : CustomFontForm
   {
      internal AddKnownHostForm()
      {
         InitializeComponent();

         applyFont(Program.Settings.MainWindowFontSizeName);
      }

      internal string Host => textBoxHost.Text;

      internal string AccessToken => textBoxAccessToken.Text;

      private void textBox_KeyDown(object sender, KeyEventArgs e)
      {
         if (e.KeyCode == Keys.Enter && Control.ModifierKeys == Keys.Control)
         {
            buttonOK.PerformClick();
         }
      }
   }
}

