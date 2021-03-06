﻿using System.Threading.Tasks;

namespace mrHelper.CustomActions
{
   public interface ICommand
   {
      string GetName();

      string GetDependency();

      bool GetStopTimer();

      bool GetReload();

      string GetHint();

      Task Run();
   }
}
