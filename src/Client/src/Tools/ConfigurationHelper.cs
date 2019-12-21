﻿using GitLabSharp.Entities;
using System;
using System.Collections.Generic;
using System.Linq;

namespace mrHelper.Client.Tools
{
   public static class ConfigurationHelper
   {
      public static string GetAccessToken(string hostname, UserDefinedSettings settings)
      {
         for (int iKnownHost = 0; iKnownHost < settings.KnownHosts.Count; ++iKnownHost)
         {
            if (hostname == settings.KnownHosts[iKnownHost])
            {
               return settings.KnownAccessTokens[iKnownHost];
            }
         }
         return String.Empty;
      }

      public static string[] GetLabels(UserDefinedSettings settings)
      {
         if (!settings.CheckedLabelsFilter)
         {
             return null;
         }

         return settings.LastUsedLabels .Split(',').Select(x => x.Trim(' ')).ToArray();
      }

#pragma warning disable 0649
      public struct HostInProjectsFile
      {
         public string Name;
         public List<Project> Projects;
      }
#pragma warning restore 0649

      public static void SetupProjects(List<HostInProjectsFile> projects, UserDefinedSettings settings)
      {
         if (projects == null)
         {
            return;
         }

         settings.SelectedProjects = projects
            .Where(x => x.Name != String.Empty && (x.Projects?.Count ?? 0) > 0)
            .ToDictionary(
               item => item.Name,
               item => String.Join(",", item.Projects.Select(x => x.Path_With_Namespace + ":true")));
      }

      public static Dictionary<string, Tuple<string, bool>[]> GetAllProjects(UserDefinedSettings settings)
      {
         return settings.SelectedProjects
            .ToDictionary(
               item => item.Key,
               item => parseProjectString(item.Value));
      }

      public static void SetProjectsForHost(string host, Tuple<string, bool>[] projects, UserDefinedSettings settings)
      {
         Dictionary<string, Tuple<string, bool>[]> allProjects = GetAllProjects(settings);

         allProjects[host] = projects;

         settings.SelectedProjects = allProjects.ToDictionary(
            item => item.Key,
            item => String.Join(",", item.Value.Select(x => x.Item1.ToString() + ":" + x.Item2.ToString())));
      }

      public static Tuple<string, bool>[] GetProjectsForHost(string host, UserDefinedSettings settings)
      {
         if (String.IsNullOrEmpty(host) || !settings.SelectedProjects.ContainsKey(host))
         {
            return null;
         }

         string projectString = settings.SelectedProjects[host];
         return parseProjectString(projectString);
      }

      public static string[] GetEnabledProjects(string host, UserDefinedSettings settings)
      {
         if (String.IsNullOrEmpty(host))
         {
            return null;
         }

         Tuple<string, bool>[] projects = ConfigurationHelper.GetProjectsForHost(host, settings);
         if (projects == null || projects.Length == 0)
         {
            return null;
         }

         return projects
            .Where(x => x.Item2)
            .Select(x => x.Item1)
            .ToArray();
      }

      private static Tuple<string, bool>[] parseProjectString(string projectString)
      {
         if (projectString == String.Empty)
         {
            return null;
         }

         string[] projectsToFlags = projectString.Split(',');
         return projectsToFlags
            .Where(x => x.Split(':').Length == 2)
            .Select(x => parseProjectStringItem(x))
            .ToArray();
      }

      private static Tuple<string, bool> parseProjectStringItem(string item)
      {
         string[] splitted = item.Split(':');
         return new Tuple<string, bool>(
            splitted[0], bool.TryParse(splitted[1], out bool result) ? result : false);
      }
   }
}

