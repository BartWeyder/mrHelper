﻿using System;
using System.Collections.Generic;
using mrHelper.Common.Tools;
using mrHelper.Common.Exceptions;
using mrHelper.Common.Interfaces;
using GitLabSharp.Entities;

namespace mrHelper.StorageSupport
{
   internal static class FileStorageUtils
   {
      internal static void InitalizeFileStorage(string path, ProjectKey projectKey)
      {
         string descriptionFilepath = System.IO.Path.Combine(path, FileStorageConfig);
         if (System.IO.File.Exists(descriptionFilepath))
         {
            return;
         }

         if (!System.IO.Directory.Exists(path))
         {
            try
            {
               System.IO.Directory.CreateDirectory(path);
            }
            catch (Exception ex)
            {
               throw new ArgumentException(String.Format("Cannot create a file storage at {0}", path), ex);
            }
         }

         FileStorageDescription fileStorageDescription = new FileStorageDescription
         {
            HostName = projectKey.HostName,
            ProjectName = projectKey.ProjectName
         };

         try
         {
            JsonUtils.SaveToFile(descriptionFilepath, fileStorageDescription);
         }
         catch (Exception ex)
         {
            throw new ArgumentException(String.Format("Cannot initialize a file storage at {0}", path), ex);
         }
      }

      internal static ProjectKey? GetFileStorageProjectKey(string path)
      {
         string descriptionFilename = System.IO.Path.Combine(path, FileStorageConfig);
         if (System.IO.File.Exists(descriptionFilename))
         {
            try
            {
               FileStorageDescription x = JsonUtils.LoadFromFile<FileStorageDescription>(descriptionFilename);
               return new ProjectKey(x.HostName, x.ProjectName);
            }
            catch (Exception ex)
            {
               ExceptionHandlers.Handle("Cannot read serialized FileStorageDescription object", ex);
            }
         }
         return null;
      }

      internal static IEnumerable<T> TransformDiffs<T>(IEnumerable<DiffStruct> diffs, string sha, bool old)
      {
         List<T> result = new List<T>();
         foreach (DiffStruct diff in diffs)
         {
            if (old && !String.IsNullOrWhiteSpace(diff.Old_Path) && !diff.New_File)
            {
               result.Add((T)Activator.CreateInstance(typeof(T), diff.Old_Path, sha));
            }
            else if (!old && !String.IsNullOrWhiteSpace(diff.New_Path) && !diff.Deleted_File)
            {
               result.Add((T)Activator.CreateInstance(typeof(T), diff.New_Path, sha));
            }
         }
         return result;
      }

      private class FileStorageDescription
      {
         // Don't make it immutable to avoid adding JsonProperty tags to avoid dependency from Newtonsoft.Json
         public string HostName { get; set; }
         public string ProjectName { get; set; }
      }

      private static readonly string FileStorageConfig = "mrHelper.filestorage.json";
   }
}

