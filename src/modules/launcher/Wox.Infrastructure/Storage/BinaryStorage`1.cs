﻿// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters;
using System.Runtime.Serialization.Formatters.Binary;
using Wox.Plugin;
using Wox.Plugin.Logger;

namespace Wox.Infrastructure.Storage
{
    /// <summary>
    /// Storage object using binary data
    /// Normally, it has better performance, but not readable
    /// </summary>
    public class BinaryStorage<T> : IStorage<T>
    {
        // This storage helper returns whether or not to delete the binary storage items
        private static readonly int _binaryStorage = 0;
        private StoragePowerToysVersionInfo _storageHelper;

        public BinaryStorage(string filename)
        {
            const string directoryName = "Cache";
            var directoryPath = Path.Combine(Constant.DataDirectory, directoryName);
            Helper.ValidateDirectory(directoryPath);

            const string fileSuffix = ".cache";
            FilePath = Path.Combine(directoryPath, $"{filename}{fileSuffix}");
        }

        public string FilePath { get; }

        public T TryLoad(T defaultData)
        {
            _storageHelper = new StoragePowerToysVersionInfo(FilePath, _binaryStorage);

            // Depending on the version number of the previously installed PT Run, delete the cache if it is found to be incompatible
            if (_storageHelper.ClearCache)
            {
                if (File.Exists(FilePath))
                {
                    File.Delete(FilePath);

                    Log.Info($"Deleting cached data at <{FilePath}>", GetType());
                }
            }

            if (File.Exists(FilePath))
            {
                if (new FileInfo(FilePath).Length == 0)
                {
                    Log.Error($"Zero length cache file <{FilePath}>", GetType());

                    Save(defaultData);
                    return defaultData;
                }

                using (var stream = new FileStream(FilePath, FileMode.Open))
                {
                    var d = Deserialize(stream, defaultData);
                    return d;
                }
            }
            else
            {
                Log.Info("Cache file not exist, load default data", GetType());

                Save(defaultData);
                return defaultData;
            }
        }

        private T Deserialize(FileStream stream, T defaultData)
        {
            // http://stackoverflow.com/questions/2120055/binaryformatter-deserialize-gives-serializationexception
            AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;
            BinaryFormatter binaryFormatter = new BinaryFormatter
            {
                AssemblyFormat = FormatterAssemblyStyle.Simple,
            };

            try
            {
                var t = ((T)binaryFormatter.Deserialize(stream)).NonNull();
                return t;
            }
            catch (System.Exception e)
            {
                Log.Exception($"Deserialize error for file <{FilePath}>", e, GetType());

                return defaultData;
            }
            finally
            {
                AppDomain.CurrentDomain.AssemblyResolve -= CurrentDomain_AssemblyResolve;
            }
        }

        private Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
        {
            Assembly ayResult = null;
            string sShortAssemblyName = args.Name.Split(',')[0];
            Assembly[] ayAssemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach (Assembly ayAssembly in ayAssemblies)
            {
                if (sShortAssemblyName == ayAssembly.FullName.Split(',')[0])
                {
                    ayResult = ayAssembly;
                    break;
                }
            }

            return ayResult;
        }

        public void Save(T data)
        {
            using (var stream = new FileStream(FilePath, FileMode.Create))
            {
                BinaryFormatter binaryFormatter = new BinaryFormatter
                {
                    AssemblyFormat = FormatterAssemblyStyle.Simple,
                };

                try
                {
                    binaryFormatter.Serialize(stream, data);
                }
                catch (SerializationException e)
                {
                    Log.Exception($"Serialize error for file <{FilePath}>", e, GetType());
                }
            }

            _storageHelper.Close();
            Log.Info($"Saving cached data at <{FilePath}>", GetType());
        }
    }
}
