﻿using System;
using System.Linq;
using GitLabSharp.Entities;
using mrHelper.Common.Tools;
using mrHelper.Common.Constants;

namespace mrHelper.App.Helpers
{
   internal static class MergeRequestFilter
   {
      public static bool IsFilteredMergeRequest(MergeRequest mergeRequest, string[] selected)
      {
         if (selected == null || (selected.Length == 1 && selected[0] == String.Empty))
         {
            return false;
         }

         foreach (string item in selected)
         {
            if (item.StartsWith(Constants.AuthorLabelPrefix))
            {
               if (mergeRequest.Author.Username.StartsWith(item.Substring(1),
                     StringComparison.CurrentCultureIgnoreCase))
               {
                  return false;
               }
            }
            else if (item.StartsWith(Constants.GitLabLabelPrefix))
            {
               if (mergeRequest.Labels.Any(x => x.StartsWith(item,
                     StringComparison.CurrentCultureIgnoreCase)))
               {
                  return false;
               }
            }
            else if (item != String.Empty)
            {
               if (mergeRequest.IId.ToString() == item
                || StringUtils.ContainsNoCase(mergeRequest.Author.Username, item)
                || StringUtils.ContainsNoCase(mergeRequest.Title, item)
                || StringUtils.ContainsNoCase(mergeRequest.Source_Branch, item)
                || StringUtils.ContainsNoCase(mergeRequest.Target_Branch, item)
                || mergeRequest.Labels.Any(x => StringUtils.ContainsNoCase(x, item)))
               {
                  return false;
               }
            }
         }

         return true;
      }
   }
}
