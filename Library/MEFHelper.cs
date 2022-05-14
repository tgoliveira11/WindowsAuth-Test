using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Library
{
    public static class MEFHelper
    {
        private static object listedLock = new object();
        private static IDictionary<string, IEnumerable> listed = new Dictionary<string, IEnumerable>();

        /// <summary>
        ///     ''' Lists all extensions of a particular interface of base class
        ///     ''' </summary>
        ///     ''' <typeparam name="T">base interface or class of the extension to search for implementations</typeparam>
        ///     ''' <returns>List with all exported types of the provided contract <typeparamref name="T"/> </returns>
        ///     ''' <exception cref="CompositionContractMismatchException">if T is not marked with ExportAttribute or InheritedExportAttribute</exception>
        public static IEnumerable<T> List<T>(bool useCache = true)
        {
            return List<T>(null, true, useCache);
        }

        /// <summary>
        ///     ''' Lists all extensions of a particular interface of base class
        ///     ''' </summary>
        ///     ''' <typeparam name="T">base interface or class of the extension to search for implementations</typeparam>
        ///     ''' <param name="folder">assemblies folder to search for extensions</param>
        ///     ''' <param name="fallbackCatalogFolder">indicates if the target type folder should be used as fallback to look for assemblies if the provided folder doesn't exist</param>
        ///     ''' <returns>List with all exported types of the provided contract <typeparamref name="T"/> </returns>
        ///     ''' <exception cref="CompositionContractMismatchException">if T is not marked with ExportAttribute or InheritedExportAttribute</exception>
        public static IEnumerable<T> List<T>(string folder, bool fallbackCatalogFolder, bool useCache = false)
        {
            var tp = typeof(T);
            var a = Attribute.GetCustomAttributes(tp, typeof(ExportAttribute), true)?.Length;
            if (a != null && a > 0)
                throw new CompositionContractMismatchException($"The type {typeof(T).FullName} must be marked with ExportAttribute or InheritedExportAttribute");

            IEnumerable<T> result = null;

            lock (listedLock)
            {
                var k = $"{folder ?? string.Empty},{fallbackCatalogFolder},{tp.FullName}";
                if (useCache && listed.ContainsKey(k))
                {
                    result = listed[k] as IEnumerable<T>;
                    if (result != null)
                        return result;
                }

                var aux = new Composer<T>();
                aux.ComposeParts(folder, fallbackCatalogFolder);

                if (aux.Items != null)
                {
                    result = aux.Items.ToArray();
                    if (useCache)
                        listed.Add(k, result);
                }
            }

            return result;
        }

        /// <summary>
        ///     ''' Simplifies the use of MEF by creating the default catalog searching the target's assembly folder.
        ///     ''' </summary>
        ///     ''' <param name="target">Object to be composed by MEF</param>
        ///     ''' <returns>Indicates if the composition was successful</returns>
        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        public static bool ComposeParts(this object target)
        {
            return ComposeParts(target, null, true);
        }

        /// <summary>
        ///     ''' Simplifies the use of MEF by creating the default catalog searching the target's assembly folder.
        ///     ''' </summary>
        ///     ''' <param name="target">Object to be composed by MEF</param>
        ///     ''' <param name="catalogFolder">Folder to search for the assemblies.</param>
        ///     ''' <param name="fallbackCatalogFolder">Indicates if the target's assembly folder should be used if the catalog folder does not exist.</param>
        ///     ''' <returns>Indicates if the composition was successful</returns>
        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        public static bool ComposeParts(this object target, string catalogFolder, bool fallbackCatalogFolder)
        {
            var result = false;
            var assemblyFolder = catalogFolder;
            try
            {
                if (target != null)
                {
                    // usa o diretório informado da dll apenas se não for informado um em catalogFolder ou se for informado um inexistente e o flag de fallback for true.
                    if (string.IsNullOrWhiteSpace(assemblyFolder) || (fallbackCatalogFolder && !System.IO.Directory.Exists(assemblyFolder)))
                        assemblyFolder = Path.GetDirectoryName(new Uri(target.GetType().Assembly.EscapedCodeBase).LocalPath);
                    if (!string.IsNullOrWhiteSpace(assemblyFolder) && System.IO.Directory.Exists(assemblyFolder))
                    {
                        using (var catalog = new AggregateCatalog())
                        {
                            catalog.Catalogs.Add(new DirectoryCatalog(assemblyFolder));
                            using (var container = new CompositionContainer(catalog))
                            {
                                container.ComposeParts(target);
                            }
                        }
                        result = true;
                    }
                }
            }
            catch (ReflectionTypeLoadException ex)
            {
                System.Text.StringBuilder sb = new System.Text.StringBuilder();
                sb.AppendLine();
                sb.AppendLine("Error searching libraries for providers !");
                sb.AppendLine("Error caught when loading the library information.");
                sb.AppendLine("Error details --->");
                foreach (var typeInfo in ex.Types)
                {
                    if (typeInfo != null && !string.IsNullOrWhiteSpace(typeInfo.ToString()))
                        sb.AppendLine(string.Format("Type Info : {0}", typeInfo.ToString()));
                }
                foreach (Exception exSub in ex.LoaderExceptions)
                {
                    if (exSub != null && !string.IsNullOrWhiteSpace(exSub.Message))
                    {
                        sb.AppendLine(string.Format("Exception message : {0}", exSub.Message));
                        sb.AppendLine(string.Format("Exception details : {0}", exSub.ToString()));
                    }
                }
                sb.AppendLine("--- End of  details ---");
                throw new ApplicationException(sb.ToString(), ex);
            }
            return result;
        }

        /// <summary>
        ///     ''' Helper class used by the List method to simplify listing extensions of a specific type.
        ///     ''' </summary>
        ///     ''' <typeparam name="T"></typeparam>
        private class Composer<T>
        {
            [ImportMany]
            public IEnumerable<T> Items { get; set; }
        }
    }

}
